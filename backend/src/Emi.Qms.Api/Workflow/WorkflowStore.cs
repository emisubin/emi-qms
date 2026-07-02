using Npgsql;

namespace Emi.Qms.Api.Workflow;

public sealed class WorkflowStore(DatabaseConnectionStringProvider connectionStringProvider)
{
    private static readonly IReadOnlyDictionary<string, string> StageToNextStage = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [WorkflowStageCodes.SalesProjectCreated] = WorkflowStageCodes.ProductionPlanning,
        [WorkflowStageCodes.ProductionPlanning] = WorkflowStageCodes.DesignPanelInfo,
        [WorkflowStageCodes.DesignPanelInfo] = WorkflowStageCodes.ProcurementInfo,
        [WorkflowStageCodes.ProcurementInfo] = WorkflowStageCodes.MaterialArrived,
        [WorkflowStageCodes.MaterialArrived] = WorkflowStageCodes.IQC,
        [WorkflowStageCodes.IQC] = WorkflowStageCodes.ReceiptConfirmed,
        [WorkflowStageCodes.ReceiptConfirmed] = WorkflowStageCodes.KittingCompleted,
        [WorkflowStageCodes.KittingCompleted] = WorkflowStageCodes.ManufacturingWork,
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
        [WorkflowStageCodes.KittingCompleted] = new("MaterialsPrimary", "MaterialsSecondary", []),
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

        await MarkStageWorkItemsCompletedAsync(connection, transaction, projectId, stageCode, cancellationToken);

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

