using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Notifications;
using Emi.Qms.Api.Pending;
using Emi.Qms.Api.Projects;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.QualityInspections;

public sealed class QualityInspectionStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    PendingStore pendingStore,
    QualityInspectionPdfRenderer pdfRenderer)
{
    private const int MaxPhotoBytes = 5 * 1024 * 1024;
    private const int MaxReportPhotoBytes = 15 * 1024 * 1024;
    private const int MaxPhotos = 5;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<QualityInspectionReconciliationResponse> ReconcileHandoffsAsync(
        Guid actorUserId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var panels = new List<QualityReconciliationPanel>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select
                    project.id,
                    panel.id,
                    panel.display_code,
                    project.fat_required,
                    lqc.id,
                    lqc.status,
                    oqc.id,
                    oqc.status,
                    customer_inspection.id,
                    customer_inspection.status,
                    fat.id,
                    fat.status,
                    exists (
                        select 1 from panel_manufacturing_executions execution
                        where execution.panel_id = panel.id
                          and execution.status in ('InProgress', 'Blocked', 'Completed')
                    ) as manufacturing_started,
                    exists (
                        select 1 from work_items work
                        where work.target_type = 'Panel'
                          and work.target_id = panel.id
                          and work.workflow_stage_code = 'LQC'
                          and work.status <> 'Cancelled'
                    ) as has_lqc_work,
                    exists (
                        select 1 from panel_manufacturing_executions execution
                        where execution.panel_id = panel.id
                          and execution.status = 'Completed'
                    ) as manufacturing_completed,
                    exists (
                        select 1 from panel_manufacturing_completion_confirmations confirmation
                        where confirmation.panel_id = panel.id
                    ) as has_confirmation,
                    exists (
                        select 1 from work_items work
                        where work.idempotency_key = 'quality:panel:' || panel.id || ':OQC:attempt:1'
                    ) as has_oqc_work,
                    exists (
                        select 1 from work_items work
                        where work.idempotency_key = 'quality:panel:' || panel.id || ':CustomerInspection:attempt:1'
                    ) as has_customer_work,
                    exists (
                        select 1 from work_items work
                        where work.idempotency_key = 'quality:panel:' || panel.id || ':FAT:attempt:1'
                    ) as has_fat_work,
                    exists (
                        select 1 from work_items work
                        where work.idempotency_key = 'quality:panel:' || panel.id || ':packing'
                    ) as has_packing_work
                from panel_placeholders panel
                join projects project on project.id = panel.project_id
                left join lateral (
                    select attempt.id, attempt.status
                    from panel_quality_inspection_attempts attempt
                    where attempt.panel_id = panel.id and attempt.stage_code = 'LQC'
                    order by attempt.attempt_number desc, attempt.created_at_utc desc
                    limit 1
                ) lqc on true
                left join lateral (
                    select attempt.id, attempt.status
                    from panel_quality_inspection_attempts attempt
                    where attempt.panel_id = panel.id and attempt.stage_code = 'OQC'
                    order by attempt.attempt_number desc, attempt.created_at_utc desc
                    limit 1
                ) oqc on true
                left join lateral (
                    select attempt.id, attempt.status
                    from panel_quality_inspection_attempts attempt
                    where attempt.panel_id = panel.id and attempt.stage_code = 'CustomerInspection'
                    order by attempt.attempt_number desc, attempt.created_at_utc desc
                    limit 1
                ) customer_inspection on true
                left join lateral (
                    select attempt.id, attempt.status
                    from panel_quality_inspection_attempts attempt
                    where attempt.panel_id = panel.id and attempt.stage_code = 'FAT'
                    order by attempt.attempt_number desc, attempt.created_at_utc desc
                    limit 1
                ) fat on true
                where panel.status = 'Active'
                  and project.deleted_at_utc is null
                  and project.status in ('Active', 'OnHold')
                  and (@has_read_all or project.project_key = any(@project_keys))
                order by project.id, panel.id;
                """;
            AddScope(command, accessScope);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                panels.Add(new QualityReconciliationPanel(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetBoolean(3),
                    reader.IsDBNull(4) ? null : reader.GetGuid(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetGuid(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7),
                    reader.IsDBNull(8) ? null : reader.GetGuid(8),
                    reader.IsDBNull(9) ? null : reader.GetString(9),
                    reader.IsDBNull(10) ? null : reader.GetGuid(10),
                    reader.IsDBNull(11) ? null : reader.GetString(11),
                    reader.GetBoolean(12),
                    reader.GetBoolean(13),
                    reader.GetBoolean(14),
                    reader.GetBoolean(15),
                    reader.GetBoolean(16),
                    reader.GetBoolean(17),
                    reader.GetBoolean(18),
                    reader.GetBoolean(19)));
            }
        }

        var recoveredLqc = 0;
        var recoveredOqc = 0;
        var recoveredInspection = 0;
        var recoveredPacking = 0;
        var unresolved = 0;

        foreach (var panel in panels)
        {
            var operationId = Guid.NewGuid();
            if (panel.ManufacturingStarted && !panel.HasLqcWork)
            {
                var lqcAssignee = await ResolveAssigneeAsync(
                    connection, transaction, panel.ProjectId, QualityInspectionStages.Lqc, cancellationToken);
                if (lqcAssignee is null)
                {
                    unresolved++;
                }
                else if (await EnsureMissingLqcWorkItemAsync(
                    connection,
                    transaction,
                    panel.ProjectId,
                    panel.PanelId,
                    panel.PanelDisplayCode,
                    lqcAssignee,
                    actorUserId,
                    cancellationToken))
                {
                    recoveredLqc++;
                }
            }

            if (panel.ManufacturingCompleted
                && string.Equals(panel.LqcStatus, "Passed", StringComparison.Ordinal)
                && (!panel.HasManufacturingConfirmation || !panel.HasOqcWork))
            {
                var result = await TryOpenOqcAfterManufacturingAndLqcAsync(
                    connection,
                    transaction,
                    panel.ProjectId,
                    panel.PanelId,
                    actorUserId,
                    operationId,
                    cancellationToken);
                if (result.ConflictMessage is not null)
                {
                    unresolved++;
                }
                else if (result.Opened)
                {
                    recoveredOqc++;
                }
            }

            if (panel.LqcAttemptId is not null && string.Equals(panel.LqcStatus, "Passed", StringComparison.Ordinal))
            {
                await EnsureProjectStageEventIfCompleteAsync(
                    connection, transaction, panel.ProjectId, QualityInspectionStages.Lqc,
                    panel.LqcAttemptId.Value, operationId, actorUserId, cancellationToken);
                await EnsureProjectConfirmationEventIfCompleteAsync(
                    connection, transaction, panel.ProjectId, panel.LqcAttemptId.Value,
                    operationId, actorUserId, cancellationToken);
            }

            if (panel.OqcAttemptId is not null && string.Equals(panel.OqcStatus, "Passed", StringComparison.Ordinal))
            {
                await EnsureProjectStageEventIfCompleteAsync(
                    connection, transaction, panel.ProjectId, QualityInspectionStages.Oqc,
                    panel.OqcAttemptId.Value, operationId, actorUserId, cancellationToken);

                if (!panel.HasCustomerInspectionWork)
                {
                    var customerAssignee = await ResolveAssigneeAsync(
                        connection, transaction, panel.ProjectId, QualityInspectionStages.CustomerInspection, cancellationToken);
                    if (customerAssignee is null)
                    {
                        unresolved++;
                    }
                    else
                    {
                        await EnsureNextWorkItemAsync(
                            connection,
                            transaction,
                            new HandoffContext(
                                panel.ProjectId, panel.PanelId, panel.PanelDisplayCode, panel.FatRequired,
                                QualityInspectionStages.Oqc, panel.OqcAttemptId.Value, 1),
                            customerAssignee,
                            actorUserId,
                            cancellationToken);
                        recoveredInspection++;
                    }
                }

                if (panel.FatRequired && !panel.HasFatWork)
                {
                    var fatAssignee = await ResolveAssigneeAsync(
                        connection, transaction, panel.ProjectId, QualityInspectionStages.Fat, cancellationToken);
                    if (fatAssignee is null)
                    {
                        unresolved++;
                    }
                    else
                    {
                        await EnsureNextWorkItemAsync(
                            connection,
                            transaction,
                            new HandoffContext(
                                panel.ProjectId, panel.PanelId, panel.PanelDisplayCode, panel.FatRequired,
                                QualityInspectionStages.Oqc, panel.OqcAttemptId.Value, 1),
                            fatAssignee,
                            actorUserId,
                            cancellationToken);
                        recoveredInspection++;
                    }
                }
            }

            if (panel.CustomerInspectionAttemptId is not null
                && string.Equals(panel.CustomerInspectionStatus, "Passed", StringComparison.Ordinal))
            {
                await EnsureProjectStageEventIfCompleteAsync(
                    connection, transaction, panel.ProjectId, QualityInspectionStages.CustomerInspection,
                    panel.CustomerInspectionAttemptId.Value, operationId, actorUserId, cancellationToken);
            }

            if (panel.FatAttemptId is not null && string.Equals(panel.FatStatus, "Passed", StringComparison.Ordinal))
            {
                await EnsureProjectStageEventIfCompleteAsync(
                    connection, transaction, panel.ProjectId, QualityInspectionStages.Fat,
                    panel.FatAttemptId.Value, operationId, actorUserId, cancellationToken);
            }

            var finalQualityPassed = string.Equals(panel.CustomerInspectionStatus, "Passed", StringComparison.Ordinal)
                && (!panel.FatRequired || string.Equals(panel.FatStatus, "Passed", StringComparison.Ordinal));
            if (finalQualityPassed && !panel.HasPackingWork)
            {
                var packingAssignee = await ResolveAssigneeAsync(
                    connection, transaction, panel.ProjectId, "PackingCompleted", cancellationToken);
                if (packingAssignee is null)
                {
                    unresolved++;
                }
                else
                {
                    var sourceAttemptId = panel.FatRequired
                        ? panel.FatAttemptId!.Value
                        : panel.CustomerInspectionAttemptId!.Value;
                    await EnsureNextWorkItemAsync(
                        connection,
                        transaction,
                        new HandoffContext(
                            panel.ProjectId, panel.PanelId, panel.PanelDisplayCode, panel.FatRequired,
                            panel.FatRequired ? QualityInspectionStages.Fat : QualityInspectionStages.CustomerInspection,
                            sourceAttemptId, 1),
                        packingAssignee,
                        actorUserId,
                        cancellationToken);
                    await AdvancePanelStageAsync(
                        connection, transaction, panel.PanelId, "InspectionCompleted", cancellationToken);
                    recoveredPacking++;
                }
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new QualityInspectionReconciliationResponse(
            recoveredLqc,
            recoveredOqc,
            recoveredInspection,
            recoveredPacking,
            unresolved);
    }

    public static async Task CancelPanelInspectionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid panelId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update panel_quality_inspection_attempts
            set status = 'Cancelled', version = version + 1,
                completed_by_user_id = @actor_id, completed_at_utc = now(), updated_at_utc = now()
            where panel_id = @panel_id and status in ('Requested', 'InProgress');
            update work_items
            set status = 'Cancelled', cancelled_at_utc = coalesce(cancelled_at_utc, now())
            where target_type = 'Panel' and target_id = @panel_id
              and workflow_stage_code in ('LQC', 'ManufacturingCompleted', 'OQC', 'CustomerInspection', 'FAT')
              and status in ('Requested', 'InProgress');
            """;
        command.Parameters.AddWithValue("panel_id", panelId);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task CancelProjectInspectionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update panel_quality_inspection_attempts
            set status = 'Cancelled', version = version + 1,
                completed_by_user_id = @actor_id, completed_at_utc = now(), updated_at_utc = now()
            where project_id = @project_id and status in ('Requested', 'InProgress');
            update work_items
            set status = 'Cancelled', cancelled_at_utc = coalesce(cancelled_at_utc, now())
            where project_id = @project_id and target_type = 'Panel'
              and workflow_stage_code in ('LQC', 'ManufacturingCompleted', 'OQC', 'CustomerInspection', 'FAT')
              and status in ('Requested', 'InProgress');
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<QualityInspectionQueueResponse> ListAsync(
        ProjectAccessScope accessScope,
        Guid? actorUserId,
        bool canInspect,
        string? stageCode,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        var normalizedStage = NormalizeStage(stageCode);
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand($"""
            with latest_work as (
                select distinct on (work_item.target_id, work_item.workflow_stage_code)
                    work_item.*
                from work_items work_item
                where work_item.target_type = 'Panel'
                  and work_item.workflow_stage_code in ('LQC', 'ManufacturingCompleted', 'OQC', 'CustomerInspection', 'FAT')
                  and work_item.status <> 'Cancelled'
                order by work_item.target_id, work_item.workflow_stage_code,
                         case work_item.status when 'InProgress' then 0 when 'Requested' then 1 else 2 end,
                         work_item.created_at_utc desc, work_item.id desc
            )
            select
                project.id, project.project_code, project.project_title, project.fat_required,
                panel.id, panel.display_code, panel.panel_name, panel.workflow_stage,
                work_item.workflow_stage_code, work_item.id, work_item.status,
                attempt.id, coalesce(attempt.attempt_number, 0),
                case
                    when work_item.workflow_stage_code = 'ManufacturingCompleted' and confirmation.id is not null then 'Confirmed'
                    when attempt.id is not null then attempt.status
                    when work_item.status = 'Completed' then 'Completed'
                    else 'Ready'
                end as status,
                coalesce(attempt.version, 0), pending.id,
                pending.issue_number, pending.action_department_code,
                (
                    @can_inspect
                    and work_item.workflow_stage_code <> 'ManufacturingCompleted'
                    and (
                        work_item.assigned_user_id = @actor_id
                        or exists (
                            select 1 from project_assignees assignee
                            where assignee.project_id = project.id
                              and assignee.assigned_user_id = @actor_id
                              and assignee.responsibility_type = any(
                                  case work_item.workflow_stage_code
                                      when 'LQC' then array['QualityLQC', 'QualityLQCSecondary']::text[]
                                      when 'OQC' then array['QualityOQC', 'QualityOQCSecondary']::text[]
                                      else array['QualityCustomerInspection', 'QualityCustomerInspectionSecondary']::text[]
                                  end
                              )
                        )
                    )
                ) as can_mutate
            from latest_work work_item
            join panel_placeholders panel on panel.id = work_item.target_id and panel.status = 'Active'
            join projects project on project.id = work_item.project_id
            left join lateral (
                select candidate.*
                from panel_quality_inspection_attempts candidate
                where candidate.panel_id = panel.id
                  and candidate.stage_code = work_item.workflow_stage_code
                order by candidate.attempt_number desc
                limit 1
            ) attempt on true
            left join pending_issues pending
              on pending.id = attempt.linked_pending_issue_id
             and pending.status <> 'Closed'
            left join panel_manufacturing_completion_confirmations confirmation on confirmation.panel_id = panel.id
            where project.deleted_at_utc is null
              and project.status <> 'Cancelled'
              and (@has_read_all or project.project_key = any(@project_keys))
              {(normalizedStage is null ? string.Empty : "and work_item.workflow_stage_code = @stage_code")}
              {(projectId is null ? string.Empty : "and project.id = @project_id")}
            order by project.project_code, panel.sequence_number, work_item.workflow_stage_code;
            """);
        command.Parameters.AddWithValue("can_inspect", canInspect);
        command.Parameters.AddWithValue("actor_id", actorUserId ?? Guid.Empty);
        AddScope(command, accessScope);
        if (normalizedStage is not null) command.Parameters.AddWithValue("stage_code", normalizedStage);
        if (projectId is not null) command.Parameters.AddWithValue("project_id", projectId.Value);

        var builders = new Dictionary<Guid, ProjectBuilder>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var currentProjectId = reader.GetGuid(0);
            if (!builders.TryGetValue(currentProjectId, out var builder))
            {
                builder = new ProjectBuilder(
                    currentProjectId,
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetBoolean(3));
                builders[currentProjectId] = builder;
            }
            var currentStage = reader.GetString(8);
            builder.Panels.Add(new QualityInspectionPanelSummary(
                reader.GetGuid(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetString(7),
                currentStage,
                StageLabel(currentStage),
                reader.GetGuid(9),
                reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetGuid(11),
                reader.GetInt32(12),
                reader.GetString(13),
                reader.GetInt32(14),
                reader.IsDBNull(15) ? null : reader.GetGuid(15),
                reader.IsDBNull(16) ? null : reader.GetInt64(16),
                reader.IsDBNull(17) ? null : reader.GetString(17),
                reader.GetBoolean(18)));
        }
        return new QualityInspectionQueueResponse(builders.Values.Select(builder => builder.Build()).ToList());
    }

    public async Task<QualityInspectionDetailResponse?> GetDetailAsync(
        Guid panelId,
        string stageCode,
        ProjectAccessScope accessScope,
        Guid? actorUserId,
        bool canInspect,
        CancellationToken cancellationToken)
    {
        var normalizedStage = NormalizeStage(stageCode);
        if (normalizedStage is null) return null;
        var queue = await ListAsync(accessScope, actorUserId, canInspect, normalizedStage, null, cancellationToken);
        var panel = queue.Projects.SelectMany(project => project.Panels)
            .FirstOrDefault(candidate => candidate.PanelId == panelId && candidate.StageCode == normalizedStage);
        if (panel is null) return null;

        if (panel.AttemptId is null)
        {
            return new QualityInspectionDetailResponse(panel, DecisionMode(normalizedStage), null, null, null, null, null, null, [], [], [], []);
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var report = await ReadReportViewAsync(connection, panel.AttemptId.Value, cancellationToken);
        var history = await ReadHistoryAsync(connection, panel.PanelId, normalizedStage, cancellationToken);
        return new QualityInspectionDetailResponse(
            panel,
            report?.DecisionMode ?? DecisionMode(normalizedStage),
            report?.ReportId,
            report?.Status,
            report?.Version,
            report?.Result,
            report?.Reason,
            report?.PdfStatus,
            report?.Items ?? [],
            report?.Responses ?? [],
            report?.Photos ?? [],
            history);
    }

    public async Task<IReadOnlyList<QualityActionDepartmentResponse>> ListActionDepartmentsAsync(CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select department.code, department.name, users.id, users.display_name
            from departments department
            left join qms_users users
              on users.department_id = department.id
             and users.is_active = true
             and exists (
                select 1
                from user_roles user_role
                join role_permissions role_permission on role_permission.role_id = user_role.role_id
                join permissions permission on permission.id = role_permission.permission_id
                where user_role.user_id = users.id and permission.code = 'Pending.Manage'
             )
            where department.is_active = true
            order by department.name, users.display_name, users.id;
            """);
        var builders = new Dictionary<string, DepartmentBuilder>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = reader.GetString(0);
            if (!builders.TryGetValue(code, out var builder))
            {
                builder = new DepartmentBuilder(code, reader.GetString(1));
                builders[code] = builder;
            }
            if (!reader.IsDBNull(2)) builder.Assignees.Add(new QualityActionOwnerResponse(reader.GetGuid(2), reader.GetString(3)));
        }
        return builders.Values.Select(builder => builder.Build()).ToList();
    }

    public async Task<QualityInspectionMutationResult<QualityInspectionMutationResponse>> StartAsync(
        StartQualityInspectionRequest request,
        Guid actorUserId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        var stage = NormalizeInspectionStage(request.StageCode);
        if (request.OperationId == Guid.Empty || request.ProjectId == Guid.Empty || request.PanelId == Guid.Empty || stage is null)
        {
            return Validation("stageCode", "검사 단계와 요청 식별자를 확인해 주세요.");
        }
        var fingerprint = Fingerprint("Start", request.ProjectId, request.PanelId, stage);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var replay = await ReadReplayAsync(connection, transaction, request.OperationId, "Start", fingerprint, cancellationToken);
        if (replay.Result is not null) return replay.Result;

        var project = await LockProjectAsync(connection, transaction, request.ProjectId, accessScope, cancellationToken);
        if (project is null) return QualityInspectionMutationResult<QualityInspectionMutationResponse>.NotFound();
        var work = await LockStageWorkAsync(connection, transaction, request.ProjectId, request.PanelId, stage, cancellationToken);
        if (work is null) return QualityInspectionMutationResult<QualityInspectionMutationResponse>.NotFound();
        if (!await HasStageAccessAsync(connection, transaction, request.ProjectId, work.WorkItemId, stage, actorUserId, false, cancellationToken))
        {
            return QualityInspectionMutationResult<QualityInspectionMutationResponse>.NotFound();
        }
        if (work.WorkStatus is not ("Requested" or "InProgress"))
        {
            return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Conflict("현재 검사 업무는 시작할 수 없습니다.");
        }

        var existing = await ReadActiveAttemptAsync(connection, transaction, request.PanelId, stage, cancellationToken);
        Guid attemptId;
        Guid reportId;
        int attemptNumber;
        int version;
        if (existing is not null)
        {
            attemptId = existing.AttemptId;
            reportId = existing.ReportId;
            attemptNumber = existing.AttemptNumber;
            version = existing.Version;
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                update panel_quality_inspection_attempts
                set status = 'InProgress',
                    started_by_user_id = coalesce(started_by_user_id, @actor_id),
                    started_at_utc = coalesce(started_at_utc, now()),
                    updated_at_utc = now()
                where id = @attempt_id and status in ('Requested', 'InProgress');
                """;
            command.Parameters.AddWithValue("actor_id", actorUserId);
            command.Parameters.AddWithValue("attempt_id", attemptId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            var templateVersionId = await ReadActiveTemplateVersionAsync(connection, transaction, stage, cancellationToken);
            if (templateVersionId is null)
            {
                return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Conflict("활성 검사 양식을 찾을 수 없습니다.");
            }
            attemptNumber = await ReadNextAttemptNumberAsync(connection, transaction, request.PanelId, stage, cancellationToken);
            attemptId = Guid.NewGuid();
            reportId = Guid.NewGuid();
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    insert into panel_quality_inspection_attempts (
                        id, project_id, panel_id, stage_code, attempt_number, status, work_item_id,
                        decision_mode, version, started_by_user_id, started_at_utc, updated_at_utc
                    ) values (
                        @id, @project_id, @panel_id, @stage_code, @attempt_number, 'InProgress', @work_item_id,
                        @decision_mode, 1, @actor_id, now(), now()
                    );
                    insert into panel_quality_reports (
                        id, attempt_id, template_version_id, status, version,
                        created_by_user_id, updated_by_user_id
                    ) values (@report_id, @id, @template_id, 'Draft', 1, @actor_id, @actor_id);
                    """;
                command.Parameters.AddWithValue("id", attemptId);
                command.Parameters.AddWithValue("project_id", request.ProjectId);
                command.Parameters.AddWithValue("panel_id", request.PanelId);
                command.Parameters.AddWithValue("stage_code", stage);
                command.Parameters.AddWithValue("attempt_number", attemptNumber);
                command.Parameters.AddWithValue("work_item_id", work.WorkItemId);
                command.Parameters.AddWithValue("decision_mode", DecisionMode(stage));
                command.Parameters.AddWithValue("actor_id", actorUserId);
                command.Parameters.AddWithValue("report_id", reportId);
                command.Parameters.AddWithValue("template_id", templateVersionId.Value);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            version = 1;
        }
        await MarkWorkInProgressAsync(connection, transaction, work.WorkItemId, cancellationToken);
        var response = new QualityInspectionMutationResponse(
            request.OperationId, request.ProjectId, request.PanelId, stage, attemptId, reportId,
            "InProgress", version, null, null, null, false);
        await InsertOperationAsync(connection, transaction, request.OperationId, "Start", request.ProjectId, request.PanelId, stage, attemptId, actorUserId, fingerprint, response, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Success(response);
    }

    public async Task<QualityInspectionMutationResult<QualityInspectionMutationResponse>> SaveResponsesAsync(
        Guid reportId,
        SaveQualityInspectionResponsesRequest request,
        Guid actorUserId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        if (request.OperationId == Guid.Empty || request.ExpectedReportVersion is null or < 1 || request.Responses is null)
        {
            return Validation("responses", "최신 version과 검사 응답을 확인해 주세요.");
        }
        var normalized = request.Responses.Select(item => new SaveQualityInspectionItemRequest(
            item.TemplateItemId,
            Normalize(item.CheckResult),
            Normalize(item.TextValue),
            Normalize(item.Note))).ToList();
        var fingerprint = Fingerprint("SaveResponses", reportId, request.ExpectedReportVersion.Value,
            string.Join('|', normalized.OrderBy(item => item.TemplateItemId).Select(item => $"{item.TemplateItemId}:{item.CheckResult}:{item.TextValue}:{item.Note}")));

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var replay = await ReadReplayAsync(connection, transaction, request.OperationId, "SaveResponses", fingerprint, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        var context = await LockReportContextAsync(connection, transaction, reportId, accessScope, cancellationToken);
        if (context is null) return QualityInspectionMutationResult<QualityInspectionMutationResponse>.NotFound();
        if (!await HasStageAccessAsync(connection, transaction, context.ProjectId, context.WorkItemId, context.StageCode, actorUserId, false, cancellationToken))
        {
            return QualityInspectionMutationResult<QualityInspectionMutationResponse>.NotFound();
        }
        if (context.ReportStatus != "Draft") return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Conflict("확정된 성적서는 수정할 수 없습니다.");
        if (context.ReportVersion != request.ExpectedReportVersion) return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Conflict("다른 사용자가 먼저 변경했습니다. 최신 내용을 다시 불러와 주세요.");
        if (context.DecisionMode == "Aggregate")
        {
            return Validation("responses", "전진검수와 FAT는 패널 통합 판정이므로 항목별 검사 응답을 저장하지 않습니다.");
        }
        var allItems = await ReadTemplateRowsAsync(connection, transaction, context.TemplateVersionId, cancellationToken);
        var reinspectionScope = await ReadReinspectionScopeAsync(
            connection, transaction, context.TemplateVersionId, context.LinkedPendingId,
            context.StageCode, context.AttemptNumber, cancellationToken);
        var items = ApplyReinspectionScope(allItems, reinspectionScope);
        var itemMap = items.ToDictionary(item => item.ItemId);
        var errors = ValidateResponses(normalized, itemMap);
        if (context.StageCode == QualityInspectionStages.Lqc)
        {
            AddLqcAvailabilityErrors(
                errors,
                normalized.Select(item => item.TemplateItemId),
                allItems,
                await ReadLqcManufacturingProgressAsync(connection, transaction, context.PanelId, cancellationToken));
        }
        if (errors.Count > 0) return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Validation(errors);
        var lqcDefinitionKeys = context.StageCode == QualityInspectionStages.Lqc
            ? await ReadLqcDefinitionKeysAsync(
                connection,
                transaction,
                context.PanelId,
                context.TemplateVersionId,
                cancellationToken)
            : new Dictionary<Guid, Guid>();

        foreach (var item in normalized)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into panel_quality_report_responses (
                    report_id, template_item_id, check_result, text_value, note,
                    manufacturing_definition_key, updated_by_user_id, updated_at_utc
                ) values (
                    @report_id, @item_id, @check_result, @text_value, @note,
                    @manufacturing_definition_key, @actor_id, now()
                )
                on conflict (report_id, template_item_id) do update set
                    check_result = excluded.check_result,
                    text_value = excluded.text_value,
                    note = excluded.note,
                    manufacturing_definition_key = excluded.manufacturing_definition_key,
                    updated_by_user_id = excluded.updated_by_user_id,
                    updated_at_utc = now();
                """;
            command.Parameters.AddWithValue("report_id", reportId);
            command.Parameters.AddWithValue("item_id", item.TemplateItemId);
            AddNullableText(command, "check_result", item.CheckResult);
            AddNullableText(command, "text_value", item.TextValue);
            AddNullableText(command, "note", item.Note);
            command.Parameters.Add("manufacturing_definition_key", NpgsqlDbType.Uuid).Value =
                lqcDefinitionKeys.TryGetValue(item.TemplateItemId, out var definitionKey)
                    ? definitionKey
                    : DBNull.Value;
            command.Parameters.AddWithValue("actor_id", actorUserId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update panel_quality_reports
                set version = version + 1, updated_by_user_id = @actor_id, updated_at_utc = now()
                where id = @report_id;
                update panel_quality_inspection_attempts
                set version = version + 1, updated_at_utc = now()
                where id = @attempt_id;
                """;
            update.Parameters.AddWithValue("actor_id", actorUserId);
            update.Parameters.AddWithValue("report_id", reportId);
            update.Parameters.AddWithValue("attempt_id", context.AttemptId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }
        var response = new QualityInspectionMutationResponse(
            request.OperationId, context.ProjectId, context.PanelId, context.StageCode, context.AttemptId,
            reportId, "InProgress", context.ReportVersion + 1, context.LinkedPendingId, null, null, false);
        await InsertOperationAsync(connection, transaction, request.OperationId, "SaveResponses", context.ProjectId, context.PanelId, context.StageCode, context.AttemptId, actorUserId, fingerprint, response, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Success(response);
    }

    public async Task<QualityInspectionMutationResult<QualityInspectionMutationResponse>> AddPhotoAsync(
        Guid reportId,
        Guid operationId,
        Guid templateItemId,
        int? expectedReportVersion,
        string? altText,
        byte[] content,
        Guid actorUserId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        var normalizedAlt = Normalize(altText);
        var normalizedMime = DetectImageMime(content);
        var errors = new Dictionary<string, string[]>();
        if (operationId == Guid.Empty) errors["operationId"] = ["요청 식별자가 필요합니다."];
        if (templateItemId == Guid.Empty) errors["templateItemId"] = ["사진과 연결할 검사 항목을 선택해 주세요."];
        if (expectedReportVersion is null or < 1) errors["expectedReportVersion"] = ["최신 성적서 version이 필요합니다."];
        if (normalizedAlt is null || normalizedAlt.Length > 200) errors["altText"] = ["사진 설명을 1~200자로 입력해 주세요."];
        if (content.Length is < 1 or > MaxPhotoBytes || normalizedMime is null) errors["photo"] = ["사진은 5MB 이하의 올바른 JPEG 또는 PNG 파일이어야 합니다."];
        if (errors.Count > 0) return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Validation(errors);

        var contentHash = Hash(content);
        var fingerprint = Fingerprint("AddPhoto", reportId, templateItemId, expectedReportVersion!.Value, normalizedAlt, contentHash);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var replay = await ReadReplayAsync(connection, transaction, operationId, "AddPhoto", fingerprint, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        var context = await LockReportContextAsync(connection, transaction, reportId, accessScope, cancellationToken);
        if (context is null) return QualityInspectionMutationResult<QualityInspectionMutationResponse>.NotFound();
        if (!await HasStageAccessAsync(connection, transaction, context.ProjectId, context.WorkItemId, context.StageCode, actorUserId, false, cancellationToken))
        {
            return QualityInspectionMutationResult<QualityInspectionMutationResponse>.NotFound();
        }
        if (context.ReportStatus != "Draft") return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Conflict("확정된 성적서에는 사진을 추가할 수 없습니다.");
        if (context.ReportVersion != expectedReportVersion) return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Conflict("다른 사용자가 먼저 변경했습니다. 최신 내용을 다시 불러와 주세요.");
        var allItems = await ReadTemplateRowsAsync(connection, transaction, context.TemplateVersionId, cancellationToken);
        var reinspectionScope = await ReadReinspectionScopeAsync(
            connection, transaction, context.TemplateVersionId, context.LinkedPendingId,
            context.StageCode, context.AttemptNumber, cancellationToken);
        var items = ApplyReinspectionScope(allItems, reinspectionScope);
        if (items.All(item => item.ItemId != templateItemId)) return Validation("templateItemId", "현재 성적서의 검사 항목을 선택해 주세요.");
        if (context.StageCode == QualityInspectionStages.Lqc)
        {
            var availabilityErrors = new Dictionary<string, string[]>();
            AddLqcAvailabilityErrors(
                availabilityErrors,
                [templateItemId],
                allItems,
                await ReadLqcManufacturingProgressAsync(connection, transaction, context.PanelId, cancellationToken));
            if (availabilityErrors.Count > 0)
            {
                return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Validation(availabilityErrors);
            }
        }

        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        var totalBytes = 0;
        await using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = "select display_name, byte_size from panel_quality_report_photos where report_id = @report_id for update;";
            read.Parameters.AddWithValue("report_id", reportId);
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                usedNames.Add(reader.GetString(0));
                totalBytes += reader.GetInt32(1);
            }
        }
        if (usedNames.Count >= MaxPhotos) return Validation("photo", "성적서에는 사진을 최대 5장까지 등록할 수 있습니다.");
        if (totalBytes + content.Length > MaxReportPhotoBytes) return Validation("photo", "성적서 사진 전체 용량은 15MB를 초과할 수 없습니다.");
        var extension = normalizedMime == "image/jpeg" ? "jpg" : "png";
        var slot = Enumerable.Range(1, MaxPhotos).First(number =>
            !usedNames.Contains($"photo-{number}.jpg") && !usedNames.Contains($"photo-{number}.png"));

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into panel_quality_report_photos (
                    report_id, template_item_id, display_name, normalized_mime, byte_size,
                    sha256, alt_text, content, created_by_user_id
                ) values (
                    @report_id, @item_id, @display_name, @mime, @byte_size,
                    @sha256, @alt_text, @content, @actor_id
                );
                """;
            insert.Parameters.AddWithValue("report_id", reportId);
            insert.Parameters.AddWithValue("item_id", templateItemId);
            insert.Parameters.AddWithValue("display_name", $"photo-{slot}.{extension}");
            insert.Parameters.AddWithValue("mime", normalizedMime!);
            insert.Parameters.AddWithValue("byte_size", content.Length);
            insert.Parameters.AddWithValue("sha256", contentHash);
            insert.Parameters.AddWithValue("alt_text", normalizedAlt!);
            insert.Parameters.Add("content", NpgsqlDbType.Bytea).Value = content;
            insert.Parameters.AddWithValue("actor_id", actorUserId);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await IncrementDraftVersionsAsync(connection, transaction, context, actorUserId, cancellationToken);
        var response = new QualityInspectionMutationResponse(
            operationId, context.ProjectId, context.PanelId, context.StageCode, context.AttemptId,
            reportId, "InProgress", context.ReportVersion + 1, context.LinkedPendingId, null, null, false);
        await InsertOperationAsync(connection, transaction, operationId, "AddPhoto", context.ProjectId, context.PanelId, context.StageCode, context.AttemptId, actorUserId, fingerprint, response, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Success(response);
    }

    public async Task<QualityInspectionMutationResult<QualityInspectionMutationResponse>> RemovePhotoAsync(
        Guid reportId,
        Guid photoId,
        Guid operationId,
        int? expectedReportVersion,
        Guid actorUserId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        if (operationId == Guid.Empty || photoId == Guid.Empty || expectedReportVersion is null or < 1)
        {
            return Validation("photo", "삭제할 사진과 최신 성적서 version을 확인해 주세요.");
        }
        var fingerprint = Fingerprint("RemovePhoto", reportId, photoId, expectedReportVersion.Value);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var replay = await ReadReplayAsync(connection, transaction, operationId, "RemovePhoto", fingerprint, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        var context = await LockReportContextAsync(connection, transaction, reportId, accessScope, cancellationToken);
        if (context is null) return QualityInspectionMutationResult<QualityInspectionMutationResponse>.NotFound();
        if (!await HasStageAccessAsync(connection, transaction, context.ProjectId, context.WorkItemId, context.StageCode, actorUserId, false, cancellationToken))
        {
            return QualityInspectionMutationResult<QualityInspectionMutationResponse>.NotFound();
        }
        if (context.ReportStatus != "Draft") return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Conflict("확정된 성적서의 사진은 삭제할 수 없습니다.");
        if (context.ReportVersion != expectedReportVersion) return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Conflict("다른 사용자가 먼저 변경했습니다. 최신 내용을 다시 불러와 주세요.");
        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "delete from panel_quality_report_photos where id = @photo_id and report_id = @report_id;";
            delete.Parameters.AddWithValue("photo_id", photoId);
            delete.Parameters.AddWithValue("report_id", reportId);
            if (await delete.ExecuteNonQueryAsync(cancellationToken) == 0) return QualityInspectionMutationResult<QualityInspectionMutationResponse>.NotFound();
        }
        await IncrementDraftVersionsAsync(connection, transaction, context, actorUserId, cancellationToken);
        var response = new QualityInspectionMutationResponse(
            operationId, context.ProjectId, context.PanelId, context.StageCode, context.AttemptId,
            reportId, "InProgress", context.ReportVersion + 1, context.LinkedPendingId, null, null, false);
        await InsertOperationAsync(connection, transaction, operationId, "RemovePhoto", context.ProjectId, context.PanelId, context.StageCode, context.AttemptId, actorUserId, fingerprint, response, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Success(response);
    }

    public async Task<QualityInspectionMutationResult<QualityInspectionMutationResponse>> FinalizeAsync(
        Guid reportId,
        FinalizeQualityInspectionRequest request,
        Guid actorUserId,
        ProjectAccessScope accessScope,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var result = Normalize(request.Result);
        var reason = Normalize(request.Reason);
        var department = Normalize(request.ActionDepartmentCode);
        var submittedResponses = request.Responses?.Select(item => new SaveQualityInspectionItemRequest(
            item.TemplateItemId,
            Normalize(item.CheckResult),
            Normalize(item.TextValue),
            Normalize(item.Note))).ToList();
        var errors = new Dictionary<string, string[]>();
        if (request.OperationId == Guid.Empty) errors["operationId"] = ["요청 식별자가 필요합니다."];
        if (request.ExpectedReportVersion is null or < 1) errors["expectedReportVersion"] = ["최신 성적서 version이 필요합니다."];
        if (result is not ("Passed" or "Failed")) errors["result"] = ["합격 또는 부적합을 선택해 주세요."];
        if (reason is null || reason.Length is < 3 or > 1000) errors["reason"] = ["판정 사유를 3~1000자로 입력해 주세요."];
        if (errors.Count > 0) return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Validation(errors);
        var responseFingerprint = submittedResponses is null
            ? null
            : string.Join('|', submittedResponses
                .OrderBy(item => item.TemplateItemId)
                .Select(item => $"{item.TemplateItemId}:{item.CheckResult}:{item.TextValue}:{item.Note}"));
        var fingerprint = Fingerprint(
            "Finalize",
            reportId,
            request.ExpectedReportVersion!.Value,
            result!,
            reason!,
            department,
            request.AssigneeUserId,
            responseFingerprint);

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var replay = await ReadReplayAsync(connection, transaction, request.OperationId, "Finalize", fingerprint, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        var context = await LockReportContextAsync(connection, transaction, reportId, accessScope, cancellationToken);
        if (context is null) return QualityInspectionMutationResult<QualityInspectionMutationResponse>.NotFound();
        if (!await HasStageAccessAsync(connection, transaction, context.ProjectId, context.WorkItemId, context.StageCode, actorUserId, false, cancellationToken))
        {
            return QualityInspectionMutationResult<QualityInspectionMutationResponse>.NotFound();
        }
        if (context.ReportStatus != "Draft") return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Conflict("이미 확정된 성적서입니다.");
        if (context.ReportVersion != request.ExpectedReportVersion) return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Conflict("다른 사용자가 먼저 변경했습니다. 최신 내용을 다시 불러와 주세요.");
        if (result == "Failed" && context.LinkedPendingId is null && department is null)
        {
            return Validation("actionDepartmentCode", "조치 담당 부서를 선택해 주세요.");
        }

        var allItems = await ReadTemplateRowsAsync(connection, transaction, context.TemplateVersionId, cancellationToken);
        var reinspectionScope = await ReadReinspectionScopeAsync(
            connection, transaction, context.TemplateVersionId, context.LinkedPendingId,
            context.StageCode, context.AttemptNumber, cancellationToken);
        var items = ApplyReinspectionScope(allItems, reinspectionScope);
        IReadOnlyList<ResponseRow> responses;
        if (submittedResponses is null)
        {
            responses = await ReadResponseRowsAsync(connection, transaction, reportId, cancellationToken);
        }
        else
        {
            var responseErrors = ValidateResponses(submittedResponses, items.ToDictionary(item => item.ItemId));
            if (context.StageCode == QualityInspectionStages.Lqc)
            {
                AddLqcAvailabilityErrors(
                    responseErrors,
                    submittedResponses.Select(item => item.TemplateItemId),
                    allItems,
                    await ReadLqcManufacturingProgressAsync(connection, transaction, context.PanelId, cancellationToken));
            }
            if (responseErrors.Count > 0)
            {
                return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Validation(responseErrors);
            }
            responses = submittedResponses
                .Select(item => new ResponseRow(item.TemplateItemId, item.CheckResult, item.TextValue, item.Note))
                .ToList();
        }
        var photos = await ReadPhotoSnapshotsAsync(connection, transaction, reportId, cancellationToken);
        if (reinspectionScope is not null)
        {
            var allowedItemIds = reinspectionScope.EvidenceByItemId.Keys.ToHashSet();
            responses = responses.Where(response => allowedItemIds.Contains(response.TemplateItemId)).ToList();
            photos = photos.Where(photo => allowedItemIds.Contains(photo.TemplateItemId)).ToList();
        }
        var invariantErrors = ValidateFinalization(items, responses, photos, context.DecisionMode, result!, reason!);
        if (context.StageCode == QualityInspectionStages.Lqc)
        {
            AddLqcAvailabilityErrors(
                invariantErrors,
                items.Where(item => item.ResponseType == "Check").Select(item => item.ItemId),
                allItems,
                await ReadLqcManufacturingProgressAsync(connection, transaction, context.PanelId, cancellationToken));
        }
        if (invariantErrors.Count > 0) return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Validation(invariantErrors);

        if (result == "Failed" && context.LinkedPendingId is null)
        {
            if (!await IsActiveDepartmentAsync(connection, transaction, department!, cancellationToken))
            {
                return Validation("actionDepartmentCode", "사용 가능한 조치 담당 부서를 선택해 주세요.");
            }
            if (request.AssigneeUserId is not null
                && !await IsValidActionAssigneeAsync(connection, transaction, request.AssigneeUserId.Value, department!, cancellationToken))
            {
                return Validation("assigneeUserId", "선택한 부서의 활성 Pending 담당자를 선택해 주세요.");
            }
        }

        var handoffs = new List<HandoffAssignee>();
        if (result == "Passed")
        {
            if (context.StageCode == QualityInspectionStages.Oqc)
            {
                var customerInspection = await ResolveAssigneeAsync(
                    connection, transaction, context.ProjectId, QualityInspectionStages.CustomerInspection, cancellationToken);
                if (customerInspection is null)
                {
                    return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Conflict("전진검수 담당자를 지정한 뒤 다시 시도해 주세요.");
                }
                handoffs.Add(customerInspection);
                if (context.FatRequired)
                {
                    var fat = await ResolveAssigneeAsync(
                        connection, transaction, context.ProjectId, QualityInspectionStages.Fat, cancellationToken);
                    if (fat is null)
                    {
                        return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Conflict("FAT 담당자를 지정한 뒤 다시 시도해 주세요.");
                    }
                    handoffs.Add(fat);
                }
            }
            else if (context.StageCode == QualityInspectionStages.CustomerInspection)
            {
                var readyForPacking = !context.FatRequired
                    || await ReadLatestPassedAttemptAsync(
                        connection, transaction, context.PanelId, QualityInspectionStages.Fat, cancellationToken) is not null;
                if (readyForPacking)
                {
                    var packing = await ResolveAssigneeAsync(
                        connection, transaction, context.ProjectId, "PackingCompleted", cancellationToken);
                    if (packing is null)
                    {
                        return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Conflict("물류 포장 담당자를 지정한 뒤 다시 시도해 주세요.");
                    }
                    handoffs.Add(packing);
                }
            }
            else if (context.StageCode == QualityInspectionStages.Fat)
            {
                var customerInspectionPassed = await ReadLatestPassedAttemptAsync(
                    connection, transaction, context.PanelId, QualityInspectionStages.CustomerInspection, cancellationToken) is not null;
                if (customerInspectionPassed)
                {
                    var packing = await ResolveAssigneeAsync(
                        connection, transaction, context.ProjectId, "PackingCompleted", cancellationToken);
                    if (packing is null)
                    {
                        return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Conflict("물류 포장 담당자를 지정한 뒤 다시 시도해 주세요.");
                    }
                    handoffs.Add(packing);
                }
            }
        }

        var actorName = await ReadActorNameAsync(connection, transaction, actorUserId, cancellationToken);
        var snapshot = BuildSnapshot(context, items, responses, photos, result!, reason!, actorName);
        var snapshotHash = Hash(Encoding.UTF8.GetBytes(snapshot));
        Guid? pendingId = null;
        long? pendingNumber = null;
        if (result == "Failed")
        {
            if (context.LinkedPendingId is not null)
            {
                pendingId = context.LinkedPendingId;
                await pendingStore.ReopenQualityIssueAfterFailedReinspectionAsync(
                    connection, transaction, context.LinkedPendingId.Value, actorUserId,
                    $"{StageLabel(context.StageCode)} {context.AttemptNumber}차 재검사 부적합: {reason}",
                    correlationId, cancellationToken);
            }
            else
            {
                var issueType = context.StageCode is QualityInspectionStages.CustomerInspection or QualityInspectionStages.Fat
                    ? PendingIssueTypes.Punch
                    : PendingIssueTypes.Nonconformance;
                var pending = await pendingStore.CreatePanelQualityIssueAsync(
                    connection, transaction, context.ProjectId, context.PanelId, context.PanelDisplayCode,
                    StageLabel(context.StageCode), issueType, reason!, department!, request.AssigneeUserId,
                    actorUserId, correlationId, cancellationToken);
                pendingId = pending.PendingId;
                pendingNumber = pending.IssueNumber;
            }
        }
        var attemptPendingId = pendingId ?? context.LinkedPendingId;

        if (submittedResponses is not null)
        {
            var lqcDefinitionKeys = context.StageCode == QualityInspectionStages.Lqc
                ? await ReadLqcDefinitionKeysAsync(
                    connection,
                    transaction,
                    context.PanelId,
                    context.TemplateVersionId,
                    cancellationToken)
                : new Dictionary<Guid, Guid>();
            await ReplaceResponseRowsAsync(
                connection,
                transaction,
                reportId,
                submittedResponses,
                lqcDefinitionKeys,
                actorUserId,
                cancellationToken);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update panel_quality_reports
                set status = 'Finalized', version = version + 1, result = @result, reason = @reason,
                    finalized_by_user_id = @actor_id, finalized_at_utc = now(),
                    snapshot_text = @snapshot, snapshot_sha256 = @snapshot_hash,
                    pdf_status = 'Pending', pdf_error_code = null, pdf_last_attempt_at_utc = now(),
                    updated_by_user_id = @actor_id, updated_at_utc = now()
                where id = @report_id;
                update panel_quality_inspection_attempts
                set status = @result, linked_pending_issue_id = @pending_id,
                    version = version + 1, completed_by_user_id = @actor_id,
                    completed_at_utc = now(),
                    started_by_user_id = coalesce(started_by_user_id, @actor_id),
                    started_at_utc = coalesce(started_at_utc, now()),
                    updated_at_utc = now()
                where id = @attempt_id;
                update work_items
                set status = 'Completed', started_at_utc = coalesce(started_at_utc, now()),
                    completed_at_utc = coalesce(completed_at_utc, now())
                where id = @work_item_id and status in ('Requested', 'InProgress');
                """;
            command.Parameters.AddWithValue("result", result!);
            command.Parameters.AddWithValue("reason", reason!);
            command.Parameters.AddWithValue("actor_id", actorUserId);
            command.Parameters.AddWithValue("snapshot", snapshot);
            command.Parameters.AddWithValue("snapshot_hash", snapshotHash);
            command.Parameters.AddWithValue("report_id", reportId);
            command.Parameters.AddWithValue("attempt_id", context.AttemptId);
            command.Parameters.AddWithValue("work_item_id", context.WorkItemId);
            AddNullableUuid(command, "pending_id", attemptPendingId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        string? nextStage = null;
        if (result == "Passed")
        {
            foreach (var handoff in handoffs)
            {
                await EnsureNextWorkItemAsync(connection, transaction, context, handoff, actorUserId, cancellationToken);
            }
            nextStage = handoffs.FirstOrDefault()?.StageCode;
            if (context.StageCode == QualityInspectionStages.Lqc)
            {
                var oqcHandoff = await TryOpenOqcAfterManufacturingAndLqcAsync(
                    connection,
                    transaction,
                    context.ProjectId,
                    context.PanelId,
                    actorUserId,
                    request.OperationId,
                    cancellationToken);
                if (oqcHandoff.ConflictMessage is not null)
                {
                    return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Conflict(oqcHandoff.ConflictMessage);
                }
                if (oqcHandoff.Opened)
                {
                    nextStage = QualityInspectionStages.Oqc;
                }
            }
            if (context.LinkedPendingId is not null)
            {
                await pendingStore.ClosePanelQualityIssueAsync(
                    connection, transaction, context.LinkedPendingId.Value, actorUserId,
                    $"{StageLabel(context.StageCode)} 재검사 합격", correlationId, cancellationToken);
            }
            if (handoffs.Any(handoff => handoff.StageCode == "PackingCompleted"))
            {
                await AdvancePanelStageAsync(connection, transaction, context.PanelId, "InspectionCompleted", cancellationToken);
            }
            await EnsureProjectStageEventIfCompleteAsync(
                connection, transaction, context.ProjectId, context.StageCode, context.AttemptId,
                request.OperationId, actorUserId, cancellationToken);
        }

        var response = new QualityInspectionMutationResponse(
            request.OperationId, context.ProjectId, context.PanelId, context.StageCode, context.AttemptId,
            reportId, result!, context.ReportVersion + 1, attemptPendingId, pendingNumber, nextStage, false);
        await InsertOperationAsync(connection, transaction, request.OperationId, "Finalize", context.ProjectId, context.PanelId, context.StageCode, context.AttemptId, actorUserId, fingerprint, response, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await TryGeneratePdfAsync(reportId, cancellationToken);
        return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Success(response);
    }

    internal static async Task<Guid?> EnsurePendingReinspectionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid pendingId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using (var existingCommand = connection.CreateCommand())
        {
            existingCommand.Transaction = transaction;
            existingCommand.CommandText = """
                select id
                from panel_quality_inspection_attempts
                where linked_pending_issue_id = @pending_id
                  and status in ('Requested', 'InProgress')
                order by attempt_number desc
                limit 1;
                """;
            existingCommand.Parameters.AddWithValue("pending_id", pendingId);
            if (await existingCommand.ExecuteScalarAsync(cancellationToken) is Guid existingAttemptId)
            {
                return existingAttemptId;
            }
        }

        Guid projectId;
        Guid panelId;
        string stageCode;
        int attemptNumber;
        string panelDisplayCode;
        Guid templateVersionId;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select attempt.project_id, attempt.panel_id, attempt.stage_code,
                       attempt.attempt_number + 1, panel.display_code, report.template_version_id
                from panel_quality_inspection_attempts attempt
                join panel_placeholders panel on panel.id = attempt.panel_id and panel.status = 'Active'
                join panel_quality_reports report on report.attempt_id = attempt.id
                where attempt.linked_pending_issue_id = @pending_id
                  and attempt.status = 'Failed'
                order by attempt.attempt_number desc
                limit 1
                for update of attempt, panel;
                """;
            command.Parameters.AddWithValue("pending_id", pendingId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            projectId = reader.GetGuid(0);
            panelId = reader.GetGuid(1);
            stageCode = reader.GetString(2);
            attemptNumber = reader.GetInt32(3);
            panelDisplayCode = reader.GetString(4);
            templateVersionId = reader.GetGuid(5);
        }

        var assignee = await ResolveAssigneeAsync(connection, transaction, projectId, stageCode, cancellationToken)
            ?? throw new InvalidOperationException("품질 재검사 담당자를 지정한 뒤 조치를 완료해 주세요.");
        var workItemId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var key = $"quality:pending:{pendingId}:reinspection:{attemptNumber}";
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into work_items (
                    id, project_id, target_type, target_id, workflow_stage_code, responsibility_type,
                    assigned_user_id, assigned_role_code, title, description, status, priority,
                    idempotency_key, created_by_user_id
                ) values (
                    @work_id, @project_id, 'Panel', @panel_id, @stage_code, @responsibility,
                    @assignee_id, @role_code, @title, @description, 'Requested', 'Blocking',
                    @key, @actor_id
                );
                insert into panel_quality_inspection_attempts (
                    id, project_id, panel_id, stage_code, attempt_number, status, work_item_id,
                    linked_pending_issue_id, decision_mode, version
                ) values (
                    @attempt_id, @project_id, @panel_id, @stage_code, @attempt_number, 'Requested', @work_id,
                    @pending_id, @decision_mode, 1
                );
                insert into panel_quality_reports (
                    id, attempt_id, template_version_id, status, version,
                    created_by_user_id, updated_by_user_id
                ) values (@report_id, @attempt_id, @template_id, 'Draft', 1, @actor_id, @actor_id);
                """;
            command.Parameters.AddWithValue("work_id", workItemId);
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("panel_id", panelId);
            command.Parameters.AddWithValue("stage_code", stageCode);
            command.Parameters.AddWithValue("responsibility", assignee.Responsibility);
            command.Parameters.AddWithValue("assignee_id", assignee.UserId);
            AddNullableText(command, "role_code", assignee.RoleCode);
            command.Parameters.AddWithValue("title", $"{StageLabel(stageCode)} 재검사 · {panelDisplayCode}");
            command.Parameters.AddWithValue("description", "연결된 Pending 조치가 완료되었습니다. 재검사를 진행해 주세요.");
            command.Parameters.AddWithValue("key", key);
            command.Parameters.AddWithValue("actor_id", actorUserId);
            command.Parameters.AddWithValue("attempt_id", attemptId);
            command.Parameters.AddWithValue("attempt_number", attemptNumber);
            command.Parameters.AddWithValue("pending_id", pendingId);
            command.Parameters.AddWithValue("decision_mode", DecisionMode(stageCode));
            command.Parameters.AddWithValue("report_id", reportId);
            command.Parameters.AddWithValue("template_id", templateVersionId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await WorkAssignmentNotificationWriter.UpsertAsync(
            connection, transaction, projectId, workItemId, assignee.UserId,
            SecondaryResponsibilities(stageCode),
            $"{StageLabel(stageCode)} 재검사 · {panelDisplayCode}",
            "Pending 조치가 완료되어 재검사를 요청했습니다.",
            $"/quality/inspections?stage={stageCode}&project={projectId}&panel={panelId}",
            $"{key}:notification", cancellationToken);
        return attemptId;
    }

    internal static async Task<(bool Opened, string? ConflictMessage)> TryOpenOqcAfterManufacturingAndLqcAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid panelId,
        Guid actorUserId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        Guid? lqcAttemptId;
        string panelDisplayCode;
        bool fatRequired;
        bool manufacturingCompleted;
        await using (var readiness = connection.CreateCommand())
        {
            readiness.Transaction = transaction;
            readiness.CommandText = """
                select panel.display_code,
                       project.fat_required,
                       (
                           select latest.id
                           from (
                               select attempt.id, attempt.status
                               from panel_quality_inspection_attempts attempt
                               where attempt.panel_id = panel.id
                                 and attempt.stage_code = 'LQC'
                               order by attempt.attempt_number desc, attempt.created_at_utc desc
                               limit 1
                           ) latest
                           where latest.status = 'Passed'
                       ) as lqc_attempt_id,
                       exists (
                           select 1
                           from panel_manufacturing_executions execution
                           where execution.panel_id = panel.id
                             and execution.status = 'Completed'
                       ) as manufacturing_completed
                from panel_placeholders panel
                join projects project on project.id = panel.project_id
                where panel.id = @panel_id
                  and panel.project_id = @project_id
                  and panel.status = 'Active'
                  and project.deleted_at_utc is null
                  and project.status <> 'Cancelled';
                """;
            readiness.Parameters.AddWithValue("panel_id", panelId);
            readiness.Parameters.AddWithValue("project_id", projectId);
            await using var reader = await readiness.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return (false, "활성 패널 정보를 다시 확인해 주세요.");
            }
            panelDisplayCode = reader.GetString(0);
            fatRequired = reader.GetBoolean(1);
            lqcAttemptId = reader.IsDBNull(2) ? null : reader.GetGuid(2);
            manufacturingCompleted = reader.GetBoolean(3);
        }

        if (lqcAttemptId is null || !manufacturingCompleted)
        {
            return (false, null);
        }

        var oqcAssignee = await ResolveAssigneeAsync(
            connection,
            transaction,
            projectId,
            QualityInspectionStages.Oqc,
            cancellationToken);
        if (oqcAssignee is null)
        {
            return (false, "OQC 담당자를 지정한 뒤 다시 시도해 주세요.");
        }

        Guid manufacturingUserId = actorUserId;
        string? manufacturingRoleCode = null;
        await using (var assignee = connection.CreateCommand())
        {
            assignee.Transaction = transaction;
            assignee.CommandText = """
                select assigned_user_id, assigned_role_code
                from work_items
                where project_id = @project_id
                  and target_type = 'Panel'
                  and target_id = @panel_id
                  and workflow_stage_code = 'ManufacturingWork'
                  and status <> 'Cancelled'
                order by created_at_utc desc, id desc
                limit 1;
                """;
            assignee.Parameters.AddWithValue("project_id", projectId);
            assignee.Parameters.AddWithValue("panel_id", panelId);
            await using var reader = await assignee.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                manufacturingUserId = reader.GetGuid(0);
                manufacturingRoleCode = reader.IsDBNull(1) ? null : reader.GetString(1);
            }
        }

        var confirmationKey = $"quality:panel:{panelId}:manufacturing-completed";
        await using (var confirmationWork = connection.CreateCommand())
        {
            confirmationWork.Transaction = transaction;
            confirmationWork.CommandText = """
                insert into work_items (
                    project_id, target_type, target_id, workflow_stage_code, responsibility_type,
                    assigned_user_id, assigned_role_code, title, description, status, priority,
                    idempotency_key, created_by_user_id, started_at_utc, completed_at_utc
                ) values (
                    @project_id, 'Panel', @panel_id, 'ManufacturingCompleted', 'ManufacturingPrimary',
                    @assignee_id, @role_code, @title, @description, 'Completed', 'Normal',
                    @key, @actor_id, now(), now()
                )
                on conflict (idempotency_key) do update
                set status = 'Completed',
                    started_at_utc = coalesce(work_items.started_at_utc, now()),
                    completed_at_utc = coalesce(work_items.completed_at_utc, now());
                """;
            confirmationWork.Parameters.AddWithValue("project_id", projectId);
            confirmationWork.Parameters.AddWithValue("panel_id", panelId);
            confirmationWork.Parameters.AddWithValue("assignee_id", manufacturingUserId);
            AddNullableText(confirmationWork, "role_code", manufacturingRoleCode);
            confirmationWork.Parameters.AddWithValue("title", $"제조·LQC 완료 자동 확인 · {panelDisplayCode}");
            confirmationWork.Parameters.AddWithValue("description", "패널 제조와 LQC가 모두 완료되어 OQC로 자동 인계했습니다.");
            confirmationWork.Parameters.AddWithValue("key", confirmationKey);
            confirmationWork.Parameters.AddWithValue("actor_id", actorUserId);
            await confirmationWork.ExecuteNonQueryAsync(cancellationToken);
        }

        Guid confirmationWorkItemId;
        await using (var readWork = connection.CreateCommand())
        {
            readWork.Transaction = transaction;
            readWork.CommandText = "select id from work_items where idempotency_key = @key;";
            readWork.Parameters.AddWithValue("key", confirmationKey);
            confirmationWorkItemId = (Guid)(await readWork.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("Manufacturing confirmation work item was not created."));
        }

        await using (var confirmation = connection.CreateCommand())
        {
            confirmation.Transaction = transaction;
            confirmation.CommandText = """
                insert into panel_manufacturing_completion_confirmations (
                    project_id, panel_id, lqc_attempt_id, work_item_id, confirmed_by_user_id
                ) values (
                    @project_id, @panel_id, @lqc_attempt_id, @work_item_id, @confirmed_by
                )
                on conflict (panel_id) do nothing;
                """;
            confirmation.Parameters.AddWithValue("project_id", projectId);
            confirmation.Parameters.AddWithValue("panel_id", panelId);
            confirmation.Parameters.AddWithValue("lqc_attempt_id", lqcAttemptId.Value);
            confirmation.Parameters.AddWithValue("work_item_id", confirmationWorkItemId);
            confirmation.Parameters.AddWithValue("confirmed_by", manufacturingUserId);
            await confirmation.ExecuteNonQueryAsync(cancellationToken);
        }

        var context = new HandoffContext(
            projectId,
            panelId,
            panelDisplayCode,
            fatRequired,
            QualityInspectionStages.ManufacturingCompleted,
            lqcAttemptId.Value,
            1);
        await EnsureNextWorkItemAsync(
            connection,
            transaction,
            context,
            oqcAssignee,
            actorUserId,
            cancellationToken);
        await AdvancePanelStageAsync(connection, transaction, panelId, "InspectionInProgress", cancellationToken);
        await EnsureProjectConfirmationEventIfCompleteAsync(
            connection,
            transaction,
            projectId,
            lqcAttemptId.Value,
            operationId,
            actorUserId,
            cancellationToken);
        return (true, null);
    }

    public async Task<QualityInspectionMutationResult<QualityInspectionMutationResponse>> RequestReinspectionAsync(
        RequestQualityReinspectionRequest request,
        Guid actorUserId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        if (request.OperationId == Guid.Empty || request.PendingId == Guid.Empty || request.ExpectedPendingVersion is null or < 1)
        {
            return Validation("pendingId", "재검사 Pending과 최신 version을 확인해 주세요.");
        }
        var fingerprint = Fingerprint("RequestReinspection", request.PendingId, request.ExpectedPendingVersion.Value);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var replay = await ReadReplayAsync(connection, transaction, request.OperationId, "RequestReinspection", fingerprint, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        var failed = await LockFailedAttemptByPendingAsync(connection, transaction, request.PendingId, accessScope, cancellationToken);
        if (failed is null) return QualityInspectionMutationResult<QualityInspectionMutationResponse>.NotFound();
        if (!await HasStageAccessAsync(connection, transaction, failed.ProjectId, failed.WorkItemId, failed.StageCode, actorUserId, false, cancellationToken))
        {
            return QualityInspectionMutationResult<QualityInspectionMutationResponse>.NotFound();
        }
        if (failed.PendingStatus != PendingStatuses.ReinspectionRequested || failed.PendingVersion != request.ExpectedPendingVersion)
        {
            return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Conflict("재검사 요청 상태와 최신 Pending version을 확인해 주세요.");
        }
        var attemptId = await EnsurePendingReinspectionAsync(connection, transaction, request.PendingId, actorUserId, cancellationToken)
            ?? throw new InvalidOperationException("연결된 품질검사 Pending을 찾을 수 없습니다.");
        Guid reportId;
        string attemptStatus;
        int reportVersion;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select report.id, attempt.status, report.version
                from panel_quality_inspection_attempts attempt
                join panel_quality_reports report on report.attempt_id = attempt.id
                where attempt.id = @attempt_id;
                """;
            command.Parameters.AddWithValue("attempt_id", attemptId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return QualityInspectionMutationResult<QualityInspectionMutationResponse>.NotFound();
            }
            reportId = reader.GetGuid(0);
            attemptStatus = reader.GetString(1);
            reportVersion = reader.GetInt32(2);
        }
        var response = new QualityInspectionMutationResponse(
            request.OperationId, failed.ProjectId, failed.PanelId, failed.StageCode, attemptId, reportId,
            attemptStatus, reportVersion, request.PendingId, failed.PendingNumber, null, false);
        await InsertOperationAsync(connection, transaction, request.OperationId, "RequestReinspection", failed.ProjectId, failed.PanelId, failed.StageCode, attemptId, actorUserId, fingerprint, response, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Success(response);
    }

    public async Task<QualityInspectionMutationResult<QualityInspectionMutationResponse>> ConfirmManufacturingCompletedAsync(
        ConfirmPanelManufacturingCompletedRequest request,
        Guid actorUserId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        if (request.OperationId == Guid.Empty || request.ProjectId == Guid.Empty || request.PanelId == Guid.Empty)
        {
            return Validation("panelId", "확인할 panel과 요청 식별자가 필요합니다.");
        }
        var fingerprint = Fingerprint("ConfirmManufacturingCompleted", request.ProjectId, request.PanelId);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var replay = await ReadReplayAsync(connection, transaction, request.OperationId, "ConfirmManufacturingCompleted", fingerprint, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        var project = await LockProjectAsync(connection, transaction, request.ProjectId, accessScope, cancellationToken);
        if (project is null) return QualityInspectionMutationResult<QualityInspectionMutationResponse>.NotFound();
        var work = await LockStageWorkAsync(connection, transaction, request.ProjectId, request.PanelId, QualityInspectionStages.ManufacturingCompleted, cancellationToken);
        if (work is null) return QualityInspectionMutationResult<QualityInspectionMutationResponse>.NotFound();
        if (!await HasStageAccessAsync(connection, transaction, request.ProjectId, work.WorkItemId, QualityInspectionStages.ManufacturingCompleted, actorUserId, true, cancellationToken))
        {
            return QualityInspectionMutationResult<QualityInspectionMutationResponse>.NotFound();
        }
        var lqcAttemptId = await ReadLatestPassedAttemptAsync(connection, transaction, request.PanelId, QualityInspectionStages.Lqc, cancellationToken);
        if (lqcAttemptId is null) return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Conflict("LQC 합격 성적서를 먼저 확정해 주세요.");
        var context = new HandoffContext(request.ProjectId, request.PanelId, work.PanelDisplayCode, project.FatRequired, QualityInspectionStages.ManufacturingCompleted, lqcAttemptId.Value, 1);
        var next = await ResolveAssigneeAsync(connection, transaction, request.ProjectId, QualityInspectionStages.Oqc, cancellationToken);
        if (next is null) return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Conflict("OQC 담당자를 지정한 뒤 다시 시도해 주세요.");

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into panel_manufacturing_completion_confirmations (
                    project_id, panel_id, lqc_attempt_id, work_item_id, confirmed_by_user_id
                ) values (@project_id, @panel_id, @attempt_id, @work_item_id, @actor_id)
                on conflict (panel_id) do nothing;
                update work_items
                set status = 'Completed', started_at_utc = coalesce(started_at_utc, now()),
                    completed_at_utc = coalesce(completed_at_utc, now())
                where id = @work_item_id and status in ('Requested', 'InProgress');
                """;
            command.Parameters.AddWithValue("project_id", request.ProjectId);
            command.Parameters.AddWithValue("panel_id", request.PanelId);
            command.Parameters.AddWithValue("attempt_id", lqcAttemptId.Value);
            command.Parameters.AddWithValue("work_item_id", work.WorkItemId);
            command.Parameters.AddWithValue("actor_id", actorUserId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await EnsureNextWorkItemAsync(connection, transaction, context, next, actorUserId, cancellationToken);
        await EnsureProjectConfirmationEventIfCompleteAsync(connection, transaction, request.ProjectId, lqcAttemptId.Value, request.OperationId, actorUserId, cancellationToken);
        var response = new QualityInspectionMutationResponse(
            request.OperationId, request.ProjectId, request.PanelId, QualityInspectionStages.ManufacturingCompleted,
            lqcAttemptId, null, "Confirmed", 1, null, null, QualityInspectionStages.Oqc, false);
        await InsertOperationAsync(connection, transaction, request.OperationId, "ConfirmManufacturingCompleted", request.ProjectId, request.PanelId, QualityInspectionStages.ManufacturingCompleted, lqcAttemptId, actorUserId, fingerprint, response, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Success(response);
    }

    public async Task<QualityInspectionMutationResult<QualityInspectionPdfDownloadResult>> GetPdfAsync(
        Guid reportId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select report.pdf_status, report.pdf_error_code, artifact.content
            from panel_quality_reports report
            join panel_quality_inspection_attempts attempt on attempt.id = report.attempt_id
            join projects project on project.id = attempt.project_id
            left join panel_quality_report_pdf_artifacts artifact on artifact.report_id = report.id
            where report.id = @report_id
              and (@has_read_all or project.project_key = any(@project_keys));
            """);
        command.Parameters.AddWithValue("report_id", reportId);
        AddScope(command, accessScope);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return QualityInspectionMutationResult<QualityInspectionPdfDownloadResult>.NotFound();
        return QualityInspectionMutationResult<QualityInspectionPdfDownloadResult>.Success(new QualityInspectionPdfDownloadResult(
            reader.IsDBNull(0) ? "Pending" : reader.GetString(0),
            reader.IsDBNull(2) ? null : (byte[])reader[2],
            reader.IsDBNull(1) ? null : reader.GetString(1)));
    }

    public async Task<QualityInspectionMutationResult<QualityInspectionPhotoContentResult>> GetPhotoContentAsync(
        Guid reportId,
        Guid photoId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select photo.content, photo.normalized_mime, photo.display_name
            from panel_quality_report_photos photo
            join panel_quality_reports report on report.id = photo.report_id
            join panel_quality_inspection_attempts attempt on attempt.id = report.attempt_id
            join projects project on project.id = attempt.project_id
            where report.id = @report_id and photo.id = @photo_id
              and project.deleted_at_utc is null
              and (@has_read_all or project.project_key = any(@project_keys));
            """);
        command.Parameters.AddWithValue("report_id", reportId);
        command.Parameters.AddWithValue("photo_id", photoId);
        AddScope(command, accessScope);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? QualityInspectionMutationResult<QualityInspectionPhotoContentResult>.Success(new QualityInspectionPhotoContentResult(
                reader.GetFieldValue<byte[]>(0), reader.GetString(1), reader.GetString(2)))
            : QualityInspectionMutationResult<QualityInspectionPhotoContentResult>.NotFound();
    }

    public async Task<QualityInspectionMutationResult<QualityInspectionMutationResponse>> RetryPdfAsync(
        Guid reportId,
        Guid operationId,
        Guid actorUserId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        if (operationId == Guid.Empty) return Validation("operationId", "요청 식별자가 필요합니다.");
        var fingerprint = Fingerprint("RetryPdf", reportId);
        QualityInspectionMutationResponse response;
        var shouldGenerate = false;
        await using (var dataSource = CreateDataSource())
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var replay = await ReadReplayAsync(connection, transaction, operationId, "RetryPdf", fingerprint, cancellationToken);
            if (replay.Result is not null) return replay.Result;
            var context = await LockReportContextAsync(connection, transaction, reportId, accessScope, cancellationToken);
            if (context is null) return QualityInspectionMutationResult<QualityInspectionMutationResponse>.NotFound();
            if (!await HasStageAccessAsync(connection, transaction, context.ProjectId, context.WorkItemId, context.StageCode, actorUserId, false, cancellationToken))
            {
                return QualityInspectionMutationResult<QualityInspectionMutationResponse>.NotFound();
            }
            if (context.ReportStatus != "Finalized") return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Conflict("확정된 성적서만 PDF를 다시 만들 수 있습니다.");
            string? pdfStatus;
            await using (var read = connection.CreateCommand())
            {
                read.Transaction = transaction;
                read.CommandText = "select pdf_status from panel_quality_reports where id = @report_id;";
                read.Parameters.AddWithValue("report_id", reportId);
                pdfStatus = (string?)await read.ExecuteScalarAsync(cancellationToken);
            }
            if (pdfStatus == "Pending") return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Conflict("PDF 생성이 진행 중입니다.");
            shouldGenerate = pdfStatus == "Failed";
            if (shouldGenerate)
            {
                await using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = """
                    update panel_quality_reports
                    set pdf_status = 'Pending', pdf_error_code = null, pdf_last_attempt_at_utc = now(), updated_at_utc = now()
                    where id = @report_id and pdf_status = 'Failed';
                    """;
                update.Parameters.AddWithValue("report_id", reportId);
                await update.ExecuteNonQueryAsync(cancellationToken);
            }
            response = new QualityInspectionMutationResponse(
                operationId, context.ProjectId, context.PanelId, context.StageCode, context.AttemptId,
                reportId, shouldGenerate ? "PdfRetryRequested" : "PdfReady", context.ReportVersion,
                context.LinkedPendingId, null, null, false);
            await InsertOperationAsync(connection, transaction, operationId, "RetryPdf", context.ProjectId, context.PanelId, context.StageCode, context.AttemptId, actorUserId, fingerprint, response, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        if (shouldGenerate) await TryGeneratePdfAsync(reportId, cancellationToken);
        return QualityInspectionMutationResult<QualityInspectionMutationResponse>.Success(response);
    }

    private async Task TryGeneratePdfAsync(Guid reportId, CancellationToken cancellationToken)
    {
        try
        {
            await using var dataSource = CreateDataSource();
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            string snapshot;
            string snapshotHash;
            await using (var read = connection.CreateCommand())
            {
                read.CommandText = "select snapshot_text, snapshot_sha256 from panel_quality_reports where id = @id and status = 'Finalized';";
                read.Parameters.AddWithValue("id", reportId);
                await using var reader = await read.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken)) return;
                snapshot = reader.GetString(0);
                snapshotHash = reader.GetString(1);
            }
            var content = pdfRenderer.Render(snapshot);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                insert into panel_quality_report_pdf_artifacts (
                    report_id, snapshot_sha256, byte_size, sha256, content, generator
                ) values (@report_id, @snapshot_hash, @size, @sha, @content, 'PDFsharp-6.2.4')
                on conflict (report_id) do nothing;
                update panel_quality_reports
                set pdf_status = 'Ready', pdf_error_code = null, pdf_last_attempt_at_utc = now(), updated_at_utc = now()
                where id = @report_id and snapshot_sha256 = @snapshot_hash;
                """;
            command.Parameters.AddWithValue("report_id", reportId);
            command.Parameters.AddWithValue("snapshot_hash", snapshotHash);
            command.Parameters.AddWithValue("size", content.Length);
            command.Parameters.AddWithValue("sha", Hash(content));
            command.Parameters.Add("content", NpgsqlDbType.Bytea).Value = content;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            await using var dataSource = CreateDataSource();
            await using var command = dataSource.CreateCommand("""
                update panel_quality_reports
                set pdf_status = 'Failed', pdf_error_code = 'render_failed',
                    pdf_last_attempt_at_utc = now(), updated_at_utc = now()
                where id = @id and status = 'Finalized' and pdf_status <> 'Ready';
                """);
            command.Parameters.AddWithValue("id", reportId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<ReportView?> ReadReportViewAsync(NpgsqlConnection connection, Guid attemptId, CancellationToken cancellationToken)
    {
        Guid reportId;
        string status;
        int version;
        string? result;
        string? reason;
        string? pdfStatus;
        string decisionMode;
        Guid templateVersionId;
        Guid panelId;
        string stageCode;
        int attemptNumber;
        Guid? linkedPendingId;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select report.id, report.status, report.version, report.result, report.reason,
                       report.pdf_status, report.template_version_id, attempt.decision_mode,
                       attempt.panel_id, attempt.stage_code, attempt.attempt_number,
                       attempt.linked_pending_issue_id
                from panel_quality_reports report
                join panel_quality_inspection_attempts attempt on attempt.id = report.attempt_id
                where report.attempt_id = @attempt_id;
                """;
            command.Parameters.AddWithValue("attempt_id", attemptId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            reportId = reader.GetGuid(0);
            status = reader.GetString(1);
            version = reader.GetInt32(2);
            result = reader.IsDBNull(3) ? null : reader.GetString(3);
            reason = reader.IsDBNull(4) ? null : reader.GetString(4);
            pdfStatus = reader.IsDBNull(5) ? null : reader.GetString(5);
            templateVersionId = reader.GetGuid(6);
            decisionMode = reader.GetString(7);
            panelId = reader.GetGuid(8);
            stageCode = reader.GetString(9);
            attemptNumber = reader.GetInt32(10);
            linkedPendingId = reader.IsDBNull(11) ? null : reader.GetGuid(11);
        }
        var allItems = await ReadTemplateRowsAsync(connection, null, templateVersionId, cancellationToken);
        var reinspectionScope = await ReadReinspectionScopeAsync(
            connection, null, templateVersionId, linkedPendingId, stageCode, attemptNumber, cancellationToken,
            allowInitialFailedAttempt: string.Equals(result, "Failed", StringComparison.Ordinal));
        IReadOnlyList<QualityInspectionTemplateItemResponse> itemResponses = allItems
            .Select(item => item.ToResponse(reinspectionScope))
            .ToList();
        if (stageCode == QualityInspectionStages.Lqc && itemResponses.Count > 0)
        {
            itemResponses = await ApplyLqcAvailabilityAsync(
                connection, null, panelId, itemResponses, cancellationToken);
        }
        var isCompletedReinspection = reinspectionScope is not null
            && string.Equals(status, "Finalized", StringComparison.Ordinal)
            && string.Equals(result, "Passed", StringComparison.Ordinal);
        if (reinspectionScope is not null && !isCompletedReinspection)
        {
            itemResponses = itemResponses
                .Where(item => reinspectionScope.EvidenceByItemId.ContainsKey(item.ItemId))
                .ToList();
        }
        List<ResponseRow> responses;
        List<QualityInspectionPhotoResponse> photos;
        if (isCompletedReinspection && linkedPendingId is not null)
        {
            responses = await ReadEffectiveReinspectionResponsesAsync(
                connection, linkedPendingId.Value, stageCode, attemptNumber, templateVersionId, cancellationToken);
            photos = await ReadReinspectionPhotosAsync(
                connection, linkedPendingId.Value, stageCode, attemptNumber, templateVersionId, cancellationToken);
        }
        else
        {
            responses = await ReadResponseRowsAsync(connection, null, reportId, cancellationToken);
            photos = await ReadReportPhotosAsync(connection, reportId, cancellationToken);
            if (reinspectionScope is not null)
            {
                var allowedItemIds = reinspectionScope.EvidenceByItemId.Keys.ToHashSet();
                responses = responses.Where(response => allowedItemIds.Contains(response.TemplateItemId)).ToList();
                photos = photos.Where(photo => allowedItemIds.Contains(photo.TemplateItemId)).ToList();
            }
        }
        return new ReportView(reportId, status, version, result, reason, pdfStatus, decisionMode,
            itemResponses,
            responses.Select(item => item.ToResponse()).ToList(), photos);
    }

    private static async Task<List<ResponseRow>> ReadEffectiveReinspectionResponsesAsync(
        NpgsqlConnection connection,
        Guid linkedPendingId,
        string stageCode,
        int attemptNumber,
        Guid currentTemplateVersionId,
        CancellationToken cancellationToken)
    {
        var result = new List<ResponseRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select distinct on (current_item.id)
                   current_item.id, response.check_result, response.text_value, response.note
            from panel_quality_inspection_attempts attempt
            join panel_quality_reports report
              on report.attempt_id = attempt.id
             and report.status = 'Finalized'
            join panel_quality_report_responses response on response.report_id = report.id
            join panel_quality_template_items source_item on source_item.id = response.template_item_id
            join panel_quality_template_items current_item
              on current_item.template_version_id = @template_version_id
             and current_item.item_code = source_item.item_code
            where attempt.linked_pending_issue_id = @pending_id
              and attempt.stage_code = @stage_code
              and attempt.attempt_number <= @attempt_number
            order by current_item.id, attempt.attempt_number desc, response.updated_at_utc desc;
            """;
        command.Parameters.AddWithValue("pending_id", linkedPendingId);
        command.Parameters.AddWithValue("stage_code", stageCode);
        command.Parameters.AddWithValue("attempt_number", attemptNumber);
        command.Parameters.AddWithValue("template_version_id", currentTemplateVersionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ResponseRow(
                reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }
        return result;
    }

    private static async Task<List<QualityInspectionPhotoResponse>> ReadReinspectionPhotosAsync(
        NpgsqlConnection connection,
        Guid linkedPendingId,
        string stageCode,
        int attemptNumber,
        Guid currentTemplateVersionId,
        CancellationToken cancellationToken)
    {
        var result = new List<QualityInspectionPhotoResponse>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select photo.id, current_item.id, photo.display_name, photo.normalized_mime,
                   photo.byte_size, photo.alt_text, photo.created_at_utc
            from panel_quality_inspection_attempts attempt
            join panel_quality_reports report
              on report.attempt_id = attempt.id
             and report.status = 'Finalized'
            join panel_quality_report_photos photo on photo.report_id = report.id
            join panel_quality_template_items source_item on source_item.id = photo.template_item_id
            join panel_quality_template_items current_item
              on current_item.template_version_id = @template_version_id
             and current_item.item_code = source_item.item_code
            where attempt.linked_pending_issue_id = @pending_id
              and attempt.stage_code = @stage_code
              and attempt.attempt_number <= @attempt_number
            order by photo.created_at_utc, photo.id;
            """;
        command.Parameters.AddWithValue("pending_id", linkedPendingId);
        command.Parameters.AddWithValue("stage_code", stageCode);
        command.Parameters.AddWithValue("attempt_number", attemptNumber);
        command.Parameters.AddWithValue("template_version_id", currentTemplateVersionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new QualityInspectionPhotoResponse(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3),
                reader.GetInt32(4), reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6)));
        }
        return result;
    }

    private static async Task<List<QualityInspectionPhotoResponse>> ReadReportPhotosAsync(
        NpgsqlConnection connection,
        Guid reportId,
        CancellationToken cancellationToken)
    {
        var result = new List<QualityInspectionPhotoResponse>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, template_item_id, display_name, normalized_mime, byte_size, alt_text, created_at_utc
            from panel_quality_report_photos where report_id = @report_id order by created_at_utc, id;
            """;
        command.Parameters.AddWithValue("report_id", reportId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new QualityInspectionPhotoResponse(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3),
                reader.GetInt32(4), reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6)));
        }
        return result;
    }

    private static async Task<IReadOnlyList<QualityInspectionAttemptHistoryResponse>> ReadHistoryAsync(
        NpgsqlConnection connection, Guid panelId, string stageCode, CancellationToken cancellationToken)
    {
        var result = new List<QualityInspectionAttemptHistoryResponse>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select attempt.id, attempt.attempt_number, attempt.status, attempt.linked_pending_issue_id,
                   pending.issue_number, attempt.completed_at_utc
            from panel_quality_inspection_attempts attempt
            left join pending_issues pending on pending.id = attempt.linked_pending_issue_id
            where attempt.panel_id = @panel_id and attempt.stage_code = @stage_code
            order by attempt.attempt_number desc;
            """;
        command.Parameters.AddWithValue("panel_id", panelId);
        command.Parameters.AddWithValue("stage_code", stageCode);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new QualityInspectionAttemptHistoryResponse(
                reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetGuid(3),
                reader.IsDBNull(4) ? null : reader.GetInt64(4),
                reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5)));
        }
        return result;
    }

    private static async Task<ProjectSnapshot?> LockProjectAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId,
        ProjectAccessScope accessScope, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, project_code, project_title, fat_required
            from projects
            where id = @project_id and deleted_at_utc is null and status <> 'Cancelled'
              and (@has_read_all or project_key = any(@project_keys))
            for update;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        AddScope(command, accessScope);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ProjectSnapshot(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3))
            : null;
    }

    private static async Task<StageWorkSnapshot?> LockStageWorkAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, Guid panelId,
        string stageCode, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select panel.display_code, panel.panel_name, panel.workflow_stage,
                   work_item.id, work_item.status, work_item.assigned_user_id
            from panel_placeholders panel
            join lateral (
                select candidate.* from work_items candidate
                where candidate.project_id = @project_id
                  and candidate.target_type = 'Panel' and candidate.target_id = panel.id
                  and candidate.workflow_stage_code = @stage_code
                  and candidate.status <> 'Cancelled'
                order by case candidate.status when 'InProgress' then 0 when 'Requested' then 1 else 2 end,
                         candidate.created_at_utc desc, candidate.id desc
                limit 1
            ) work_item on true
            where panel.id = @panel_id and panel.project_id = @project_id and panel.status = 'Active'
            for update of panel, work_item;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("panel_id", panelId);
        command.Parameters.AddWithValue("stage_code", stageCode);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new StageWorkSnapshot(
                reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetString(2),
                reader.GetGuid(3), reader.GetString(4), reader.GetGuid(5))
            : null;
    }

    private static async Task<bool> HasStageAccessAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, Guid workItemId,
        string stageCode, Guid actorUserId, bool manufacturing, CancellationToken cancellationToken)
    {
        var responsibilities = Responsibilities(stageCode);
        var permission = manufacturing ? "manufacturing.update" : "quality.inspect";
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists (
                select 1 from qms_users actor
                where actor.id = @actor_id and actor.is_active = true
                  and exists (
                    select 1 from user_roles user_role
                    join role_permissions role_permission on role_permission.role_id = user_role.role_id
                    join permissions permission on permission.id = role_permission.permission_id
                    where user_role.user_id = actor.id and permission.code = @permission
                  )
                  and (
                    exists (select 1 from work_items where id = @work_item_id and assigned_user_id = actor.id and status in ('Requested', 'InProgress'))
                    or exists (
                        select 1 from project_assignees assignee
                        where assignee.project_id = @project_id and assignee.assigned_user_id = actor.id
                          and assignee.responsibility_type = any(@responsibilities)
                    )
                  )
            );
            """;
        command.Parameters.AddWithValue("actor_id", actorUserId);
        command.Parameters.AddWithValue("permission", permission);
        command.Parameters.AddWithValue("work_item_id", workItemId);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("responsibilities", responsibilities);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<ActiveAttemptSnapshot?> ReadActiveAttemptAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid panelId, string stageCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select attempt.id, attempt.attempt_number, attempt.version, report.id
            from panel_quality_inspection_attempts attempt
            join panel_quality_reports report on report.attempt_id = attempt.id
            where attempt.panel_id = @panel_id and attempt.stage_code = @stage_code
              and attempt.status in ('Requested', 'InProgress')
            for update of attempt, report;
            """;
        command.Parameters.AddWithValue("panel_id", panelId);
        command.Parameters.AddWithValue("stage_code", stageCode);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ActiveAttemptSnapshot(reader.GetGuid(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetGuid(3))
            : null;
    }

    private static async Task<Guid?> ReadActiveTemplateVersionAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string stageCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select id from panel_quality_template_versions where stage_code = @stage_code and is_active = true;";
        command.Parameters.AddWithValue("stage_code", stageCode);
        return (Guid?)await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task<int> ReadNextAttemptNumberAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid panelId, string stageCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select coalesce(max(attempt_number), 0) + 1 from panel_quality_inspection_attempts where panel_id = @panel_id and stage_code = @stage_code;";
        command.Parameters.AddWithValue("panel_id", panelId);
        command.Parameters.AddWithValue("stage_code", stageCode);
        return (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 1);
    }

    private static async Task<ReportContext?> LockReportContextAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid reportId,
        ProjectAccessScope accessScope, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select report.id, report.status, report.version, report.template_version_id,
                   attempt.id, attempt.project_id, attempt.panel_id, attempt.stage_code,
                   attempt.attempt_number, attempt.work_item_id, attempt.linked_pending_issue_id,
                   panel.display_code, panel.panel_name, project.project_code, project.project_title,
                   project.fat_required, attempt.decision_mode
            from panel_quality_reports report
            join panel_quality_inspection_attempts attempt on attempt.id = report.attempt_id
            join panel_placeholders panel on panel.id = attempt.panel_id and panel.status = 'Active'
            join projects project on project.id = attempt.project_id
            join work_items work_item on work_item.id = attempt.work_item_id
            where report.id = @report_id and project.deleted_at_utc is null and project.status <> 'Cancelled'
              and (@has_read_all or project.project_key = any(@project_keys))
            for update of report, attempt, panel, project, work_item;
            """;
        command.Parameters.AddWithValue("report_id", reportId);
        AddScope(command, accessScope);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ReportContext(
                reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2), reader.GetGuid(3),
                reader.GetGuid(4), reader.GetGuid(5), reader.GetGuid(6), reader.GetString(7),
                reader.GetInt32(8), reader.GetGuid(9), reader.IsDBNull(10) ? null : reader.GetGuid(10),
                reader.GetString(11), reader.IsDBNull(12) ? null : reader.GetString(12),
                reader.GetString(13), reader.GetString(14), reader.GetBoolean(15), reader.GetString(16))
            : null;
    }

    private static async Task<List<TemplateRow>> ReadTemplateRowsAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid versionId,
        CancellationToken cancellationToken)
    {
        var result = new List<TemplateRow>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, item_code, display_order, label, guidance, response_type, is_required, max_text_length
            from panel_quality_template_items where template_version_id = @version_id order by display_order;
            """;
        command.Parameters.AddWithValue("version_id", versionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new TemplateRow(
                reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5), reader.GetBoolean(6),
                reader.IsDBNull(7) ? null : reader.GetInt32(7)));
        }
        return result;
    }

    private static async Task<ReinspectionScope?> ReadReinspectionScopeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid currentTemplateVersionId,
        Guid? linkedPendingId,
        string stageCode,
        int attemptNumber,
        CancellationToken cancellationToken,
        bool allowInitialFailedAttempt = false)
    {
        if (linkedPendingId is null
            || stageCode is not (QualityInspectionStages.Lqc or QualityInspectionStages.Oqc))
        {
            return null;
        }

        var evidenceByItemId = new Dictionary<Guid, string?>();
        var previousFailedItemCount = 0;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            with previous_attempt as (
                select attempt.id
                from panel_quality_inspection_attempts attempt
                where attempt.linked_pending_issue_id = @pending_id
                  and attempt.stage_code = @stage_code
                  and attempt.status = 'Failed'
                  and attempt.attempt_number < @attempt_number
                order by attempt.attempt_number desc, attempt.created_at_utc desc
                limit 1
            ),
            failed_items as (
                select previous_item.item_code,
                       coalesce(
                           nullif(btrim(response.note), ''),
                           nullif(btrim(report.reason), ''),
                           '이전 검사에서 부적합으로 판정된 항목입니다.'
                       ) as previous_failure_evidence
                from previous_attempt
                join panel_quality_reports report on report.attempt_id = previous_attempt.id
                join panel_quality_report_responses response on response.report_id = report.id
                join panel_quality_template_items previous_item
                  on previous_item.id = response.template_item_id
                where response.check_result = 'Fail'
            )
            select current_item.id, failed_items.previous_failure_evidence,
                   count(*) over ()::int as failed_item_count
            from failed_items
            left join panel_quality_template_items current_item
              on current_item.template_version_id = @template_version_id
             and current_item.item_code = failed_items.item_code
            order by failed_items.item_code;
            """;
        command.Parameters.AddWithValue("pending_id", linkedPendingId.Value);
        command.Parameters.AddWithValue("stage_code", stageCode);
        command.Parameters.AddWithValue("attempt_number", attemptNumber);
        command.Parameters.AddWithValue("template_version_id", currentTemplateVersionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            previousFailedItemCount = reader.GetInt32(2);
            if (reader.IsDBNull(0))
            {
                continue;
            }
            evidenceByItemId[reader.GetGuid(0)] = reader.IsDBNull(1) ? null : reader.GetString(1);
        }
        if (previousFailedItemCount == 0)
        {
            if (allowInitialFailedAttempt)
            {
                return null;
            }
            throw new InvalidOperationException("재검사할 이전 부적합 항목을 찾을 수 없습니다.");
        }
        if (evidenceByItemId.Count != previousFailedItemCount)
        {
            throw new InvalidOperationException("검사 양식 변경으로 재검사 항목 일부를 복원할 수 없습니다.");
        }
        return new ReinspectionScope(evidenceByItemId);
    }

    private static IReadOnlyList<TemplateRow> ApplyReinspectionScope(
        IReadOnlyList<TemplateRow> items,
        ReinspectionScope? scope)
        => scope is null
            ? items
            : items.Where(item => scope.EvidenceByItemId.ContainsKey(item.ItemId)).ToList();

    private static async Task<IReadOnlyList<QualityInspectionTemplateItemResponse>> ApplyLqcAvailabilityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid panelId,
        IReadOnlyList<QualityInspectionTemplateItemResponse> items,
        CancellationToken cancellationToken)
    {
        var progress = await ReadLqcManufacturingProgressAsync(
            connection,
            transaction,
            panelId,
            cancellationToken);
        var checkRank = 0;
        return items
            .OrderBy(item => item.DisplayOrder)
            .Select(item =>
            {
                if (item.ResponseType != "Check") return item;
                checkRank += 1;
                var available = checkRank <= progress.AvailableStepCount;
                var manufacturingStepName = checkRank <= progress.StepNames.Count
                    ? progress.StepNames[checkRank - 1]
                    : $"제조 {checkRank}단계";
                return item with
                {
                    IsAvailable = available,
                    AvailabilityMessage = available
                        ? $"제조 단계: {manufacturingStepName} — 현재 LQC 입력 가능"
                        : $"제조 단계: {manufacturingStepName} — 단계 시작 후 입력 가능 · 현재 {progress.AvailableStepCount}/{progress.TotalStepCount}단계 개방"
                };
            })
            .ToList();
    }

    private static async Task<LqcManufacturingProgress> ReadLqcManufacturingProgressAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid panelId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select execution.status,
                   count(step.id) filter (where step.checked_at_utc is not null)::int as checked_count,
                   count(step.id)::int as total_count,
                   coalesce(
                       array_agg(step.step_name order by step.sequence_number) filter (where step.id is not null),
                       array[]::text[]
                   ) as step_names
            from panel_manufacturing_executions execution
            left join panel_manufacturing_execution_steps step on step.execution_id = execution.id
            where execution.id = (
                select candidate.id
                from panel_manufacturing_executions candidate
                where candidate.panel_id = @panel_id and candidate.status <> 'Cancelled'
                order by candidate.started_at_utc desc, candidate.id desc
                limit 1
            )
            group by execution.status;
            """;
        command.Parameters.AddWithValue("panel_id", panelId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new LqcManufacturingProgress(0, 0, []);
        }
        var status = reader.GetString(0);
        var checkedCount = reader.GetInt32(1);
        var totalCount = reader.GetInt32(2);
        var stepNames = reader.GetFieldValue<string[]>(3);
        var availableCount = status == Emi.Qms.Api.Manufacturing.ManufacturingExecutionStatuses.Completed
            ? totalCount
            : status is Emi.Qms.Api.Manufacturing.ManufacturingExecutionStatuses.InProgress
                or Emi.Qms.Api.Manufacturing.ManufacturingExecutionStatuses.Blocked
                ? Math.Min(totalCount, checkedCount + 1)
                : checkedCount;
        return new LqcManufacturingProgress(availableCount, totalCount, stepNames);
    }

    private static void AddLqcAvailabilityErrors(
        Dictionary<string, string[]> errors,
        IEnumerable<Guid> submittedItemIds,
        IReadOnlyList<TemplateRow> items,
        LqcManufacturingProgress progress)
    {
        var submitted = submittedItemIds.ToHashSet();
        var unavailableLabels = items
            .Where(item => item.ResponseType == "Check")
            .OrderBy(item => item.DisplayOrder)
            .Select((item, index) => new { Item = item, Rank = index + 1 })
            .Where(candidate => submitted.Contains(candidate.Item.ItemId)
                && candidate.Rank > progress.AvailableStepCount)
            .Select(candidate => candidate.Item.Label)
            .ToList();
        if (unavailableLabels.Count > 0)
        {
            errors["responses"] =
            [
                $"아직 제조 단계가 시작되지 않은 LQC 항목은 입력할 수 없습니다: {string.Join(", ", unavailableLabels)}"
            ];
        }
    }

    private static async Task<List<ResponseRow>> ReadResponseRowsAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid reportId,
        CancellationToken cancellationToken)
    {
        var result = new List<ResponseRow>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select template_item_id, check_result, text_value, note
            from panel_quality_report_responses where report_id = @report_id;
            """;
        command.Parameters.AddWithValue("report_id", reportId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ResponseRow(
                reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
        }
        return result;
    }

    private static async Task<List<PhotoSnapshot>> ReadPhotoSnapshotsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid reportId,
        CancellationToken cancellationToken)
    {
        var result = new List<PhotoSnapshot>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, template_item_id, display_name, normalized_mime, byte_size, sha256, alt_text
            from panel_quality_report_photos
            where report_id = @report_id
            order by created_at_utc, id;
            """;
        command.Parameters.AddWithValue("report_id", reportId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new PhotoSnapshot(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3),
                reader.GetInt32(4), reader.GetString(5), reader.GetString(6)));
        }
        return result;
    }

    private static async Task IncrementDraftVersionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ReportContext context,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            update panel_quality_reports
            set version = version + 1, updated_by_user_id = @actor_id, updated_at_utc = now()
            where id = @report_id;
            update panel_quality_inspection_attempts
            set version = version + 1, updated_at_utc = now()
            where id = @attempt_id;
            """;
        update.Parameters.AddWithValue("actor_id", actorUserId);
        update.Parameters.AddWithValue("report_id", context.ReportId);
        update.Parameters.AddWithValue("attempt_id", context.AttemptId);
        await update.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ReplaceResponseRowsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid reportId,
        IReadOnlyList<SaveQualityInspectionItemRequest> responses,
        IReadOnlyDictionary<Guid, Guid> lqcDefinitionKeys,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "delete from panel_quality_report_responses where report_id = @report_id;";
            delete.Parameters.AddWithValue("report_id", reportId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var item in responses)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into panel_quality_report_responses (
                    report_id, template_item_id, check_result, text_value, note,
                    manufacturing_definition_key, updated_by_user_id, updated_at_utc
                ) values (
                    @report_id, @item_id, @check_result, @text_value, @note,
                    @manufacturing_definition_key, @actor_id, now()
                );
                """;
            insert.Parameters.AddWithValue("report_id", reportId);
            insert.Parameters.AddWithValue("item_id", item.TemplateItemId);
            AddNullableText(insert, "check_result", item.CheckResult);
            AddNullableText(insert, "text_value", item.TextValue);
            AddNullableText(insert, "note", item.Note);
            insert.Parameters.Add("manufacturing_definition_key", NpgsqlDbType.Uuid).Value =
                lqcDefinitionKeys.TryGetValue(item.TemplateItemId, out var definitionKey)
                    ? definitionKey
                    : DBNull.Value;
            insert.Parameters.AddWithValue("actor_id", actorUserId);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<Dictionary<Guid, Guid>> ReadLqcDefinitionKeysAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid panelId,
        Guid templateVersionId,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, Guid>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            with lqc_items as (
                select item.id,
                       row_number() over (order by item.display_order, item.id) as item_rank
                from panel_quality_template_items item
                where item.template_version_id = @template_version_id
                  and item.response_type = 'Check'
            ),
            manufacturing_steps as (
                select step.definition_key,
                       row_number() over (order by step.sequence_number, step.id) as step_rank
                from panel_manufacturing_execution_steps step
                where step.execution_id = (
                    select execution.id
                    from panel_manufacturing_executions execution
                    where execution.panel_id = @panel_id
                      and execution.status <> 'Cancelled'
                    order by execution.started_at_utc desc, execution.id desc
                    limit 1
                )
                  and step.definition_key is not null
            )
            select item.id, step.definition_key
            from lqc_items item
            join manufacturing_steps step on step.step_rank = item.item_rank;
            """;
        command.Parameters.AddWithValue("template_version_id", templateVersionId);
        command.Parameters.AddWithValue("panel_id", panelId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result[reader.GetGuid(0)] = reader.GetGuid(1);
        }
        return result;
    }

    private static Dictionary<string, string[]> ValidateResponses(
        IReadOnlyList<SaveQualityInspectionItemRequest> responses,
        IReadOnlyDictionary<Guid, TemplateRow> items)
    {
        var errors = new Dictionary<string, string[]>();
        if (responses.GroupBy(item => item.TemplateItemId).Any(group => group.Count() > 1))
        {
            errors["responses"] = ["같은 검사 항목을 두 번 저장할 수 없습니다."];
            return errors;
        }
        foreach (var response in responses)
        {
            if (!items.TryGetValue(response.TemplateItemId, out var item))
            {
                errors[$"responses.{response.TemplateItemId}"] = ["현재 양식의 검사 항목을 선택해 주세요."];
                continue;
            }
            if (item.ResponseType == "Check")
            {
                if (response.CheckResult is not ("Pass" or "Fail" or "NotApplicable") || response.TextValue is not null)
                    errors[$"responses.{response.TemplateItemId}"] = ["적합·부적합·해당없음 중 하나를 선택해 주세요."];
                else if (response.CheckResult == "NotApplicable" && response.Note is null)
                    errors[$"responses.{response.TemplateItemId}.note"] = ["해당없음 사유를 입력해 주세요."];
            }
            else if (response.CheckResult is not null || response.TextValue is null || response.TextValue.Length > item.MaxTextLength)
            {
                errors[$"responses.{response.TemplateItemId}"] = ["허용된 길이의 메모를 입력해 주세요."];
            }
            if (response.Note?.Length > 1000) errors[$"responses.{response.TemplateItemId}.note"] = ["항목 메모는 1000자 이하로 입력해 주세요."];
        }
        return errors;
    }

    private static Dictionary<string, string[]> ValidateFinalization(
        IReadOnlyList<TemplateRow> items,
        IReadOnlyList<ResponseRow> responses,
        IReadOnlyList<PhotoSnapshot> photos,
        string decisionMode,
        string result,
        string reason)
    {
        var errors = new Dictionary<string, string[]>();
        if (decisionMode == "Aggregate")
        {
            if (responses.Count > 0)
                errors["responses"] = ["전진검수와 FAT는 패널 통합 판정이므로 항목별 검사 응답을 포함할 수 없습니다."];
            if (result == "Failed" && photos.Count == 0 && reason.Trim().Length < 30)
                errors["reason"] = ["부적합 판정은 사진 1장 이상 또는 구체적인 근거 30자 이상이 필요합니다."];
            return errors;
        }
        var map = responses.ToDictionary(item => item.TemplateItemId);
        foreach (var item in items.Where(item => item.IsRequired))
        {
            if (!map.TryGetValue(item.ItemId, out var response))
            {
                errors[$"items.{item.ItemId}"] = ["필수 검사 결과를 입력해 주세요."];
                continue;
            }
            if (item.ResponseType == "Check" && response.CheckResult is not ("Pass" or "Fail" or "NotApplicable"))
                errors[$"items.{item.ItemId}"] = ["필수 검사 결과를 입력해 주세요."];
            if (response.CheckResult == "NotApplicable" && string.IsNullOrWhiteSpace(response.Note))
                errors[$"items.{item.ItemId}.note"] = ["해당없음 사유를 입력해 주세요."];
        }
        var hasFail = responses.Any(item => item.CheckResult == "Fail");
        if (result == "Passed" && hasFail) errors["result"] = ["부적합 항목이 있어 합격으로 확정할 수 없습니다."];
        if (result == "Failed" && !hasFail) errors["result"] = ["부적합 판정에는 하나 이상의 부적합 항목이 필요합니다."];
        if (result == "Failed" && photos.Count == 0 && reason.Trim().Length < 30)
            errors["reason"] = ["부적합 판정은 사진 1장 이상 또는 구체적인 근거 30자 이상이 필요합니다."];
        return errors;
    }

    private static async Task<bool> IsActiveDepartmentAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string code, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select exists(select 1 from departments where code = @code and is_active = true);";
        command.Parameters.AddWithValue("code", code);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<bool> IsValidActionAssigneeAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid userId, string departmentCode, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists (
                select 1 from qms_users users
                join departments department on department.id = users.department_id
                join user_roles user_role on user_role.user_id = users.id
                join role_permissions role_permission on role_permission.role_id = user_role.role_id
                join permissions permission on permission.id = role_permission.permission_id
                where users.id = @user_id and users.is_active = true
                  and department.code = @department_code and permission.code = 'Pending.Manage'
            );
            """;
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("department_code", departmentCode);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<HandoffAssignee?> ResolveAssigneeAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, string stageCode,
        CancellationToken cancellationToken)
    {
        var responsibilities = Responsibilities(stageCode);
        var roleCode = stageCode switch
        {
            QualityInspectionStages.ManufacturingCompleted => "manufacturing",
            "PackingCompleted" => "logistics",
            _ => "quality"
        };
        var permission = stageCode switch
        {
            QualityInspectionStages.ManufacturingCompleted => "manufacturing.update",
            "PackingCompleted" => null,
            _ => "quality.inspect"
        };
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            with candidates as (
                select assignee.assigned_user_id as user_id, role.code as role_code,
                       array_position(@responsibilities, assignee.responsibility_type) as priority,
                       users.display_name
                from project_assignees assignee
                join qms_users users on users.id = assignee.assigned_user_id and users.is_active = true
                left join user_roles user_role on user_role.user_id = users.id
                left join roles role on role.id = user_role.role_id
                where assignee.project_id = @project_id
                  and assignee.responsibility_type = any(@responsibilities)
                  and (@permission is null or exists (
                      select 1 from user_roles allowed_role
                      join role_permissions role_permission on role_permission.role_id = allowed_role.role_id
                      join permissions permission on permission.id = role_permission.permission_id
                      where allowed_role.user_id = users.id and permission.code = @permission
                  ))
                union all
                select users.id, role.code, 100, users.display_name
                from qms_users users
                join user_roles user_role on user_role.user_id = users.id
                join roles role on role.id = user_role.role_id and role.code = @role_code
                where users.is_active = true
                  and (@permission is null or exists (
                      select 1 from role_permissions role_permission
                      join permissions permission on permission.id = role_permission.permission_id
                      where role_permission.role_id = role.id and permission.code = @permission
                  ))
            )
            select user_id, role_code from candidates order by priority, display_name, user_id limit 1;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("responsibilities", responsibilities);
        command.Parameters.AddWithValue("role_code", roleCode);
        AddNullableText(command, "permission", permission);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new HandoffAssignee(stageCode, PrimaryResponsibility(stageCode), reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetString(1))
            : null;
    }

    private static async Task<bool> EnsureMissingLqcWorkItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid panelId,
        string panelDisplayCode,
        HandoffAssignee assignee,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        const string description = "제조 시작과 함께 현재 진행 중인 단계의 LQC를 진행해 주세요.";
        var title = $"LQC 입력 · {panelDisplayCode}";
        var key = $"manufacturing:panel:{panelId}:lqc";
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into work_items (
                project_id, target_type, target_id, workflow_stage_code, responsibility_type,
                assigned_user_id, assigned_role_code, title, description, status, priority,
                idempotency_key, created_by_user_id
            )
            values (
                @project_id, 'Panel', @panel_id, 'LQC', 'QualityLQC',
                @assignee_id, @role_code, @title, @description, 'Requested', 'Normal',
                @key, @actor_id
            )
            on conflict (idempotency_key) do update set
                assigned_user_id = excluded.assigned_user_id,
                assigned_role_code = excluded.assigned_role_code,
                title = excluded.title,
                description = excluded.description,
                status = 'Requested',
                started_at_utc = null,
                completed_at_utc = null,
                cancelled_at_utc = null
            where work_items.status = 'Cancelled';
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("panel_id", panelId);
        command.Parameters.AddWithValue("assignee_id", assignee.UserId);
        AddNullableText(command, "role_code", assignee.RoleCode);
        command.Parameters.AddWithValue("title", title);
        command.Parameters.AddWithValue(
            "description",
            $"{description} /quality/inspections?stage=LQC&project={projectId}&panel={panelId}");
        command.Parameters.AddWithValue("key", key);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        var inserted = await command.ExecuteNonQueryAsync(cancellationToken) == 1;

        await using var readCommand = connection.CreateCommand();
        readCommand.Transaction = transaction;
        readCommand.CommandText = "select id from work_items where idempotency_key = @key;";
        readCommand.Parameters.AddWithValue("key", key);
        if (await readCommand.ExecuteScalarAsync(cancellationToken) is Guid workItemId)
        {
            await WorkAssignmentNotificationWriter.UpsertAsync(
                connection,
                transaction,
                projectId,
                workItemId,
                assignee.UserId,
                SecondaryResponsibilities(QualityInspectionStages.Lqc),
                title,
                description,
                $"/quality/inspections?stage=LQC&project={projectId}&panel={panelId}",
                $"{key}:notification",
                cancellationToken);
        }
        return inserted;
    }

    private static async Task EnsureNextWorkItemAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, HandoffContext context,
        HandoffAssignee next, Guid actorUserId, CancellationToken cancellationToken)
    {
        var key = next.StageCode switch
        {
            QualityInspectionStages.ManufacturingCompleted => $"quality:panel:{context.PanelId}:manufacturing-completed",
            "PackingCompleted" => $"quality:panel:{context.PanelId}:packing",
            _ => $"quality:panel:{context.PanelId}:{next.StageCode}:attempt:1"
        };
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into work_items (
                project_id, target_type, target_id, workflow_stage_code, responsibility_type,
                assigned_user_id, assigned_role_code, title, description, status, priority,
                idempotency_key, created_by_user_id
            ) values (
                @project_id, 'Panel', @panel_id, @stage_code, @responsibility,
                @assignee_id, @role_code, @title, @description, 'Requested', 'Normal',
                @key, @actor_id
            ) on conflict (idempotency_key) do nothing;
            """;
        command.Parameters.AddWithValue("project_id", context.ProjectId);
        command.Parameters.AddWithValue("panel_id", context.PanelId);
        command.Parameters.AddWithValue("stage_code", next.StageCode);
        command.Parameters.AddWithValue("responsibility", next.Responsibility);
        command.Parameters.AddWithValue("assignee_id", next.UserId);
        AddNullableText(command, "role_code", next.RoleCode);
        command.Parameters.AddWithValue("title", $"{StageLabel(next.StageCode)} · {context.PanelDisplayCode}");
        command.Parameters.AddWithValue("description", next.StageCode == "PackingCompleted"
            ? "품질검사가 완료된 패널의 포장 업무를 진행해 주세요."
            : $"{StageLabel(next.StageCode)} 작업을 진행해 주세요.");
        command.Parameters.AddWithValue("key", key);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await using var readCommand = connection.CreateCommand();
        readCommand.Transaction = transaction;
        readCommand.CommandText = "select id from work_items where idempotency_key=@key;";
        readCommand.Parameters.AddWithValue("key", key);
        var value = await readCommand.ExecuteScalarAsync(cancellationToken);
        if (value is Guid workItemId)
        {
            var title = $"{StageLabel(next.StageCode)} · {context.PanelDisplayCode}";
            var message = next.StageCode == "PackingCompleted"
                ? "품질검사가 완료된 패널의 포장 업무를 진행해 주세요."
                : $"{StageLabel(next.StageCode)} 작업을 진행해 주세요.";
            var linkUrl = LinkUrlForHandoff(next.StageCode, context.ProjectId, context.PanelId);
            await WorkAssignmentNotificationWriter.UpsertAsync(
                connection, transaction, context.ProjectId, workItemId, next.UserId,
                SecondaryResponsibilities(next.StageCode), title, message, linkUrl,
                $"{key}:notification", cancellationToken);
        }
    }

    private static IReadOnlyList<string> SecondaryResponsibilities(string stageCode) => stageCode switch
    {
        QualityInspectionStages.Lqc => ["QualityLQCSecondary"],
        QualityInspectionStages.ManufacturingCompleted => ["ManufacturingSecondary"],
        QualityInspectionStages.Oqc => ["QualityOQCSecondary"],
        QualityInspectionStages.CustomerInspection or QualityInspectionStages.Fat => ["QualityCustomerInspectionSecondary"],
        "PackingCompleted" => ["LogisticsSecondary"],
        _ => []
    };

    private static string LinkUrlForHandoff(string stageCode, Guid projectId, Guid panelId) => stageCode switch
    {
        "PackingCompleted" => $"/logistics?stage=packing&project={projectId}&panel={panelId}",
        _ => $"/quality/inspections?stage={stageCode}&project={projectId}&panel={panelId}"
    };

    private static async Task EnsureNextWorkItemAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, ReportContext context,
        HandoffAssignee next, Guid actorUserId, CancellationToken cancellationToken)
        => await EnsureNextWorkItemAsync(connection, transaction,
            new HandoffContext(context.ProjectId, context.PanelId, context.PanelDisplayCode, context.FatRequired, context.StageCode, context.AttemptId, context.AttemptNumber),
            next, actorUserId, cancellationToken);

    private static async Task EnsureProjectStageEventIfCompleteAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, string stageCode,
        Guid sourceId, Guid operationId, Guid actorUserId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into project_workflow_events (
                project_id, stage_code, event_type, event_status, source_type,
                source_id, correlation_id, created_by_user_id, note
            )
            select @project_id, @stage_code, 'StageCompleted', 'Succeeded',
                   'PanelQualityInspection', @source_id, @correlation_id, @actor_id,
                   '모든 활성 패널 품질검사 완료'
            where (select count(*) from panel_placeholders where project_id = @project_id and status = 'Active') > 0
              and (select count(*) from panel_placeholders where project_id = @project_id and status = 'Active') =
                  (select count(distinct attempt.panel_id)
                   from panel_quality_inspection_attempts attempt
                   join panel_placeholders panel on panel.id = attempt.panel_id and panel.status = 'Active'
                   where attempt.project_id = @project_id and attempt.stage_code = @stage_code and attempt.status = 'Passed')
              and not exists (
                  select 1 from project_workflow_events
                  where project_id = @project_id and stage_code = @stage_code
                    and event_type = 'StageCompleted' and event_status = 'Succeeded'
              );
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("stage_code", stageCode);
        command.Parameters.AddWithValue("source_id", sourceId);
        command.Parameters.AddWithValue("correlation_id", operationId.ToString("D"));
        command.Parameters.AddWithValue("actor_id", actorUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureProjectConfirmationEventIfCompleteAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, Guid sourceId,
        Guid operationId, Guid actorUserId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into project_workflow_events (
                project_id, stage_code, event_type, event_status, source_type,
                source_id, correlation_id, created_by_user_id, note
            )
            select @project_id, 'ManufacturingCompleted', 'StageCompleted', 'Succeeded',
                   'PanelManufacturingConfirmation', @source_id, @correlation_id, @actor_id,
                   '모든 활성 패널 제조 완료 확인'
            where (select count(*) from panel_placeholders where project_id = @project_id and status = 'Active') > 0
              and (select count(*) from panel_placeholders where project_id = @project_id and status = 'Active') =
                  (select count(*) from panel_manufacturing_completion_confirmations confirmation
                   join panel_placeholders panel on panel.id = confirmation.panel_id and panel.status = 'Active'
                   where confirmation.project_id = @project_id)
              and not exists (
                  select 1 from project_workflow_events
                  where project_id = @project_id and stage_code = 'ManufacturingCompleted'
                    and event_type = 'StageCompleted' and event_status = 'Succeeded'
              );
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("source_id", sourceId);
        command.Parameters.AddWithValue("correlation_id", operationId.ToString("D"));
        command.Parameters.AddWithValue("actor_id", actorUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<FailedAttemptSnapshot?> LockFailedAttemptByPendingAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid pendingId,
        ProjectAccessScope accessScope, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select attempt.project_id, attempt.panel_id, attempt.stage_code, attempt.attempt_number,
                   attempt.work_item_id, panel.display_code, pending.status, pending.version, pending.issue_number
            from panel_quality_inspection_attempts attempt
            join pending_issues pending on pending.id = attempt.linked_pending_issue_id
            join panel_placeholders panel on panel.id = attempt.panel_id and panel.status = 'Active'
            join projects project on project.id = attempt.project_id
            where pending.id = @pending_id and attempt.status = 'Failed'
              and (@has_read_all or project.project_key = any(@project_keys))
            for update of attempt, pending, panel, project;
            """;
        command.Parameters.AddWithValue("pending_id", pendingId);
        AddScope(command, accessScope);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new FailedAttemptSnapshot(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetInt32(3), reader.GetGuid(4),
                reader.GetString(5), reader.GetString(6), reader.GetInt32(7), reader.GetInt64(8))
            : null;
    }

    private static async Task<Guid?> ReadLatestPassedAttemptAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid panelId, string stageCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select latest.id
            from (
                select id, status
                from panel_quality_inspection_attempts
                where panel_id = @panel_id and stage_code = @stage_code
                order by attempt_number desc, created_at_utc desc
                limit 1
            ) latest
            where latest.status = 'Passed';
            """;
        command.Parameters.AddWithValue("panel_id", panelId);
        command.Parameters.AddWithValue("stage_code", stageCode);
        return (Guid?)await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task MarkWorkInProgressAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid workItemId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "update work_items set status = 'InProgress', started_at_utc = coalesce(started_at_utc, now()) where id = @id and status in ('Requested', 'InProgress');";
        command.Parameters.AddWithValue("id", workItemId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task AdvancePanelStageAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid panelId, string stage, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update panel_placeholders
            set workflow_stage = @stage, updated_at_utc = now()
            where id = @id
              and case workflow_stage
                    when 'BeforeManufacturing' then 0 when 'ManufacturingInProgress' then 1
                    when 'ManufacturingCompleted' then 2 when 'InspectionInProgress' then 3
                    when 'InspectionCompleted' then 4 when 'PackingCompleted' then 5
                    when 'ShipmentCompleted' then 6 else -1 end
                  <
                  case @stage
                    when 'InspectionInProgress' then 3 when 'InspectionCompleted' then 4 else -1 end;
            """;
        command.Parameters.AddWithValue("id", panelId);
        command.Parameters.AddWithValue("stage", stage);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string BuildSnapshot(
        ReportContext context,
        IReadOnlyList<TemplateRow> items,
        IReadOnlyList<ResponseRow> responses,
        IReadOnlyList<PhotoSnapshot> photos,
        string result,
        string reason,
        string actorName)
    {
        var responseMap = responses.ToDictionary(item => item.TemplateItemId);
        return JsonSerializer.Serialize(new
        {
            schema = "emi-qms-panel-quality-v1",
            context.ProjectId,
            context.ProjectCode,
            context.ProjectTitle,
            context.PanelId,
            panelCode = context.PanelDisplayCode,
            panelName = context.PanelName,
            context.StageCode,
            context.DecisionMode,
            stageLabel = StageLabel(context.StageCode),
            context.AttemptNumber,
            result,
            reason,
            finalizedBy = actorName,
            finalizedAtUtc = DateTimeOffset.UtcNow,
            items = items.Select(item =>
            {
                responseMap.TryGetValue(item.ItemId, out var response);
                return new
                {
                    item.ItemId,
                    item.ItemCode,
                    item.DisplayOrder,
                    item.Label,
                    item.ResponseType,
                    checkResult = response?.CheckResult,
                    textValue = response?.TextValue,
                    note = response?.Note
                };
            }),
            photos = photos.Select(photo => new
            {
                photo.PhotoId,
                photo.TemplateItemId,
                photo.DisplayName,
                photo.NormalizedMime,
                photo.ByteSize,
                photo.Sha256,
                photo.AltText
            })
        }, JsonOptions);
    }

    private static async Task<string> ReadActorNameAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid actorId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select display_name from qms_users where id = @id;";
        command.Parameters.AddWithValue("id", actorId);
        return (string?)await command.ExecuteScalarAsync(cancellationToken) ?? "검사 담당자";
    }

    private static async Task<ReplayRead> ReadReplayAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid operationId, string action,
        string fingerprint, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select action, payload_fingerprint, result_projection::text from panel_quality_operations where operation_id = @id;";
        command.Parameters.AddWithValue("id", operationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return new ReplayRead(null);
        if (!string.Equals(reader.GetString(0), action, StringComparison.Ordinal)
            || !string.Equals(reader.GetString(1), fingerprint, StringComparison.Ordinal))
        {
            return new ReplayRead(QualityInspectionMutationResult<QualityInspectionMutationResponse>.Conflict("같은 요청 식별자를 다른 내용으로 재사용할 수 없습니다."));
        }
        var response = JsonSerializer.Deserialize<QualityInspectionMutationResponse>(reader.GetString(2), JsonOptions)
            ?? throw new InvalidOperationException("저장된 품질검사 요청 결과를 읽을 수 없습니다.");
        return new ReplayRead(QualityInspectionMutationResult<QualityInspectionMutationResponse>.Success(response with { Replayed = true }));
    }

    private static async Task InsertOperationAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid operationId, string action,
        Guid projectId, Guid panelId, string? stageCode, Guid? attemptId, Guid actorUserId,
        string fingerprint, QualityInspectionMutationResponse response, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into panel_quality_operations (
                operation_id, action, project_id, panel_id, stage_code, attempt_id,
                requested_by_user_id, payload_fingerprint, result_projection
            ) values (
                @operation_id, @action, @project_id, @panel_id, @stage_code, @attempt_id,
                @actor_id, @fingerprint, @projection::jsonb
            );
            """;
        command.Parameters.AddWithValue("operation_id", operationId);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("panel_id", panelId);
        AddNullableText(command, "stage_code", stageCode);
        AddNullableUuid(command, "attempt_id", attemptId);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        command.Parameters.AddWithValue("fingerprint", fingerprint);
        command.Parameters.AddWithValue("projection", JsonSerializer.Serialize(response, JsonOptions));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Fingerprint(params object?[] values)
        => Hash(Encoding.UTF8.GetBytes(string.Join('|', values.Select(value => value?.ToString() ?? "<null>"))));

    private static string? DetectImageMime(byte[] content)
    {
        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF) return "image/jpeg";
        ReadOnlySpan<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        return content.AsSpan().StartsWith(png) ? "image/png" : null;
    }

    private static string Hash(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizeStage(string? value)
    {
        var stage = Normalize(value);
        return stage is QualityInspectionStages.Lqc or QualityInspectionStages.ManufacturingCompleted
            or QualityInspectionStages.Oqc or QualityInspectionStages.CustomerInspection or QualityInspectionStages.Fat
            ? stage : null;
    }
    private static string? NormalizeInspectionStage(string? value)
    {
        var stage = NormalizeStage(value);
        return stage is not null && QualityInspectionStages.InspectionStages.Contains(stage) ? stage : null;
    }
    private static string DecisionMode(string stageCode)
        => stageCode is QualityInspectionStages.CustomerInspection or QualityInspectionStages.Fat
            ? "Aggregate"
            : "Checklist";
    private static string StageLabel(string stageCode) => stageCode switch
    {
        QualityInspectionStages.Lqc => "LQC",
        QualityInspectionStages.ManufacturingCompleted => "제조 완료 확인",
        QualityInspectionStages.Oqc => "OQC 자체검수",
        QualityInspectionStages.CustomerInspection => "전진검수",
        QualityInspectionStages.Fat => "FAT",
        "PackingCompleted" => "포장",
        _ => stageCode
    };
    private static string PrimaryResponsibility(string stageCode) => stageCode switch
    {
        QualityInspectionStages.Lqc => "QualityLQC",
        QualityInspectionStages.ManufacturingCompleted => "ManufacturingPrimary",
        QualityInspectionStages.Oqc => "QualityOQC",
        QualityInspectionStages.CustomerInspection or QualityInspectionStages.Fat => "QualityCustomerInspection",
        "PackingCompleted" => "LogisticsPrimary",
        _ => "Quality"
    };
    private static string[] Responsibilities(string stageCode) => stageCode switch
    {
        QualityInspectionStages.Lqc => ["QualityLQC", "QualityLQCSecondary"],
        QualityInspectionStages.ManufacturingCompleted => ["ManufacturingPrimary", "ManufacturingSecondary", "Manufacturing"],
        QualityInspectionStages.Oqc => ["QualityOQC", "QualityOQCSecondary"],
        QualityInspectionStages.CustomerInspection or QualityInspectionStages.Fat => ["QualityCustomerInspection", "QualityCustomerInspectionSecondary"],
        "PackingCompleted" => ["LogisticsPrimary", "LogisticsSecondary", "Logistics"],
        _ => []
    };
    private static QualityInspectionMutationResult<QualityInspectionMutationResponse> Validation(string field, string message)
        => QualityInspectionMutationResult<QualityInspectionMutationResponse>.Validation(new Dictionary<string, string[]> { [field] = [message] });
    private static void AddScope(NpgsqlCommand command, ProjectAccessScope accessScope)
    {
        command.Parameters.AddWithValue("has_read_all", accessScope.HasProjectReadAll);
        command.Parameters.AddWithValue("project_keys", accessScope.ProjectKeys.ToArray());
    }
    private static void AddNullableText(NpgsqlCommand command, string name, string? value)
        => command.Parameters.Add(name, NpgsqlDbType.Text).Value = value ?? (object)DBNull.Value;
    private static void AddNullableUuid(NpgsqlCommand command, string name, Guid? value)
        => command.Parameters.Add(name, NpgsqlDbType.Uuid).Value = value ?? (object)DBNull.Value;
    private NpgsqlDataSource CreateDataSource()
    {
        var value = connectionStringProvider.GetConnectionString();
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("QMS database connection string is not configured.");
        return NpgsqlDataSource.Create(value);
    }

    private sealed record ProjectSnapshot(Guid ProjectId, string ProjectCode, string ProjectTitle, bool FatRequired);
    private sealed record QualityReconciliationPanel(
        Guid ProjectId,
        Guid PanelId,
        string PanelDisplayCode,
        bool FatRequired,
        Guid? LqcAttemptId,
        string? LqcStatus,
        Guid? OqcAttemptId,
        string? OqcStatus,
        Guid? CustomerInspectionAttemptId,
        string? CustomerInspectionStatus,
        Guid? FatAttemptId,
        string? FatStatus,
        bool ManufacturingStarted,
        bool HasLqcWork,
        bool ManufacturingCompleted,
        bool HasManufacturingConfirmation,
        bool HasOqcWork,
        bool HasCustomerInspectionWork,
        bool HasFatWork,
        bool HasPackingWork);
    private sealed record StageWorkSnapshot(string PanelDisplayCode, string? PanelName, string WorkflowStage, Guid WorkItemId, string WorkStatus, Guid AssignedUserId);
    private sealed record ActiveAttemptSnapshot(Guid AttemptId, int AttemptNumber, int Version, Guid ReportId);
    private sealed record ReportContext(
        Guid ReportId, string ReportStatus, int ReportVersion, Guid TemplateVersionId,
        Guid AttemptId, Guid ProjectId, Guid PanelId, string StageCode, int AttemptNumber,
        Guid WorkItemId, Guid? LinkedPendingId, string PanelDisplayCode, string? PanelName,
        string ProjectCode, string ProjectTitle, bool FatRequired, string DecisionMode);
    private record HandoffContext(Guid ProjectId, Guid PanelId, string PanelDisplayCode, bool FatRequired, string StageCode, Guid AttemptId, int AttemptNumber);
    private sealed record HandoffAssignee(string StageCode, string Responsibility, Guid UserId, string? RoleCode);
    private sealed record FailedAttemptSnapshot(
        Guid ProjectId, Guid PanelId, string StageCode, int AttemptNumber, Guid WorkItemId,
        string PanelDisplayCode, string PendingStatus, int PendingVersion, long PendingNumber);
    private sealed record TemplateRow(Guid ItemId, string ItemCode, int DisplayOrder, string Label, string? Guidance, string ResponseType, bool IsRequired, int? MaxTextLength)
    {
        public QualityInspectionTemplateItemResponse ToResponse(ReinspectionScope? scope = null)
        {
            string? evidence = null;
            var isTarget = scope is not null && scope.EvidenceByItemId.TryGetValue(ItemId, out evidence);
            return new QualityInspectionTemplateItemResponse(
                ItemId,
                ItemCode,
                DisplayOrder,
                Label,
                Guidance,
                ResponseType,
                IsRequired,
                MaxTextLength,
                IsReinspectionTarget: isTarget,
                PreviousFailureEvidence: isTarget ? evidence : null);
        }
    }
    private sealed record ResponseRow(Guid TemplateItemId, string? CheckResult, string? TextValue, string? Note)
    {
        public QualityInspectionItemValueResponse ToResponse() => new(TemplateItemId, CheckResult, TextValue, Note);
    }
    private sealed record PhotoSnapshot(
        Guid PhotoId,
        Guid TemplateItemId,
        string DisplayName,
        string NormalizedMime,
        int ByteSize,
        string Sha256,
        string AltText);
    private sealed record LqcManufacturingProgress(
        int AvailableStepCount,
        int TotalStepCount,
        IReadOnlyList<string> StepNames);
    private sealed record ReinspectionScope(IReadOnlyDictionary<Guid, string?> EvidenceByItemId);
    private sealed record ReportView(
        Guid ReportId, string Status, int Version, string? Result, string? Reason, string? PdfStatus, string DecisionMode,
        IReadOnlyList<QualityInspectionTemplateItemResponse> Items,
        IReadOnlyList<QualityInspectionItemValueResponse> Responses,
        IReadOnlyList<QualityInspectionPhotoResponse> Photos);
    private sealed record ReplayRead(QualityInspectionMutationResult<QualityInspectionMutationResponse>? Result);
    private sealed class ProjectBuilder(Guid id, string code, string title, bool fatRequired)
    {
        public List<QualityInspectionPanelSummary> Panels { get; } = [];
        public QualityInspectionProjectResponse Build() => new(
            id, code, title, fatRequired,
            Panels.Count(item => item.Status is "Ready" or "Requested"),
            Panels.Count(item => item.Status == "InProgress"),
            Panels.Count(item => item.Status == "Failed"),
            Panels.Count(item => item.Status is "Passed" or "Confirmed" or "Completed"),
            Panels);
    }
    private sealed class DepartmentBuilder(string code, string name)
    {
        public List<QualityActionOwnerResponse> Assignees { get; } = [];
        public QualityActionDepartmentResponse Build() => new(code, name, Assignees);
    }
}
