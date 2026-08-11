using Npgsql;
using Emi.Qms.Api.Workflow;

namespace Emi.Qms.Api.Notifications;

internal static class WorkAssignmentNotificationWriter
{
    private static readonly IReadOnlyDictionary<string, string[]> StageResponsibilityTypes =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [WorkflowStageCodes.DesignPanelInfo] = ["DesignPrimary", "DesignSecondary"],
            [WorkflowStageCodes.ProductionPlanning] = ["ProductionPlanningPrimary", "ProductionPlanningSecondary", "ProductionPlanning"],
            [WorkflowStageCodes.ProcurementInfo] = ["ProcurementPrimary", "ProcurementSecondary", "Procurement"],
            [WorkflowStageCodes.MaterialArrived] = ["MaterialsPrimary", "MaterialsSecondary"],
            [WorkflowStageCodes.IQC] = ["QualityIQC", "QualityIQCSecondary", "Quality"],
            [WorkflowStageCodes.ReceiptConfirmed] = ["MaterialsPrimary", "MaterialsSecondary"],
            [WorkflowStageCodes.KittingCompleted] = ["ProductionPlanningPrimary", "ProductionPlanningSecondary", "ProductionPlanning"],
            [WorkflowStageCodes.ManufacturingWork] = ["ManufacturingPrimary", "ManufacturingSecondary", "Manufacturing"],
            [WorkflowStageCodes.LQC] = ["QualityLQC", "QualityLQCSecondary", "Quality"],
            [WorkflowStageCodes.ManufacturingCompleted] = ["ManufacturingPrimary", "ManufacturingSecondary", "Manufacturing"],
            [WorkflowStageCodes.OQC] = ["QualityOQC", "QualityOQCSecondary", "Quality"],
            [WorkflowStageCodes.CustomerInspection] = ["QualityCustomerInspection", "QualityCustomerInspectionSecondary", "Quality"],
            [WorkflowStageCodes.FAT] = ["QualityCustomerInspection", "QualityCustomerInspectionSecondary", "Quality"],
            [WorkflowStageCodes.PackingCompleted] = ["LogisticsPrimary", "LogisticsSecondary", "Logistics"],
            [WorkflowStageCodes.DepartureProcessed] = ["LogisticsPrimary", "LogisticsSecondary", "Logistics"],
            [WorkflowStageCodes.DeliveryCompleted] = ["LogisticsPrimary", "LogisticsSecondary", "Logistics"],
            [WorkflowStageCodes.SalesSettlementCompleted] = ["SalesPrimary", "SalesSecondary"]
        };

    public static async Task UpsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid workItemId,
        Guid primaryUserId,
        IReadOnlyList<string> secondaryResponsibilityTypes,
        string title,
        string message,
        string linkUrl,
        string idempotencyKey,
        CancellationToken cancellationToken,
        string sourceKind = NotificationSourceKinds.WorkAssignment)
    {
        Guid notificationId;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into notifications (
                    project_id, notification_type, severity, title, message, link_url,
                    idempotency_key, visibility_scope, source_kind, work_item_id
                ) values (
                    @project_id, 'Info', 'Info', @title, @message, @link_url,
                    @idempotency_key, 'RecipientOnly', @source_kind, @work_item_id
                )
                on conflict (idempotency_key) do update
                set title=excluded.title, message=excluded.message, link_url=excluded.link_url, work_item_id=excluded.work_item_id
                returning id;
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("title", $"새 업무 · {title}");
            command.Parameters.AddWithValue("message", message);
            command.Parameters.AddWithValue("link_url", linkUrl);
            command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
            command.Parameters.AddWithValue("source_kind", sourceKind);
            command.Parameters.AddWithValue("work_item_id", workItemId);
            notificationId = (Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty);
        }

        var isDepartmentHeadFallback = await ExpandDepartmentHeadFallbackAsync(
            connection,
            transaction,
            projectId,
            workItemId,
            primaryUserId,
            title,
            message,
            linkUrl,
            idempotencyKey,
            sourceKind,
            cancellationToken);

        var recipientIds = new List<Guid> { primaryUserId };
        if (!isDepartmentHeadFallback && secondaryResponsibilityTypes.Count > 0)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                select distinct assigned_user_id
                from project_assignees
                where project_id=@project_id and responsibility_type=any(@responsibilities);
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("responsibilities", secondaryResponsibilityTypes.ToArray());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) recipientIds.Add(reader.GetGuid(0));
        }

        foreach (var recipientId in recipientIds.Distinct())
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into notification_recipients (notification_id, user_id)
                values (@notification_id, @user_id)
                on conflict (notification_id, user_id) do nothing;
                """;
            command.Parameters.AddWithValue("notification_id", notificationId);
            command.Parameters.AddWithValue("user_id", recipientId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<bool> ExpandDepartmentHeadFallbackAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid workItemId,
        Guid primaryUserId,
        string title,
        string message,
        string linkUrl,
        string notificationIdempotencyKey,
        string sourceKind,
        CancellationToken cancellationToken)
    {
        Guid departmentId;
        var fallbackGroupKey = string.Empty;
        var workflowStageCode = string.Empty;
        var workItemResponsibilityType = string.Empty;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select department.id, work_item.idempotency_key, work_item.workflow_stage_code, work_item.responsibility_type
                from work_items work_item
                join workflow_stages stage on stage.stage_code = work_item.workflow_stage_code
                join departments department on department.code = stage.department_code and department.is_active = true
                join qms_users assigned_user
                  on assigned_user.id = work_item.assigned_user_id
                 and assigned_user.department_id = department.id
                 and assigned_user.is_active = true
                 and assigned_user.is_department_head = true
                where work_item.id = @work_item_id
                  and work_item.project_id = @project_id
                  and work_item.assigned_user_id = @primary_user_id;
                """;
            command.Parameters.AddWithValue("work_item_id", workItemId);
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("primary_user_id", primaryUserId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return false;
            }

            departmentId = reader.GetGuid(0);
            fallbackGroupKey = reader.GetString(1);
            workflowStageCode = reader.GetString(2);
            workItemResponsibilityType = reader.GetString(3);
        }

        var responsibilityTypes = StageResponsibilityTypes.TryGetValue(workflowStageCode, out var configuredTypes)
            ? configuredTypes
            : FallbackResponsibilityTypes(workItemResponsibilityType);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select exists (
                    select 1
                    from project_assignees project_assignee
                    join qms_users project_user
                      on project_user.id = project_assignee.assigned_user_id
                     and project_user.is_active = true
                    where project_assignee.project_id = @project_id
                      and project_assignee.responsibility_type = any(@responsibility_types)
                );
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("responsibility_types", responsibilityTypes);
            if (await command.ExecuteScalarAsync(cancellationToken) is true)
            {
                return false;
            }
        }

        var departmentHeads = new List<Guid>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select id
                from qms_users
                where department_id = @department_id
                  and is_active = true
                  and is_department_head = true
                order by display_name, id;
                """;
            command.Parameters.AddWithValue("department_id", departmentId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                departmentHeads.Add(reader.GetGuid(0));
            }
        }

        if (departmentHeads.Count == 0)
        {
            throw new DepartmentHeadRequiredException(await ReadDepartmentCodeAsync(
                connection,
                transaction,
                departmentId,
                cancellationToken));
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update work_items
                set fallback_group_key = @fallback_group_key
                where id = @work_item_id;
                """;
            command.Parameters.AddWithValue("fallback_group_key", fallbackGroupKey);
            command.Parameters.AddWithValue("work_item_id", workItemId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var departmentHeadId in departmentHeads.Where(id => id != primaryUserId))
        {
            Guid fallbackWorkItemId;
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    insert into work_items (
                        project_id, target_type, target_id, workflow_stage_code, responsibility_type,
                        assigned_user_id, assigned_role_code, title, description, status, priority,
                        due_date, generated_by_event_id, idempotency_key, created_by_user_id, link_url,
                        fallback_group_key)
                    select
                        source.project_id, source.target_type, source.target_id, source.workflow_stage_code,
                        source.responsibility_type, @department_head_id, null, source.title, source.description,
                        source.status, source.priority, source.due_date, source.generated_by_event_id,
                        source.idempotency_key || ':fallback:' || @department_head_id::text,
                        source.created_by_user_id, source.link_url, @fallback_group_key
                    from work_items source
                    where source.id = @source_work_item_id
                    on conflict (idempotency_key) do update
                    set title = excluded.title,
                        description = excluded.description,
                        due_date = excluded.due_date,
                        link_url = excluded.link_url,
                        fallback_group_key = excluded.fallback_group_key
                    returning id;
                    """;
                command.Parameters.AddWithValue("department_head_id", departmentHeadId);
                command.Parameters.AddWithValue("fallback_group_key", fallbackGroupKey);
                command.Parameters.AddWithValue("source_work_item_id", workItemId);
                fallbackWorkItemId = (Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty);
            }

            Guid fallbackNotificationId;
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    insert into notifications (
                        project_id, notification_type, severity, title, message, link_url,
                        idempotency_key, visibility_scope, source_kind, work_item_id)
                    values (
                        @project_id, 'Info', 'Info', @title, @message, @link_url,
                        @idempotency_key, 'RecipientOnly', @source_kind, @work_item_id)
                    on conflict (idempotency_key) do update
                    set title = excluded.title,
                        message = excluded.message,
                        link_url = excluded.link_url,
                        work_item_id = excluded.work_item_id
                    returning id;
                    """;
                command.Parameters.AddWithValue("project_id", projectId);
                command.Parameters.AddWithValue("title", $"새 업무 · {title}");
                command.Parameters.AddWithValue("message", message);
                command.Parameters.AddWithValue("link_url", linkUrl);
                command.Parameters.AddWithValue("idempotency_key", $"{notificationIdempotencyKey}:fallback:{departmentHeadId}");
                command.Parameters.AddWithValue("source_kind", sourceKind);
                command.Parameters.AddWithValue("work_item_id", fallbackWorkItemId);
                fallbackNotificationId = (Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty);
            }

            await using var recipient = connection.CreateCommand();
            recipient.Transaction = transaction;
            recipient.CommandText = """
                insert into notification_recipients (notification_id, user_id)
                values (@notification_id, @user_id)
                on conflict (notification_id, user_id) do nothing;
                """;
            recipient.Parameters.AddWithValue("notification_id", fallbackNotificationId);
            recipient.Parameters.AddWithValue("user_id", departmentHeadId);
            await recipient.ExecuteNonQueryAsync(cancellationToken);
        }

        return true;
    }

    private static string[] FallbackResponsibilityTypes(string responsibilityType)
    {
        var secondary = responsibilityType.EndsWith("Primary", StringComparison.Ordinal)
            ? $"{responsibilityType[..^"Primary".Length]}Secondary"
            : $"{responsibilityType}Secondary";
        return [responsibilityType, secondary];
    }

    private static async Task<string> ReadDepartmentCodeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid departmentId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select code from departments where id = @department_id;";
        command.Parameters.AddWithValue("department_id", departmentId);
        return (string?)await command.ExecuteScalarAsync(cancellationToken) ?? "담당";
    }
}
