using Emi.Qms.Api.Identity;
using Emi.Qms.Api.ProductionPlanning;
using Npgsql;

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
                error_code, error_message, dedupe_key, group_key, next_attempt_at_utc
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
                @now
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

    public async Task<IReadOnlyList<NotificationDeliveryRecord>> GetDueDeliveriesAsync(int limit, int retryCount, CancellationToken cancellationToken)
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
                n.severity
            from notification_deliveries nd
            left join qms_users u on u.id = nd.recipient_user_id
            left join notifications n on n.id = nd.notification_id
            left join projects p on p.id = coalesce(nd.project_id, n.project_id)
            where nd.status in ('Pending', 'Failed')
              and nd.delivery_type <> 'ManualTest'
              and nd.attempt_count < @max_attempt_count
              and (nd.next_attempt_at_utc is null or nd.next_attempt_at_utc <= @now)
            order by nd.created_at_utc
            limit @limit;
            """);
        command.Parameters.AddWithValue("now", timeProvider.GetUtcNow());
        command.Parameters.AddWithValue("max_attempt_count", Math.Max(1, retryCount));
        command.Parameters.AddWithValue("limit", Math.Max(1, limit));

        var rows = new List<NotificationDeliveryRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(ReadDeliveryRecord(reader));
        }

        return rows;
    }

    public async Task MarkDeliveryResultAsync(
        Guid deliveryId,
        NotificationChannelResult result,
        int retryCount,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var isFinal = result.Status is NotificationDeliveryStatuses.Sent
            or NotificationDeliveryStatuses.DryRunSent
            or NotificationDeliveryStatuses.Disabled
            or NotificationDeliveryStatuses.Suppressed;

        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            update notification_deliveries
            set status = @status,
                attempt_count = attempt_count + 1,
                last_attempt_at_utc = @now,
                sent_at_utc = case when @status in ('Sent', 'DryRunSent') then @now else sent_at_utc end,
                suppressed_at_utc = case when @status in ('Suppressed', 'Disabled') then @now else suppressed_at_utc end,
                next_attempt_at_utc = case
                    when @is_final then null
                    when attempt_count + 1 >= @retry_count then null
                    else @next_attempt_at_utc
                end,
                error_code = @error_code,
                error_message = @error_message,
                provider_message_id = @provider_message_id,
                updated_at_utc = @now
            where id = @id;
            """);
        command.Parameters.AddWithValue("id", deliveryId);
        command.Parameters.AddWithValue("status", result.Status);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("is_final", isFinal);
        command.Parameters.AddWithValue("retry_count", Math.Max(1, retryCount));
        command.Parameters.AddWithValue("next_attempt_at_utc", now.AddMinutes(5));
        command.Parameters.AddWithValue("error_code", (object?)result.ErrorCode ?? DBNull.Value);
        command.Parameters.AddWithValue("error_message", (object?)SanitizeError(result.ErrorMessage) ?? DBNull.Value);
        command.Parameters.AddWithValue("provider_message_id", (object?)result.ProviderMessageId ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<NotificationDeliveryListResponse> ListDeliveriesAsync(CancellationToken cancellationToken)
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
                n.severity
            from notification_deliveries nd
            left join qms_users u on u.id = nd.recipient_user_id
            left join notifications n on n.id = nd.notification_id
            left join projects p on p.id = coalesce(nd.project_id, n.project_id)
            order by nd.created_at_utc desc
            limit 200;
            """);

        var items = new List<NotificationDeliveryResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = ReadDeliveryRecord(reader);
            items.Add(new NotificationDeliveryResponse(
                row.DeliveryId,
                row.Channel,
                row.DeliveryType,
                row.Status,
                row.AttemptCount,
                row.NextAttemptAtUtc,
                row.LastAttemptAtUtc,
                row.SentAtUtc,
                row.SuppressedAtUtc,
                row.ErrorCode,
                row.ErrorMessage,
                row.RecipientDisplayName,
                row.RecipientEmail,
                row.ProjectTitle,
                row.ProjectCode,
                row.NotificationTitle,
                row.CreatedAtUtc,
                row.UpdatedAtUtc));
        }

        return new NotificationDeliveryListResponse(items);
    }

    public async Task<Guid> CreateManualTestMailDeliveryAsync(CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            insert into notification_deliveries (
                channel,
                delivery_type,
                status,
                dedupe_key,
                group_key
            )
            values (
                'Mail',
                'ManualTest',
                'Pending',
                @dedupe_key,
                @group_key
            )
            returning id;
            """);
        var now = timeProvider.GetUtcNow();
        command.Parameters.AddWithValue("dedupe_key", $"manual-test-mail:{now:yyyyMMddHHmmss}:{Guid.NewGuid():N}");
        command.Parameters.AddWithValue("group_key", "manual-test-mail");
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid deliveryId
            ? deliveryId
            : throw new InvalidOperationException("Manual test delivery id was not returned.");
    }

    public async Task<Guid> CreateManualTestTeamsActivityDeliveryAsync(Guid recipientUserId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            insert into notification_deliveries (
                recipient_user_id,
                channel,
                delivery_type,
                status,
                dedupe_key,
                group_key,
                next_attempt_at_utc
            )
            values (
                @recipient_user_id,
                'TeamsActivity',
                'ManualTest',
                'Pending',
                @dedupe_key,
                @group_key,
                @now
            )
            returning id;
            """);
        var now = timeProvider.GetUtcNow();
        command.Parameters.AddWithValue("recipient_user_id", recipientUserId);
        command.Parameters.AddWithValue("dedupe_key", $"manual-test-teams-activity:{now:yyyyMMddHHmmss}:{Guid.NewGuid():N}");
        command.Parameters.AddWithValue("group_key", "manual-test-teams-activity");
        command.Parameters.AddWithValue("now", now);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid deliveryId
            ? deliveryId
            : throw new InvalidOperationException("Manual Teams Activity test delivery id was not returned.");
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

    public async Task<NotificationDeliveryMessage> RenderMessageAsync(NotificationDeliveryRecord delivery, CancellationToken cancellationToken)
    {
        if (delivery.DeliveryType == NotificationDeliveryTypes.DailyDigest && delivery.RecipientUserId is not null)
        {
            return await RenderDailyDigestAsync(delivery, cancellationToken);
        }

        var projectLine = string.IsNullOrWhiteSpace(delivery.ProjectTitle)
            ? ""
            : $"{Environment.NewLine}프로젝트: {delivery.ProjectTitle} ({delivery.ProjectCode})";
        var linkLine = string.IsNullOrWhiteSpace(delivery.LinkUrl)
            ? ""
            : $"{Environment.NewLine}링크: {BuildLink(delivery.LinkUrl)}";
        var body = $"""
            {SystemName}

            {delivery.NotificationMessage ?? delivery.NotificationTitle ?? "알림이 생성되었습니다."}
            유형: {delivery.NotificationType ?? delivery.DeliveryType}
            심각도: {delivery.Severity ?? "Info"}{projectLine}
            수신자: {delivery.RecipientDisplayName ?? "통합 채널"}{linkLine}
            발생 시각: {delivery.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC
            """;

        return new NotificationDeliveryMessage(
            delivery.DeliveryId,
            delivery.Channel,
            delivery.DeliveryType,
            delivery.NotificationTitle ?? "EMI 프로젝트 통합관리시스템 알림",
            body,
            delivery.LinkUrl,
            delivery.RecipientDisplayName,
            delivery.RecipientEmail,
            RecipientUserId: delivery.RecipientUserId,
            RecipientEntraObjectId: delivery.RecipientEntraObjectId,
            RecipientAuthProvider: delivery.RecipientAuthProvider,
            RecipientUserIsActive: delivery.RecipientUserIsActive);
    }

    private async Task<NotificationDeliveryMessage> RenderDailyDigestAsync(NotificationDeliveryRecord delivery, CancellationToken cancellationToken)
    {
        if (delivery.RecipientUserId is not { } recipientUserId)
        {
            return new NotificationDeliveryMessage(
                delivery.DeliveryId,
                delivery.Channel,
                delivery.DeliveryType,
                "EMI 프로젝트 통합관리시스템 일일 요약",
                "수신자 정보가 없어 일일 요약을 생성할 수 없습니다.",
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
            SystemName,
            "",
            $"{delivery.RecipientDisplayName ?? "사용자"}님의 일일 요약입니다.",
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

        return new NotificationDeliveryMessage(
            delivery.DeliveryId,
            delivery.Channel,
            delivery.DeliveryType,
            "EMI 프로젝트 통합관리시스템 일일 요약",
            string.Join(Environment.NewLine, lines),
            null,
            delivery.RecipientDisplayName,
            delivery.RecipientEmail,
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
                notification_id, project_id, channel, delivery_type, dedupe_key, group_key, next_attempt_at_utc
            )
            select
                n.id,
                n.project_id,
                'TeamsChannel',
                'UrgentBlocking',
                concat('notification:', n.id::text, ':teams-channel:urgent'),
                concat('urgent:', coalesce(n.project_id::text, n.id::text), ':', floor(extract(epoch from n.created_at_utc) / @batch_window_seconds)::bigint),
                @now
            from notifications n
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
                channel, delivery_type, dedupe_key, group_key, next_attempt_at_utc
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
                @now
            from notifications n
            join notification_recipients nr on nr.notification_id = n.id
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
                channel, delivery_type, dedupe_key, group_key, next_attempt_at_utc
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
                @now
            from notifications n
            join notification_recipients nr on nr.notification_id = n.id
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

        return message.Length <= 500 ? message : message[..500];
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
            reader.IsDBNull(25) ? null : reader.GetBoolean(25));
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