        if (StageToNextStage.TryGetValue(stageCode, out var nextStageCode)
            && StageResponsibilities.TryGetValue(nextStageCode, out var target))
        {
            var stage = await ReadStageAsync(connection, transaction, nextStageCode, cancellationToken);
            if (stage is not null)
            {
                var assignee = await ResolveAssigneeAsync(connection, transaction, project, target, cancellationToken);
                if (assignee.UserId is not null)
                {
                    await CreateWorkItemAsync(
                        connection,
                        transaction,
                        projectId,
                        nextStageCode,
                        target.Primary,
                        assignee.UserId.Value,
                        assignee.RoleCode,
                        WorkItemTitleForStage(stage.StageCode),
                        BuildWorkDescription(stage, assignee),
                        eventId,
                        createdByUserId,
                        $"project:{projectId}:stage:{nextStageCode}:work:{target.Primary}",
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

        foreach (var target in new[]
        {
            (StageCode: WorkflowStageCodes.DesignPanelInfo, Responsibility: StageResponsibilities[WorkflowStageCodes.DesignPanelInfo]),
            (StageCode: WorkflowStageCodes.ProcurementInfo, Responsibility: StageResponsibilities[WorkflowStageCodes.ProcurementInfo])
        })
        {
            var stage = await ReadStageAsync(connection, transaction, target.StageCode, cancellationToken);
            if (stage is null)
            {
                continue;
            }

            var assignee = await ResolveAssigneeAsync(connection, transaction, project, target.Responsibility, cancellationToken);
            if (assignee.UserId is null)
            {
                continue;
            }

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
        var completedRequiredCount = requiredStages.Count(stage => string.Equals(stage.Status, "Completed", StringComparison.Ordinal));
        var progressPercent = requiredStages.Count == 0
            ? 0
            : (int)Math.Round(completedRequiredCount * 100m / requiredStages.Count, MidpointRounding.AwayFromZero);
        var currentStage = requiredStages.FirstOrDefault(stage => !string.Equals(stage.Status, "Completed", StringComparison.Ordinal))
            ?? requiredStages.LastOrDefault()
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
            await MarkStageWorkItemsCompletedAsync(projectId, stageCode, cancellationToken);
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
                wi.completed_at_utc
            from work_items wi
            join projects p on p.id = wi.project_id
            join workflow_stages ws on ws.stage_code = wi.workflow_stage_code
            where wi.assigned_user_id = @user_id
              and p.deleted_at_utc is null
              {(statusFilter is null ? "" : "and wi.status = @status")}
            order by
                case wi.status when 'Requested' then 0 when 'InProgress' then 1 when 'Completed' then 2 else 3 end,
                wi.created_at_utc desc;
            """);
        command.Parameters.AddWithValue("user_id", userId);
        if (statusFilter is not null)
        {
            command.Parameters.AddWithValue("status", statusFilter);
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

    private async Task MarkStageWorkItemsCompletedAsync(Guid projectId, string stageCode, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await MarkStageWorkItemsCompletedAsync(connection, null, projectId, stageCode, cancellationToken);
    }

    private static async Task MarkStageWorkItemsCompletedAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid projectId, string stageCode, CancellationToken cancellationToken)
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
            from notification_recipients nr
            join notifications n on n.id = nr.notification_id
            where nr.user_id = @user_id;
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
                n.notification_type,
                n.severity,
                n.title,
                n.message,
                n.link_url,
                n.created_at_utc,
                nr.read_at_utc
            from notification_recipients nr
            join notifications n on n.id = nr.notification_id
            left join projects p on p.id = n.project_id
            where nr.user_id = @user_id
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

    public async Task<WorkflowMutationResult<NotificationResponse>> MarkNotificationReadAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

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

    private async Task<WorkflowMutationResult<MyWorkItemResponse>> TransitionWorkItemAsync(
        Guid workItemId,
        Guid userId,
        string action,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using (var command = connection.CreateCommand())
        {
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
                    update work_items
                    set status = 'Completed',
                        started_at_utc = coalesce(started_at_utc, now()),
                        completed_at_utc = coalesce(completed_at_utc, now())
                    where id = @id
                      and assigned_user_id = @user_id
                      and status in ('Requested', 'InProgress', 'Completed')
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
                var existing = await WorkItemExistsAsync(connection, workItemId, cancellationToken);
                return existing
                    ? WorkflowMutationResult<MyWorkItemResponse>.Forbidden()
                    : WorkflowMutationResult<MyWorkItemResponse>.NotFound();
            }
        }

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
                wi.completed_at_utc
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
                    count(pi.id)::int as item_count,
                    count(pi.id) filter (where pi.is_required)::int as required_item_count,
                    count(pi.id) filter (where pi.is_required and pi.planned_date is not null)::int as planned_required_item_count
                from project_production_plans pp
                left join project_production_plan_items pi on pi.production_plan_id = pp.id and pi.is_active = true
                where pp.project_id = @project_id
            ),
            assignee_summary as (
                select count(*) filter (
                    where responsibility_type in (
                        'SalesPrimary',
                        'DesignPrimary',
                        'ProductionPlanningPrimary',
                        'ProcurementPrimary',
                        'MaterialsPrimary',
                        'ManufacturingPrimary',
                        'LogisticsPrimary',
                        'QualityIQC',
                        'QualityLQC',
                        'QualityOQC',
                        'QualityCustomerInspection'
                    )
                      and assigned_user_id is not null
                )::int as assigned_count
                from project_assignees
                where project_id = @project_id
            ),
            procurement_summary as (
                select
                    count(*)::int as item_count,
                    count(*) filter (where nullif(btrim(coalesce(order_item, '')), '') is not null)::int as named_item_count
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
                coalesce(prs.required_item_count, 0),
                coalesce(prs.matched_required_item_count, 0)
            from (select 1) anchor
            left join panel_summary ps on true
            left join production_plan_summary pps on true
            left join assignee_summary a on true
            left join procurement_summary pr on true
            left join procurement_required_summary prs on true;
            """;
        command.Parameters.AddWithValue("project_id", projectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new WorkflowCompletionFacts(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
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
            reader.GetInt32(10));
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
            select p.id, p.project_title, p.project_code, p.sales_owner_user_id, u.is_active
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
            !reader.IsDBNull(4) && reader.GetBoolean(4));
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
            on conflict (idempotency_key) do nothing;
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
        await command.ExecuteNonQueryAsync(cancellationToken);
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

        var salesPrimary = await ReadAssigneeAsync(connection, transaction, project.ProjectId, ["SalesPrimary"], cancellationToken);
        if (salesPrimary is not null)
        {
            return salesPrimary with { SourceLabel = "영업 정담당자 fallback" };
        }

        var salesSecondary = await ReadAssigneeAsync(connection, transaction, project.ProjectId, ["SalesSecondary"], cancellationToken);
        if (salesSecondary is not null)
        {
            return salesSecondary with { SourceLabel = "영업 부담당자 fallback" };
        }

        var admin = await ReadFirstActiveRoleUserAsync(connection, transaction, "system-administrator", cancellationToken);
        return admin is null
            ? new ResolvedAssignee(null, null, "담당자 없음")
            : admin with { SourceLabel = "관리자 fallback" };
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

    private static async Task<ResolvedAssignee?> ReadFirstActiveRoleUserAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string roleCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select u.id, r.code
            from qms_users u
            join user_roles ur on ur.user_id = u.id
            join roles r on r.id = ur.role_id
            where u.is_active = true
              and r.code = @role_code
            order by u.display_name
            limit 1;
            """;
        command.Parameters.AddWithValue("role_code", roleCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ResolvedAssignee(reader.GetGuid(0), reader.GetString(1), "역할 사용자")
            : null;
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
        var recipients = await ReadActiveUsersForRolesAsync(
            connection,
            transaction,
            ["design", "production-planning", "procurement", "materials", "manufacturing", "quality", "logistics"],
            cancellationToken);

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
            cancellationToken);
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
              and r.code = any(@role_codes);
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
        CancellationToken cancellationToken)
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
                generated_by_event_id, idempotency_key
            )
            values (
                @project_id, @notification_type, @severity, @title, @message, @link_url,
                @event_id, @idempotency_key
            )
            on conflict (idempotency_key) do update
            set title = excluded.title
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
                n.notification_type,
                n.severity,
                n.title,
                n.message,
                n.link_url,
                n.created_at_utc,
                nr.read_at_utc
            from notification_recipients nr
            join notifications n on n.id = nr.notification_id
            left join projects p on p.id = n.project_id
            where n.id = @notification_id
              and nr.user_id = @user_id;
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
            LinkUrlForStage(projectId, reader.GetString(6)));
    }

    private static NotificationResponse ReadNotification(NpgsqlDataReader reader)
    {
        var type = reader.GetString(5);
        var severity = reader.GetString(6);
        return new NotificationResponse(
            reader.GetGuid(0),
            reader.IsDBNull(1) ? null : reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            type,
            NotificationTypeLabel(type),
            severity,
            SeverityLabel(severity),
            reader.GetString(7),
            reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.GetFieldValue<DateTimeOffset>(10),
            reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11));
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
            WorkflowStageCodes.KittingCompleted => "키팅 완료 입력",
            WorkflowStageCodes.ManufacturingWork => "제조 작업 입력",
            WorkflowStageCodes.LQC => "LQC 입력",
            WorkflowStageCodes.ManufacturingCompleted => "제조 완료 입력",
            WorkflowStageCodes.OQC => "자체검수 입력",
            WorkflowStageCodes.CustomerInspection => "전진검수 입력",
            WorkflowStageCodes.FAT => "FAT 입력",
            WorkflowStageCodes.PackingCompleted => "포장 완료 입력",
            WorkflowStageCodes.DepartureProcessed => "출발 처리 입력",
            WorkflowStageCodes.DeliveryCompleted => "납품 완료 입력",
            WorkflowStageCodes.SalesSettlementCompleted => "세금계산서, 완료 처리",
            _ => "업무 입력"
        };
    }

    private static string StageDisplayName(string stageCode, string stageName)
    {
        return string.Equals(stageCode, WorkflowStageCodes.DesignPanelInfo, StringComparison.Ordinal)
            ? "패널명·사이즈"
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
            return "InProgress";
        }

        return currentStatus;
    }

    private static string ProcurementStatus(WorkflowCompletionFacts facts, string currentStatus)
    {
        if (facts.RequiredProcurementItemCount > 0)
        {
            if (facts.MatchedRequiredProcurementItemCount >= facts.RequiredProcurementItemCount)
            {
                return "Completed";
            }

            if (facts.ProcurementItemCount > 0)
            {
                return "InProgress";
            }

            return currentStatus;
        }

        if (facts.ProcurementItemCount > 0 && facts.NamedProcurementItemCount >= facts.ProcurementItemCount)
        {
            return "Completed";
        }

        if (facts.ProcurementItemCount > 0)
        {
            return "InProgress";
        }

        return currentStatus;
    }

    private static string WorkflowStatusLabel(string status)
    {
        return status switch
        {
            "Completed" => "완료",
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
            _ => $"/projects/{projectId}?section=workflow"
        };
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
        bool SalesOwnerIsActive);

    private sealed record WorkflowCompletionFacts(
        int ActivePanelCount,
        int CompletedPanelCount,
        int TouchedPanelCount,
        int ProductionPlanItemCount,
        int RequiredPlanItemCount,
        int PlannedRequiredPlanItemCount,
        int RequiredPrimaryAssigneeCount,
        int ProcurementItemCount,
        int NamedProcurementItemCount,
        int RequiredProcurementItemCount,
        int MatchedRequiredProcurementItemCount);

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
