using Emi.Qms.Api.Calendar;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Notifications;

public sealed class WorkItemEscalationStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    TimeProvider timeProvider)
{
    private static readonly IReadOnlyDictionary<string, string[]> StageSecondaryResponsibilities =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["ProductionPlanning"] = ["ProductionPlanningSecondary"],
            ["DesignPanelInfo"] = ["DesignSecondary"],
            ["ProcurementInfo"] = ["ProcurementSecondary"],
            ["MaterialArrived"] = ["MaterialsSecondary"],
            ["IQC"] = ["QualityIQCSecondary"],
            ["ReceiptConfirmed"] = ["MaterialsSecondary"],
            ["KittingCompleted"] = ["MaterialsSecondary"],
            ["ManufacturingWork"] = ["ManufacturingSecondary"],
            ["LQC"] = ["QualityLQCSecondary"],
            ["ManufacturingCompleted"] = ["ManufacturingSecondary"],
            ["OQC"] = ["QualityOQCSecondary"],
            ["CustomerInspection"] = ["QualityCustomerInspectionSecondary"],
            ["FAT"] = ["QualityCustomerInspectionSecondary"],
            ["PackingCompleted"] = ["LogisticsSecondary"],
            ["DepartureProcessed"] = ["LogisticsSecondary"],
            ["DeliveryCompleted"] = ["LogisticsSecondary"],
            ["SalesSettlementCompleted"] = ["SalesSecondary"]
        };

    private static readonly string[] ProductionPlanningResponsibilities =
    [
        "ProductionPlanningPrimary",
        "ProductionPlanningSecondary",
        "ProductionPlanning"
    ];

    private static readonly string[] SalesResponsibilities =
    [
        "SalesPrimary",
        "SalesSecondary"
    ];

    public async Task<IReadOnlyList<BusinessCalendarHoliday>> ReadHolidaysAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select holiday_date, name, holiday_type
            from system_holidays
            where is_active = true
              and country_code = 'KR'
              and holiday_date >= @date_from
              and holiday_date <= @date_to
            order by holiday_date, name;
            """);
        command.Parameters.AddWithValue("date_from", from);
        command.Parameters.AddWithValue("date_to", to);

        var holidays = new List<BusinessCalendarHoliday>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            holidays.Add(new BusinessCalendarHoliday(
                reader.GetFieldValue<DateOnly>(0),
                reader.GetString(1),
                SystemHolidayTypes.Normalize(reader.GetString(2))));
        }

        return holidays;
    }

    public async Task<IReadOnlyList<WorkItemEscalationCandidate>> ReadOpenCandidatesAsync(
        int maxBatchSize,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select
                wi.id,
                wi.project_id,
                p.project_title,
                p.project_code,
                wi.workflow_stage_code,
                ws.stage_name,
                wi.responsibility_type,
                wi.assigned_user_id,
                u.display_name,
                u.is_active,
                wi.due_date,
                wi.title,
                wi.status,
                case when wie.due_date is distinct from wi.due_date then 'None' else wie.current_level end,
                case when wie.due_date is distinct from wi.due_date then null else wie.l0_sent_at_utc end,
                case when wie.due_date is distinct from wi.due_date then null else wie.l1_sent_at_utc end,
                case when wie.due_date is distinct from wi.due_date then null else wie.l2_sent_at_utc end,
                case when wie.due_date is distinct from wi.due_date then null else wie.l3_sent_at_utc end
            from work_items wi
            join projects p on p.id = wi.project_id
            join workflow_stages ws on ws.stage_code = wi.workflow_stage_code
            join qms_users u on u.id = wi.assigned_user_id
            left join work_item_escalations wie on wie.work_item_id = wi.id
            where wi.due_date is not null
              and wi.status in ('Requested', 'InProgress')
              and p.deleted_at_utc is null
            order by
                case
                    when wie.work_item_id is null
                      or wie.due_date is distinct from wi.due_date
                      or wie.status <> 'Active'
                    then 0
                    else 1
                end,
                case
                    when wie.work_item_id is null
                      or wie.due_date is distinct from wi.due_date
                      or wie.status <> 'Active'
                    then wi.created_at_utc
                    else wie.updated_at_utc
                end,
                wi.due_date,
                wi.created_at_utc,
                wi.id
            limit @limit;
            """);
        command.Parameters.AddWithValue("limit", Math.Max(1, maxBatchSize));

        var rows = new List<WorkItemEscalationCandidate>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new WorkItemEscalationCandidate(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetGuid(7),
                reader.GetString(8),
                reader.GetBoolean(9),
                reader.GetFieldValue<DateOnly>(10),
                reader.GetString(11),
                reader.GetString(12),
                reader.IsDBNull(13) ? null : reader.GetString(13),
                reader.IsDBNull(14) ? null : reader.GetFieldValue<DateTimeOffset>(14),
                reader.IsDBNull(15) ? null : reader.GetFieldValue<DateTimeOffset>(15),
                reader.IsDBNull(16) ? null : reader.GetFieldValue<DateTimeOffset>(16),
                reader.IsDBNull(17) ? null : reader.GetFieldValue<DateTimeOffset>(17)));
        }

        return rows;
    }

    public async Task<int> ResolveClosedOrUndatedWorkItemsAsync(CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            update work_item_escalations wie
            set status = case when wi.status = 'Cancelled' then 'Cancelled' else 'Resolved' end,
                resolved_at_utc = coalesce(wie.resolved_at_utc, @now),
                next_check_at_utc = null,
                updated_at_utc = @now
            from work_items wi
            where wi.id = wie.work_item_id
              and wie.status = 'Active'
              and (
                  wi.status in ('Completed', 'Cancelled')
                  or wi.due_date is null
              );
            """);
        command.Parameters.AddWithValue("now", timeProvider.GetUtcNow());
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpsertActiveEscalationAsync(
        WorkItemEscalationCandidate candidate,
        DateTimeOffset? nextCheckAtUtc,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            insert into work_item_escalations (
                work_item_id, project_id, workflow_stage_code, assigned_user_id, due_date,
                status, current_level, next_check_at_utc, updated_at_utc
            )
            values (
                @work_item_id, @project_id, @workflow_stage_code, @assigned_user_id, @due_date,
                'Active', 'None', @next_check_at_utc, @now
            )
            on conflict (work_item_id) do update
            set project_id = excluded.project_id,
                workflow_stage_code = excluded.workflow_stage_code,
                assigned_user_id = excluded.assigned_user_id,
                due_date = excluded.due_date,
                status = 'Active',
                resolved_at_utc = null,
                next_check_at_utc = excluded.next_check_at_utc,
                current_level = case
                    when work_item_escalations.due_date <> excluded.due_date then 'None'
                    else work_item_escalations.current_level
                end,
                l0_sent_at_utc = case when work_item_escalations.due_date <> excluded.due_date then null else work_item_escalations.l0_sent_at_utc end,
                l1_sent_at_utc = case when work_item_escalations.due_date <> excluded.due_date then null else work_item_escalations.l1_sent_at_utc end,
                l2_sent_at_utc = case when work_item_escalations.due_date <> excluded.due_date then null else work_item_escalations.l2_sent_at_utc end,
                l3_sent_at_utc = case when work_item_escalations.due_date <> excluded.due_date then null else work_item_escalations.l3_sent_at_utc end,
                updated_at_utc = @now;
            """);
        AddCandidateParameters(command, candidate);
        command.Parameters.AddWithValue("next_check_at_utc", (object?)nextCheckAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("now", timeProvider.GetUtcNow());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<EscalationCreateResult> CreateEscalationAsync(
        WorkItemEscalationCandidate candidate,
        string level,
        NotificationEscalationOptions options,
        CancellationToken cancellationToken)
    {
        var recipients = await ResolveRecipientsAsync(candidate, level, cancellationToken);
        if (recipients.Count == 0)
        {
            return new EscalationCreateResult(0, 0);
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        var notificationId = await InsertNotificationAsync(connection, transaction, candidate, level, recipients, cancellationToken);
        var recipientIds = await ReadNotificationRecipientIdsAsync(connection, transaction, notificationId, cancellationToken);
        var deliveryCount = 0;

        foreach (var recipient in recipients)
        {
            recipientIds.TryGetValue(recipient.UserId, out var notificationRecipientId);
            deliveryCount += await InsertDeliveriesForRecipientAsync(
                connection,
                transaction,
                candidate,
                level,
                notificationId,
                notificationRecipientId,
                recipient,
                options,
                now,
                cancellationToken);
        }

        if (ShouldCreateTeamsChannelFallback(level, options))
        {
            deliveryCount += await InsertDeliveryAsync(
                connection,
                transaction,
                candidate,
                level,
                notificationId,
                notificationRecipientId: null,
                recipient: null,
                NotificationDeliveryChannels.TeamsChannel,
                DeliveryTypeForLevel(level),
                status: NotificationDeliveryStatuses.Pending,
                providerMessageId: null,
                now,
                cancellationToken);
        }

        await MarkLevelSentAsync(connection, transaction, candidate.WorkItemId, level, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new EscalationCreateResult(1, deliveryCount);
    }

    public async Task<WorkItemEscalationListResponse> ListEscalationsAsync(
        string? status,
        string? level,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select
                wie.id,
                wie.work_item_id,
                wie.project_id,
                p.project_title,
                p.project_code,
                wie.workflow_stage_code,
                ws.stage_name,
                wi.title,
                wie.due_date,
                wie.status,
                wie.current_level,
                wie.last_escalated_at_utc,
                wie.next_check_at_utc,
                wie.l0_sent_at_utc,
                wie.l1_sent_at_utc,
                wie.l2_sent_at_utc,
                wie.l3_sent_at_utc,
                wie.resolved_at_utc,
                u.display_name,
                (
                    select string_agg(summary.delivery_type || ':' || summary.status || '=' || summary.count::text, ', ' order by summary.delivery_type, summary.status)
                    from (
                        select nd.delivery_type, nd.status, count(*) as count
                        from notification_deliveries nd
                        where nd.work_item_id = wie.work_item_id
                        group by nd.delivery_type, nd.status
                    ) summary
                ) as delivery_status_summary,
                wie.created_at_utc,
                wie.updated_at_utc
            from work_item_escalations wie
            join work_items wi on wi.id = wie.work_item_id
            join projects p on p.id = wie.project_id
            join workflow_stages ws on ws.stage_code = wie.workflow_stage_code
            left join qms_users u on u.id = wie.assigned_user_id
            where (@status is null or wie.status = @status)
              and (@level is null or wie.current_level = @level)
            order by wie.updated_at_utc desc
            limit 200;
            """);
        command.Parameters.Add(new NpgsqlParameter("status", NpgsqlDbType.Text) { Value = NormalizeFilter(status) ?? (object)DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("level", NpgsqlDbType.Text) { Value = NormalizeFilter(level) ?? (object)DBNull.Value });

        var items = new List<WorkItemEscalationResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new WorkItemEscalationResponse(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetFieldValue<DateOnly>(8),
                reader.GetString(9),
                reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11),
                reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12),
                reader.IsDBNull(13) ? null : reader.GetFieldValue<DateTimeOffset>(13),
                reader.IsDBNull(14) ? null : reader.GetFieldValue<DateTimeOffset>(14),
                reader.IsDBNull(15) ? null : reader.GetFieldValue<DateTimeOffset>(15),
                reader.IsDBNull(16) ? null : reader.GetFieldValue<DateTimeOffset>(16),
                reader.IsDBNull(17) ? null : reader.GetFieldValue<DateTimeOffset>(17),
                reader.IsDBNull(18) ? null : reader.GetString(18),
                reader.IsDBNull(19) ? null : reader.GetString(19),
                reader.GetFieldValue<DateTimeOffset>(20),
                reader.GetFieldValue<DateTimeOffset>(21)));
        }

        return new WorkItemEscalationListResponse(items);
    }

    private async Task<IReadOnlyList<EscalationRecipient>> ResolveRecipientsAsync(
        WorkItemEscalationCandidate candidate,
        string level,
        CancellationToken cancellationToken)
    {
        if (level is WorkItemEscalationLevels.L0 or WorkItemEscalationLevels.L1)
        {
            return candidate.AssignedUserIsActive
                ? [new EscalationRecipient(candidate.AssignedUserId, candidate.AssignedDisplayName, null)]
                : [];
        }

        var responsibilityTypes = level == WorkItemEscalationLevels.L2
            ? L2ResponsibilityTypes(candidate.WorkflowStageCode)
            : L3ResponsibilityTypes();

        return await ReadActiveAssigneesAsync(candidate.ProjectId, responsibilityTypes, cancellationToken);
    }

    private static string[] L2ResponsibilityTypes(string workflowStageCode)
    {
        var values = new List<string>();
        if (StageSecondaryResponsibilities.TryGetValue(workflowStageCode, out var secondary))
        {
            values.AddRange(secondary);
        }

        values.AddRange(ProductionPlanningResponsibilities);
        return values.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string[] L3ResponsibilityTypes()
    {
        return [.. ProductionPlanningResponsibilities, .. SalesResponsibilities];
    }

    private async Task<IReadOnlyList<EscalationRecipient>> ReadActiveAssigneesAsync(
        Guid projectId,
        IReadOnlyList<string> responsibilityTypes,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select distinct on (u.id)
                u.id,
                u.display_name,
                u.email
            from project_assignees pa
            join qms_users u on u.id = pa.assigned_user_id
            where pa.project_id = @project_id
              and pa.responsibility_type = any(@responsibility_types)
              and u.is_active = true
            order by u.id, array_position(@responsibility_types, pa.responsibility_type);
            """);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("responsibility_types", responsibilityTypes.ToArray());

        var rows = new List<EscalationRecipient>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new EscalationRecipient(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return rows;
    }

    private async Task<Guid> InsertNotificationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        WorkItemEscalationCandidate candidate,
        string level,
        IReadOnlyList<EscalationRecipient> recipients,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into notifications (
                project_id, notification_type, severity, title, message, link_url, idempotency_key
            )
            values (
                @project_id, @notification_type, @severity, @title, @message, @link_url, @idempotency_key
            )
            on conflict (idempotency_key) do update
            set title = excluded.title,
                message = excluded.message
            returning id;
            """;
        command.Parameters.AddWithValue("project_id", candidate.ProjectId);
        command.Parameters.AddWithValue("notification_type", NotificationTypeForLevel(level));
        command.Parameters.AddWithValue("severity", SeverityForLevel(level));
        command.Parameters.AddWithValue("title", TitleForLevel(candidate, level));
        command.Parameters.AddWithValue("message", MessageForLevel(candidate, level));
        command.Parameters.AddWithValue("link_url", LinkUrlForStage(candidate.ProjectId, candidate.WorkflowStageCode));
        command.Parameters.AddWithValue("idempotency_key", NotificationIdempotencyKey(candidate.WorkItemId, candidate.DueDate, level));
        var notificationId = (Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty);

        foreach (var recipientId in recipients.Select(recipient => recipient.UserId).Distinct())
        {
            await using var recipientCommand = connection.CreateCommand();
            recipientCommand.Transaction = transaction;
            recipientCommand.CommandText = """
                insert into notification_recipients (notification_id, user_id)
                values (@notification_id, @user_id)
                on conflict (notification_id, user_id) do nothing;
                """;
            recipientCommand.Parameters.AddWithValue("notification_id", notificationId);
            recipientCommand.Parameters.AddWithValue("user_id", recipientId);
            await recipientCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        return notificationId;
    }

    private static async Task<IReadOnlyDictionary<Guid, Guid>> ReadNotificationRecipientIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select user_id, id
            from notification_recipients
            where notification_id = @notification_id;
            """;
        command.Parameters.AddWithValue("notification_id", notificationId);

        var rows = new Dictionary<Guid, Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows[reader.GetGuid(0)] = reader.GetGuid(1);
        }

        return rows;
    }

    private async Task<int> InsertDeliveriesForRecipientAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        WorkItemEscalationCandidate candidate,
        string level,
        Guid notificationId,
        Guid? notificationRecipientId,
        EscalationRecipient recipient,
        NotificationEscalationOptions options,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var count = 0;
        if (level is WorkItemEscalationLevels.L0 or WorkItemEscalationLevels.L1 or WorkItemEscalationLevels.L2)
        {
            var personalDelivery = ResolveTeamsPersonalDelivery(options);
            count += await InsertDeliveryAsync(
                connection,
                transaction,
                candidate,
                level,
                notificationId,
                notificationRecipientId,
                recipient,
                personalDelivery.Channel,
                DeliveryTypeForLevel(level),
                personalDelivery.Status,
                personalDelivery.ProviderMessageId,
                now,
                cancellationToken);
        }

        if (options.MailEnabled && level is WorkItemEscalationLevels.L1 or WorkItemEscalationLevels.L3)
        {
            count += await InsertDeliveryAsync(
                connection,
                transaction,
                candidate,
                level,
                notificationId,
                notificationRecipientId,
                recipient,
                NotificationDeliveryChannels.Mail,
                DeliveryTypeForLevel(level),
                NotificationDeliveryStatuses.Pending,
                providerMessageId: null,
                now,
                cancellationToken);
        }

        return count;
    }

    private static TeamsPersonalDeliveryPlan ResolveTeamsPersonalDelivery(NotificationEscalationOptions options)
    {
        if (string.Equals(options.TeamsPersonalChannelStrategy, "TeamsActivity", StringComparison.OrdinalIgnoreCase))
        {
            return new TeamsPersonalDeliveryPlan(
                NotificationDeliveryChannels.TeamsActivity,
                NotificationDeliveryStatuses.Pending,
                null);
        }

        return new TeamsPersonalDeliveryPlan(
            NotificationDeliveryChannels.TeamsDirectMessage,
            options.TeamsPersonalDryRun ? NotificationDeliveryStatuses.DryRunSent : NotificationDeliveryStatuses.Pending,
            options.TeamsPersonalDryRun ? "teams-personal-dry-run" : null);
    }

    private async Task<int> InsertDeliveryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        WorkItemEscalationCandidate candidate,
        string level,
        Guid notificationId,
        Guid? notificationRecipientId,
        EscalationRecipient? recipient,
        string channel,
        string deliveryType,
        string status,
        string? providerMessageId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into notification_deliveries (
                notification_id,
                notification_recipient_id,
                recipient_user_id,
                project_id,
                work_item_id,
                channel,
                delivery_type,
                status,
                attempt_count,
                next_attempt_at_utc,
                sent_at_utc,
                dedupe_key,
                group_key,
                provider_message_id,
                display_title,
                display_message,
                display_project_name,
                display_work_item_title,
                display_recipient_name,
                display_recipient_email,
                display_recipient_kind,
                display_channel_target,
                updated_at_utc
            )
            values (
                @notification_id,
                @notification_recipient_id,
                @recipient_user_id,
                @project_id,
                @work_item_id,
                @channel,
                @delivery_type,
                @status,
                case when @status = 'DryRunSent' then 1 else 0 end,
                case when @status = 'Pending' then @now else null end,
                case when @status = 'DryRunSent' then @now else null end,
                @dedupe_key,
                @group_key,
                @provider_message_id,
                @display_title,
                @display_message,
                @display_project_name,
                @display_work_item_title,
                @display_recipient_name,
                @display_recipient_email,
                @display_recipient_kind,
                @display_channel_target,
                @now
            )
            on conflict do nothing;
            """;
        command.Parameters.AddWithValue("notification_id", notificationId);
        command.Parameters.AddWithValue("notification_recipient_id", (object?)notificationRecipientId ?? DBNull.Value);
        command.Parameters.AddWithValue("recipient_user_id", (object?)recipient?.UserId ?? DBNull.Value);
        command.Parameters.AddWithValue("project_id", candidate.ProjectId);
        command.Parameters.AddWithValue("work_item_id", candidate.WorkItemId);
        command.Parameters.AddWithValue("channel", channel);
        command.Parameters.AddWithValue("delivery_type", deliveryType);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("dedupe_key", DeliveryDedupeKey(candidate.WorkItemId, candidate.DueDate, level, channel, recipient?.UserId));
        command.Parameters.AddWithValue("group_key", $"work-item-escalation:{candidate.WorkItemId}:{level}");
        command.Parameters.AddWithValue("provider_message_id", (object?)providerMessageId ?? DBNull.Value);
        command.Parameters.AddWithValue("display_title", TitleForLevel(candidate, level));
        command.Parameters.AddWithValue("display_message", MessageForLevel(candidate, level));
        command.Parameters.AddWithValue("display_project_name", candidate.ProjectTitle);
        command.Parameters.AddWithValue("display_work_item_title", candidate.WorkItemTitle);
        command.Parameters.AddWithValue("display_recipient_name", (object?)recipient?.DisplayName ?? DBNull.Value);
        command.Parameters.AddWithValue("display_recipient_email", (object?)recipient?.Email ?? DBNull.Value);
        command.Parameters.AddWithValue("display_recipient_kind", recipient is null ? "TeamsChannel" : "User");
        command.Parameters.AddWithValue("display_channel_target", channel == NotificationDeliveryChannels.TeamsChannel ? "Teams 채널" : DBNull.Value);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task MarkLevelSentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid workItemId,
        string level,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var sentColumn = level switch
        {
            WorkItemEscalationLevels.L0 => "l0_sent_at_utc",
            WorkItemEscalationLevels.L1 => "l1_sent_at_utc",
            WorkItemEscalationLevels.L2 => "l2_sent_at_utc",
            WorkItemEscalationLevels.L3 => "l3_sent_at_utc",
            _ => throw new InvalidOperationException("Unsupported escalation level.")
        };
        command.CommandText = $"""
            update work_item_escalations
            set current_level = @level,
                last_escalated_at_utc = @now,
                {sentColumn} = coalesce({sentColumn}, @now),
                next_check_at_utc = @next_check_at_utc,
                updated_at_utc = @now
            where work_item_id = @work_item_id;
            """;
        command.Parameters.AddWithValue("work_item_id", workItemId);
        command.Parameters.AddWithValue("level", level);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("next_check_at_utc", now.AddHours(1));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddCandidateParameters(NpgsqlCommand command, WorkItemEscalationCandidate candidate)
    {
        command.Parameters.AddWithValue("work_item_id", candidate.WorkItemId);
        command.Parameters.AddWithValue("project_id", candidate.ProjectId);
        command.Parameters.AddWithValue("workflow_stage_code", candidate.WorkflowStageCode);
        command.Parameters.AddWithValue("assigned_user_id", candidate.AssignedUserId);
        command.Parameters.AddWithValue("due_date", candidate.DueDate);
    }

    private static bool ShouldCreateTeamsChannelFallback(string level, NotificationEscalationOptions options)
    {
        return options.UseTeamsChannelFallback && level == WorkItemEscalationLevels.L2;
    }

    private static string DeliveryTypeForLevel(string level)
    {
        return level switch
        {
            WorkItemEscalationLevels.L0 => NotificationDeliveryTypes.DueSoonL0,
            WorkItemEscalationLevels.L1 => NotificationDeliveryTypes.OverdueL1,
            WorkItemEscalationLevels.L2 => NotificationDeliveryTypes.OverdueL2,
            WorkItemEscalationLevels.L3 => NotificationDeliveryTypes.OverdueL3,
            _ => throw new InvalidOperationException("Unsupported escalation level.")
        };
    }

    private static string NotificationTypeForLevel(string level)
    {
        return "Info";
    }

    private static string SeverityForLevel(string level)
    {
        return level switch
        {
            WorkItemEscalationLevels.L0 => "Info",
            _ => "Warning"
        };
    }

    private static string TitleForLevel(WorkItemEscalationCandidate candidate, string level)
    {
        return level switch
        {
            WorkItemEscalationLevels.L0 => $"예정일 임박: {candidate.WorkItemTitle}",
            WorkItemEscalationLevels.L1 => $"예정일 초과: {candidate.WorkItemTitle}",
            WorkItemEscalationLevels.L2 => $"예정일 초과 +2영업일: {candidate.WorkItemTitle}",
            WorkItemEscalationLevels.L3 => $"예정일 초과 +3영업일: {candidate.WorkItemTitle}",
            _ => candidate.WorkItemTitle
        };
    }

    private static string MessageForLevel(WorkItemEscalationCandidate candidate, string level)
    {
        var prefix = level switch
        {
            WorkItemEscalationLevels.L0 => "예정일의 직전 영업일입니다.",
            WorkItemEscalationLevels.L1 => "예정일이 지났습니다.",
            WorkItemEscalationLevels.L2 => "예정일 이후 2영업일이 지났지만 아직 조치되지 않았습니다.",
            WorkItemEscalationLevels.L3 => "예정일 이후 3영업일이 지났지만 아직 조치되지 않았습니다.",
            _ => "업무 예정일 확인이 필요합니다."
        };
        return $"{prefix} 프로젝트 {candidate.ProjectTitle}의 {candidate.WorkflowStageName} 업무를 확인해 주세요. 예정일: {candidate.DueDate:yyyy-MM-dd}";
    }

    private static string LinkUrlForStage(Guid projectId, string stageCode)
    {
        return stageCode switch
        {
            "ProductionPlanning" => $"/projects/{projectId}/production-planning/edit",
            "DesignPanelInfo" => $"/projects/{projectId}/panel-information/edit",
            "ProcurementInfo" => $"/projects/{projectId}/procurement/edit",
            _ => $"/projects/{projectId}?section=workflow"
        };
    }

    private static string NotificationIdempotencyKey(Guid workItemId, DateOnly dueDate, string level)
    {
        return $"work-item:{workItemId}:due:{dueDate:yyyyMMdd}:escalation:{level}:notification";
    }

    private static string DeliveryDedupeKey(Guid workItemId, DateOnly dueDate, string level, string channel, Guid? recipientUserId)
    {
        var recipient = recipientUserId?.ToString() ?? "channel";
        return $"work-item:{workItemId}:due:{dueDate:yyyyMMdd}:escalation:{level}:{channel}:{recipient}";
    }

    private static string? NormalizeFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
}
