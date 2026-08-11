using Emi.Qms.Api.Notifications;
using Npgsql;

namespace Emi.Qms.Api.Workflow;

public sealed class WorkflowStore(DatabaseConnectionStringProvider connectionStringProvider)
{
    private static readonly IReadOnlySet<string> ProgressImplicitCompletionStageCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        WorkflowStageCodes.ProcurementInfo,
        WorkflowStageCodes.MaterialArrived,
        WorkflowStageCodes.IQC,
        WorkflowStageCodes.ReceiptConfirmed
    };

    private static readonly IReadOnlyDictionary<string, string> StageToNextStage = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [WorkflowStageCodes.SalesProjectCreated] = WorkflowStageCodes.ProductionPlanning,
        [WorkflowStageCodes.ProductionPlanning] = WorkflowStageCodes.DesignPanelInfo,
        [WorkflowStageCodes.DesignPanelInfo] = WorkflowStageCodes.ProcurementInfo,
        [WorkflowStageCodes.ProcurementInfo] = WorkflowStageCodes.MaterialArrived,
        [WorkflowStageCodes.MaterialArrived] = WorkflowStageCodes.IQC,
        [WorkflowStageCodes.IQC] = WorkflowStageCodes.ReceiptConfirmed,
        [WorkflowStageCodes.ManufacturingWork] = WorkflowStageCodes.LQC,
        [WorkflowStageCodes.LQC] = WorkflowStageCodes.ManufacturingCompleted,
        [WorkflowStageCodes.ManufacturingCompleted] = WorkflowStageCodes.OQC,
        [WorkflowStageCodes.OQC] = WorkflowStageCodes.CustomerInspection,
        [WorkflowStageCodes.CustomerInspection] = WorkflowStageCodes.FAT,
        [WorkflowStageCodes.FAT] = WorkflowStageCodes.PackingCompleted,
        [WorkflowStageCodes.PackingCompleted] = WorkflowStageCodes.DepartureProcessed,
        [WorkflowStageCodes.DepartureProcessed] = WorkflowStageCodes.DeliveryCompleted,
        [WorkflowStageCodes.DeliveryCompleted] = WorkflowStageCodes.SalesSettlementCompleted
    };

    private static readonly IReadOnlyDictionary<string, ResponsibilityTarget> StageResponsibilities = new Dictionary<string, ResponsibilityTarget>(StringComparer.Ordinal)
    {
        [WorkflowStageCodes.DesignPanelInfo] = new("DesignPrimary", "DesignSecondary", []),
        [WorkflowStageCodes.ProductionPlanning] = new("ProductionPlanningPrimary", "ProductionPlanningSecondary", ["ProductionPlanning"]),
        [WorkflowStageCodes.ProcurementInfo] = new("ProcurementPrimary", "ProcurementSecondary", ["Procurement"]),
        [WorkflowStageCodes.MaterialArrived] = new("MaterialsPrimary", "MaterialsSecondary", []),
        [WorkflowStageCodes.IQC] = new("QualityIQC", "QualityIQCSecondary", ["Quality"]),
        [WorkflowStageCodes.ReceiptConfirmed] = new("MaterialsPrimary", "MaterialsSecondary", []),
        // The persisted stage code is kept for migration compatibility. Its product meaning is
        // now the production-planning manufacturing request, not mandatory material kitting.
        [WorkflowStageCodes.KittingCompleted] = new("ProductionPlanningPrimary", "ProductionPlanningSecondary", ["ProductionPlanning"]),
        [WorkflowStageCodes.ManufacturingWork] = new("ManufacturingPrimary", "ManufacturingSecondary", ["Manufacturing"]),
        [WorkflowStageCodes.LQC] = new("QualityLQC", "QualityLQCSecondary", ["Quality"]),
        [WorkflowStageCodes.ManufacturingCompleted] = new("ManufacturingPrimary", "ManufacturingSecondary", ["Manufacturing"]),
        [WorkflowStageCodes.OQC] = new("QualityOQC", "QualityOQCSecondary", ["Quality"]),
        [WorkflowStageCodes.CustomerInspection] = new("QualityCustomerInspection", "QualityCustomerInspectionSecondary", ["Quality"]),
        [WorkflowStageCodes.FAT] = new("QualityCustomerInspection", "QualityCustomerInspectionSecondary", ["Quality"]),
        [WorkflowStageCodes.PackingCompleted] = new("LogisticsPrimary", "LogisticsSecondary", ["Logistics"]),
        [WorkflowStageCodes.DepartureProcessed] = new("LogisticsPrimary", "LogisticsSecondary", ["Logistics"]),
        [WorkflowStageCodes.DeliveryCompleted] = new("LogisticsPrimary", "LogisticsSecondary", ["Logistics"]),
        [WorkflowStageCodes.SalesSettlementCompleted] = new("SalesPrimary", "SalesSecondary", [])
    };

    internal static async Task<Guid?> EnsureEffectiveKittingStageCompletedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        string sourceType,
        Guid sourceId,
        Guid operationId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        Guid? eventId;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into project_workflow_events (
                    project_id, stage_code, event_type, event_status, source_type, source_id,
                    correlation_id, created_by_user_id, note
                )
                select
                    @project_id, 'KittingCompleted', 'StageCompleted', 'Succeeded',
                    @source_type, @source_id, @correlation_id, @actor_id,
                    '모든 활성 패널에 생산관리 제조 투입 요청됨'
                where (
                    select count(*)
                    from panel_placeholders panel
                    where panel.project_id = @project_id
                      and panel.status = 'Active'
                ) > 0
                  and (
                    select count(*)
                    from panel_placeholders panel
                    where panel.project_id = @project_id
                      and panel.status = 'Active'
                      and exists (
                          select 1 from panel_manufacturing_release_operations release
                          where release.project_id = @project_id
                            and panel.id = any(release.panel_ids)
                      )
                ) = (
                    select count(*)
                    from panel_placeholders panel
                    where panel.project_id = @project_id
                      and panel.status = 'Active'
                )
                  and not exists (
                      select 1
                      from project_workflow_events event
                      where event.project_id = @project_id
                        and event.stage_code = 'KittingCompleted'
                        and event.event_type = 'StageCompleted'
                        and event.event_status = 'Succeeded'
                  )
                returning id;
                """;
            insert.Parameters.AddWithValue("project_id", projectId);
            insert.Parameters.AddWithValue("source_type", sourceType);
            insert.Parameters.AddWithValue("source_id", sourceId);
            insert.Parameters.AddWithValue("correlation_id", operationId.ToString("D"));
            insert.Parameters.AddWithValue("actor_id", actorUserId);
            eventId = (Guid?)await insert.ExecuteScalarAsync(cancellationToken);
        }

        if (eventId is null)
        {
            await using var existing = connection.CreateCommand();
            existing.Transaction = transaction;
            existing.CommandText = """
                select id
                from project_workflow_events
                where project_id = @project_id
                  and stage_code = 'KittingCompleted'
                  and event_type = 'StageCompleted'
                  and event_status = 'Succeeded'
                order by created_at_utc
                limit 1;
                """;
            existing.Parameters.AddWithValue("project_id", projectId);
            eventId = (Guid?)await existing.ExecuteScalarAsync(cancellationToken);
        }

        if (eventId is not null)
        {
            await MarkStageWorkItemsCompletedAsync(
                connection,
                transaction,
                projectId,
                WorkflowStageCodes.KittingCompleted,
                actorUserId,
                cancellationToken);
        }

        return eventId;
    }

    public async Task CompleteStageAsync(
        Guid projectId,
        string stageCode,
        string sourceType,
        Guid? sourceId,
        Guid createdByUserId,
        string? correlationId,
        string? note,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var project = await ReadProjectAsync(connection, transaction, projectId, cancellationToken);
        if (project is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        await MarkStageWorkItemsCompletedAsync(connection, transaction, projectId, stageCode, createdByUserId, cancellationToken);

        var eventId = await InsertWorkflowEventAsync(
            connection,
            transaction,
            projectId,
            stageCode,
            "StageCompleted",
            sourceType,
            sourceId,
            createdByUserId,
            correlationId,
            note,
            cancellationToken);

        if (!string.Equals(stageCode, WorkflowStageCodes.SalesProjectCreated, StringComparison.Ordinal)
            && StageToNextStage.TryGetValue(stageCode, out var nextStageCode))
        {
            var effectiveNextStageCode = string.Equals(nextStageCode, WorkflowStageCodes.LQC, StringComparison.Ordinal)
                && !project.LqcOperational
                    ? WorkflowStageCodes.ManufacturingCompleted
                    : nextStageCode;
            var nextStageCodes = string.Equals(stageCode, WorkflowStageCodes.ProductionPlanning, StringComparison.Ordinal)
                ? new[] { effectiveNextStageCode, WorkflowStageCodes.ProcurementInfo }
                : new[] { effectiveNextStageCode };
            foreach (var activatedStageCode in nextStageCodes)
            {
                if (!StageResponsibilities.TryGetValue(activatedStageCode, out var target)) continue;
                var stage = await ReadStageAsync(connection, transaction, activatedStageCode, cancellationToken);
                if (stage is null) continue;
                var assignee = await ResolveAssigneeAsync(connection, transaction, project, target, cancellationToken);
                if (assignee.UserId is not null)
                {
                    await CreateWorkItemAsync(
                        connection,
                        transaction,
                        projectId,
                        activatedStageCode,
                        target.Primary,
                        assignee.UserId.Value,
                        assignee.RoleCode,
                        WorkItemTitleForStage(stage.StageCode),
                        BuildWorkDescription(stage, assignee),
                        eventId,
                        createdByUserId,
                        $"project:{projectId}:stage:{activatedStageCode}:work:{target.Primary}",
                        cancellationToken);
                }

                await CreateSecondaryReferenceNotificationAsync(
                    connection,
                    transaction,
                    project,
                    stage,
                    target,
                    eventId,
                    cancellationToken);
            }
        }

        if (string.Equals(stageCode, WorkflowStageCodes.SalesProjectCreated, StringComparison.Ordinal))
        {
            await CreateAllDepartmentsReferenceNotificationAsync(connection, transaction, project, eventId, cancellationToken);
        }
        else if (string.Equals(stageCode, WorkflowStageCodes.ProcurementInfo, StringComparison.Ordinal))
        {
            await CreateDepartmentReferenceNotificationAsync(
                connection,
                transaction,
                project,
                eventId,
                "구매정보가 저장되었습니다.",
                "생산관리와 제조 담당자는 구매정보 저장 내용을 참고해 주세요.",
                ["ProductionPlanningPrimary", "ProductionPlanning", "ManufacturingPrimary", "Manufacturing"],
                ["production-planning", "manufacturing"],
                $"project:{projectId}:stage:{stageCode}:reference:production-manufacturing",
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task GenerateProductionPlanningAssigneeFollowUpsAsync(
        Guid projectId,
        Guid changedByUserId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var project = await ReadProjectAsync(connection, transaction, projectId, cancellationToken);
        if (project is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        var eventId = await InsertWorkflowEventAsync(
            connection,
            transaction,
            projectId,
            WorkflowStageCodes.ProductionPlanning,
            "WorkGenerated",
            "Project",
            projectId,
            changedByUserId,
            correlationId,
            "담당자 지정 저장",
            cancellationToken);

        foreach (var assignee in await ReadProjectAssigneesAsync(connection, transaction, projectId, cancellationToken))
        {
            await CreateNotificationAsync(
                connection,
                transaction,
                projectId,
                "Reference",
                "Info",
                "프로젝트 담당자로 지정되었습니다.",
                $"{project.ProjectTitle} 프로젝트의 {ResponsibilityLabel(assignee.ResponsibilityType)}로 지정되었습니다.",
                $"/projects/{projectId}?section=production-planning",
                eventId,
                $"project:{projectId}:assignee:{assignee.ResponsibilityType}:{assignee.UserId}:reference",
                [assignee.UserId],
                cancellationToken);
        }

        var target = (
            StageCode: WorkflowStageCodes.ProductionPlanning,
            Responsibility: StageResponsibilities[WorkflowStageCodes.ProductionPlanning]);
        var stage = await ReadStageAsync(connection, transaction, target.StageCode, cancellationToken);
        if (stage is not null)
        {
            var assignee = await ResolveAssigneeAsync(connection, transaction, project, target.Responsibility, cancellationToken);
            if (assignee.UserId is not null)
            {
                await CreateWorkItemAsync(
                    connection,
                    transaction,
                    projectId,
                    target.StageCode,
                    target.Responsibility.Primary,
                    assignee.UserId.Value,
                    assignee.RoleCode,
                    WorkItemTitleForStage(stage.StageCode),
                    BuildWorkDescription(stage, assignee),
                    eventId,
                    changedByUserId,
                    $"project:{projectId}:assignee-save:stage:{target.StageCode}:work:{target.Responsibility.Primary}:{assignee.UserId.Value}",
                    cancellationToken);
            }

            await CreateSecondaryReferenceNotificationAsync(
                connection,
                transaction,
                project,
                stage,
                target.Responsibility,
                eventId,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowStageResponse>> ListStagesAsync(CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select stage_code, sequence_number, department_code, stage_name, is_optional, is_active
            from workflow_stages
            order by sequence_number;
            """);

        var stages = new List<WorkflowStageResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var department = reader.GetString(2);
            stages.Add(new WorkflowStageResponse(
                reader.GetString(0),
                reader.GetInt32(1),
                department,
                DepartmentLabel(department),
                StageDisplayName(reader.GetString(0), reader.GetString(3)),
                reader.GetBoolean(4),
                reader.GetBoolean(5)));
        }

        return stages;
    }

    public async Task<ProjectWorkflowResponse?> GetProjectWorkflowAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var projectCommand = connection.CreateCommand();
        projectCommand.CommandText = "select fat_required from projects where id = @project_id and deleted_at_utc is null;";
        projectCommand.Parameters.AddWithValue("project_id", projectId);
        var fatRequiredValue = await projectCommand.ExecuteScalarAsync(cancellationToken);
        if (fatRequiredValue is not bool projectFatRequired)
        {
            return null;
        }

        var facts = await ReadWorkflowFactsAsync(connection, projectId, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                ws.stage_code,
                ws.sequence_number,
                ws.department_code,
                ws.stage_name,
                ws.is_optional,
                max(e.created_at_utc) filter (where e.event_type = 'StageCompleted' and e.event_status = 'Succeeded') as completed_at_utc,
                count(wi.id) as work_item_count,
                bool_or(wi.status = 'InProgress') as has_in_progress,
                bool_or(wi.status = 'Requested') as has_requested,
                bool_or(wi.priority = 'Blocking' and wi.status in ('Requested', 'InProgress')) as has_blocking
            from workflow_stages ws
            left join project_workflow_events e on e.project_id = @project_id and e.stage_code = ws.stage_code
            left join work_items wi on wi.project_id = @project_id and wi.workflow_stage_code = ws.stage_code
            where ws.is_active = true
              and (
                  ws.stage_code <> 'LQC'
                  or exists (
                      select 1
                      from projects project
                      where project.id = @project_id
                        and project.lqc_operational_snapshot
                  )
              )
            group by ws.stage_code, ws.sequence_number, ws.department_code, ws.stage_name, ws.is_optional
            order by ws.sequence_number;
            """;
        command.Parameters.AddWithValue("project_id", projectId);

        var stages = new List<ProjectWorkflowStageResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var completedAt = reader.IsDBNull(5) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(5);
            var workItemCount = reader.GetInt64(6);
            var hasInProgress = !reader.IsDBNull(7) && reader.GetBoolean(7);
            var hasRequested = !reader.IsDBNull(8) && reader.GetBoolean(8);
            var hasBlocking = !reader.IsDBNull(9) && reader.GetBoolean(9);
            var stageCode = reader.GetString(0);
            var isOptional = reader.GetBoolean(4);
            var status = ApplyImplementedStageStatus(
                stageCode,
                DetermineWorkflowStatus(completedAt, hasInProgress, hasRequested, hasBlocking),
                facts);
            if (string.Equals(stageCode, WorkflowStageCodes.FAT, StringComparison.Ordinal) && isOptional && !projectFatRequired)
            {
                status = "Skipped";
            }
            var department = reader.GetString(2);

            stages.Add(new ProjectWorkflowStageResponse(
                stageCode,
                reader.GetInt32(1),
                department,
                DepartmentLabel(department),
                StageDisplayName(stageCode, reader.GetString(3)),
                isOptional,
                status,
                WorkflowStatusLabel(status),
                checked((int)workItemCount),
                completedAt));
        }

        var requiredStages = stages
            .Where(stage => !stage.IsOptional || (string.Equals(stage.StageCode, WorkflowStageCodes.FAT, StringComparison.Ordinal) && projectFatRequired))
            .ToList();
        var furthestReachedSequence = requiredStages
            .Where(IsReachedStage)
            .Select(stage => stage.SequenceNumber)
            .DefaultIfEmpty(0)
            .Max();
        var completedRequiredCount = requiredStages.Count(stage =>
            string.Equals(stage.Status, "Completed", StringComparison.Ordinal)
            || (ProgressImplicitCompletionStageCodes.Contains(stage.StageCode)
                && stage.SequenceNumber < furthestReachedSequence));
        var progressPercent = requiredStages.Count == 0
            ? 0
            : (int)Math.Round(completedRequiredCount * 100m / requiredStages.Count, MidpointRounding.AwayFromZero);
        var currentStage = SelectCurrentStage(requiredStages)
            ?? stages.LastOrDefault();

        return new ProjectWorkflowResponse(
            projectId,
            stages,
            stages.Sum(stage => stage.WorkItemCount),
            requiredStages.Count,
            completedRequiredCount,
            progressPercent,
            currentStage?.StageCode ?? WorkflowStageCodes.SalesProjectCreated,
            currentStage?.StageName ?? "프로젝트 생성",
            currentStage?.DepartmentCode ?? "sales",
            currentStage?.DepartmentLabel ?? "영업");
    }

    private static ProjectWorkflowStageResponse? SelectCurrentStage(IReadOnlyList<ProjectWorkflowStageResponse> requiredStages)
    {
        for (var index = requiredStages.Count - 1; index >= 0; index--)
        {
            var stage = requiredStages[index];
            if (!IsReachedStage(stage))
            {
                continue;
            }

            return string.Equals(stage.Status, "Completed", StringComparison.Ordinal)
                && index + 1 < requiredStages.Count
                    ? requiredStages[index + 1]
                    : stage;
        }

        return requiredStages.FirstOrDefault();
    }

    private static bool IsReachedStage(ProjectWorkflowStageResponse stage) =>
        !string.Equals(stage.Status, "NotStarted", StringComparison.Ordinal)
        && !string.Equals(stage.Status, "Skipped", StringComparison.Ordinal);

    public async Task SyncStageWorkItemsAfterSaveAsync(
        Guid projectId,
        string stageCode,
        string sourceType,
        Guid? sourceId,
        Guid changedByUserId,
        string? correlationId,
        string? completedNote,
        CancellationToken cancellationToken)
    {
        var workflow = await GetProjectWorkflowAsync(projectId, cancellationToken);
        var stage = workflow?.Stages.FirstOrDefault(item => string.Equals(item.StageCode, stageCode, StringComparison.Ordinal));
        if (stage is null)
        {
            return;
        }

        if (string.Equals(stage.Status, "Completed", StringComparison.Ordinal))
        {
            await MarkStageWorkItemsCompletedAsync(projectId, stageCode, changedByUserId, cancellationToken);
            if (!await HasCompletedStageEventAsync(projectId, stageCode, cancellationToken))
            {
                await CompleteStageAsync(
                    projectId,
                    stageCode,
                    sourceType,
                    sourceId,
                    changedByUserId,
                    correlationId,
                    completedNote,
                    cancellationToken);
            }

            return;
        }

        if (string.Equals(stage.Status, "InProgress", StringComparison.Ordinal))
        {
            await MarkStageWorkItemsStartedAsync(projectId, stageCode, cancellationToken);
        }
    }

    public async Task<MyWorkSummaryResponse> GetMyWorkSummaryAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select
                count(*) filter (where status = 'Requested'),
                count(*) filter (where status = 'InProgress'),
                count(*) filter (where status = 'Completed'),
                count(*) filter (where priority = 'Blocking' and status in ('Requested', 'InProgress'))
            from work_items
            where assigned_user_id = @user_id;
            """);
        command.Parameters.AddWithValue("user_id", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new MyWorkSummaryResponse(0, 0, 0, 0, 0, []);
        }

        var requested = checked((int)reader.GetInt64(0));
        var inProgress = checked((int)reader.GetInt64(1));
        var completed = checked((int)reader.GetInt64(2));
        var blocking = checked((int)reader.GetInt64(3));
        await reader.DisposeAsync();
        var assignedProjectCount = await ReadAssignedProjectCountAsync(dataSource, userId, cancellationToken);
        var breakdown = await ReadAssignedProjectBreakdownAsync(dataSource, userId, cancellationToken);

        return new MyWorkSummaryResponse(requested, inProgress, completed, blocking, assignedProjectCount, breakdown);
    }

    public async Task<MyWorkListResponse> GetMyWorkItemsAsync(Guid userId, string? status, CancellationToken cancellationToken)
    {
        return await GetMyWorkItemsAsync(userId, status, null, cancellationToken);
    }

    public Task<MyWorkListResponse> GetMyWorkItemsForExportAsync(
        Guid userId,
        string? status,
        int rowLimit,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rowLimit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rowLimit, 10_001);
        return GetMyWorkItemsAsync(userId, status, rowLimit, cancellationToken);
    }

    private async Task<MyWorkListResponse> GetMyWorkItemsAsync(
        Guid userId,
        string? status,
        int? rowLimit,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        var statusFilter = NormalizeWorkStatusFilter(status);
        await using var command = dataSource.CreateCommand($"""
            select
                wi.id,
                wi.project_id,
                p.project_title,
                p.project_code,
                p.item,
                p.delivery_date,
                wi.workflow_stage_code,
                ws.stage_name,
                wi.responsibility_type,
                wi.title,
                wi.description,
                wi.status,
                wi.priority,
                wi.due_date,
                wi.created_at_utc,
                wi.started_at_utc,
                wi.completed_at_utc,
                wi.target_type,
                wi.target_id,
                wi.link_url,
                wi.fallback_group_key is not null,
                wi.fallback_auto_closed_at_utc is not null
            from work_items wi
            join projects p on p.id = wi.project_id
            join workflow_stages ws on ws.stage_code = wi.workflow_stage_code
            where wi.assigned_user_id = @user_id
              and p.deleted_at_utc is null
              {(statusFilter is null ? "" : "and wi.status = @status")}
            order by
                case wi.status when 'Requested' then 0 when 'InProgress' then 1 when 'Completed' then 2 else 3 end,
                wi.created_at_utc desc
            {(rowLimit is null ? ";" : "limit @row_limit;")}
            """);
        command.Parameters.AddWithValue("user_id", userId);
        if (statusFilter is not null)
        {
            command.Parameters.AddWithValue("status", statusFilter);
        }
        if (rowLimit is not null)
        {
            command.Parameters.AddWithValue("row_limit", rowLimit.Value);
        }

        var items = new List<MyWorkItemResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadWorkItem(reader));
        }

        return new MyWorkListResponse(items);
    }

    private static async Task<int> ReadAssignedProjectCountAsync(
        NpgsqlDataSource dataSource,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            select count(distinct p.id)::int
            from project_assignees pa
            join projects p on p.id = pa.project_id
            where pa.assigned_user_id = @user_id
              and p.deleted_at_utc is null
              and p.status not in ('Cancelled', 'Completed');
            """);
        command.Parameters.AddWithValue("user_id", userId);
        return (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
    }

    private static async Task<IReadOnlyList<MyAssignedProjectBreakdownResponse>> ReadAssignedProjectBreakdownAsync(
        NpgsqlDataSource dataSource,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            select pa.responsibility_type, count(distinct p.id)::int
            from project_assignees pa
            join projects p on p.id = pa.project_id
            where pa.assigned_user_id = @user_id
              and p.deleted_at_utc is null
              and p.status not in ('Cancelled', 'Completed')
            group by pa.responsibility_type
            order by pa.responsibility_type;
            """);
        command.Parameters.AddWithValue("user_id", userId);

        var rows = new List<MyAssignedProjectBreakdownResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var responsibilityType = reader.GetString(0);
            rows.Add(new MyAssignedProjectBreakdownResponse(
                responsibilityType,
                ResponsibilityLabel(responsibilityType),
                reader.GetInt32(1)));
        }

        return rows;
    }

    public async Task<MyAssignedProjectsResponse> GetMyAssignedProjectsAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select
                p.id,
                p.project_title,
                p.project_code,
                p.item,
                p.delivery_date,
                p.status,
                pa.responsibility_type
            from project_assignees pa
            join projects p on p.id = pa.project_id
            where pa.assigned_user_id = @user_id
              and p.deleted_at_utc is null
              and p.status in ('Active', 'OnHold')
            order by p.delivery_date nulls last, p.project_title, pa.responsibility_type;
            """);
        command.Parameters.AddWithValue("user_id", userId);

        var grouped = new Dictionary<Guid, AssignedProjectBuilder>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var projectId = reader.GetGuid(0);
            if (!grouped.TryGetValue(projectId, out var builder))
            {
                builder = new AssignedProjectBuilder(
                    projectId,
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4),
                    reader.GetString(5));
                grouped[projectId] = builder;
            }

            var responsibility = reader.GetString(6);
            builder.Responsibilities.Add(new MyAssignedProjectResponsibilityResponse(
                responsibility,
                ResponsibilityLabel(responsibility)));
        }

        return new MyAssignedProjectsResponse(grouped.Values
            .Select(item => new MyAssignedProjectResponse(
                item.ProjectId,
                item.ProjectTitle,
                item.ProjectCode,
                item.Item,
                item.DeliveryDate,
                item.ProjectStatus,
                ProjectStatusLabel(item.ProjectStatus),
                item.Responsibilities
                    .DistinctBy(responsibility => responsibility.ResponsibilityType)
                    .OrderBy(responsibility => responsibility.ResponsibilityLabel, StringComparer.Ordinal)
                    .ToList()))
            .ToList());
    }

    public async Task<WorkflowMutationResult<MyWorkItemResponse>> GetMyWorkItemAsync(
        Guid workItemId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var item = await ReadAssignedWorkItemAsync(workItemId, userId, cancellationToken);
        return item is null
            ? WorkflowMutationResult<MyWorkItemResponse>.NotFound()
            : WorkflowMutationResult<MyWorkItemResponse>.Success(item);
    }

    public Task<WorkflowMutationResult<MyWorkItemResponse>> StartWorkItemAsync(Guid workItemId, Guid userId, CancellationToken cancellationToken)
    {
        return TransitionWorkItemAsync(workItemId, userId, "start", cancellationToken);
    }

    public Task<WorkflowMutationResult<MyWorkItemResponse>> CompleteWorkItemAsync(Guid workItemId, Guid userId, CancellationToken cancellationToken)
    {
        return TransitionWorkItemAsync(workItemId, userId, "complete", cancellationToken);
    }

    public Task<WorkflowMutationResult<MyWorkItemResponse>> CancelWorkItemAsync(Guid workItemId, Guid userId, CancellationToken cancellationToken)
    {
        return TransitionWorkItemAsync(workItemId, userId, "cancel", cancellationToken);
    }

    private async Task MarkStageWorkItemsStartedAsync(Guid projectId, string stageCode, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await MarkStageWorkItemsStartedAsync(connection, null, projectId, stageCode, cancellationToken);
    }

    private static async Task MarkStageWorkItemsStartedAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid projectId, string stageCode, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update work_items
            set status = case when status = 'Requested' then 'InProgress' else status end,
                started_at_utc = coalesce(started_at_utc, now())
            where project_id = @project_id
              and workflow_stage_code = @stage_code
              and status in ('Requested', 'InProgress');
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("stage_code", stageCode);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task MarkStageWorkItemsCompletedAsync(
        Guid projectId,
        string stageCode,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await MarkStageWorkItemsCompletedAsync(connection, transaction, projectId, stageCode, actorUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task MarkStageWorkItemsCompletedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        string stageCode,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update work_items
            set status = 'Completed',
                started_at_utc = coalesce(started_at_utc, now()),
                completed_at_utc = coalesce(completed_at_utc, now())
            where project_id = @project_id
              and workflow_stage_code = @stage_code
              and status in ('Requested', 'InProgress');
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("stage_code", stageCode);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await WorkItemFallbackCompletion.SynchronizeForProjectStageAsync(
            connection,
            transaction,
            projectId,
            stageCode,
            actorUserId,
            cancellationToken);
    }

    private async Task<bool> HasCompletedStageEventAsync(Guid projectId, string stageCode, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select exists (
                select 1
                from project_workflow_events
                where project_id = @project_id
                  and stage_code = @stage_code
                  and event_type = 'StageCompleted'
                  and event_status = 'Succeeded'
            );
            """);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("stage_code", stageCode);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool exists && exists;
    }

    public async Task<NotificationSummaryResponse> GetNotificationSummaryAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select
                count(*) filter (where nr.read_at_utc is null),
                count(*) filter (where nr.read_at_utc is null and n.severity in ('Warning', 'Critical'))
            from notifications n
            left join notification_recipients nr on nr.notification_id = n.id
                and nr.user_id = @user_id
            where exists (
                    select 1
                    from qms_users u
                    where u.id = @user_id
                      and u.is_active = true
                )
              and (nr.id is not null or n.visibility_scope = 'Authenticated');
            """);
        command.Parameters.AddWithValue("user_id", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new NotificationSummaryResponse(0, 0);
        }

        return new NotificationSummaryResponse(checked((int)reader.GetInt64(0)), checked((int)reader.GetInt64(1)));
    }

    public async Task<NotificationListResponse> GetNotificationsAsync(Guid userId, string? readStatus, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        var readFilter = NormalizeNotificationReadFilter(readStatus);
        await using var command = dataSource.CreateCommand($"""
            select
                n.id,
                n.project_id,
                p.project_title,
                p.project_code,
                p.item,
                n.work_item_id,
                wi.title,
                wi.workflow_stage_code,
                ws.stage_name,
                n.notification_type,
                n.severity,
                n.visibility_scope,
                n.source_kind,
                n.title,
                n.message,
                n.link_url,
                n.created_at_utc,
                nr.read_at_utc
            from notifications n
            left join notification_recipients nr on nr.notification_id = n.id
                and nr.user_id = @user_id
            left join projects p on p.id = n.project_id
            left join work_items wi on wi.id = n.work_item_id
            left join workflow_stages ws on ws.stage_code = wi.workflow_stage_code
            where exists (
                    select 1
                    from qms_users u
                    where u.id = @user_id
                      and u.is_active = true
                )
              and (nr.id is not null or n.visibility_scope = 'Authenticated')
              {(readFilter == "unread" ? "and nr.read_at_utc is null" : "")}
              {(readFilter == "read" ? "and nr.read_at_utc is not null" : "")}
            order by nr.read_at_utc nulls first, n.created_at_utc desc;
            """);
        command.Parameters.AddWithValue("user_id", userId);

        var items = new List<NotificationResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadNotification(reader));
        }

        return new NotificationListResponse(items);
    }

    public async Task<WorkflowMutationResult<NotificationResponse>> GetNotificationDetailAsync(
        Guid notificationId,
        Guid userId,
        bool canReadAll,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select
                n.id,
                n.project_id,
                p.project_title,
                p.project_code,
                p.item,
                n.work_item_id,
                wi.title,
                wi.workflow_stage_code,
                ws.stage_name,
                n.notification_type,
                n.severity,
                n.visibility_scope,
                n.source_kind,
                n.title,
                n.message,
                n.link_url,
                n.created_at_utc,
                nr.read_at_utc,
                nr.id,
                exists (
                    select 1
                    from qms_users u
                    where u.id = @user_id
                      and u.is_active = true
                ) as is_active_user
            from notifications n
            left join notification_recipients nr on nr.notification_id = n.id
                and nr.user_id = @user_id
            left join projects p on p.id = n.project_id
            left join work_items wi on wi.id = n.work_item_id
            left join workflow_stages ws on ws.stage_code = wi.workflow_stage_code
            where n.id = @notification_id;
            """);
        command.Parameters.AddWithValue("notification_id", notificationId);
        command.Parameters.AddWithValue("user_id", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return WorkflowMutationResult<NotificationResponse>.NotFound();
        }

        var visibilityScope = reader.GetString(11);
        var hasRecipient = !reader.IsDBNull(18);
        var isActiveUser = reader.GetBoolean(19);
        var canRead = visibilityScope switch
        {
            "Authenticated" => isActiveUser,
            "AdminOnly" => canReadAll,
            _ => hasRecipient
        };
        if (!canRead)
        {
            return WorkflowMutationResult<NotificationResponse>.Forbidden();
        }

        return WorkflowMutationResult<NotificationResponse>.Success(ReadNotification(reader));
    }

    public async Task<WorkflowMutationResult<NotificationResponse>> MarkNotificationReadAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        string visibilityScope;
        bool hasRecipient;
        bool isActiveUser;
        await using (var access = connection.CreateCommand())
        {
            access.CommandText = """
                select
                    n.visibility_scope,
                    nr.id is not null as has_recipient,
                    exists (
                        select 1
                        from qms_users u
                        where u.id = @user_id
                          and u.is_active = true
                    ) as is_active_user
                from notifications n
                left join notification_recipients nr on nr.notification_id = n.id
                    and nr.user_id = @user_id
                where n.id = @notification_id;
                """;
            access.Parameters.AddWithValue("notification_id", notificationId);
            access.Parameters.AddWithValue("user_id", userId);
            await using var reader = await access.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return WorkflowMutationResult<NotificationResponse>.NotFound();
            }

            visibilityScope = reader.GetString(0);
            hasRecipient = reader.GetBoolean(1);
            isActiveUser = reader.GetBoolean(2);
        }

        if (string.Equals(visibilityScope, "Authenticated", StringComparison.Ordinal))
        {
            if (!isActiveUser)
            {
                return WorkflowMutationResult<NotificationResponse>.Forbidden();
            }

            await using var insert = connection.CreateCommand();
            insert.CommandText = """
                insert into notification_recipients (notification_id, user_id)
                values (@notification_id, @user_id)
                on conflict (notification_id, user_id) do nothing;
                """;
            insert.Parameters.AddWithValue("notification_id", notificationId);
            insert.Parameters.AddWithValue("user_id", userId);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        else if (!hasRecipient)
        {
            return WorkflowMutationResult<NotificationResponse>.Forbidden();
        }

        await using (var update = connection.CreateCommand())
        {
            update.CommandText = """
                update notification_recipients
                set read_at_utc = coalesce(read_at_utc, now())
                where notification_id = @notification_id
                  and user_id = @user_id;
                """;
            update.Parameters.AddWithValue("notification_id", notificationId);
            update.Parameters.AddWithValue("user_id", userId);
            var affected = await update.ExecuteNonQueryAsync(cancellationToken);
            if (affected == 0)
            {
                return WorkflowMutationResult<NotificationResponse>.NotFound();
            }
        }

        var item = await ReadNotificationAsync(connection, notificationId, userId, cancellationToken);
        return item is null
            ? WorkflowMutationResult<NotificationResponse>.NotFound()
            : WorkflowMutationResult<NotificationResponse>.Success(item);
    }

    public async Task<NotificationSummaryResponse> MarkAllNotificationsReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using (var insert = dataSource.CreateCommand("""
            insert into notification_recipients (notification_id, user_id)
            select n.id, @user_id
            from notifications n
            where n.visibility_scope = 'Authenticated'
              and exists (
                    select 1
                    from qms_users u
                    where u.id = @user_id
                      and u.is_active = true
                )
            on conflict (notification_id, user_id) do nothing;
            """))
        {
            insert.Parameters.AddWithValue("user_id", userId);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = dataSource.CreateCommand("""
            update notification_recipients
            set read_at_utc = coalesce(read_at_utc, now())
            where user_id = @user_id
              and read_at_utc is null;
            """);
        command.Parameters.AddWithValue("user_id", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return await GetNotificationSummaryAsync(userId, cancellationToken);
    }

    public async Task<NotificationSummaryResponse> MarkProjectNotificationsReadAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using (var insert = dataSource.CreateCommand("""
            insert into notification_recipients (notification_id, user_id)
            select n.id, @user_id
            from notifications n
            where n.project_id = @project_id
              and n.visibility_scope = 'Authenticated'
              and exists (
                    select 1
                    from qms_users u
                    where u.id = @user_id
                      and u.is_active = true
                )
            on conflict (notification_id, user_id) do nothing;
            """))
        {
            insert.Parameters.AddWithValue("project_id", projectId);
            insert.Parameters.AddWithValue("user_id", userId);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = dataSource.CreateCommand("""
            update notification_recipients nr
            set read_at_utc = coalesce(nr.read_at_utc, now())
            from notifications n
            where nr.notification_id = n.id
              and nr.user_id = @user_id
              and n.project_id = @project_id
              and nr.read_at_utc is null;
            """);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("user_id", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return await GetNotificationSummaryAsync(userId, cancellationToken);
    }

    private async Task<WorkflowMutationResult<MyWorkItemResponse>> TransitionWorkItemAsync(
        Guid workItemId,
        Guid userId,
        string action,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        string? fallbackGroupKey = null;

        await using (var guard = connection.CreateCommand())
        {
            guard.Transaction = transaction;
            guard.CommandText = """
                select assigned_user_id, target_type, workflow_stage_code, project_id, fallback_group_key
                from work_items
                where id = @id;
                """;
            guard.Parameters.AddWithValue("id", workItemId);

            await using var reader = await guard.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return WorkflowMutationResult<MyWorkItemResponse>.NotFound();
            }

            if (reader.IsDBNull(0) || reader.GetGuid(0) != userId)
            {
                return WorkflowMutationResult<MyWorkItemResponse>.Forbidden();
            }

            if (string.Equals(reader.GetString(1), "Pending", StringComparison.Ordinal))
            {
                return WorkflowMutationResult<MyWorkItemResponse>.Conflict("Pending 상세에서 상태를 변경해 주세요.");
            }

            var stageCode = reader.GetString(2);
            if (string.Equals(reader.GetString(1), "Project", StringComparison.Ordinal)
                && stageCode == WorkflowStageCodes.SalesSettlementCompleted)
            {
                return WorkflowMutationResult<MyWorkItemResponse>.Conflict($"프로젝트 정산 화면에서 작업을 진행해 주세요. /projects/{reader.GetGuid(3)}/settlement");
            }

            if (string.Equals(reader.GetString(1), "Panel", StringComparison.Ordinal)
                && stageCode is WorkflowStageCodes.ManufacturingWork
                    or WorkflowStageCodes.LQC
                    or WorkflowStageCodes.ManufacturingCompleted
                    or WorkflowStageCodes.OQC
                    or WorkflowStageCodes.CustomerInspection
                    or WorkflowStageCodes.FAT
                    or WorkflowStageCodes.PackingCompleted
                    or WorkflowStageCodes.DepartureProcessed
                    or WorkflowStageCodes.DeliveryCompleted)
            {
                var destination = stageCode is WorkflowStageCodes.ManufacturingWork or WorkflowStageCodes.ManufacturingCompleted
                    ? "/manufacturing/work"
                    : stageCode is WorkflowStageCodes.PackingCompleted or WorkflowStageCodes.DepartureProcessed or WorkflowStageCodes.DeliveryCompleted
                        ? "/logistics"
                        : "/quality/inspections";
                return WorkflowMutationResult<MyWorkItemResponse>.Conflict($"전용 화면에서 작업을 진행해 주세요. {destination}");
            }

            fallbackGroupKey = reader.IsDBNull(4) ? null : reader.GetString(4);
        }

        if (action == "complete" && !string.IsNullOrWhiteSpace(fallbackGroupKey))
        {
            await using var fallbackLock = connection.CreateCommand();
            fallbackLock.Transaction = transaction;
            fallbackLock.CommandText = "select pg_advisory_xact_lock(hashtextextended(@fallback_group_key, 0));";
            fallbackLock.Parameters.AddWithValue("fallback_group_key", fallbackGroupKey);
            await fallbackLock.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = action switch
            {
                "start" => """
                    update work_items
                    set status = case when status = 'Requested' then 'InProgress' else status end,
                        started_at_utc = coalesce(started_at_utc, now())
                    where id = @id
                      and assigned_user_id = @user_id
                      and status in ('Requested', 'InProgress')
                    """,
                "complete" => """
                    with selected as (
                        select id, fallback_group_key
                        from work_items
                        where id = @id
                          and assigned_user_id = @user_id
                          and status in ('Requested', 'InProgress')
                    )
                    update work_items target
                    set status = 'Completed',
                        started_at_utc = coalesce(target.started_at_utc, now()),
                        completed_at_utc = coalesce(target.completed_at_utc, now()),
                        fallback_completed_by_user_id = case
                            when selected.fallback_group_key is not null then @user_id
                            else target.fallback_completed_by_user_id
                        end,
                        fallback_auto_closed_at_utc = case
                            when selected.fallback_group_key is not null and target.id <> selected.id
                                then coalesce(target.fallback_auto_closed_at_utc, now())
                            else target.fallback_auto_closed_at_utc
                        end
                    from selected
                    where target.status in ('Requested', 'InProgress')
                      and (
                          target.id = selected.id
                          or (
                              selected.fallback_group_key is not null
                              and target.fallback_group_key = selected.fallback_group_key
                          )
                      )
                    """,
                "cancel" => """
                    update work_items
                    set status = 'Cancelled',
                        cancelled_at_utc = coalesce(cancelled_at_utc, now())
                    where id = @id
                      and assigned_user_id = @user_id
                      and status in ('Requested', 'InProgress', 'Cancelled')
                    """,
                _ => throw new InvalidOperationException("Unsupported work item transition.")
            };
            command.Parameters.AddWithValue("id", workItemId);
            command.Parameters.AddWithValue("user_id", userId);
            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            if (affected == 0)
            {
                return WorkflowMutationResult<MyWorkItemResponse>.Conflict("다른 사용자가 먼저 업무 상태를 변경했습니다. 다시 확인해 주세요.");
            }
        }

        await transaction.CommitAsync(cancellationToken);
        var item = await ReadAssignedWorkItemAsync(connection, workItemId, userId, cancellationToken);
        return item is null
            ? WorkflowMutationResult<MyWorkItemResponse>.NotFound()
            : WorkflowMutationResult<MyWorkItemResponse>.Success(item);
    }

    private async Task<MyWorkItemResponse?> ReadAssignedWorkItemAsync(Guid workItemId, Guid userId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadAssignedWorkItemAsync(connection, workItemId, userId, cancellationToken);
    }

    private static async Task<MyWorkItemResponse?> ReadAssignedWorkItemAsync(
        NpgsqlConnection connection,
        Guid workItemId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                wi.id,
                wi.project_id,
                p.project_title,
                p.project_code,
                p.item,
                p.delivery_date,
                wi.workflow_stage_code,
                ws.stage_name,
                wi.responsibility_type,
                wi.title,
                wi.description,
                wi.status,
                wi.priority,
                wi.due_date,
                wi.created_at_utc,
                wi.started_at_utc,
                wi.completed_at_utc,
                wi.target_type,
                wi.target_id,
                wi.link_url,
                wi.fallback_group_key is not null,
                wi.fallback_auto_closed_at_utc is not null
            from work_items wi
            join projects p on p.id = wi.project_id
            join workflow_stages ws on ws.stage_code = wi.workflow_stage_code
            where wi.id = @id
              and wi.assigned_user_id = @user_id;
            """;
        command.Parameters.AddWithValue("id", workItemId);
        command.Parameters.AddWithValue("user_id", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadWorkItem(reader) : null;
    }

    private static async Task<bool> WorkItemExistsAsync(NpgsqlConnection connection, Guid workItemId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select exists (select 1 from work_items where id = @id);";
        command.Parameters.AddWithValue("id", workItemId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is bool exists && exists;
    }

    private static async Task<WorkflowCompletionFacts> ReadWorkflowFactsAsync(
        NpgsqlConnection connection,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            with panel_summary as (
                select
                    count(*)::int as active_panel_count,
                    count(*) filter (where panel_info_completed)::int as completed_panel_count,
                    count(*) filter (
                        where panel_info_completed
                           or nullif(btrim(coalesce(panel_name, '')), '') is not null
                           or width_mm is not null
                           or height_mm is not null
                           or depth_mm is not null
                    )::int as touched_panel_count
                from panel_placeholders
                where project_id = @project_id
                  and status = 'Active'
            ),
            production_plan_summary as (
                select
                    case when project.structure_mode='Ul891Set' and pp.model_version='LINKED_V1'
                         then coalesce(set_counts.item_count,0) else coalesce(base_counts.item_count,0) end as item_count,
                    case when project.structure_mode='Ul891Set' and pp.model_version='LINKED_V1'
                         then coalesce(set_counts.required_item_count,0) else coalesce(base_counts.required_item_count,0) end as required_item_count,
                    case when project.structure_mode='Ul891Set' and pp.model_version='LINKED_V1'
                         then coalesce(set_counts.planned_required_item_count,0) else coalesce(base_counts.planned_required_item_count,0) end as planned_required_item_count
                from projects project
                left join project_production_plans pp on pp.project_id=project.id
                left join lateral (
                    select count(pi.id)::int as item_count,
                           count(pi.id) filter (where pi.is_required)::int as required_item_count,
                           count(pi.id) filter (
                               where pi.is_required and (
                                 (pp.model_version='LEGACY' and pi.planned_date is not null)
                                 or (pp.model_version='LINKED_V1' and pi.planned_start_date is not null and pi.planned_end_date is not null)
                               )
                           )::int as planned_required_item_count
                    from project_production_plan_items pi
                    where pi.production_plan_id=pp.id and pi.is_active
                ) base_counts on true
                left join lateral (
                    select count(pi.id)::int as item_count,
                           count(pi.id) filter (where pi.is_required)::int as required_item_count,
                           count(pi.id) filter (
                               where pi.is_required
                                 and value.planned_start_date is not null
                                 and value.planned_end_date is not null
                           )::int as planned_required_item_count
                    from project_production_plan_set_scopes scope
                    join ul891_set_instances instance on instance.id=scope.set_instance_id and instance.status='Active'
                    join project_production_plan_items pi on pi.production_plan_id=scope.production_plan_id and pi.is_active
                    left join project_production_plan_set_item_values value
                      on value.set_scope_id=scope.id and value.production_plan_item_id=pi.id
                    where scope.production_plan_id=pp.id
                ) set_counts on true
                where project.id = @project_id
            ),
            assignee_summary as (
                select (
                    count(distinct responsibility_type) filter (
                        where responsibility_type in (
                        'SalesPrimary',
                        'DesignPrimary',
                        'ProductionPlanningPrimary',
                        'ProcurementPrimary',
                        'MaterialsPrimary',
                        'ManufacturingPrimary',
                        'LogisticsPrimary',
                        'QualityIQC',
                        'QualityOQC',
                        'QualityCustomerInspection'
                        )
                          and assigned_user_id is not null
                    )
                    + case when exists (
                        select 1 from projects project
                        where project.id = @project_id
                          and project.lqc_operational_snapshot
                    ) then count(distinct responsibility_type) filter (
                        where responsibility_type = 'QualityLQC'
                          and assigned_user_id is not null
                    ) else 1 end
                )::int as assigned_count
                from project_assignees
                where project_id = @project_id
            ),
            procurement_summary as (
                select
                    count(*)::int as item_count,
                    count(*) filter (where nullif(btrim(coalesce(order_item, '')), '') is not null)::int as named_item_count,
                    count(*) filter (
                        where nullif(btrim(coalesce(order_item, '')), '') is not null
                          and expected_receipt_date is not null
                          and (
                              (supply_type = 'Purchased'
                               and nullif(btrim(coalesce(supplier_name, '')), '') is not null
                               and order_date is not null)
                              or
                              (supply_type = 'CustomerSupplied'
                               and order_quantity > 0
                               and nullif(btrim(coalesce(order_unit, '')), '') is not null)
                          )
                    )::int as complete_item_count
                from project_procurement_items
                where project_id = @project_id
                  and status = 'Active'
            ),
            procurement_required_summary as (
                select
                    count(rows.id) filter (where rows.is_required and rows.is_active)::int as required_item_count,
                    count(rows.id) filter (
                        where rows.is_required
                          and rows.is_active
                          and exists (
                              select 1
                              from project_procurement_items items
                              where items.project_id = project_context.project_id
                                and items.status = 'Active'
                                and items.is_confirmed = true
                                and upper(regexp_replace(coalesce(items.order_item, ''), '\s+', '', 'g')) = rows.normalized_item_name
                          )
                    )::int as matched_required_item_count
                from (
                    select p.id as project_id, upper(btrim(p.item)) as item_code
                    from projects p
                    where p.id = @project_id
                ) project_context
                join procurement_required_item_templates templates
                  on upper(btrim(templates.item_code)) = project_context.item_code
                 and templates.is_active = true
                join procurement_required_item_template_rows rows on rows.template_id = templates.id
            ),
            iqc_summary as (
                select
                    count(receipt.id) filter (where receipt.status <> 'Cancelled')::int as receipt_count,
                    count(receipt.id) filter (where receipt.status in ('Passed', 'Confirmed'))::int as passed_count,
                    count(receipt.id) filter (where receipt.status in ('IqcRequested', 'Passed', 'FailedBlocked', 'Confirmed'))::int as started_count,
                    count(receipt.id) filter (where receipt.status = 'FailedBlocked')::int as failed_count
                from project_procurement_items item
                join material_receipts receipt on receipt.procurement_item_id = item.id
                where item.project_id = @project_id and item.status = 'Active'
            ),
            material_summary as (
                select
                    count(*)::int as active_item_count,
                    count(*) filter (
                        where exists (
                            select 1 from material_receipts receipt
                            where receipt.procurement_item_id = item.id
                              and receipt.status <> 'Cancelled'
                        )
                    )::int as arrived_item_count,
                    count(*) filter (
                        where item.material_arrivals_closed_at_utc is not null
                    )::int as arrival_closed_item_count,
                    count(*) filter (
                        where item.receipt_completed = true
                           or (
                               item.order_quantity is not null
                               and coalesce((
                                   select sum(receipt.quantity)
                                   from material_receipts receipt
                                   where receipt.procurement_item_id = item.id
                                     and receipt.status = 'Confirmed'
                                     and receipt.quantity is not null
                               ), 0) >= item.order_quantity
                           )
                    )::int as receipt_confirmed_item_count
                from project_procurement_items item
                where item.project_id = @project_id
                  and item.status = 'Active'
            ),
            confirmed_receipt_summary as (
                select count(receipt.id)::int as confirmed_receipt_count
                from project_procurement_items item
                join material_receipts receipt
                  on receipt.procurement_item_id = item.id
                 and receipt.status = 'Confirmed'
                where item.project_id = @project_id
                  and item.status = 'Active'
            ),
            kitting_summary as (
                select count(*) filter (
                    where exists (
                        select 1 from panel_manufacturing_release_operations release
                        where release.project_id = @project_id
                          and panel.id = any(release.panel_ids)
                    )
                )::int as ready_panel_count
                from panel_placeholders panel
                where panel.project_id = @project_id
                  and panel.status = 'Active'
            ),
            manufacturing_summary as (
                select
                    count(distinct execution.panel_id) filter (
                        where execution.status in ('InProgress', 'Blocked', 'Completed')
                    )::int as started_panel_count,
                    count(distinct execution.panel_id) filter (
                        where execution.status = 'Completed'
                    )::int as completed_panel_count,
                    count(distinct execution.panel_id) filter (
                        where execution.status = 'Blocked'
                    )::int as blocked_panel_count
                from panel_manufacturing_executions execution
                join panel_placeholders panel
                  on panel.id = execution.panel_id
                 and panel.status = 'Active'
                where execution.project_id = @project_id
            ),
            latest_quality_attempts as (
                select distinct on (attempt.panel_id, attempt.stage_code)
                    attempt.panel_id,
                    attempt.stage_code,
                    attempt.status
                from panel_quality_inspection_attempts attempt
                join panel_placeholders panel
                  on panel.id = attempt.panel_id
                 and panel.status = 'Active'
                where attempt.project_id = @project_id
                  and attempt.status <> 'Cancelled'
                order by attempt.panel_id, attempt.stage_code, attempt.attempt_number desc, attempt.created_at_utc desc
            ),
            quality_summary as (
                select
                    count(*) filter (where stage_code = 'LQC')::int as lqc_started_count,
                    count(*) filter (where stage_code = 'LQC' and status = 'Passed')::int as lqc_passed_count,
                    count(*) filter (where stage_code = 'LQC' and status = 'Failed')::int as lqc_failed_count,
                    count(*) filter (where stage_code = 'OQC')::int as oqc_started_count,
                    count(*) filter (where stage_code = 'OQC' and status = 'Passed')::int as oqc_passed_count,
                    count(*) filter (where stage_code = 'OQC' and status = 'Failed')::int as oqc_failed_count,
                    count(*) filter (where stage_code = 'CustomerInspection')::int as customer_started_count,
                    count(*) filter (where stage_code = 'CustomerInspection' and status = 'Passed')::int as customer_passed_count,
                    count(*) filter (where stage_code = 'CustomerInspection' and status = 'Failed')::int as customer_failed_count,
                    count(*) filter (where stage_code = 'FAT')::int as fat_started_count,
                    count(*) filter (where stage_code = 'FAT' and status = 'Passed')::int as fat_passed_count,
                    count(*) filter (where stage_code = 'FAT' and status = 'Failed')::int as fat_failed_count
                from latest_quality_attempts
            ),
            manufacturing_confirmation_summary as (
                select count(distinct confirmation.panel_id)::int as ready_panel_count
                from panel_manufacturing_completion_confirmations confirmation
                join panel_placeholders panel
                  on panel.id = confirmation.panel_id
                 and panel.status = 'Active'
                where confirmation.project_id = @project_id
            ),
            logistics_summary as (
                select
                    count(*) filter (
                        where exists (
                            select 1
                            from logistics_packing_unit_panels membership
                            join logistics_packing_units unit on unit.id = membership.packing_unit_id
                            where membership.panel_id = panel.id
                              and membership.active
                              and unit.status <> 'Cancelled'
                        )
                    )::int as packing_started_count,
                    count(*) filter (
                        where exists (
                            select 1
                            from logistics_packing_unit_panels membership
                            join logistics_packing_units unit on unit.id = membership.packing_unit_id
                            where membership.panel_id = panel.id
                              and membership.active
                              and unit.status = 'Finalized'
                        )
                    )::int as packing_completed_count,
                    count(*) filter (
                        where exists (
                            select 1
                            from logistics_batch_panels membership
                            join logistics_batches batch
                              on batch.id = membership.batch_id
                             and batch.stage_code = 'DepartureProcessed'
                             and batch.status <> 'Cancelled'
                            where membership.panel_id = panel.id
                              and membership.active
                        )
                    )::int as departure_started_count,
                    count(*) filter (
                        where exists (
                            select 1
                            from logistics_batch_panels membership
                            join logistics_batches batch
                              on batch.id = membership.batch_id
                             and batch.stage_code = 'DepartureProcessed'
                             and batch.status = 'Finalized'
                            where membership.panel_id = panel.id
                              and membership.active
                        )
                    )::int as departure_completed_count,
                    count(*) filter (
                        where exists (
                            select 1
                            from logistics_batch_panels membership
                            join logistics_batches batch
                              on batch.id = membership.batch_id
                             and batch.stage_code = 'DeliveryCompleted'
                             and batch.status <> 'Cancelled'
                            where membership.panel_id = panel.id
                              and membership.active
                        )
                    )::int as delivery_started_count,
                    count(*) filter (
                        where exists (
                            select 1
                            from logistics_delivery_results result
                            where result.panel_id = panel.id
                        )
                    )::int as delivery_completed_count
                from panel_placeholders panel
                where panel.project_id = @project_id
                  and panel.status = 'Active'
            )
            select
                coalesce(ps.active_panel_count, 0),
                coalesce(ps.completed_panel_count, 0),
                coalesce(ps.touched_panel_count, 0),
                coalesce(pps.item_count, 0),
                coalesce(pps.required_item_count, 0),
                coalesce(pps.planned_required_item_count, 0),
                coalesce(a.assigned_count, 0),
                coalesce(pr.item_count, 0),
                coalesce(pr.named_item_count, 0),
                coalesce(pr.complete_item_count, 0),
                coalesce(prs.required_item_count, 0),
                coalesce(prs.matched_required_item_count, 0),
                coalesce(iqc.receipt_count, 0),
                coalesce(iqc.passed_count, 0),
                coalesce(iqc.started_count, 0),
                coalesce(iqc.failed_count, 0),
                coalesce(material.active_item_count, 0),
                coalesce(material.arrived_item_count, 0),
                coalesce(material.arrival_closed_item_count, 0),
                coalesce(material.receipt_confirmed_item_count, 0),
                coalesce(confirmed_receipt.confirmed_receipt_count, 0),
                coalesce(kitting.ready_panel_count, 0),
                coalesce(manufacturing.started_panel_count, 0),
                coalesce(manufacturing.completed_panel_count, 0),
                coalesce(manufacturing.blocked_panel_count, 0),
                coalesce(quality.lqc_started_count, 0),
                coalesce(quality.lqc_passed_count, 0),
                coalesce(quality.lqc_failed_count, 0),
                coalesce(confirmation.ready_panel_count, 0),
                coalesce(quality.oqc_started_count, 0),
                coalesce(quality.oqc_passed_count, 0),
                coalesce(quality.oqc_failed_count, 0),
                coalesce(quality.customer_started_count, 0),
                coalesce(quality.customer_passed_count, 0),
                coalesce(quality.customer_failed_count, 0),
                coalesce(quality.fat_started_count, 0),
                coalesce(quality.fat_passed_count, 0),
                coalesce(quality.fat_failed_count, 0),
                coalesce(logistics.packing_started_count, 0),
                coalesce(logistics.packing_completed_count, 0),
                coalesce(logistics.departure_started_count, 0),
                coalesce(logistics.departure_completed_count, 0),
                coalesce(logistics.delivery_started_count, 0),
                coalesce(logistics.delivery_completed_count, 0)
            from (select 1) anchor
            left join panel_summary ps on true
            left join production_plan_summary pps on true
            left join assignee_summary a on true
            left join procurement_summary pr on true
            left join procurement_required_summary prs on true
            left join iqc_summary iqc on true
            left join material_summary material on true
            left join confirmed_receipt_summary confirmed_receipt on true
            left join kitting_summary kitting on true
            left join manufacturing_summary manufacturing on true
            left join quality_summary quality on true
            left join manufacturing_confirmation_summary confirmation on true
            left join logistics_summary logistics on true;
            """;
        command.Parameters.AddWithValue("project_id", projectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new WorkflowCompletionFacts();
        }

        return new WorkflowCompletionFacts(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5),
            reader.GetInt32(6),
            reader.GetInt32(7),
            reader.GetInt32(8),
            reader.GetInt32(9),
            reader.GetInt32(10),
            reader.GetInt32(11),
            reader.GetInt32(12),
            reader.GetInt32(13),
            reader.GetInt32(14),
            reader.GetInt32(15),
            reader.GetInt32(16),
            reader.GetInt32(17),
            reader.GetInt32(18),
            reader.GetInt32(19),
            reader.GetInt32(20),
            reader.GetInt32(21),
            reader.GetInt32(22),
            reader.GetInt32(23),
            reader.GetInt32(24),
            reader.GetInt32(25),
            reader.GetInt32(26),
            reader.GetInt32(27),
            reader.GetInt32(28),
            reader.GetInt32(29),
            reader.GetInt32(30),
            reader.GetInt32(31),
            reader.GetInt32(32),
            reader.GetInt32(33),
            reader.GetInt32(34),
            reader.GetInt32(35),
            reader.GetInt32(36),
            reader.GetInt32(37),
            reader.GetInt32(38),
            reader.GetInt32(39),
            reader.GetInt32(40),
            reader.GetInt32(41),
            reader.GetInt32(42),
            reader.GetInt32(43));
    }

    private static async Task<ProjectWorkflowSnapshot?> ReadProjectAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select p.id, p.project_title, p.project_code, p.sales_owner_user_id, u.is_active,
                   p.lqc_operational_snapshot
            from projects p
            left join qms_users u on u.id = p.sales_owner_user_id
            where p.id = @project_id
              and p.deleted_at_utc is null;
            """;
        command.Parameters.AddWithValue("project_id", projectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ProjectWorkflowSnapshot(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetGuid(3),
            !reader.IsDBNull(4) && reader.GetBoolean(4),
            reader.GetBoolean(5));
    }

    private static async Task<StageSnapshot?> ReadStageAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string stageCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select stage_code, sequence_number, department_code, stage_name
            from workflow_stages
            where stage_code = @stage_code
              and is_active = true;
            """;
        command.Parameters.AddWithValue("stage_code", stageCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new StageSnapshot(reader.GetString(0), reader.GetInt32(1), reader.GetString(2), StageDisplayName(reader.GetString(0), reader.GetString(3)))
            : null;
    }

    private static async Task<Guid> InsertWorkflowEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        string stageCode,
        string eventType,
        string sourceType,
        Guid? sourceId,
        Guid createdByUserId,
        string? correlationId,
        string? note,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into project_workflow_events (
                project_id, stage_code, event_type, event_status, source_type, source_id,
                correlation_id, created_by_user_id, note
            )
            values (
                @project_id, @stage_code, @event_type, 'Succeeded', @source_type, @source_id,
                @correlation_id, @created_by_user_id, @note
            )
            returning id;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("stage_code", stageCode);
        command.Parameters.AddWithValue("event_type", eventType);
        command.Parameters.AddWithValue("source_type", sourceType);
        command.Parameters.AddWithValue("source_id", (object?)sourceId ?? DBNull.Value);
        command.Parameters.AddWithValue("correlation_id", (object?)correlationId ?? DBNull.Value);
        command.Parameters.AddWithValue("created_by_user_id", createdByUserId);
        command.Parameters.AddWithValue("note", (object?)note ?? DBNull.Value);
        return (Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty);
    }

    private static async Task CreateWorkItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        string stageCode,
        string responsibilityType,
        Guid assignedUserId,
        string? assignedRoleCode,
        string title,
        string description,
        Guid eventId,
        Guid createdByUserId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            with inserted as (
                insert into work_items (
                    project_id, target_type, target_id, workflow_stage_code, responsibility_type,
                    assigned_user_id, assigned_role_code, title, description, status, priority,
                    generated_by_event_id, idempotency_key, created_by_user_id
                )
                select
                    @project_id, 'Project', @project_id, @stage_code, @responsibility_type,
                    @assigned_user_id, @assigned_role_code, @title, @description, 'Requested', 'Normal',
                    @event_id, @idempotency_key, @created_by_user_id
                where not exists (
                    select 1
                    from work_items
                    where project_id = @project_id
                      and workflow_stage_code = @stage_code
                      and responsibility_type = @responsibility_type
                      and assigned_user_id = @assigned_user_id
                      and status <> 'Cancelled'
                )
                on conflict (idempotency_key) do update set title = excluded.title
                returning id
            )
            select id from inserted
            union all
            select id
            from work_items
            where project_id = @project_id
              and workflow_stage_code = @stage_code
              and responsibility_type = @responsibility_type
              and assigned_user_id = @assigned_user_id
              and status <> 'Cancelled'
            order by id
            limit 1;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("stage_code", stageCode);
        command.Parameters.AddWithValue("responsibility_type", responsibilityType);
        command.Parameters.AddWithValue("assigned_user_id", assignedUserId);
        command.Parameters.AddWithValue("assigned_role_code", (object?)assignedRoleCode ?? DBNull.Value);
        command.Parameters.AddWithValue("title", title);
        command.Parameters.AddWithValue("description", description);
        command.Parameters.AddWithValue("event_id", eventId);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
        command.Parameters.AddWithValue("created_by_user_id", createdByUserId);
        var workItemId = (Guid?)(await command.ExecuteScalarAsync(cancellationToken));
        if (workItemId is not null)
        {
            await WorkAssignmentNotificationWriter.UpsertAsync(
                connection,
                transaction,
                projectId,
                workItemId.Value,
                assignedUserId,
                [],
                title,
                description,
                LinkUrlForStage(projectId, stageCode),
                $"{idempotencyKey}:notification",
                cancellationToken);
            if (string.Equals(stageCode, WorkflowStageCodes.MaterialArrived, StringComparison.Ordinal))
            {
                await WorkItemDueDateSynchronizer.SyncProcurementProjectAsync(
                    connection, transaction, projectId, cancellationToken);
            }
        }
    }

    private static async Task<ResolvedAssignee> ResolveAssigneeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ProjectWorkflowSnapshot project,
        ResponsibilityTarget target,
        CancellationToken cancellationToken)
    {
        var primary = await ReadAssigneeAsync(connection, transaction, project.ProjectId, [target.Primary, .. target.LegacyPrimaryAliases], cancellationToken);
        if (primary is not null)
        {
            return primary with { SourceLabel = ResponsibilityLabel(target.Primary) };
        }

        if (!string.IsNullOrWhiteSpace(target.Secondary))
        {
            var secondary = await ReadAssigneeAsync(connection, transaction, project.ProjectId, [target.Secondary], cancellationToken);
            if (secondary is not null)
            {
                return secondary with { SourceLabel = ResponsibilityLabel(target.Secondary!) };
            }
        }

        var departmentCode = DepartmentCodeForResponsibility(target.Primary);
        var departmentHead = await ReadFirstActiveDepartmentHeadAsync(
            connection,
            transaction,
            departmentCode,
            cancellationToken);
        return departmentHead ?? throw new DepartmentHeadRequiredException(departmentCode);
    }

    private static async Task<ResolvedAssignee?> ReadAssigneeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        IReadOnlyList<string> responsibilityTypes,
        CancellationToken cancellationToken)
    {
        if (responsibilityTypes.Count == 0)
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select pa.assigned_user_id, r.code
            from project_assignees pa
            join qms_users u on u.id = pa.assigned_user_id
            left join user_roles ur on ur.user_id = u.id
            left join roles r on r.id = ur.role_id
            where pa.project_id = @project_id
              and pa.responsibility_type = any(@responsibility_types)
              and pa.assigned_user_id is not null
              and u.is_active = true
            order by array_position(@responsibility_types, pa.responsibility_type), r.code nulls last
            limit 1;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("responsibility_types", responsibilityTypes.ToArray());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ResolvedAssignee(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetString(1), "담당자")
            : null;
    }

    private static async Task<ResolvedAssignee?> ReadFirstActiveDepartmentHeadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string departmentCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select u.id, role.code
            from qms_users u
            join departments department on department.id = u.department_id
            left join lateral (
                select roles.code
                from user_roles user_role
                join roles on roles.id = user_role.role_id
                where user_role.user_id = u.id
                order by roles.code
                limit 1
            ) role on true
            where u.is_active = true
              and u.is_department_head = true
              and department.is_active = true
              and department.code = @department_code
            order by u.display_name, u.id
            limit 1;
            """;
        command.Parameters.AddWithValue("department_code", departmentCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ResolvedAssignee(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetString(1), "부서장 fallback")
            : null;
    }

    private static string DepartmentCodeForResponsibility(string responsibilityType)
    {
        if (responsibilityType.StartsWith("Sales", StringComparison.Ordinal)) return "sales";
        if (responsibilityType.StartsWith("Design", StringComparison.Ordinal)) return "design";
        if (responsibilityType.StartsWith("ProductionPlanning", StringComparison.Ordinal)) return "production-planning";
        if (responsibilityType.StartsWith("Procurement", StringComparison.Ordinal)) return "procurement";
        if (responsibilityType.StartsWith("Materials", StringComparison.Ordinal)) return "materials";
        if (responsibilityType.StartsWith("Manufacturing", StringComparison.Ordinal)) return "manufacturing";
        if (responsibilityType.StartsWith("Quality", StringComparison.Ordinal)) return "quality";
        if (responsibilityType.StartsWith("Logistics", StringComparison.Ordinal)) return "logistics";
        throw new InvalidOperationException($"업무 책임 유형 '{responsibilityType}'의 담당 부서를 확인할 수 없습니다.");
    }

    private static async Task CreateSecondaryReferenceNotificationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ProjectWorkflowSnapshot project,
        StageSnapshot stage,
        ResponsibilityTarget target,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(target.Secondary))
        {
            return;
        }

        var secondary = await ReadAssigneeAsync(connection, transaction, project.ProjectId, [target.Secondary], cancellationToken);
        if (secondary?.UserId is null)
        {
            return;
        }

        await CreateNotificationAsync(
            connection,
            transaction,
            project.ProjectId,
            "Reference",
            "Info",
            $"{stage.StageName} 업무가 생성되었습니다.",
            $"{project.ProjectTitle}의 {stage.StageName} 업무가 정담당자에게 생성되었습니다.",
            LinkUrlForStage(project.ProjectId, stage.StageCode),
            eventId,
            $"project:{project.ProjectId}:stage:{stage.StageCode}:reference:{target.Secondary}",
            [secondary.UserId.Value],
            cancellationToken);
    }

    private static async Task CreateAllDepartmentsReferenceNotificationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ProjectWorkflowSnapshot project,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var recipients = await ReadAllActiveUserIdsAsync(connection, transaction, cancellationToken);

        await CreateNotificationAsync(
            connection,
            transaction,
            project.ProjectId,
            "Reference",
            "Info",
            "프로젝트가 생성되었습니다.",
            $"{project.ProjectTitle} 프로젝트가 생성되었습니다.",
            LinkUrlForStage(project.ProjectId, WorkflowStageCodes.SalesProjectCreated),
            eventId,
            $"project:{project.ProjectId}:stage:{WorkflowStageCodes.SalesProjectCreated}:reference:all-departments",
            recipients,
            cancellationToken,
            NotificationSourceKinds.ProjectCreated);
    }

    private static async Task<IReadOnlyList<Guid>> ReadAllActiveUserIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select id from qms_users where is_active = true order by id;";

        var users = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(reader.GetGuid(0));
        }

        return users;
    }

    private static async Task CreateDepartmentReferenceNotificationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ProjectWorkflowSnapshot project,
        Guid eventId,
        string title,
        string message,
        IReadOnlyList<string> responsibilityTypes,
        IReadOnlyList<string> roleCodes,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var recipients = new HashSet<Guid>();
        foreach (var responsibilityType in responsibilityTypes)
        {
            var assignee = await ReadAssigneeAsync(connection, transaction, project.ProjectId, [responsibilityType], cancellationToken);
            if (assignee?.UserId is not null)
            {
                recipients.Add(assignee.UserId.Value);
            }
        }

        foreach (var roleUserId in await ReadActiveUsersForRolesAsync(connection, transaction, roleCodes, cancellationToken))
        {
            recipients.Add(roleUserId);
        }

        await CreateNotificationAsync(
            connection,
            transaction,
            project.ProjectId,
            "Reference",
            "Info",
            title,
            message,
            LinkUrlForStage(project.ProjectId, WorkflowStageCodes.ProcurementInfo),
            eventId,
            idempotencyKey,
            recipients.ToList(),
            cancellationToken);
    }

    private static async Task<IReadOnlyList<Guid>> ReadActiveUsersForRolesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<string> roleCodes,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select distinct u.id
            from qms_users u
            join user_roles ur on ur.user_id = u.id
            join roles r on r.id = ur.role_id
            where u.is_active = true
              and r.code = any(@role_codes)
              and not exists (
                  select 1
                  from user_roles excluded_user_role
                  join roles excluded_role on excluded_role.id=excluded_user_role.role_id
                  where excluded_user_role.user_id=u.id
                    and excluded_role.code in ('system-administrator', 'read-only')
              );
            """;
        command.Parameters.AddWithValue("role_codes", roleCodes.ToArray());

        var users = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(reader.GetGuid(0));
        }

        return users;
    }

    private static async Task<IReadOnlyList<ProjectAssigneeSnapshot>> ReadProjectAssigneesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select distinct pa.responsibility_type, pa.assigned_user_id
            from project_assignees pa
            join qms_users u on u.id = pa.assigned_user_id
            where pa.project_id = @project_id
              and pa.assigned_user_id is not null
              and u.is_active = true;
            """;
        command.Parameters.AddWithValue("project_id", projectId);

        var rows = new List<ProjectAssigneeSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ProjectAssigneeSnapshot(reader.GetString(0), reader.GetGuid(1)));
        }

        return rows;
    }

    private static async Task CreateNotificationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        string notificationType,
        string severity,
        string title,
        string message,
        string linkUrl,
        Guid eventId,
        string idempotencyKey,
        IReadOnlyList<Guid> recipientIds,
        CancellationToken cancellationToken,
        string sourceKind = NotificationSourceKinds.Automatic)
    {
        if (recipientIds.Count == 0)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into notifications (
                project_id, notification_type, severity, title, message, link_url,
                generated_by_event_id, idempotency_key, source_kind
            )
            values (
                @project_id, @notification_type, @severity, @title, @message, @link_url,
                @event_id, @idempotency_key, @source_kind
            )
            on conflict (idempotency_key) do update
            set title = excluded.title,
                source_kind = excluded.source_kind
            returning id;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("notification_type", notificationType);
        command.Parameters.AddWithValue("severity", severity);
        command.Parameters.AddWithValue("title", title);
        command.Parameters.AddWithValue("message", message);
        command.Parameters.AddWithValue("link_url", linkUrl);
        command.Parameters.AddWithValue("event_id", eventId);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
        command.Parameters.AddWithValue("source_kind", sourceKind);
        var notificationId = (Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty);

        foreach (var recipientId in recipientIds.Distinct())
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
    }

    private static async Task<NotificationResponse?> ReadNotificationAsync(
        NpgsqlConnection connection,
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                n.id,
                n.project_id,
                p.project_title,
                p.project_code,
                p.item,
                n.work_item_id,
                wi.title,
                wi.workflow_stage_code,
                ws.stage_name,
                n.notification_type,
                n.severity,
                n.visibility_scope,
                n.source_kind,
                n.title,
                n.message,
                n.link_url,
                n.created_at_utc,
                nr.read_at_utc
            from notifications n
            left join notification_recipients nr on nr.notification_id = n.id
                and nr.user_id = @user_id
            left join projects p on p.id = n.project_id
            left join work_items wi on wi.id = n.work_item_id
            left join workflow_stages ws on ws.stage_code = wi.workflow_stage_code
            where n.id = @notification_id
              and (nr.id is not null or n.visibility_scope = 'Authenticated');
            """;
        command.Parameters.AddWithValue("notification_id", notificationId);
        command.Parameters.AddWithValue("user_id", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadNotification(reader) : null;
    }

    private static MyWorkItemResponse ReadWorkItem(NpgsqlDataReader reader)
    {
        var workItemId = reader.GetGuid(0);
        var projectId = reader.GetGuid(1);
        var status = reader.GetString(11);
        var priority = reader.GetString(12);
        return new MyWorkItemResponse(
            workItemId,
            projectId,
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetFieldValue<DateOnly>(5),
            reader.GetString(6),
            StageDisplayName(reader.GetString(6), reader.GetString(7)),
            reader.GetString(8),
            ResponsibilityLabel(reader.GetString(8)),
            reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            status,
            WorkItemStatusLabel(status),
            priority,
            PriorityLabel(priority),
            reader.IsDBNull(13) ? null : reader.GetFieldValue<DateOnly>(13),
            reader.GetFieldValue<DateTimeOffset>(14),
            reader.IsDBNull(15) ? null : reader.GetFieldValue<DateTimeOffset>(15),
            reader.IsDBNull(16) ? null : reader.GetFieldValue<DateTimeOffset>(16),
            reader.IsDBNull(19)
                ? LinkUrlForWorkItem(
                    projectId,
                    reader.GetString(6),
                    reader.GetString(17),
                    reader.IsDBNull(18) ? null : reader.GetGuid(18))
                : reader.GetString(19),
            reader.GetBoolean(20),
            reader.GetBoolean(21));
    }

    private static NotificationResponse ReadNotification(NpgsqlDataReader reader)
    {
        var type = reader.GetString(9);
        var severity = reader.GetString(10);
        var visibilityScope = reader.GetString(11);
        var sourceKind = reader.GetString(12);
        return new NotificationResponse(
            reader.GetGuid(0),
            reader.IsDBNull(1) ? null : reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetGuid(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : StageDisplayName(reader.GetString(7), reader.GetString(8)),
            type,
            NotificationTypeLabel(type),
            severity,
            SeverityLabel(severity),
            visibilityScope,
            VisibilityScopeLabel(visibilityScope),
            sourceKind,
            SourceKindLabel(sourceKind),
            reader.GetString(13),
            reader.GetString(14),
            reader.IsDBNull(15) ? null : reader.GetString(15),
            reader.GetFieldValue<DateTimeOffset>(16),
            reader.IsDBNull(17) ? null : reader.GetFieldValue<DateTimeOffset>(17));
    }

    private static string BuildWorkDescription(StageSnapshot stage, ResolvedAssignee assignee)
    {
        return assignee.SourceLabel.Contains("fallback", StringComparison.Ordinal)
            ? $"{stage.StageName} 단계 처리가 필요합니다. 원 담당자가 없어 {assignee.SourceLabel} 기준으로 배정되었습니다."
            : $"{stage.StageName} 단계 처리가 필요합니다.";
    }

    private static string WorkItemTitleForStage(string stageCode)
    {
        return stageCode switch
        {
            WorkflowStageCodes.ProductionPlanning => "생산계획, 담당자 입력",
            WorkflowStageCodes.DesignPanelInfo => "패널명, 사이즈 입력",
            WorkflowStageCodes.ProcurementInfo => "구매정보 입력",
            WorkflowStageCodes.MaterialArrived => "자재 도착 등록",
            WorkflowStageCodes.IQC => "수입검사 입력",
            WorkflowStageCodes.ReceiptConfirmed => "입고 확정 입력",
            WorkflowStageCodes.KittingCompleted => "제조 투입 검토·요청",
            WorkflowStageCodes.ManufacturingWork => "제조 작업 입력",
            WorkflowStageCodes.LQC => "LQC 입력",
            WorkflowStageCodes.ManufacturingCompleted => "제조 완료 입력",
            WorkflowStageCodes.OQC => "자체검수 입력",
            WorkflowStageCodes.CustomerInspection => "전진검수 입력",
            WorkflowStageCodes.FAT => "FAT 입력",
            WorkflowStageCodes.PackingCompleted => "포장 완료 입력",
            WorkflowStageCodes.DepartureProcessed => "출발 처리 입력",
            WorkflowStageCodes.DeliveryCompleted => "납품 완료 입력",
            WorkflowStageCodes.SalesSettlementCompleted => "세금계산서 발행 요청 준비",
            _ => "업무 입력"
        };
    }

    private static string StageDisplayName(string stageCode, string stageName)
    {
        if (string.Equals(stageCode, WorkflowStageCodes.DesignPanelInfo, StringComparison.Ordinal))
        {
            return "패널명·사이즈";
        }

        return string.Equals(stageCode, WorkflowStageCodes.KittingCompleted, StringComparison.Ordinal)
            ? "제조 요청"
            : stageName;
    }

    private static string DetermineWorkflowStatus(DateTimeOffset? completedAt, bool hasInProgress, bool hasRequested, bool hasBlocking)
    {
        if (hasBlocking)
        {
            return "Blocked";
        }

        if (completedAt is not null)
        {
            return "Completed";
        }

        if (hasInProgress)
        {
            return "InProgress";
        }

        if (hasRequested)
        {
            return "Requested";
        }

        return "NotStarted";
    }

    private static string ApplyImplementedStageStatus(string stageCode, string currentStatus, WorkflowCompletionFacts facts)
    {
        if (string.Equals(currentStatus, "Blocked", StringComparison.Ordinal))
        {
            return currentStatus;
        }

        return stageCode switch
        {
            WorkflowStageCodes.SalesProjectCreated => "Completed",
            WorkflowStageCodes.ProductionPlanning => ProductionPlanningStatus(facts, currentStatus),
            WorkflowStageCodes.DesignPanelInfo => DesignPanelInfoStatus(facts, currentStatus),
            WorkflowStageCodes.ProcurementInfo => ProcurementStatus(facts, currentStatus),
            WorkflowStageCodes.MaterialArrived => AggregateStageStatus(
                facts.ActiveMaterialItemCount,
                facts.ArrivedMaterialItemCount,
                facts.ArrivalClosedMaterialItemCount,
                0,
                currentStatus),
            WorkflowStageCodes.IQC => IqcStatus(facts, currentStatus),
            WorkflowStageCodes.ReceiptConfirmed => AggregateStageStatus(
                facts.ActiveMaterialItemCount,
                facts.ArrivedMaterialItemCount,
                facts.ReceiptConfirmedMaterialItemCount,
                0,
                currentStatus),
            WorkflowStageCodes.KittingCompleted => ManufacturingRequestStatus(facts, currentStatus),
            WorkflowStageCodes.ManufacturingWork => AggregateStageStatus(
                facts.ActivePanelCount,
                facts.ManufacturingStartedPanelCount,
                facts.ManufacturingCompletedPanelCount,
                facts.ManufacturingBlockedPanelCount,
                currentStatus),
            WorkflowStageCodes.LQC => AggregateStageStatus(
                facts.ActivePanelCount,
                facts.LqcStartedPanelCount,
                facts.LqcPassedPanelCount,
                facts.LqcFailedPanelCount,
                currentStatus),
            WorkflowStageCodes.ManufacturingCompleted => AggregateStageStatus(
                facts.ActivePanelCount,
                facts.ManufacturingCompletedPanelCount,
                facts.ManufacturingLqcReadyPanelCount,
                facts.ManufacturingBlockedPanelCount + facts.LqcFailedPanelCount,
                currentStatus),
            WorkflowStageCodes.OQC => AggregateStageStatus(
                facts.ActivePanelCount,
                facts.OqcStartedPanelCount,
                facts.OqcPassedPanelCount,
                facts.OqcFailedPanelCount,
                currentStatus),
            WorkflowStageCodes.CustomerInspection => AggregateStageStatus(
                facts.ActivePanelCount,
                facts.CustomerInspectionStartedPanelCount,
                facts.CustomerInspectionPassedPanelCount,
                facts.CustomerInspectionFailedPanelCount,
                currentStatus),
            WorkflowStageCodes.FAT => AggregateStageStatus(
                facts.ActivePanelCount,
                facts.FatStartedPanelCount,
                facts.FatPassedPanelCount,
                facts.FatFailedPanelCount,
                currentStatus),
            WorkflowStageCodes.PackingCompleted => AggregateStageStatus(
                facts.ActivePanelCount,
                facts.PackingStartedPanelCount,
                facts.PackingCompletedPanelCount,
                0,
                currentStatus),
            WorkflowStageCodes.DepartureProcessed => AggregateStageStatus(
                facts.ActivePanelCount,
                facts.DepartureStartedPanelCount,
                facts.DepartureCompletedPanelCount,
                0,
                currentStatus),
            WorkflowStageCodes.DeliveryCompleted => AggregateStageStatus(
                facts.ActivePanelCount,
                facts.DeliveryStartedPanelCount,
                facts.DeliveryCompletedPanelCount,
                0,
                currentStatus),
            _ => currentStatus
        };
    }

    private static string ProductionPlanningStatus(WorkflowCompletionFacts facts, string currentStatus)
    {
        var requiredPlanComplete = facts.RequiredPlanItemCount == 0
            ? facts.ProductionPlanItemCount > 0
            : facts.PlannedRequiredPlanItemCount >= facts.RequiredPlanItemCount;
        var requiredAssigneesComplete = facts.RequiredPrimaryAssigneeCount >= 11;
        if (requiredPlanComplete && requiredAssigneesComplete)
        {
            return "Completed";
        }

        if (facts.ProductionPlanItemCount > 0 || facts.RequiredPrimaryAssigneeCount > 0)
        {
            return "InProgress";
        }

        return currentStatus;
    }

    private static string DesignPanelInfoStatus(WorkflowCompletionFacts facts, string currentStatus)
    {
        if (facts.ActivePanelCount > 0 && facts.CompletedPanelCount >= facts.ActivePanelCount)
        {
            return "Completed";
        }

        if (facts.TouchedPanelCount > 0)
        {
            return facts.CompletedPanelCount > 0 ? WorkflowStatuses.PartiallyCompleted : "InProgress";
        }

        return currentStatus;
    }

    private static string ProcurementStatus(WorkflowCompletionFacts facts, string currentStatus)
    {
        if (string.Equals(currentStatus, "Completed", StringComparison.Ordinal))
        {
            return currentStatus;
        }

        var allActiveItemsComplete = facts.ProcurementItemCount > 0
            && facts.CompletedProcurementItemCount >= facts.ProcurementItemCount;
        if (facts.RequiredProcurementItemCount > 0)
        {
            if (allActiveItemsComplete
                && facts.MatchedRequiredProcurementItemCount >= facts.RequiredProcurementItemCount)
            {
                return "Completed";
            }

            if (facts.ProcurementItemCount > 0)
            {
                return facts.CompletedProcurementItemCount > 0
                    ? WorkflowStatuses.PartiallyCompleted
                    : "InProgress";
            }

            return currentStatus;
        }

        if (allActiveItemsComplete)
        {
            return "Completed";
        }

        if (facts.ProcurementItemCount > 0)
        {
            return facts.CompletedProcurementItemCount > 0
                ? WorkflowStatuses.PartiallyCompleted
                : "InProgress";
        }

        return currentStatus;
    }

    private static string IqcStatus(WorkflowCompletionFacts facts, string currentStatus)
    {
        return AggregateStageStatus(
            facts.IqcReceiptCount,
            facts.IqcStartedCount,
            facts.IqcPassedCount,
            facts.IqcFailedCount,
            currentStatus);
    }

    private static string ManufacturingRequestStatus(WorkflowCompletionFacts facts, string currentStatus)
    {
        if (facts.ActivePanelCount > 0
            && facts.ManufacturingReleasedPanelCount >= facts.ActivePanelCount)
        {
            return "Completed";
        }

        if (facts.ManufacturingReleasedPanelCount > 0)
        {
            return WorkflowStatuses.PartiallyCompleted;
        }

        if (facts.ConfirmedReceiptCount > 0)
        {
            return "InProgress";
        }

        return string.Equals(currentStatus, "Completed", StringComparison.Ordinal)
            ? "NotStarted"
            : currentStatus;
    }

    private static string AggregateStageStatus(
        int totalCount,
        int startedCount,
        int completedCount,
        int failedCount,
        string currentStatus)
    {
        if (failedCount > 0 || string.Equals(currentStatus, "Blocked", StringComparison.Ordinal))
        {
            return "Blocked";
        }

        if (totalCount <= 0)
        {
            return currentStatus;
        }

        if (completedCount >= totalCount)
        {
            return "Completed";
        }

        if (completedCount > 0)
        {
            return WorkflowStatuses.PartiallyCompleted;
        }

        if (startedCount > 0)
        {
            return "InProgress";
        }

        return string.Equals(currentStatus, "Completed", StringComparison.Ordinal)
            ? "NotStarted"
            : currentStatus;
    }

    private static string WorkflowStatusLabel(string status)
    {
        return status switch
        {
            "Completed" => "완료",
            WorkflowStatuses.PartiallyCompleted => "부분 완료",
            "InProgress" => "진행 중",
            "Requested" => "내 업무 생성됨",
            "Blocked" => "차단",
            "Skipped" => "제외",
            _ => "미시작"
        };
    }

    private static string LinkUrlForStage(Guid projectId, string stageCode)
    {
        return stageCode switch
        {
            WorkflowStageCodes.ProductionPlanning => $"/projects/{projectId}/production-planning/edit",
            WorkflowStageCodes.DesignPanelInfo => $"/projects/{projectId}/panel-information/edit",
            WorkflowStageCodes.ProcurementInfo => $"/projects/{projectId}/procurement/edit",
            WorkflowStageCodes.KittingCompleted => $"/production-planning/releases?project={projectId}",
            _ => $"/projects/{projectId}?section=workflow"
        };
    }

    private static string LinkUrlForWorkItem(Guid projectId, string stageCode, string targetType, Guid? targetId)
    {
        if (stageCode == WorkflowStageCodes.IQC
            && string.Equals(targetType, "Inspection", StringComparison.Ordinal)
            && targetId is not null)
        {
            return $"/quality/iqc?request={targetId.Value}";
        }

        if (stageCode == WorkflowStageCodes.SalesSettlementCompleted
            && string.Equals(targetType, "Project", StringComparison.Ordinal))
        {
            return $"/projects/{projectId}/settlement";
        }

        if (string.Equals(targetType, "Pending", StringComparison.Ordinal) && targetId is not null)
        {
            return $"/pending/{targetId.Value}";
        }

        if (stageCode is WorkflowStageCodes.ManufacturingWork or WorkflowStageCodes.ManufacturingCompleted
            && string.Equals(targetType, "Panel", StringComparison.Ordinal)
            && targetId is not null)
        {
            return $"/manufacturing/work?project={projectId}&panel={targetId.Value}";
        }

        if (stageCode is WorkflowStageCodes.LQC or WorkflowStageCodes.OQC
                or WorkflowStageCodes.CustomerInspection or WorkflowStageCodes.FAT
            && string.Equals(targetType, "Panel", StringComparison.Ordinal)
            && targetId is not null)
        {
            return $"/quality/inspections?stage={stageCode}&project={projectId}&panel={targetId.Value}";
        }

        if (stageCode is WorkflowStageCodes.PackingCompleted or WorkflowStageCodes.DepartureProcessed or WorkflowStageCodes.DeliveryCompleted
            && string.Equals(targetType, "Panel", StringComparison.Ordinal)
            && targetId is not null)
        {
            var logisticsStage = stageCode switch
            {
                WorkflowStageCodes.PackingCompleted => "packing",
                WorkflowStageCodes.DepartureProcessed => "departure",
                _ => "delivery"
            };
            return $"/logistics?stage={logisticsStage}&project={projectId}&panel={targetId.Value}";
        }

        if (stageCode == WorkflowStageCodes.ReceiptConfirmed)
        {
            return $"/materials/receipts?projectId={projectId}";
        }

        if (stageCode == WorkflowStageCodes.MaterialArrived)
        {
            return $"/materials/receipts?projectId={projectId}";
        }

        return LinkUrlForStage(projectId, stageCode);
    }

    private static string WorkItemStatusLabel(string status)
    {
        return status switch
        {
            "Requested" => "시작 전",
            "InProgress" => "진행 중",
            "Completed" => "완료",
            "Cancelled" => "취소",
            _ => "시작 전"
        };
    }

    private static string ProjectStatusLabel(string status)
    {
        return status switch
        {
            "Active" => "진행",
            "OnHold" => "보류",
            "Cancelled" => "취소",
            "Completed" => "완료",
            _ => status
        };
    }

    private static string PriorityLabel(string priority)
    {
        return priority switch
        {
            "Blocking" => "차단",
            _ => "일반"
        };
    }

    private static string NotificationTypeLabel(string type)
    {
        return type switch
        {
            "Reference" => "참조",
            "Blocking" => "차단",
            _ => "정보"
        };
    }

    private static string SeverityLabel(string severity)
    {
        return severity switch
        {
            "Warning" => "주의",
            "Critical" => "긴급",
            _ => "정보"
        };
    }

    private static string VisibilityScopeLabel(string scope)
    {
        return scope switch
        {
            "Authenticated" => "로그인 사용자",
            "AdminOnly" => "관리자 전용",
            _ => "수신자 전용"
        };
    }

    private static string SourceKindLabel(string sourceKind)
    {
        return sourceKind switch
        {
            "Manual" => "관리자 수동 발송",
            "ChannelNotice" => "채널 공지",
            "WorkAssignment" => "업무 배정",
            "DailyDigest" => "일일 요약",
            "Escalation" => "에스컬레이션",
            "System" => "시스템 알림",
            _ => "자동 알림"
        };
    }

    private static string? NormalizeWorkStatusFilter(string? status)
    {
        return status?.Trim() switch
        {
            "Requested" => "Requested",
            "InProgress" => "InProgress",
            "Completed" => "Completed",
            "Cancelled" => "Cancelled",
            _ => null
        };
    }

    private static string? NormalizeNotificationReadFilter(string? readStatus)
    {
        return readStatus?.Trim().ToLowerInvariant() switch
        {
            "unread" => "unread",
            "read" => "read",
            _ => null
        };
    }

    private static string DepartmentLabel(string departmentCode)
    {
        return departmentCode switch
        {
            "sales" => "영업",
            "design" => "설계",
            "production-planning" => "생산관리",
            "procurement" => "구매",
            "materials" => "자재",
            "manufacturing" => "제조",
            "quality" => "품질",
            "logistics" => "물류",
            _ => departmentCode
        };
    }

    private static string ResponsibilityLabel(string responsibilityType)
    {
        return responsibilityType switch
        {
            "SalesPrimary" => "영업 정",
            "SalesSecondary" => "영업 부",
            "DesignPrimary" => "설계 정",
            "DesignSecondary" => "설계 부",
            "ProductionPlanningPrimary" or "ProductionPlanning" => "생산관리 정",
            "ProductionPlanningSecondary" => "생산관리 부",
            "ProcurementPrimary" or "Procurement" => "구매 정",
            "ProcurementSecondary" => "구매 부",
            "MaterialsPrimary" => "자재 정",
            "MaterialsSecondary" => "자재 부",
            "ManufacturingPrimary" or "Manufacturing" => "제조 정",
            "ManufacturingSecondary" => "제조 부",
            "LogisticsPrimary" or "Logistics" => "물류 정",
            "LogisticsSecondary" => "물류 부",
            "QualityIQC" => "IQC 정",
            "QualityIQCSecondary" => "IQC 부",
            "QualityLQC" => "LQC 정",
            "QualityLQCSecondary" => "LQC 부",
            "QualityOQC" or "Quality" => "OQC 정",
            "QualityOQCSecondary" => "OQC 부",
            "QualityCustomerInspection" => "전진검수/FAT 정",
            "QualityCustomerInspectionSecondary" => "전진검수/FAT 부",
            "PendingAction" => "Pending 조치",
            _ => responsibilityType
        };
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

    private sealed record ProjectWorkflowSnapshot(
        Guid ProjectId,
        string ProjectTitle,
        string ProjectCode,
        Guid? SalesOwnerUserId,
        bool SalesOwnerIsActive,
        bool LqcOperational);

    private sealed record WorkflowCompletionFacts(
        int ActivePanelCount = 0,
        int CompletedPanelCount = 0,
        int TouchedPanelCount = 0,
        int ProductionPlanItemCount = 0,
        int RequiredPlanItemCount = 0,
        int PlannedRequiredPlanItemCount = 0,
        int RequiredPrimaryAssigneeCount = 0,
        int ProcurementItemCount = 0,
        int NamedProcurementItemCount = 0,
        int CompletedProcurementItemCount = 0,
        int RequiredProcurementItemCount = 0,
        int MatchedRequiredProcurementItemCount = 0,
        int IqcReceiptCount = 0,
        int IqcPassedCount = 0,
        int IqcStartedCount = 0,
        int IqcFailedCount = 0,
        int ActiveMaterialItemCount = 0,
        int ArrivedMaterialItemCount = 0,
        int ArrivalClosedMaterialItemCount = 0,
        int ReceiptConfirmedMaterialItemCount = 0,
        int ConfirmedReceiptCount = 0,
        int ManufacturingReleasedPanelCount = 0,
        int ManufacturingStartedPanelCount = 0,
        int ManufacturingCompletedPanelCount = 0,
        int ManufacturingBlockedPanelCount = 0,
        int LqcStartedPanelCount = 0,
        int LqcPassedPanelCount = 0,
        int LqcFailedPanelCount = 0,
        int ManufacturingLqcReadyPanelCount = 0,
        int OqcStartedPanelCount = 0,
        int OqcPassedPanelCount = 0,
        int OqcFailedPanelCount = 0,
        int CustomerInspectionStartedPanelCount = 0,
        int CustomerInspectionPassedPanelCount = 0,
        int CustomerInspectionFailedPanelCount = 0,
        int FatStartedPanelCount = 0,
        int FatPassedPanelCount = 0,
        int FatFailedPanelCount = 0,
        int PackingStartedPanelCount = 0,
        int PackingCompletedPanelCount = 0,
        int DepartureStartedPanelCount = 0,
        int DepartureCompletedPanelCount = 0,
        int DeliveryStartedPanelCount = 0,
        int DeliveryCompletedPanelCount = 0);

    private sealed record StageSnapshot(
        string StageCode,
        int SequenceNumber,
        string DepartmentCode,
        string StageName);

    private sealed record ResponsibilityTarget(
        string Primary,
        string? Secondary,
        IReadOnlyList<string> LegacyPrimaryAliases);

    private sealed record ResolvedAssignee(
        Guid? UserId,
        string? RoleCode,
        string SourceLabel);

    private sealed record ProjectAssigneeSnapshot(
        string ResponsibilityType,
        Guid UserId);

    private sealed record AssignedProjectBuilder(
        Guid ProjectId,
        string ProjectTitle,
        string ProjectCode,
        string Item,
        DateOnly? DeliveryDate,
        string ProjectStatus)
    {
        public List<MyAssignedProjectResponsibilityResponse> Responsibilities { get; } = [];
    }
}
