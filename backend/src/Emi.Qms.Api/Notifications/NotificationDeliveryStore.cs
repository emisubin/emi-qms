using System.Text.Json;
using System.Text.RegularExpressions;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.ProductionPlanning;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Notifications;

public sealed class NotificationDeliveryStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    TimeProvider timeProvider,
    IConfiguration configuration)
{
    private const string SystemName = "EMI 프로젝트 통합관리시스템";

    public async Task<int> CreateImmediateDeliveriesAsync(NotificationOptions options, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var dedupeAfter = now.AddHours(-Math.Max(1, options.Dispatch.DedupeWindowHours));
        var batchWindowSeconds = Math.Max(1, options.Dispatch.BatchWindowSeconds);

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var created = 0;
        created += await InsertUrgentTeamsChannelDeliveriesAsync(connection, transaction, dedupeAfter, batchWindowSeconds, cancellationToken);
        created += await InsertUrgentMailDeliveriesAsync(connection, transaction, dedupeAfter, batchWindowSeconds, cancellationToken);
        created += await InsertWorkItemTeamsDirectMessageDeliveriesAsync(connection, transaction, dedupeAfter, batchWindowSeconds, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return created;
    }

    public async Task<int> CreateDailyDigestDeliveriesIfDueAsync(NotificationOptions options, CancellationToken cancellationToken)
    {
        if (!options.DailyDigest.Enabled)
        {
            return 0;
        }

        var now = timeProvider.GetUtcNow();
        if (!TryGetDailyDigestWindow(options.DailyDigest, now, out var digestDate, out var previousDayStartUtc, out var previousDayEndUtc))
        {
            return 0;
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            with digest_users as (
                select u.id as user_id,
                       u.email,
                       u.display_name,
                       concat('daily-digest:', @digest_date, ':', u.id::text, ':mail') as dedupe_key
                from qms_users u
                where u.is_active = true
                  and (
                    exists (
                        select 1
                        from work_items wi
                        where wi.assigned_user_id = u.id
                          and wi.status in ('Requested', 'InProgress')
                    )
                    or exists (
                        select 1
                        from work_items wi
                        where wi.assigned_user_id = u.id
                          and wi.created_at_utc >= @previous_day_start_utc
                          and wi.created_at_utc < @previous_day_end_utc
                    )
                    or exists (
                        select 1
                        from notification_recipients nr
                        join notifications n on n.id = nr.notification_id
                        where nr.user_id = u.id
                          and nr.read_at_utc is null
                          and n.notification_type in ('Reference', 'Info')
                    )
                    or exists (
                        select 1
                        from project_assignees pa
                        join projects p on p.id = pa.project_id
                        where pa.assigned_user_id = u.id
                          and p.deleted_at_utc is null
                          and p.status = 'Active'
                    )
                  )
            )
            insert into notification_deliveries (
                recipient_user_id, channel, delivery_type, status, suppressed_at_utc,
                error_code, error_message, dedupe_key, group_key, next_attempt_at_utc,
                display_title, display_message, display_project_name,
                display_recipient_name, display_recipient_email, display_recipient_kind
            )
            select
                user_id,
                'Mail',
                'DailyDigest',
                case when email is null or btrim(email) = '' then 'Suppressed' else 'Pending' end,
                case when email is null or btrim(email) = '' then @now else null end,
                case when email is null or btrim(email) = '' then 'RecipientEmailMissing' else null end,
                case when email is null or btrim(email) = '' then '사용자 이메일이 없어 일일 요약 메일을 보내지 않았습니다.' else null end,
                dedupe_key,
                concat('daily-digest:', @digest_date),
                @now,
                concat(@digest_date, ' 업무 요약'),
                '일일 업무 요약 알림입니다.',
                '여러 프로젝트',
                display_name,
                email,
                'User'
            from digest_users
            where not exists (
                select 1
                from notification_deliveries existing
                where existing.delivery_type = 'DailyDigest'
                  and existing.channel = 'Mail'
                  and existing.recipient_user_id = digest_users.user_id
                  and existing.dedupe_key = digest_users.dedupe_key
            );
            """;
        command.Parameters.AddWithValue("digest_date", digestDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("previous_day_start_utc", previousDayStartUtc);
        command.Parameters.AddWithValue("previous_day_end_utc", previousDayEndUtc);
        command.Parameters.AddWithValue("now", now);
        var created = await command.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return created;
    }

    public Task<IReadOnlyList<ClaimedNotificationDelivery>> ClaimDueDeliveriesAsync(
        int limit,
        int retryCount,
        string workerInstanceId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        return ClaimDeliveriesAsync(null, limit, retryCount, workerInstanceId, leaseDuration, cancellationToken);
    }

    public async Task<ClaimedNotificationDelivery?> ClaimDeliveryAsync(
        Guid deliveryId,
        int retryCount,
        string workerInstanceId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        var claimed = await ClaimDeliveriesAsync(deliveryId, 1, retryCount, workerInstanceId, leaseDuration, cancellationToken);
        return claimed.SingleOrDefault();
    }

    private async Task<IReadOnlyList<ClaimedNotificationDelivery>> ClaimDeliveriesAsync(
        Guid? deliveryId,
        int limit,
        int retryCount,
        string workerInstanceId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(workerInstanceId))
        {
            throw new ArgumentException("Worker instance id is required.", nameof(workerInstanceId));
        }

        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        var now = timeProvider.GetUtcNow();
        var leaseExpiresAtUtc = now.Add(leaseDuration);
        var maxAttempts = Math.Max(1, retryCount);

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var expireAttempts = connection.CreateCommand())
        {
            expireAttempts.Transaction = transaction;
            expireAttempts.CommandText = """
                update notification_delivery_attempts attempt
                set outcome = case
                        when attempt.provider_call_started_at_utc is null then 'LeaseExpiredBeforeProviderCall'
                        else 'LeaseExpiredAfterProviderCallStarted'
                    end,
                    completed_at_utc = @now,
                    error_code = case
                        when attempt.provider_call_started_at_utc is null then 'NotificationDeliveryLeaseExpiredBeforeProviderCall'
                        else 'NotificationDeliveryLeaseExpiredAfterProviderCallStarted'
                    end,
                    error_message = case
                        when attempt.provider_call_started_at_utc is null then 'Provider 호출 전에 claim lease가 만료되었습니다.'
                        else 'Provider 호출 시작 후 claim lease가 만료되어 결과가 불확실합니다.'
                    end,
                    updated_at_utc = @now
                from notification_deliveries delivery
                where attempt.delivery_id = delivery.id
                  and attempt.claim_token = delivery.claim_token
                  and attempt.outcome = 'Processing'
                  and delivery.status = 'Processing'
                  and delivery.claim_expires_at_utc <= @now;
                """;
            expireAttempts.Parameters.AddWithValue("now", now);
            await expireAttempts.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var finalizeExhausted = connection.CreateCommand())
        {
            finalizeExhausted.Transaction = transaction;
            finalizeExhausted.CommandText = """
                update notification_deliveries
                set status = 'Failed',
                    last_attempt_at_utc = @now,
                    next_attempt_at_utc = null,
                    error_code = 'NotificationDeliveryLeaseExpiredAtRetryLimit',
                    error_message = '재시도 한도에서 claim lease가 만료되어 자동 재시도를 중단했습니다.',
                    claim_token = null,
                    claimed_at_utc = null,
                    claim_expires_at_utc = null,
                    claimed_by_instance_id = null,
                    updated_at_utc = @now
                where status = 'Processing'
                  and claim_expires_at_utc <= @now
                  and attempt_count >= @max_attempt_count;
                """;
            finalizeExhausted.Parameters.AddWithValue("now", now);
            finalizeExhausted.Parameters.AddWithValue("max_attempt_count", maxAttempts);
            await finalizeExhausted.ExecuteNonQueryAsync(cancellationToken);
        }

        var candidateIds = new List<Guid>();
        await using (var candidates = connection.CreateCommand())
        {
            candidates.Transaction = transaction;
            candidates.CommandText = """
                select id
                from notification_deliveries
                where (@delivery_id is null or id = @delivery_id)
                  and (
                      (
                          status = 'Pending'
                          and (next_attempt_at_utc is null or next_attempt_at_utc <= @now)
                      )
                      or (
                          status = 'Processing'
                          and claim_expires_at_utc <= @now
                      )
                  )
                  and coalesce(admin_handling_status, 'Open') = 'Open'
                  and attempt_count < @max_attempt_count
                order by coalesce(next_attempt_at_utc, claim_expires_at_utc, created_at_utc), created_at_utc, id
                for update skip locked
                limit @limit;
                """;
            candidates.Parameters.Add(new NpgsqlParameter("delivery_id", NpgsqlDbType.Uuid) { Value = (object?)deliveryId ?? DBNull.Value });
            candidates.Parameters.AddWithValue("now", now);
            candidates.Parameters.AddWithValue("max_attempt_count", maxAttempts);
            candidates.Parameters.AddWithValue("limit", Math.Max(1, limit));
            await using var reader = await candidates.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                candidateIds.Add(reader.GetGuid(0));
            }
        }

        if (candidateIds.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return [];
        }

        await using (var claim = connection.CreateCommand())
        {
            claim.Transaction = transaction;
            claim.CommandText = """
                update notification_deliveries
                set status = 'Processing',
                    attempt_count = attempt_count + 1,
                    next_attempt_at_utc = null,
                    last_attempt_at_utc = @now,
                    error_code = null,
                    error_message = null,
                    claim_token = uuid_generate_v4(),
                    claimed_at_utc = @now,
                    claim_expires_at_utc = @lease_expires_at_utc,
                    claimed_by_instance_id = @worker_instance_id,
                    updated_at_utc = @now
                where id = any(@ids);
                """;
            claim.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = candidateIds.ToArray() });
            claim.Parameters.AddWithValue("now", now);
            claim.Parameters.AddWithValue("lease_expires_at_utc", leaseExpiresAtUtc);
            claim.Parameters.AddWithValue("worker_instance_id", workerInstanceId);
            await claim.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var createAttempts = connection.CreateCommand())
        {
            createAttempts.Transaction = transaction;
            createAttempts.CommandText = """
                insert into notification_delivery_attempts (
                    delivery_id,
                    attempt_no,
                    claim_token,
                    worker_instance_id,
                    claimed_at_utc,
                    lease_expires_at_utc,
                    outcome
                )
                select
                    id,
                    attempt_count,
                    claim_token,
                    claimed_by_instance_id,
                    claimed_at_utc,
                    claim_expires_at_utc,
                    'Processing'
                from notification_deliveries
                where id = any(@ids);
                """;
            createAttempts.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = candidateIds.ToArray() });
            await createAttempts.ExecuteNonQueryAsync(cancellationToken);
        }

        var claimedRows = new List<ClaimedNotificationDelivery>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
            select
                nd.id,
                nd.notification_id,
                nd.notification_recipient_id,
                nd.recipient_user_id,
                nd.project_id,
                nd.work_item_id,
                nd.channel,
                nd.delivery_type,
                nd.status,
                nd.attempt_count,
                nd.next_attempt_at_utc,
                nd.last_attempt_at_utc,
                nd.sent_at_utc,
                nd.suppressed_at_utc,
                nd.error_code,
                nd.error_message,
                nd.dedupe_key,
                nd.group_key,
                nd.provider_message_id,
                nd.created_at_utc,
                nd.updated_at_utc,
                u.display_name,
                u.email,
                u.entra_object_id,
                u.auth_provider,
                u.is_active,
                n.title,
                n.message,
                n.link_url,
                p.project_title,
                p.project_code,
                n.notification_type,
                n.severity,
                wi.title,
                ws.stage_name,
                nd.admin_handling_status,
                nd.admin_handled_at_utc,
                nd.admin_handled_by_user_id,
                handled_by.display_name,
                nd.admin_handling_note,
                nd.display_title,
                nd.display_message,
                nd.display_project_name,
                nd.display_work_item_title,
                nd.display_recipient_name,
                nd.display_recipient_email,
                nd.display_recipient_kind,
                nd.display_channel_target,
                nd.manual_notification_kind,
                nd.correlation_id,
                nd.manual_payload_json::text,
                nd.manual_requested_by_user_id,
                nd.manual_requested_at_utc,
                nd.claim_token,
                nd.claimed_at_utc,
                nd.claim_expires_at_utc,
                nd.claimed_by_instance_id
            from notification_deliveries nd
            left join qms_users u on u.id = nd.recipient_user_id
            left join notifications n on n.id = nd.notification_id
            left join projects p on p.id = coalesce(nd.project_id, n.project_id)
            left join work_items wi on wi.id = nd.work_item_id
            left join workflow_stages ws on ws.stage_code = wi.workflow_stage_code
            left join qms_users handled_by on handled_by.id = nd.admin_handled_by_user_id
            where nd.id = any(@ids)
            order by array_position(@ids, nd.id);
            """;
            command.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = candidateIds.ToArray() });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = ReadDeliveryRecord(reader);
                claimedRows.Add(new ClaimedNotificationDelivery(
                    row,
                    row.ClaimToken ?? throw new InvalidOperationException("Claim token was not returned."),
                    row.AttemptCount,
                    row.ClaimExpiresAtUtc ?? throw new InvalidOperationException("Claim expiry was not returned.")));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return claimedRows;
    }

    public async Task<bool> MarkProviderCallStartedAsync(
        Guid deliveryId,
        Guid claimToken,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            update notification_delivery_attempts attempt
            set provider_call_started_at_utc = @now,
                updated_at_utc = @now
            from notification_deliveries delivery
            where attempt.delivery_id = @delivery_id
              and attempt.claim_token = @claim_token
              and attempt.outcome = 'Processing'
              and attempt.provider_call_started_at_utc is null
              and delivery.id = attempt.delivery_id
              and delivery.status = 'Processing'
              and delivery.claim_token = @claim_token;
            """);
        command.Parameters.AddWithValue("delivery_id", deliveryId);
        command.Parameters.AddWithValue("claim_token", claimToken);
        command.Parameters.AddWithValue("now", now);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> CompleteDeliveryAttemptAsync(
        Guid deliveryId,
        Guid claimToken,
        NotificationChannelResult result,
        int retryCount,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var terminalResult = (result.Status is NotificationDeliveryStatuses.Sent
            or NotificationDeliveryStatuses.DryRunSent
            or NotificationDeliveryStatuses.Disabled
            or NotificationDeliveryStatuses.Suppressed)
            || IsNonRetryableNotificationFailure(result.ErrorCode);
        var maxAttempts = Math.Max(1, retryCount);

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update notification_deliveries
            set status = case
                    when @result_status = 'Failed' and not @terminal_result and attempt_count < @retry_count then 'Pending'
                    else @result_status
                end,
                last_attempt_at_utc = @now,
                sent_at_utc = case when @result_status in ('Sent', 'DryRunSent') then @now else sent_at_utc end,
                suppressed_at_utc = case when @result_status in ('Suppressed', 'Disabled') then @now else suppressed_at_utc end,
                next_attempt_at_utc = case
                    when @result_status = 'Failed' and not @terminal_result and attempt_count < @retry_count then @next_attempt_at_utc
                    else null
                end,
                error_code = @error_code,
                error_message = @error_message,
                provider_message_id = @provider_message_id,
                claim_token = null,
                claimed_at_utc = null,
                claim_expires_at_utc = null,
                claimed_by_instance_id = null,
                updated_at_utc = @now
            where id = @delivery_id
              and status = 'Processing'
              and claim_token = @claim_token
            returning attempt_count;
            """;
        command.Parameters.AddWithValue("delivery_id", deliveryId);
        command.Parameters.AddWithValue("claim_token", claimToken);
        command.Parameters.AddWithValue("result_status", result.Status);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("terminal_result", terminalResult);
        command.Parameters.AddWithValue("retry_count", maxAttempts);
        command.Parameters.AddWithValue("next_attempt_at_utc", now.AddMinutes(5));
        command.Parameters.AddWithValue("error_code", (object?)result.ErrorCode ?? DBNull.Value);
        command.Parameters.AddWithValue("error_message", (object?)SanitizeError(result.ErrorMessage) ?? DBNull.Value);
        command.Parameters.AddWithValue("provider_message_id", (object?)result.ProviderMessageId ?? DBNull.Value);
        var attemptCountValue = await command.ExecuteScalarAsync(cancellationToken);

        if (attemptCountValue is not int attemptCount)
        {
            await using var ownershipLost = connection.CreateCommand();
            ownershipLost.Transaction = transaction;
            ownershipLost.CommandText = """
                update notification_delivery_attempts
                set outcome = case when outcome = 'Processing' then 'OwnershipLost' else outcome end,
                    completed_at_utc = coalesce(completed_at_utc, @now),
                    error_code = 'NotificationDeliveryClaimLost',
                    error_message = '현재 claim 소유권이 없어 늦은 완료 결과를 반영하지 않았습니다.',
                    updated_at_utc = @now
                where delivery_id = @delivery_id
                  and claim_token = @claim_token;
                """;
            ownershipLost.Parameters.AddWithValue("delivery_id", deliveryId);
            ownershipLost.Parameters.AddWithValue("claim_token", claimToken);
            ownershipLost.Parameters.AddWithValue("now", now);
            await ownershipLost.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        var retryScheduled = result.Status == NotificationDeliveryStatuses.Failed
            && !terminalResult
            && attemptCount < maxAttempts;
        var outcome = result.Status switch
        {
            NotificationDeliveryStatuses.Sent => NotificationDeliveryAttemptOutcomes.Sent,
            NotificationDeliveryStatuses.DryRunSent => NotificationDeliveryAttemptOutcomes.DryRunSent,
            NotificationDeliveryStatuses.Disabled => NotificationDeliveryAttemptOutcomes.Disabled,
            NotificationDeliveryStatuses.Suppressed => NotificationDeliveryAttemptOutcomes.Suppressed,
            NotificationDeliveryStatuses.Failed when retryScheduled => NotificationDeliveryAttemptOutcomes.RetryScheduled,
            _ => NotificationDeliveryAttemptOutcomes.FailedPermanent
        };

        await using var completeAttempt = connection.CreateCommand();
        completeAttempt.Transaction = transaction;
        completeAttempt.CommandText = """
            update notification_delivery_attempts
            set outcome = @outcome,
                completed_at_utc = @now,
                error_code = @error_code,
                error_message = @error_message,
                provider_message_id = @provider_message_id,
                updated_at_utc = @now
            where delivery_id = @delivery_id
              and claim_token = @claim_token
              and outcome = 'Processing';
            """;
        completeAttempt.Parameters.AddWithValue("delivery_id", deliveryId);
        completeAttempt.Parameters.AddWithValue("claim_token", claimToken);
        completeAttempt.Parameters.AddWithValue("outcome", outcome);
        completeAttempt.Parameters.AddWithValue("now", now);
        completeAttempt.Parameters.AddWithValue("error_code", (object?)result.ErrorCode ?? DBNull.Value);
        completeAttempt.Parameters.AddWithValue("error_message", (object?)SanitizeError(result.ErrorMessage) ?? DBNull.Value);
        completeAttempt.Parameters.AddWithValue("provider_message_id", (object?)result.ProviderMessageId ?? DBNull.Value);
        if (await completeAttempt.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException("Notification delivery attempt audit completion failed.");
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static bool IsNonRetryableNotificationFailure(string? errorCode)
    {
        return errorCode is "TeamsActivityAppNotInstalled"
            or "TeamsActivityInvalidActivityType"
            or "TeamsActivityInvalidTopic"
            or "TeamsActivityInvalidInstalledAppId"
            or "TeamsActivityPermissionDenied"
            or "TeamsActivityUserOrAppNotFound";
    }

    public async Task<NotificationDeliveryListResponse> ListDeliveriesAsync(
        string? status,
        string? channel,
        string? deliveryType,
        string? handlingStatus,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select
                nd.id,
                nd.notification_id,
                nd.notification_recipient_id,
                nd.recipient_user_id,
                nd.project_id,
                nd.work_item_id,
                nd.channel,
                nd.delivery_type,
                nd.status,
                nd.attempt_count,
                nd.next_attempt_at_utc,
                nd.last_attempt_at_utc,
                nd.sent_at_utc,
                nd.suppressed_at_utc,
                nd.error_code,
                nd.error_message,
                nd.dedupe_key,
                nd.group_key,
                nd.provider_message_id,
                nd.created_at_utc,
                nd.updated_at_utc,
                u.display_name,
                u.email,
                u.entra_object_id,
                u.auth_provider,
                u.is_active,
                n.title,
                n.message,
                n.link_url,
                p.project_title,
                p.project_code,
                n.notification_type,
                n.severity,
                wi.title,
                ws.stage_name,
                nd.admin_handling_status,
                nd.admin_handled_at_utc,
                nd.admin_handled_by_user_id,
                handled_by.display_name,
                nd.admin_handling_note,
                nd.display_title,
                nd.display_message,
                nd.display_project_name,
                nd.display_work_item_title,
                nd.display_recipient_name,
                nd.display_recipient_email,
                nd.display_recipient_kind,
                nd.display_channel_target,
                nd.manual_notification_kind,
                nd.correlation_id,
                nd.manual_payload_json::text,
                nd.manual_requested_by_user_id,
                nd.manual_requested_at_utc,
                nd.claim_token,
                nd.claimed_at_utc,
                nd.claim_expires_at_utc,
                nd.claimed_by_instance_id
            from notification_deliveries nd
            left join qms_users u on u.id = nd.recipient_user_id
            left join notifications n on n.id = nd.notification_id
            left join projects p on p.id = coalesce(nd.project_id, n.project_id)
            left join work_items wi on wi.id = nd.work_item_id
            left join workflow_stages ws on ws.stage_code = wi.workflow_stage_code
            left join qms_users handled_by on handled_by.id = nd.admin_handled_by_user_id
            where (@status is null or nd.status = @status)
              and (@channel is null or nd.channel = @channel)
              and (@delivery_type is null or nd.delivery_type = @delivery_type)
              and (
                  @handling_status is null
                  or (@handling_status = 'Open' and coalesce(nd.admin_handling_status, 'Open') = 'Open')
                  or nd.admin_handling_status = @handling_status
              )
            order by nd.created_at_utc desc
            limit 200;
            """);
        command.Parameters.Add(new NpgsqlParameter("status", NpgsqlDbType.Text) { Value = NormalizeFilter(status) ?? (object)DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("channel", NpgsqlDbType.Text) { Value = NormalizeFilter(channel) ?? (object)DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("delivery_type", NpgsqlDbType.Text) { Value = NormalizeFilter(deliveryType) ?? (object)DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("handling_status", NpgsqlDbType.Text) { Value = NormalizeHandlingFilter(handlingStatus) ?? (object)DBNull.Value });

        var items = new List<NotificationDeliveryResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = ReadDeliveryRecord(reader);
            items.Add(ToResponse(row, timeProvider.GetUtcNow()));
        }

        return new NotificationDeliveryListResponse(items);
    }

    public async Task<NotificationDeliveryDetailResponse?> GetDeliveryDetailAsync(
        Guid deliveryId,
        CancellationToken cancellationToken,
        Guid? recipientUserId = null,
        bool teamsActivityOnly = false)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select
                nd.id,
                nd.notification_id,
                nd.notification_recipient_id,
                nd.recipient_user_id,
                nd.project_id,
                nd.work_item_id,
                nd.channel,
                nd.delivery_type,
                nd.status,
                nd.attempt_count,
                nd.next_attempt_at_utc,
                nd.last_attempt_at_utc,
                nd.sent_at_utc,
                nd.suppressed_at_utc,
                nd.error_code,
                nd.error_message,
                nd.dedupe_key,
                nd.group_key,
                nd.provider_message_id,
                nd.created_at_utc,
                nd.updated_at_utc,
                u.display_name,
                u.email,
                u.entra_object_id,
                u.auth_provider,
                u.is_active,
                n.title,
                n.message,
                n.link_url,
                p.project_title,
                p.project_code,
                n.notification_type,
                n.severity,
                wi.title,
                ws.stage_name,
                nd.admin_handling_status,
                nd.admin_handled_at_utc,
                nd.admin_handled_by_user_id,
                handled_by.display_name,
                nd.admin_handling_note,
                nd.display_title,
                nd.display_message,
                nd.display_project_name,
                nd.display_work_item_title,
                nd.display_recipient_name,
                nd.display_recipient_email,
                nd.display_recipient_kind,
                nd.display_channel_target,
                nd.manual_notification_kind,
                nd.correlation_id,
                nd.manual_payload_json::text,
                nd.manual_requested_by_user_id,
                nd.manual_requested_at_utc,
                nd.claim_token,
                nd.claimed_at_utc,
                nd.claim_expires_at_utc,
                nd.claimed_by_instance_id
            from notification_deliveries nd
            left join qms_users u on u.id = nd.recipient_user_id
            left join notifications n on n.id = nd.notification_id
            left join projects p on p.id = coalesce(nd.project_id, n.project_id)
            left join work_items wi on wi.id = nd.work_item_id
            left join workflow_stages ws on ws.stage_code = wi.workflow_stage_code
            left join qms_users handled_by on handled_by.id = nd.admin_handled_by_user_id
            where nd.id = @id
              and (@recipient_user_id is null or nd.recipient_user_id = @recipient_user_id)
              and (@teams_activity_only = false or nd.channel = 'TeamsActivity');
            """);
        command.Parameters.AddWithValue("id", deliveryId);
        command.Parameters.Add(new NpgsqlParameter("recipient_user_id", NpgsqlDbType.Uuid) { Value = (object?)recipientUserId ?? DBNull.Value });
        command.Parameters.AddWithValue("teams_activity_only", teamsActivityOnly);

        NotificationDeliveryRecord row;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            row = ReadDeliveryRecord(reader);
        }

        var attempts = await ListDeliveryAttemptsAsync(dataSource, deliveryId, cancellationToken);
        return ToDetailResponse(row, attempts, timeProvider.GetUtcNow());
    }

    public async Task<ManualNotificationProjectSnapshot?> GetProjectSnapshotAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select id, project_title, project_code
            from projects
            where id = @project_id
              and deleted_at_utc is null;
            """);
        command.Parameters.AddWithValue("project_id", projectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ManualNotificationProjectSnapshot(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2));
    }

    public async Task<NotificationDeliveryAdminActionResponse> HandleDeliveriesAsync(
        IReadOnlyList<Guid> ids,
        string handlingStatus,
        string? note,
        Guid handledByUserId,
        CancellationToken cancellationToken)
    {
        if (handlingStatus is not NotificationDeliveryAdminHandlingStatuses.Acknowledged
            and not NotificationDeliveryAdminHandlingStatuses.Dismissed)
        {
            throw new ArgumentException("Unsupported handling status.", nameof(handlingStatus));
        }

        return await MutateDeliveriesAsync(
            ids,
            async (connection, transaction, id, ct) =>
            {
                var current = await ReadDeliveryStatusAsync(connection, transaction, id, ct);
                if (current is null)
                {
                    return new NotificationDeliveryAdminActionItemResponse(id, "Failed", "알림 발송 이력을 찾을 수 없습니다.");
                }

                if (current is not NotificationDeliveryStatuses.Failed and not NotificationDeliveryStatuses.Pending)
                {
                    return new NotificationDeliveryAdminActionItemResponse(id, "Skipped", "실패 또는 대기 상태의 알림만 확인/제외 처리할 수 있습니다.");
                }

                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    update notification_deliveries
                    set admin_handling_status = @handling_status,
                        admin_handled_at_utc = @now,
                        admin_handled_by_user_id = @handled_by_user_id,
                        admin_handling_note = @note,
                        updated_at_utc = @now
                    where id = @id;
                    """;
                var now = timeProvider.GetUtcNow();
                command.Parameters.AddWithValue("id", id);
                command.Parameters.AddWithValue("handling_status", handlingStatus);
                command.Parameters.AddWithValue("now", now);
                command.Parameters.AddWithValue("handled_by_user_id", handledByUserId);
                command.Parameters.AddWithValue("note", (object?)SanitizeError(note) ?? DBNull.Value);
                await command.ExecuteNonQueryAsync(ct);

                var label = handlingStatus == NotificationDeliveryAdminHandlingStatuses.Acknowledged ? "확인 처리했습니다." : "목록 제외 처리했습니다.";
                return new NotificationDeliveryAdminActionItemResponse(id, "Succeeded", label);
            },
            cancellationToken);
    }

    public async Task<NotificationDeliveryAdminActionResponse> RetryPendingDeliveriesAsync(
        IReadOnlyList<Guid> ids,
        string? note,
        Guid handledByUserId,
        CancellationToken cancellationToken)
    {
        return await MutateDeliveriesAsync(
            ids,
            async (connection, transaction, id, ct) =>
            {
                var current = await ReadDeliveryStatusAsync(connection, transaction, id, ct);
                if (current is null)
                {
                    return new NotificationDeliveryAdminActionItemResponse(id, "Failed", "알림 발송 이력을 찾을 수 없습니다.");
                }

                if (current != NotificationDeliveryStatuses.Pending)
                {
                    return new NotificationDeliveryAdminActionItemResponse(id, "Skipped", "발송 대기 상태의 알림만 재발송 대기열에 등록할 수 있습니다.");
                }

                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    update notification_deliveries
                    set next_attempt_at_utc = @now,
                        admin_handling_status = 'Open',
                        admin_handled_at_utc = @now,
                        admin_handled_by_user_id = @handled_by_user_id,
                        admin_handling_note = @note,
                        updated_at_utc = @now
                    where id = @id;
                    """;
                var now = timeProvider.GetUtcNow();
                command.Parameters.AddWithValue("id", id);
                command.Parameters.AddWithValue("now", now);
                command.Parameters.AddWithValue("handled_by_user_id", handledByUserId);
                command.Parameters.AddWithValue("note", (object?)SanitizeError(note) ?? DBNull.Value);
                await command.ExecuteNonQueryAsync(ct);

                return new NotificationDeliveryAdminActionItemResponse(id, "Succeeded", "재발송 대기열에 등록했습니다.");
            },
            cancellationToken);
    }

    public async Task<Guid> CreateManualTestMailDeliveryAsync(
        NotificationDeliveryDisplaySnapshot? snapshot,
        CancellationToken cancellationToken)
    {
        return await CreateManualDeliveryAsync(
            NotificationDeliveryChannels.Mail,
            null,
            snapshot,
            "manual-test-mail",
            cancellationToken);
    }

    public async Task<Guid> CreateManualTestTeamsActivityDeliveryAsync(
        Guid recipientUserId,
        NotificationDeliveryDisplaySnapshot? snapshot,
        CancellationToken cancellationToken)
    {
        return await CreateManualDeliveryAsync(
            NotificationDeliveryChannels.TeamsActivity,
            recipientUserId,
            snapshot,
            "manual-test-teams-activity",
            cancellationToken);
    }

    public async Task<Guid> CreateManualNotificationAsync(
        Guid? projectId,
        Guid? workItemId,
        string notificationKind,
        string title,
        string message,
        string correlationId,
        string visibilityScope,
        string sourceKind,
        Guid requestedByUserId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            insert into notifications (
                id,
                project_id,
                work_item_id,
                notification_type,
                severity,
                title,
                message,
                link_url,
                idempotency_key,
                visibility_scope,
                source_kind,
                manual_requested_by_user_id
            )
            values (
                @id,
                @project_id,
                @work_item_id,
                'Info',
                @severity,
                @title,
                @message,
                @link_url,
                @idempotency_key,
                @visibility_scope,
                @source_kind,
                @manual_requested_by_user_id
            )
            on conflict (idempotency_key) do update
            set title = excluded.title,
                message = excluded.message,
                link_url = excluded.link_url,
                visibility_scope = excluded.visibility_scope,
                source_kind = excluded.source_kind,
                work_item_id = excluded.work_item_id,
                manual_requested_by_user_id = excluded.manual_requested_by_user_id
            returning id;
            """);
        var notificationId = Guid.NewGuid();
        command.Parameters.AddWithValue("id", notificationId);
        command.Parameters.AddWithValue("project_id", (object?)projectId ?? DBNull.Value);
        command.Parameters.AddWithValue("work_item_id", (object?)workItemId ?? DBNull.Value);
        command.Parameters.AddWithValue("severity", ManualNotificationSeverity(notificationKind));
        command.Parameters.AddWithValue("title", title);
        command.Parameters.AddWithValue("message", message);
        command.Parameters.AddWithValue("link_url", (object?)BuildTeamsActivityNotificationDetailUrl(notificationId) ?? DBNull.Value);
        command.Parameters.AddWithValue("idempotency_key", $"manual:{correlationId}");
        command.Parameters.AddWithValue("visibility_scope", NormalizeVisibilityScope(visibilityScope));
        command.Parameters.AddWithValue("source_kind", NormalizeSourceKind(sourceKind));
        command.Parameters.AddWithValue("manual_requested_by_user_id", requestedByUserId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid id
            ? id
            : throw new InvalidOperationException("Manual notification id was not returned.");
    }

    public async Task<Guid> CreateManualWorkItemAsync(
        Guid projectId,
        Guid assignedUserId,
        string workflowStageCode,
        string title,
        string message,
        DateOnly? dueDate,
        Guid createdByUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            insert into work_items (
                project_id,
                target_type,
                target_id,
                workflow_stage_code,
                responsibility_type,
                assigned_user_id,
                assigned_role_code,
                title,
                description,
                status,
                priority,
                due_date,
                idempotency_key,
                created_by_user_id
            )
            values (
                @project_id,
                'Project',
                @project_id,
                @workflow_stage_code,
                'ManualAssignment',
                @assigned_user_id,
                null,
                @title,
                @description,
                'Requested',
                'Normal',
                @due_date,
                @idempotency_key,
                @created_by_user_id
            )
            on conflict (idempotency_key) do update
            set title = excluded.title,
                description = excluded.description,
                due_date = excluded.due_date
            returning id;
            """);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("workflow_stage_code", workflowStageCode);
        command.Parameters.AddWithValue("assigned_user_id", assignedUserId);
        command.Parameters.AddWithValue("title", title);
        command.Parameters.AddWithValue("description", message);
        command.Parameters.AddWithValue("due_date", (object?)dueDate ?? DBNull.Value);
        command.Parameters.AddWithValue("idempotency_key", $"manual-work:{correlationId}:{assignedUserId:N}");
        command.Parameters.AddWithValue("created_by_user_id", createdByUserId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid id
            ? id
            : throw new InvalidOperationException("Manual work item id was not returned.");
    }

    public async Task<Guid> EnsureNotificationRecipientAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            insert into notification_recipients (notification_id, user_id)
            values (@notification_id, @user_id)
            on conflict (notification_id, user_id) do update
            set user_id = excluded.user_id
            returning id;
            """);
        command.Parameters.AddWithValue("notification_id", notificationId);
        command.Parameters.AddWithValue("user_id", userId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid id
            ? id
            : throw new InvalidOperationException("Manual notification recipient id was not returned.");
    }

    public async Task<Guid> CreateManualDeliveryAsync(
        string channel,
        Guid? recipientUserId,
        NotificationDeliveryDisplaySnapshot? snapshot,
        string groupKey,
        CancellationToken cancellationToken,
        Guid? projectId = null,
        NotificationManualPayload? manualPayload = null,
        Guid? requestedByUserId = null,
        Guid? notificationId = null,
        Guid? notificationRecipientId = null,
        Guid? workItemId = null)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            insert into notification_deliveries (
                notification_id,
                notification_recipient_id,
                project_id,
                work_item_id,
                recipient_user_id,
                channel,
                delivery_type,
                status,
                dedupe_key,
                group_key,
                next_attempt_at_utc,
                display_title,
                display_message,
                display_project_name,
                display_work_item_title,
                display_recipient_name,
                display_recipient_email,
                display_recipient_kind,
                display_channel_target,
                manual_notification_kind,
                correlation_id,
                manual_payload_json,
                manual_requested_by_user_id,
                manual_requested_at_utc
            )
            values (
                @notification_id,
                @notification_recipient_id,
                @project_id,
                @work_item_id,
                @recipient_user_id,
                @channel,
                'ManualTest',
                'Pending',
                @dedupe_key,
                @group_key,
                @now,
                @display_title,
                @display_message,
                @display_project_name,
                @display_work_item_title,
                @display_recipient_name,
                @display_recipient_email,
                @display_recipient_kind,
                @display_channel_target,
                @manual_notification_kind,
                @correlation_id,
                @manual_payload_json,
                @manual_requested_by_user_id,
                @manual_requested_at_utc
            )
            returning id;
            """);
        var now = timeProvider.GetUtcNow();
        command.Parameters.AddWithValue("notification_id", (object?)notificationId ?? DBNull.Value);
        command.Parameters.AddWithValue("notification_recipient_id", (object?)notificationRecipientId ?? DBNull.Value);
        command.Parameters.AddWithValue("project_id", (object?)projectId ?? DBNull.Value);
        command.Parameters.AddWithValue("work_item_id", (object?)workItemId ?? DBNull.Value);
        command.Parameters.AddWithValue("recipient_user_id", (object?)recipientUserId ?? DBNull.Value);
        command.Parameters.AddWithValue("channel", channel);
        command.Parameters.AddWithValue("dedupe_key", $"{groupKey}:{now:yyyyMMddHHmmss}:{Guid.NewGuid():N}");
        command.Parameters.AddWithValue("group_key", groupKey);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("display_title", (object?)TrimSnapshot(snapshot?.DisplayTitle, 300) ?? DBNull.Value);
        command.Parameters.AddWithValue("display_message", (object?)TrimSnapshot(snapshot?.DisplayMessage, 2_000) ?? DBNull.Value);
        command.Parameters.AddWithValue("display_project_name", (object?)TrimSnapshot(snapshot?.DisplayProjectName, 300) ?? DBNull.Value);
        command.Parameters.AddWithValue("display_work_item_title", (object?)TrimSnapshot(snapshot?.DisplayWorkItemTitle, 300) ?? DBNull.Value);
        command.Parameters.AddWithValue("display_recipient_name", (object?)TrimSnapshot(snapshot?.DisplayRecipientName, 200) ?? DBNull.Value);
        command.Parameters.AddWithValue("display_recipient_email", (object?)TrimSnapshot(snapshot?.DisplayRecipientEmail, 320) ?? DBNull.Value);
        command.Parameters.AddWithValue("display_recipient_kind", (object?)NormalizeDisplayRecipientKind(snapshot?.DisplayRecipientKind) ?? DBNull.Value);
        command.Parameters.AddWithValue("display_channel_target", (object?)TrimSnapshot(snapshot?.DisplayChannelTarget, 300) ?? DBNull.Value);
        command.Parameters.AddWithValue("manual_notification_kind", (object?)NormalizeManualNotificationKind(snapshot?.ManualNotificationKind) ?? DBNull.Value);
        command.Parameters.AddWithValue("correlation_id", (object?)TrimSnapshot(snapshot?.CorrelationId, 100) ?? DBNull.Value);
        command.Parameters.Add(new NpgsqlParameter("manual_payload_json", NpgsqlDbType.Jsonb)
        {
            Value = manualPayload is null ? DBNull.Value : JsonSerializer.Serialize(manualPayload)
        });
        command.Parameters.Add(new NpgsqlParameter("manual_requested_by_user_id", NpgsqlDbType.Uuid)
        {
            Value = (object?)requestedByUserId ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("manual_requested_at_utc", NpgsqlDbType.TimestampTz)
        {
            Value = manualPayload is null ? DBNull.Value : manualPayload.RequestedAtUtc
        });
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid deliveryId
            ? deliveryId
            : throw new InvalidOperationException("Manual notification delivery id was not returned.");
    }

    public async Task<TeamsActivityRecipientProfile?> GetTeamsActivityRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select id, display_name, email, entra_object_id, auth_provider, is_active
            from qms_users
            where id = @recipient_user_id;
            """);
        command.Parameters.AddWithValue("recipient_user_id", recipientUserId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new TeamsActivityRecipientProfile(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(4),
            reader.GetBoolean(5));
    }

    public async Task<TeamsActivityRecipientProfile?> GetActiveUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select id, display_name, email, entra_object_id, auth_provider, is_active
            from qms_users
            where is_active = true
              and lower(btrim(email)) = lower(btrim(@email))
            order by created_at_utc
            limit 1;
            """);
        command.Parameters.AddWithValue("email", email);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new TeamsActivityRecipientProfile(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(4),
            reader.GetBoolean(5));
    }

    public async Task<NotificationDeliveryMessage> RenderMessageAsync(NotificationDeliveryRecord delivery, CancellationToken cancellationToken)
    {
        if (delivery.DeliveryType == NotificationDeliveryTypes.DailyDigest && delivery.RecipientUserId is not null)
        {
            return await RenderDailyDigestAsync(delivery, cancellationToken);
        }

        if (delivery.DeliveryType == NotificationDeliveryTypes.ManualTest)
        {
            return RenderManualMessage(delivery);
        }

        return RenderAutomaticMessage(delivery);
    }

    private NotificationDeliveryMessage RenderAutomaticMessage(NotificationDeliveryRecord delivery)
    {
        var kindLabel = DeliveryTypeLabel(delivery);
        var title = ResolveDisplayTitle(delivery);
        var projectName = ResolveDisplayProject(delivery);
        var message = delivery.DisplayMessage
            ?? delivery.NotificationMessage
            ?? "알림이 생성되었습니다.";

        if (delivery.Channel == NotificationDeliveryChannels.TeamsActivity)
        {
            var detailUrl = ResolveTeamsActivityNotificationLink(delivery);
            return new NotificationDeliveryMessage(
                delivery.DeliveryId,
                delivery.Channel,
                delivery.DeliveryType,
                $"{kindLabel}, {title}",
                message,
                detailUrl ?? delivery.LinkUrl,
                delivery.DisplayRecipientName ?? delivery.RecipientDisplayName,
                delivery.DisplayRecipientEmail ?? delivery.RecipientEmail,
                CorrelationId: delivery.CorrelationId,
                RecipientUserId: delivery.RecipientUserId,
                RecipientEntraObjectId: delivery.RecipientEntraObjectId,
                RecipientAuthProvider: delivery.RecipientAuthProvider,
                RecipientUserIsActive: delivery.RecipientUserIsActive,
                TeamsActivityType: ResolveAutomaticTeamsActivityType(delivery.DeliveryType));
        }

        var linkUrl = ResolveExternalNotificationLink(delivery);
        var body = BuildNotificationBody(kindLabel, title, projectName, message, timeProvider.GetUtcNow(), linkUrl);
        var subject = delivery.Channel == NotificationDeliveryChannels.Mail
            ? $"[{kindLabel}] {title}"
            : "EMI 프로젝트 통합관리시스템 알림";

        return new NotificationDeliveryMessage(
            delivery.DeliveryId,
            delivery.Channel,
            delivery.DeliveryType,
            subject,
            body,
            linkUrl,
            delivery.DisplayRecipientName ?? delivery.RecipientDisplayName,
            delivery.DisplayRecipientEmail ?? delivery.RecipientEmail,
            delivery.Channel == NotificationDeliveryChannels.Mail,
            delivery.CorrelationId,
            RecipientUserId: delivery.RecipientUserId,
            RecipientEntraObjectId: delivery.RecipientEntraObjectId,
            RecipientAuthProvider: delivery.RecipientAuthProvider,
            RecipientUserIsActive: delivery.RecipientUserIsActive);
    }

    private NotificationDeliveryMessage RenderManualMessage(NotificationDeliveryRecord delivery)
    {
        var payload = ReadManualPayload(delivery);
        var kind = payload.NotificationKind;
        var kindLabel = !string.IsNullOrWhiteSpace(payload.NotificationKindLabel)
            ? payload.NotificationKindLabel
            : ManualNotificationKindLabel(kind) ?? "수동 알림";
        var title = string.IsNullOrWhiteSpace(payload.Title)
            ? ResolveDisplayTitle(delivery)
            : payload.Title;
        var projectName = string.IsNullOrWhiteSpace(payload.ProjectName)
            ? ResolveDisplayProject(delivery)
            : payload.ProjectName;
        var message = string.IsNullOrWhiteSpace(payload.Message)
            ? delivery.DisplayMessage ?? delivery.NotificationMessage ?? "관리자 수동 알림입니다."
            : payload.Message;
        var requestedAtUtc = payload.RequestedAtUtc == default
            ? delivery.ManualRequestedAtUtc ?? delivery.CreatedAtUtc
            : payload.RequestedAtUtc;

        if (delivery.Channel == NotificationDeliveryChannels.TeamsActivity)
        {
            var detailUrl = ResolveTeamsActivityNotificationLink(delivery);
            return new NotificationDeliveryMessage(
                delivery.DeliveryId,
                delivery.Channel,
                delivery.DeliveryType,
                $"{kindLabel}, {title}",
                message,
                detailUrl ?? delivery.LinkUrl,
                delivery.DisplayRecipientName ?? delivery.RecipientDisplayName,
                delivery.DisplayRecipientEmail ?? delivery.RecipientEmail,
                CorrelationId: delivery.CorrelationId,
                RecipientUserId: delivery.RecipientUserId,
                RecipientEntraObjectId: delivery.RecipientEntraObjectId,
                RecipientAuthProvider: delivery.RecipientAuthProvider,
                RecipientUserIsActive: delivery.RecipientUserIsActive,
                TeamsActivityType: ResolveManualTeamsActivityType(kind));
        }

        var linkUrl = ResolveExternalNotificationLink(delivery);
        var body = BuildNotificationBody(kindLabel, title, projectName, message, requestedAtUtc, linkUrl);
        var subject = delivery.Channel == NotificationDeliveryChannels.Mail
            ? $"[{kindLabel}] {title}"
            : "EMI 프로젝트 통합관리시스템 알림";
        return new NotificationDeliveryMessage(
            delivery.DeliveryId,
            delivery.Channel,
            delivery.DeliveryType,
            subject,
            body,
            linkUrl,
            delivery.DisplayRecipientName ?? delivery.RecipientDisplayName,
            delivery.DisplayRecipientEmail ?? delivery.RecipientEmail,
            delivery.Channel == NotificationDeliveryChannels.Mail,
            delivery.CorrelationId,
            RecipientUserId: delivery.RecipientUserId,
            RecipientEntraObjectId: delivery.RecipientEntraObjectId,
            RecipientAuthProvider: delivery.RecipientAuthProvider,
            RecipientUserIsActive: delivery.RecipientUserIsActive);
    }

    private static NotificationManualPayload ReadManualPayload(NotificationDeliveryRecord delivery)
    {
        if (!string.IsNullOrWhiteSpace(delivery.ManualPayloadJson))
        {
            try
            {
                var payload = JsonSerializer.Deserialize<NotificationManualPayload>(delivery.ManualPayloadJson);
                if (payload is not null)
                {
                    return payload;
                }
            }
            catch (JsonException)
            {
                // Fall through to the display snapshot fallback. The worker should still send a readable manual notification.
            }
        }

        var kind = NormalizeManualNotificationKind(delivery.ManualNotificationKind) ?? NotificationManualKinds.Custom;
        return new NotificationManualPayload(
            kind,
            ManualNotificationKindLabel(kind) ?? "수동 알림",
            ResolveDisplayTitle(delivery),
            ResolveDisplayProject(delivery),
            delivery.DisplayMessage ?? delivery.NotificationMessage ?? "관리자 수동 알림입니다.",
            delivery.ManualRequestedAtUtc ?? delivery.CreatedAtUtc);
    }

    private static string BuildNotificationBody(
        string kindLabel,
        string title,
        string? projectName,
        string message,
        DateTimeOffset requestedAtUtc,
        string? detailUrl = null)
    {
        var detailLine = string.IsNullOrWhiteSpace(detailUrl)
            ? string.Empty
            : $"""

                알림 상세 보기:
                {detailUrl}
                """;
        return $"""
            EMI 프로젝트 통합관리시스템 알림

            알림 유형: {kindLabel}
            프로젝트명: {projectName ?? "기타"}

            제목: {title}
            내용:
            {message}

            발송시각: {FormatKoreanDateTime(requestedAtUtc)}
            {detailLine}

            끝.
            """;
    }

    private string? BuildTeamsActivityDeliveryDetailUrl(Guid deliveryId)
    {
        return new NotificationLinkBuilder(configuration).BuildDeliveryDetailUrl(deliveryId);
    }

    private string? ResolveExternalNotificationLink(NotificationDeliveryRecord delivery)
    {
        return delivery.NotificationId is { } notificationId
            ? BuildTeamsActivityNotificationDetailUrl(notificationId)
            : delivery.LinkUrl;
    }

    private string? ResolveTeamsActivityNotificationLink(NotificationDeliveryRecord delivery)
    {
        if (delivery.NotificationId is { } notificationId)
        {
            return new NotificationLinkBuilder(configuration).BuildTeamsActivityNotificationWebUrl(notificationId);
        }

        return delivery.LinkUrl;
    }

    private string? BuildTeamsActivityNotificationDetailUrl(Guid notificationId)
    {
        return new NotificationLinkBuilder(configuration).BuildNotificationDetailUrl(notificationId);
    }

    private string ResolveManualTeamsActivityType(string? kind)
    {
        var prefix = "Notifications:TeamsActivity:ActivityTypes:";
        return kind switch
        {
            NotificationManualKinds.Urgent => configuration[$"{prefix}UrgentPending"] ?? "urgentPending",
            NotificationManualKinds.DailyDigest => configuration[$"{prefix}DailyDigest"] ?? "dailyDigest",
            _ => configuration[$"{prefix}WorkItemAssigned"] ?? "workItemAssigned"
        };
    }

    private string ResolveAutomaticTeamsActivityType(string deliveryType)
    {
        var prefix = "Notifications:TeamsActivity:ActivityTypes:";
        return deliveryType switch
        {
            NotificationDeliveryTypes.DueSoonL0 => configuration[$"{prefix}DeadlineApproaching"] ?? "deadlineApproaching",
            NotificationDeliveryTypes.OverdueL1
                or NotificationDeliveryTypes.OverdueL2
                or NotificationDeliveryTypes.OverdueL3 => configuration[$"{prefix}DeadlineOverdue"] ?? "deadlineOverdue",
            NotificationDeliveryTypes.UrgentBlocking => configuration[$"{prefix}UrgentPending"] ?? "urgentPending",
            NotificationDeliveryTypes.DailyDigest => configuration[$"{prefix}DailyDigest"] ?? "dailyDigest",
            NotificationDeliveryTypes.ProjectCompletion => configuration[$"{prefix}ProjectCompleted"] ?? "projectCompleted",
            _ => configuration[$"{prefix}WorkItemAssigned"] ?? "workItemAssigned"
        };
    }

    private static string FormatKoreanDateTime(DateTimeOffset value)
    {
        return value.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string ResolveDailyDigestDateLabel(NotificationDeliveryRecord delivery)
    {
        foreach (var value in new[] { delivery.GroupKey, delivery.DedupeKey })
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            foreach (var part in value.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (DateOnly.TryParseExact(
                    part,
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var date))
                {
                    return date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }

        return delivery.CreatedAtUtc
            .ToOffset(TimeSpan.FromHours(9))
            .ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string ManualNotificationSeverity(string notificationKind)
    {
        return string.Equals(notificationKind, NotificationManualKinds.Urgent, StringComparison.Ordinal)
            ? "Critical"
            : "Info";
    }

    private static string NormalizeVisibilityScope(string? value)
    {
        return value?.Trim() switch
        {
            NotificationVisibilityScopes.Authenticated => NotificationVisibilityScopes.Authenticated,
            NotificationVisibilityScopes.AdminOnly => NotificationVisibilityScopes.AdminOnly,
            _ => NotificationVisibilityScopes.RecipientOnly
        };
    }

    private static string NormalizeSourceKind(string? value)
    {
        return value?.Trim() switch
        {
            NotificationSourceKinds.Manual => NotificationSourceKinds.Manual,
            NotificationSourceKinds.ChannelNotice => NotificationSourceKinds.ChannelNotice,
            NotificationSourceKinds.WorkAssignment => NotificationSourceKinds.WorkAssignment,
            NotificationSourceKinds.DailyDigest => NotificationSourceKinds.DailyDigest,
            NotificationSourceKinds.Escalation => NotificationSourceKinds.Escalation,
            NotificationSourceKinds.System => NotificationSourceKinds.System,
            _ => NotificationSourceKinds.Automatic
        };
    }

    private async Task<NotificationDeliveryMessage> RenderDailyDigestAsync(NotificationDeliveryRecord delivery, CancellationToken cancellationToken)
    {
        var kindLabel = DeliveryTypeLabel(NotificationDeliveryTypes.DailyDigest);
        var digestTitle = $"{ResolveDailyDigestDateLabel(delivery)} 업무 요약";

        if (delivery.RecipientUserId is not { } recipientUserId)
        {
            var body = BuildNotificationBody(
                kindLabel,
                digestTitle,
                "해당 없음",
                "수신자 정보가 없어 일일 요약을 생성할 수 없습니다.",
                timeProvider.GetUtcNow());
            return new NotificationDeliveryMessage(
                delivery.DeliveryId,
                delivery.Channel,
                delivery.DeliveryType,
                $"[{kindLabel}] {digestTitle}",
                body,
                null,
                delivery.RecipientDisplayName,
                delivery.RecipientEmail,
                RecipientUserId: delivery.RecipientUserId,
                RecipientEntraObjectId: delivery.RecipientEntraObjectId,
                RecipientAuthProvider: delivery.RecipientAuthProvider,
                RecipientUserIsActive: delivery.RecipientUserIsActive);
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var lines = new List<string>
        {
            $"{delivery.RecipientDisplayName ?? "사용자"}님의 일일 업무 요약입니다.",
            ""
        };

        await AppendAssignedProjectDigestSectionAsync(
            connection,
            lines,
            recipientUserId,
            cancellationToken);

        await AppendDigestSectionAsync(
            connection,
            lines,
            "내 미완료 업무",
            """
            select wi.title, p.project_title, wi.due_date, null::text
            from work_items wi
            join projects p on p.id = wi.project_id
            where wi.assigned_user_id = @user_id
              and wi.status in ('Requested', 'InProgress')
            order by wi.due_date nulls last, wi.created_at_utc desc
            limit 10;
            """,
            recipientUserId,
            cancellationToken);

        await AppendRecentWorkDigestSectionAsync(
            connection,
            lines,
            recipientUserId,
            timeProvider.GetUtcNow().AddDays(-1),
            cancellationToken);

        await AppendDigestSectionAsync(
            connection,
            lines,
            "읽지 않은 참조 알림",
            """
            select n.title, coalesce(p.project_title, ''), null::date, n.link_url
            from notification_recipients nr
            join notifications n on n.id = nr.notification_id
            left join projects p on p.id = n.project_id
            where nr.user_id = @user_id
              and nr.read_at_utc is null
              and n.notification_type in ('Reference', 'Info')
            order by n.created_at_utc desc
            limit 10;
            """,
            recipientUserId,
            cancellationToken);

        var content = string.Join(Environment.NewLine, lines).Trim();
        var bodyText = BuildNotificationBody(
            kindLabel,
            digestTitle,
            "여러 프로젝트",
            string.IsNullOrWhiteSpace(content) ? "오늘 표시할 업무 요약이 없습니다." : content,
            timeProvider.GetUtcNow());

        if (delivery.Channel == NotificationDeliveryChannels.TeamsActivity)
        {
            var linkUrl = ResolveTeamsActivityNotificationLink(delivery);
            return new NotificationDeliveryMessage(
                delivery.DeliveryId,
                delivery.Channel,
                delivery.DeliveryType,
                $"{kindLabel}, {digestTitle}",
                string.IsNullOrWhiteSpace(content) ? "오늘 표시할 업무 요약이 없습니다." : content,
                linkUrl,
                delivery.RecipientDisplayName,
                delivery.RecipientEmail,
                CorrelationId: delivery.CorrelationId,
                RecipientUserId: delivery.RecipientUserId,
                RecipientEntraObjectId: delivery.RecipientEntraObjectId,
                RecipientAuthProvider: delivery.RecipientAuthProvider,
                RecipientUserIsActive: delivery.RecipientUserIsActive,
                TeamsActivityType: ResolveAutomaticTeamsActivityType(delivery.DeliveryType));
        }

        return new NotificationDeliveryMessage(
            delivery.DeliveryId,
            delivery.Channel,
            delivery.DeliveryType,
            delivery.Channel == NotificationDeliveryChannels.Mail
                ? $"[{kindLabel}] {digestTitle}"
                : "EMI 프로젝트 통합관리시스템 알림",
            bodyText,
            null,
            delivery.RecipientDisplayName,
            delivery.RecipientEmail,
            delivery.Channel == NotificationDeliveryChannels.Mail,
            RecipientUserId: delivery.RecipientUserId,
            RecipientEntraObjectId: delivery.RecipientEntraObjectId,
            RecipientAuthProvider: delivery.RecipientAuthProvider,
            RecipientUserIsActive: delivery.RecipientUserIsActive);
    }

    private async Task<int> InsertUrgentTeamsChannelDeliveriesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateTimeOffset dedupeAfter,
        int batchWindowSeconds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into notification_deliveries (
                notification_id, project_id, channel, delivery_type, dedupe_key, group_key, next_attempt_at_utc,
                display_title, display_message, display_project_name, display_recipient_kind, display_channel_target
            )
            select
                n.id,
                n.project_id,
                'TeamsChannel',
                'UrgentBlocking',
                concat('notification:', n.id::text, ':teams-channel:urgent'),
                concat('urgent:', coalesce(n.project_id::text, n.id::text), ':', floor(extract(epoch from n.created_at_utc) / @batch_window_seconds)::bigint),
                @now,
                n.title,
                n.message,
                p.project_title,
                'TeamsChannel',
                'Teams 채널'
            from notifications n
            left join projects p on p.id = n.project_id
            where (n.notification_type = 'Blocking' or n.severity = 'Critical')
              and not exists (
                  select 1
                  from notification_deliveries existing
                  where existing.dedupe_key = concat('notification:', n.id::text, ':teams-channel:urgent')
                    and existing.created_at_utc >= @dedupe_after
              )
            on conflict do nothing;
            """;
        command.Parameters.AddWithValue("now", timeProvider.GetUtcNow());
        command.Parameters.AddWithValue("dedupe_after", dedupeAfter);
        command.Parameters.AddWithValue("batch_window_seconds", batchWindowSeconds);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<int> InsertUrgentMailDeliveriesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateTimeOffset dedupeAfter,
        int batchWindowSeconds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into notification_deliveries (
                notification_id, notification_recipient_id, recipient_user_id, project_id,
                channel, delivery_type, dedupe_key, group_key, next_attempt_at_utc,
                display_title, display_message, display_project_name,
                display_recipient_name, display_recipient_email, display_recipient_kind
            )
            select
                n.id,
                nr.id,
                nr.user_id,
                n.project_id,
                'Mail',
                'UrgentBlocking',
                concat('notification:', n.id::text, ':mail:', nr.user_id::text, ':urgent'),
                concat('urgent:', coalesce(n.project_id::text, n.id::text), ':', floor(extract(epoch from n.created_at_utc) / @batch_window_seconds)::bigint),
                @now,
                n.title,
                n.message,
                p.project_title,
                u.display_name,
                u.email,
                'User'
            from notifications n
            join notification_recipients nr on nr.notification_id = n.id
            join qms_users u on u.id = nr.user_id
            left join projects p on p.id = n.project_id
            where (n.notification_type = 'Blocking' or n.severity = 'Critical')
              and not exists (
                  select 1
                  from notification_deliveries existing
                  where existing.dedupe_key = concat('notification:', n.id::text, ':mail:', nr.user_id::text, ':urgent')
                    and existing.created_at_utc >= @dedupe_after
              )
            on conflict do nothing;
            """;
        command.Parameters.AddWithValue("now", timeProvider.GetUtcNow());
        command.Parameters.AddWithValue("dedupe_after", dedupeAfter);
        command.Parameters.AddWithValue("batch_window_seconds", batchWindowSeconds);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<int> InsertWorkItemTeamsDirectMessageDeliveriesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateTimeOffset dedupeAfter,
        int batchWindowSeconds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into notification_deliveries (
                notification_id, notification_recipient_id, recipient_user_id, project_id,
                channel, delivery_type, dedupe_key, group_key, next_attempt_at_utc,
                display_title, display_message, display_project_name,
                display_recipient_name, display_recipient_email, display_recipient_kind
            )
            select
                n.id,
                nr.id,
                nr.user_id,
                n.project_id,
                'TeamsDirectMessage',
                'WorkItemCreated',
                concat('notification:', n.id::text, ':teams-dm:', nr.user_id::text, ':work-item-created'),
                concat('work-item:', coalesce(n.project_id::text, n.id::text), ':', floor(extract(epoch from n.created_at_utc) / @batch_window_seconds)::bigint),
                @now,
                n.title,
                n.message,
                p.project_title,
                u.display_name,
                u.email,
                'User'
            from notifications n
            join notification_recipients nr on nr.notification_id = n.id
            join qms_users u on u.id = nr.user_id
            left join projects p on p.id = n.project_id
            where n.title like '%업무%생성%'
              and not exists (
                  select 1
                  from notification_deliveries existing
                  where existing.dedupe_key = concat('notification:', n.id::text, ':teams-dm:', nr.user_id::text, ':work-item-created')
                    and existing.created_at_utc >= @dedupe_after
              )
            on conflict do nothing;
            """;
        command.Parameters.AddWithValue("now", timeProvider.GetUtcNow());
        command.Parameters.AddWithValue("dedupe_after", dedupeAfter);
        command.Parameters.AddWithValue("batch_window_seconds", batchWindowSeconds);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task AppendDigestSectionAsync(
        NpgsqlConnection connection,
        List<string> lines,
        string title,
        string sql,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("user_id", userId);

        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var itemTitle = reader.GetString(0);
            var projectTitle = reader.GetString(1);
            var dueDate = reader.IsDBNull(2) ? null : reader.GetFieldValue<DateOnly>(2).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            var linkUrl = reader.IsDBNull(3) ? null : reader.GetString(3);
            var dueText = string.IsNullOrWhiteSpace(dueDate) ? "" : $" / 예정일 {dueDate}";
            var linkText = string.IsNullOrWhiteSpace(linkUrl) ? "" : $" / {linkUrl}";
            rows.Add($"- {itemTitle} ({projectTitle}){dueText}{linkText}");
        }

        if (rows.Count == 0)
        {
            return;
        }

        lines.Add(title);
        lines.AddRange(rows);
        lines.Add("");
    }

    private static async Task AppendRecentWorkDigestSectionAsync(
        NpgsqlConnection connection,
        List<string> lines,
        Guid userId,
        DateTimeOffset sinceUtc,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select wi.title, p.project_title, wi.created_at_utc
            from work_items wi
            join projects p on p.id = wi.project_id
            where wi.assigned_user_id = @user_id
              and wi.created_at_utc >= @since_utc
            order by wi.created_at_utc desc
            limit 10;
            """;
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("since_utc", sinceUtc);

        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add($"- {reader.GetString(0)} ({reader.GetString(1)}) / 생성 {reader.GetFieldValue<DateTimeOffset>(2):yyyy-MM-dd HH:mm} UTC");
        }

        if (rows.Count == 0)
        {
            return;
        }

        lines.Add("어제 새로 생성된 내 업무");
        lines.AddRange(rows);
        lines.Add("");
    }

    private static async Task AppendAssignedProjectDigestSectionAsync(
        NpgsqlConnection connection,
        List<string> lines,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                p.id,
                p.project_title,
                p.delivery_date,
                array_agg(distinct pa.responsibility_type order by pa.responsibility_type) as responsibilities
            from project_assignees pa
            join projects p on p.id = pa.project_id
            where pa.assigned_user_id = @user_id
              and p.deleted_at_utc is null
              and p.status = 'Active'
            group by p.id, p.project_title, p.delivery_date
            order by p.delivery_date nulls last, p.project_title
            limit 10;
            """;
        command.Parameters.AddWithValue("user_id", userId);

        var rows = new List<AssignedProjectDigestRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new AssignedProjectDigestRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetFieldValue<DateOnly>(2),
                reader.GetFieldValue<string[]>(3)));
        }

        if (rows.Count == 0)
        {
            return;
        }

        lines.Add("내 담당 프로젝트 요약");
        foreach (var row in rows)
        {
            var deliveryDate = row.DeliveryDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "미등록";
            var labels = row.ResponsibilityTypes
                .Select(DigestResponsibilityLabel)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(label => label, StringComparer.Ordinal)
                .ToArray();
            var responsibilityText = labels.Length == 0 ? "미등록" : string.Join(", ", labels);
            lines.Add($"- {row.ProjectTitle} / 납기일 {deliveryDate} / 담당역할 {responsibilityText} / /projects/{row.ProjectId}");
        }

        lines.Add("");
    }

    private bool TryGetDailyDigestWindow(
        NotificationDailyDigestOptions options,
        DateTimeOffset nowUtc,
        out DateOnly digestDate,
        out DateTimeOffset previousDayStartUtc,
        out DateTimeOffset previousDayEndUtc)
    {
        var timeZone = ResolveTimeZone(options.TimeZone);
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, timeZone);
        var digestTime = TimeOnly.TryParse(options.Time, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : new TimeOnly(7, 30);

        digestDate = DateOnly.FromDateTime(localNow.Date);
        if (TimeOnly.FromDateTime(localNow.DateTime) < digestTime)
        {
            previousDayStartUtc = default;
            previousDayEndUtc = default;
            return false;
        }

        var previousLocalDate = digestDate.AddDays(-1);
        var previousLocalStart = previousLocalDate.ToDateTime(TimeOnly.MinValue);
        var previousLocalEnd = digestDate.ToDateTime(TimeOnly.MinValue);
        previousDayStartUtc = TimeZoneInfo.ConvertTimeToUtc(previousLocalStart, timeZone);
        previousDayEndUtc = TimeZoneInfo.ConvertTimeToUtc(previousLocalEnd, timeZone);
        return true;
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(timeZoneId) ? "Asia/Seoul" : timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
        }
    }

    private string BuildLink(string linkUrl)
    {
        if (Uri.TryCreate(linkUrl, UriKind.Absolute, out _))
        {
            return linkUrl;
        }

        var origin = configuration["FRONTEND_ORIGIN"]
            ?? configuration["Frontend:Origin"]
            ?? "";
        return string.IsNullOrWhiteSpace(origin)
            ? linkUrl
            : $"{origin.TrimEnd('/')}/{linkUrl.TrimStart('/')}";
    }

    private static string DigestResponsibilityLabel(string responsibilityType)
    {
        return responsibilityType switch
        {
            "SalesPrimary" => "영업 정담당자",
            "SalesSecondary" => "영업 부담당자",
            "DesignPrimary" => "설계 정담당자",
            "DesignSecondary" => "설계 부담당자",
            "ProductionPlanningPrimary" => "생산관리 정담당자",
            "ProductionPlanningSecondary" => "생산관리 부담당자",
            "ProcurementPrimary" => "구매 정담당자",
            "ProcurementSecondary" => "구매 부담당자",
            "MaterialsPrimary" => "자재 정담당자",
            "MaterialsSecondary" => "자재 부담당자",
            "ManufacturingPrimary" => "제조 정담당자",
            "ManufacturingSecondary" => "제조 부담당자",
            "LogisticsPrimary" => "물류 정담당자",
            "LogisticsSecondary" => "물류 부담당자",
            "QualityIQC" => "IQC 정담당자",
            "QualityIQCSecondary" => "IQC 부담당자",
            "QualityLQC" => "LQC 정담당자",
            "QualityLQCSecondary" => "LQC 부담당자",
            "QualityOQC" => "OQC 정담당자",
            "QualityOQCSecondary" => "OQC 부담당자",
            "QualityCustomerInspection" => "전진검수/FAT 정담당자",
            "QualityCustomerInspectionSecondary" => "전진검수/FAT 부담당자",
            _ => ProductionPlanningDomain.ResponsibilityLabel(responsibilityType)
        };
    }

    private static string? SanitizeError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var singleLine = string.Join(' ', message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var masked = Regex.Replace(singleLine, @"[A-Za-z0-9_-]{16,}", "[MASKED_ID]");
        return masked.Length <= 500 ? masked : masked[..500];
    }

    private static string? NormalizeFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeHandlingFilter(string? value)
    {
        var normalized = NormalizeFilter(value);
        return normalized switch
        {
            null => null,
            "Open" or "Acknowledged" or "Dismissed" => normalized,
            _ => null
        };
    }

    private static string? TrimSnapshot(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? NormalizeDisplayRecipientKind(string? value)
    {
        var normalized = NormalizeFilter(value);
        return normalized switch
        {
            NotificationDisplayRecipientKinds.User
                or NotificationDisplayRecipientKinds.Email
                or NotificationDisplayRecipientKinds.TeamsChannel
                or NotificationDisplayRecipientKinds.Unknown => normalized,
            _ => null
        };
    }

    private static string? NormalizeManualNotificationKind(string? value)
    {
        var normalized = NormalizeFilter(value);
        return normalized switch
        {
            NotificationManualKinds.ProjectCreated
                or NotificationManualKinds.WorkItemAssigned
                or NotificationManualKinds.Urgent
                or NotificationManualKinds.DailyDigest
                or NotificationManualKinds.Custom => normalized,
            _ => NotificationManualKinds.Custom
        };
    }

    private async Task<NotificationDeliveryAdminActionResponse> MutateDeliveriesAsync(
        IReadOnlyList<Guid> ids,
        Func<NpgsqlConnection, NpgsqlTransaction, Guid, CancellationToken, Task<NotificationDeliveryAdminActionItemResponse>> mutateOne,
        CancellationToken cancellationToken)
    {
        var distinctIds = ids.Distinct().ToArray();
        if (distinctIds.Length == 0)
        {
            return new NotificationDeliveryAdminActionResponse(0, 0, 0, 0, []);
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var results = new List<NotificationDeliveryAdminActionItemResponse>();
        foreach (var id in distinctIds)
        {
            results.Add(await mutateOne(connection, transaction, id, cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
        return new NotificationDeliveryAdminActionResponse(
            distinctIds.Length,
            results.Count(item => item.Status == "Succeeded"),
            results.Count(item => item.Status == "Failed"),
            results.Count(item => item.Status == "Skipped"),
            results);
    }

    private static async Task<string?> ReadDeliveryStatusAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid deliveryId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select status
            from notification_deliveries
            where id = @id
            for update;
            """;
        command.Parameters.AddWithValue("id", deliveryId);
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    private static NotificationDeliveryResponse ToResponse(NotificationDeliveryRecord row, DateTimeOffset now)
    {
        var responseRecipientEmail = !string.IsNullOrWhiteSpace(row.DisplayRecipientEmail)
            ? row.DisplayRecipientEmail
            : row.RecipientEmail;
        var notificationMessageSummary = Summarize(row.NotificationMessage, 140);
        var displayMessageSummary = Summarize(row.DisplayMessage ?? row.NotificationMessage, 160);
        var displayTitle = ResolveDisplayTitle(row);
        var displayRecipient = ResolveDisplayRecipient(row);
        var displayProject = ResolveDisplayProject(row);
        var handlingStatus = string.IsNullOrWhiteSpace(row.AdminHandlingStatus)
            ? NotificationDeliveryAdminHandlingStatuses.Open
            : row.AdminHandlingStatus;

        return new NotificationDeliveryResponse(
            row.DeliveryId,
            row.NotificationId,
            row.RecipientUserId,
            row.ProjectId,
            row.WorkItemId,
            row.Channel,
            ChannelLabel(row.Channel),
            row.DeliveryType,
            DeliveryTypeLabel(row),
            row.Status,
            StatusLabel(row.Status),
            row.AttemptCount,
            row.NextAttemptAtUtc,
            row.LastAttemptAtUtc,
            row.SentAtUtc,
            row.SuppressedAtUtc,
            row.ErrorCode,
            SanitizeError(row.ErrorMessage),
            ActionGuide(row, now),
            PendingReason(row, now),
            row.DisplayRecipientName ?? row.RecipientDisplayName,
            responseRecipientEmail,
            MaskAddress(responseRecipientEmail),
            row.ProjectTitle,
            row.ProjectCode,
            row.DisplayWorkItemTitle ?? row.WorkItemTitle,
            row.WorkflowStageName,
            row.NotificationTitle,
            notificationMessageSummary,
            displayMessageSummary,
            displayTitle,
            displayRecipient,
            displayProject,
            row.DisplayRecipientKind,
            row.DisplayChannelTarget,
            row.ManualNotificationKind,
            ManualNotificationKindLabel(row.ManualNotificationKind),
            row.CorrelationId,
            row.LinkUrl,
            handlingStatus,
            AdminHandlingStatusLabel(handlingStatus),
            row.AdminHandledAtUtc,
            row.AdminHandledByUserId,
            row.AdminHandledByDisplayName,
            row.AdminHandlingNote,
            row.ClaimedAtUtc,
            row.ClaimExpiresAtUtc,
            row.Status == NotificationDeliveryStatuses.Processing && row.ClaimExpiresAtUtc <= now,
            row.CreatedAtUtc,
            row.UpdatedAtUtc);
    }

    private static NotificationDeliveryDetailResponse ToDetailResponse(
        NotificationDeliveryRecord row,
        IReadOnlyList<NotificationDeliveryAttemptResponse> attempts,
        DateTimeOffset now)
    {
        var handlingStatus = string.IsNullOrWhiteSpace(row.AdminHandlingStatus)
            ? NotificationDeliveryAdminHandlingStatuses.Open
            : row.AdminHandlingStatus;
        return new NotificationDeliveryDetailResponse(
            row.DeliveryId,
            ResolveCategoryLabel(row),
            ManualNotificationKindLabel(row.ManualNotificationKind),
            ResolveDisplayProject(row),
            ResolveDisplayTitle(row),
            row.DisplayMessage ?? row.NotificationMessage,
            row.ManualRequestedAtUtc,
            row.CreatedAtUtc,
            row.Channel,
            ChannelLabel(row.Channel),
            ResolveDisplayRecipient(row),
            row.Status,
            StatusLabel(row.Status),
            row.AttemptCount,
            row.NextAttemptAtUtc,
            row.LastAttemptAtUtc,
            row.SentAtUtc,
            row.ErrorCode,
            SanitizeError(row.ErrorMessage),
            ActionGuide(row, now),
            handlingStatus,
            AdminHandlingStatusLabel(handlingStatus),
            row.AdminHandlingNote,
            row.CorrelationId,
            MaskProviderMessageId(row.ProviderMessageId),
            row.ClaimedAtUtc,
            row.ClaimExpiresAtUtc,
            row.Status == NotificationDeliveryStatuses.Processing && row.ClaimExpiresAtUtc <= now,
            MaskWorkerInstanceId(row.ClaimedByInstanceId),
            attempts);
    }

    private static async Task<IReadOnlyList<NotificationDeliveryAttemptResponse>> ListDeliveryAttemptsAsync(
        NpgsqlDataSource dataSource,
        Guid deliveryId,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            select
                attempt_no,
                worker_instance_id,
                claimed_at_utc,
                lease_expires_at_utc,
                provider_call_started_at_utc,
                completed_at_utc,
                outcome,
                error_code,
                error_message,
                provider_message_id
            from notification_delivery_attempts
            where delivery_id = @delivery_id
            order by attempt_no desc;
            """);
        command.Parameters.AddWithValue("delivery_id", deliveryId);

        var attempts = new List<NotificationDeliveryAttemptResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            attempts.Add(new NotificationDeliveryAttemptResponse(
                reader.GetInt32(0),
                MaskWorkerInstanceId(reader.GetString(1)) ?? "opaque",
                reader.GetFieldValue<DateTimeOffset>(2),
                reader.GetFieldValue<DateTimeOffset>(3),
                reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
                reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
                reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : SanitizeError(reader.GetString(8)),
                reader.IsDBNull(9) ? null : MaskProviderMessageId(reader.GetString(9))));
        }

        return attempts;
    }

    private static string ResolveCategoryLabel(NotificationDeliveryRecord row)
    {
        if (row.DeliveryType == NotificationDeliveryTypes.ManualTest)
        {
            return "관리자 수동 발송";
        }

        if (row.DeliveryType == NotificationDeliveryTypes.DailyDigest)
        {
            return "일일 업무 요약";
        }

        if (row.DeliveryType is NotificationDeliveryTypes.DueSoonL0
            or NotificationDeliveryTypes.OverdueL1
            or NotificationDeliveryTypes.OverdueL2
            or NotificationDeliveryTypes.OverdueL3)
        {
            return "에스컬레이션 알림";
        }

        return "자동 알림";
    }

    private static string? MaskProviderMessageId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Length <= 24)
        {
            return value;
        }

        return value[..12] + "..." + value[^8..];
    }

    private static string? MaskWorkerInstanceId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= 8 ? "opaque" : value[..4] + "..." + value[^4..];
    }

    private static string ResolveDisplayTitle(NotificationDeliveryRecord row)
    {
        if (!string.IsNullOrWhiteSpace(row.DisplayTitle))
        {
            return row.DisplayTitle;
        }

        if (row.DeliveryType == NotificationDeliveryTypes.ManualTest
            && !string.IsNullOrWhiteSpace(row.ManualNotificationKind))
        {
            return ManualNotificationKindLabel(row.ManualNotificationKind) ?? "수동 발송";
        }

        if (!string.IsNullOrWhiteSpace(row.NotificationTitle))
        {
            return row.NotificationTitle;
        }

        if (!string.IsNullOrWhiteSpace(row.WorkItemTitle))
        {
            return row.WorkItemTitle;
        }

        return row.DeliveryType == NotificationDeliveryTypes.ManualTest
            ? "수동 발송"
            : DeliveryTypeLabel(row.DeliveryType);
    }

    private static string ResolveDisplayRecipient(NotificationDeliveryRecord row)
    {
        if (row.Channel == NotificationDeliveryChannels.TeamsChannel)
        {
            return !string.IsNullOrWhiteSpace(row.DisplayChannelTarget)
                ? row.DisplayChannelTarget
                : "Teams 채널";
        }

        if (!string.IsNullOrWhiteSpace(row.RecipientDisplayName))
        {
            return row.RecipientDisplayName;
        }

        if (!string.IsNullOrWhiteSpace(row.DisplayRecipientName))
        {
            return row.DisplayRecipientName;
        }

        if (!string.IsNullOrWhiteSpace(row.DisplayRecipientEmail))
        {
            return MaskAddress(row.DisplayRecipientEmail) ?? "메일 수신자 미등록";
        }

        if (!string.IsNullOrWhiteSpace(row.RecipientEmail))
        {
            return MaskAddress(row.RecipientEmail) ?? "수신자 미등록";
        }

        return row.Channel switch
        {
            NotificationDeliveryChannels.TeamsActivity => "Activity Feed 수신자",
            NotificationDeliveryChannels.Mail => "메일 수신자 미등록",
            _ => "수신자 미등록"
        };
    }

    private static string ResolveDisplayProject(NotificationDeliveryRecord row)
    {
        if (!string.IsNullOrWhiteSpace(row.DisplayProjectName))
        {
            return row.DisplayProjectName;
        }

        if (!string.IsNullOrWhiteSpace(row.ProjectTitle))
        {
            return string.IsNullOrWhiteSpace(row.ProjectCode)
                ? row.ProjectTitle
                : $"{row.ProjectTitle} · {row.ProjectCode}";
        }

        return "프로젝트 없음";
    }

    private static string? Summarize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "...";
    }

    private static string? MaskAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var at = value.LastIndexOf('@');
        if (at <= 0 || at == value.Length - 1)
        {
            return value.Length <= 4 ? "***" : value[..1] + "***";
        }

        return value[..1] + "***" + value[at..];
    }

    private static string ChannelLabel(string channel)
    {
        return channel switch
        {
            NotificationDeliveryChannels.TeamsChannel => "Teams 채널",
            NotificationDeliveryChannels.TeamsDirectMessage => "Teams 개인 dry-run",
            NotificationDeliveryChannels.TeamsActivity => "Teams Activity",
            NotificationDeliveryChannels.Mail => "메일",
            _ => channel
        };
    }

    private static string DeliveryTypeLabel(string deliveryType)
    {
        return deliveryType switch
        {
            NotificationDeliveryTypes.WorkItemCreated => "업무 배정 알림",
            NotificationDeliveryTypes.ReferenceDigest => "참조 알림",
            NotificationDeliveryTypes.UrgentBlocking => "긴급 알림",
            NotificationDeliveryTypes.DailyDigest => "일일 업무 요약",
            NotificationDeliveryTypes.ProjectCompletion => "프로젝트 완료 알림",
            NotificationDeliveryTypes.ManualTest => "수동 알림",
            NotificationDeliveryTypes.DueSoonL0 => "예정일 임박 알림",
            NotificationDeliveryTypes.OverdueL1
                or NotificationDeliveryTypes.OverdueL2
                or NotificationDeliveryTypes.OverdueL3 => "예정일 초과 알림",
            _ => deliveryType
        };
    }

    private static string DeliveryTypeLabel(NotificationDeliveryRecord row)
    {
        return row.DeliveryType == NotificationDeliveryTypes.ManualTest
            && !string.IsNullOrWhiteSpace(row.ManualNotificationKind)
            ? ManualNotificationKindLabel(row.ManualNotificationKind) ?? "수동 발송"
            : DeliveryTypeLabel(row.DeliveryType);
    }

    private static string? ManualNotificationKindLabel(string? kind)
    {
        return kind switch
        {
            NotificationManualKinds.ProjectCreated => "프로젝트 생성 알림",
            NotificationManualKinds.WorkItemAssigned => "업무 배정 알림",
            NotificationManualKinds.Urgent => "긴급 알림",
            NotificationManualKinds.DailyDigest => "일일 업무 요약",
            NotificationManualKinds.Custom => "일반 알림",
            _ => null
        };
    }

    private static string StatusLabel(string status)
    {
        return status switch
        {
            NotificationDeliveryStatuses.Pending => "발송 대기",
            NotificationDeliveryStatuses.Processing => "발송 처리 중",
            NotificationDeliveryStatuses.Sent => "발송 완료",
            NotificationDeliveryStatuses.Failed => "발송 실패",
            NotificationDeliveryStatuses.Suppressed => "발송 제외",
            NotificationDeliveryStatuses.Disabled => "채널 비활성",
            NotificationDeliveryStatuses.DryRunSent => "Dry-run 처리",
            _ => status
        };
    }

    private static string AdminHandlingStatusLabel(string status)
    {
        return status switch
        {
            NotificationDeliveryAdminHandlingStatuses.Acknowledged => "확인됨",
            NotificationDeliveryAdminHandlingStatuses.Dismissed => "제외됨",
            _ => "미처리"
        };
    }

    private static string? PendingReason(NotificationDeliveryRecord row, DateTimeOffset now)
    {
        if (row.Status == NotificationDeliveryStatuses.Processing)
        {
            return row.ClaimExpiresAtUtc <= now
                ? "처리 lease가 만료되어 다음 worker의 회수를 기다리고 있습니다."
                : "한 worker가 claim lease 안에서 발송을 처리하고 있습니다.";
        }

        if (row.Status != NotificationDeliveryStatuses.Pending)
        {
            return null;
        }

        if (row.AttemptCount == 0)
        {
            return "발송 worker 처리 대기 중입니다.";
        }

        if (row.NextAttemptAtUtc is not null)
        {
            return "다음 재시도 시각까지 대기 중입니다.";
        }

        return "발송 worker 또는 채널 설정 확인이 필요합니다.";
    }

    private static string ActionGuide(NotificationDeliveryRecord row, DateTimeOffset now)
    {
        if (row.Status == NotificationDeliveryStatuses.Pending)
        {
            return PendingReason(row, now) ?? "대기 상태입니다. 오래된 대기 건은 worker/dispatch 설정을 확인하세요.";
        }

        if (row.Status == NotificationDeliveryStatuses.Processing)
        {
            return "발송 처리 중에는 확인, 제외 또는 재시도를 실행할 수 없습니다. lease 만료 시 worker가 안전하게 회수합니다.";
        }

        return row.ErrorCode switch
        {
            "RecipientEmailMissing" => "수신자 이메일 또는 사용자 정보를 확인하세요.",
            "SmtpAuthenticationFailed" => "SMTP 계정 또는 앱 비밀번호를 확인하세요.",
            "SmtpConnectionFailed" => "SMTP 서버, 포트, 보안 연결 설정을 확인하세요.",
            "SmtpSendFailed" => "메일 서버 응답과 수신자 주소를 확인하세요.",
            "TeamsActivityAppNotInstalled" => "수신자의 Teams 앱 설치 상태 또는 Teams 앱 정책을 확인하세요.",
            "TeamsActivityPermissionDenied" => "Graph 권한과 관리자 동의를 확인하세요.",
            "TeamsActivityInvalidActivityType" => "Teams manifest의 activityType 선언을 확인하세요.",
            "TeamsActivityInvalidTopic" => "Teams manifest validDomains와 webUrl 도메인을 확인하세요.",
            "TeamsWebhookFailed" => "Teams 채널 Webhook 또는 Power Automate 실행 결과를 확인하세요.",
            "TeamsActivityThrottled" => "일시 오류일 수 있으니 재시도 상태를 확인하세요.",
            "TeamsActivityGraphError" => "오류 코드와 서버 로그를 확인하세요.",
            _ when row.Status == NotificationDeliveryStatuses.Failed => "오류 코드와 서버 로그를 확인하세요.",
            _ => "상태를 확인하세요."
        };
    }

    private static NotificationDeliveryRecord ReadDeliveryRecord(NpgsqlDataReader reader)
    {
        return new NotificationDeliveryRecord(
            reader.GetGuid(0),
            reader.IsDBNull(1) ? null : reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetGuid(2),
            reader.IsDBNull(3) ? null : reader.GetGuid(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            reader.IsDBNull(5) ? null : reader.GetGuid(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.GetInt32(9),
            reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10),
            reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11),
            reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12),
            reader.IsDBNull(13) ? null : reader.GetFieldValue<DateTimeOffset>(13),
            reader.IsDBNull(14) ? null : reader.GetString(14),
            reader.IsDBNull(15) ? null : reader.GetString(15),
            reader.GetString(16),
            reader.IsDBNull(17) ? null : reader.GetString(17),
            reader.IsDBNull(18) ? null : reader.GetString(18),
            reader.GetFieldValue<DateTimeOffset>(19),
            reader.GetFieldValue<DateTimeOffset>(20),
            reader.IsDBNull(21) ? null : reader.GetString(21),
            reader.IsDBNull(22) ? null : reader.GetString(22),
            reader.IsDBNull(26) ? null : reader.GetString(26),
            reader.IsDBNull(27) ? null : reader.GetString(27),
            reader.IsDBNull(28) ? null : reader.GetString(28),
            reader.IsDBNull(29) ? null : reader.GetString(29),
            reader.IsDBNull(30) ? null : reader.GetString(30),
            reader.IsDBNull(31) ? null : reader.GetString(31),
            reader.IsDBNull(32) ? null : reader.GetString(32),
            reader.IsDBNull(23) ? null : reader.GetString(23),
            reader.IsDBNull(24) ? null : reader.GetString(24),
            reader.IsDBNull(25) ? null : reader.GetBoolean(25),
            reader.IsDBNull(33) ? null : reader.GetString(33),
            reader.IsDBNull(34) ? null : reader.GetString(34),
            reader.IsDBNull(35) ? null : reader.GetString(35),
            reader.IsDBNull(36) ? null : reader.GetFieldValue<DateTimeOffset>(36),
            reader.IsDBNull(37) ? null : reader.GetGuid(37),
            reader.IsDBNull(38) ? null : reader.GetString(38),
            reader.IsDBNull(39) ? null : reader.GetString(39),
            reader.IsDBNull(40) ? null : reader.GetString(40),
            reader.IsDBNull(41) ? null : reader.GetString(41),
            reader.IsDBNull(42) ? null : reader.GetString(42),
            reader.IsDBNull(43) ? null : reader.GetString(43),
            reader.IsDBNull(44) ? null : reader.GetString(44),
            reader.IsDBNull(45) ? null : reader.GetString(45),
            reader.IsDBNull(46) ? null : reader.GetString(46),
            reader.IsDBNull(47) ? null : reader.GetString(47),
            reader.IsDBNull(48) ? null : reader.GetString(48),
            reader.IsDBNull(49) ? null : reader.GetString(49),
            reader.IsDBNull(50) ? null : reader.GetString(50),
            reader.IsDBNull(51) ? null : reader.GetGuid(51),
            reader.IsDBNull(52) ? null : reader.GetFieldValue<DateTimeOffset>(52),
            reader.IsDBNull(53) ? null : reader.GetGuid(53),
            reader.IsDBNull(54) ? null : reader.GetFieldValue<DateTimeOffset>(54),
            reader.IsDBNull(55) ? null : reader.GetFieldValue<DateTimeOffset>(55),
            reader.IsDBNull(56) ? null : reader.GetString(56));
    }

    private NpgsqlDataSource CreateDataSource()
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("QMS database connection string is not configured.");
        }

        return NpgsqlDataSource.Create(connectionString);
    }

    private sealed record AssignedProjectDigestRow(
        Guid ProjectId,
        string ProjectTitle,
        DateOnly? DeliveryDate,
        IReadOnlyList<string> ResponsibilityTypes);
}
