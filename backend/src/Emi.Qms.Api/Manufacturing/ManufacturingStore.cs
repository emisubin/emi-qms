using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Notifications;
using Emi.Qms.Api.Pending;
using Emi.Qms.Api.Projects;
using Emi.Qms.Api.QualityInspections;
using Emi.Qms.Api.Workflow;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Manufacturing;

public sealed class ManufacturingStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    PendingStore pendingStore)
{
    private const int MaxReleasePanelCount = 500;

    public async Task<ManufacturingQueueResponse> ListAsync(
        ProjectAccessScope accessScope,
        Guid? actorUserId,
        bool canReadAllTimes,
        bool canMutate,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand($"""
            select
                project.id,
                project.project_code,
                project.project_title,
                panel.id,
                panel.display_code,
                panel.panel_name,
                panel.workflow_stage,
                work_item.id,
                work_item.status,
                execution.id,
                coalesce(execution.status, 'Ready'),
                coalesce(execution.version, 0),
                coalesce(step_counts.checked_count, 0),
                coalesce(step_counts.total_count, 0),
                execution.active_stop_pending_id,
                pending.issue_number,
                pending.action_department_code,
                execution.started_by_user_id,
                execution.started_at_utc,
                execution.completed_at_utc,
                completion.id is not null,
                batch_steps.steps_json
            from projects project
            join panel_placeholders panel
              on panel.project_id = project.id
             and panel.status = 'Active'
            left join panel_kitting_completions completion on completion.panel_id = panel.id
            join work_items work_item
              on work_item.idempotency_key = 'kitting:panel:' || panel.id::text || ':manufacturing'
             and work_item.target_type = 'Panel'
             and work_item.target_id = panel.id
            left join lateral (
                select candidate.*
                from panel_manufacturing_executions candidate
                where candidate.panel_id = panel.id
                order by candidate.started_at_utc desc, candidate.id desc
                limit 1
            ) execution on true
            left join lateral (
                select count(*) filter (where step.checked_at_utc is not null)::int as checked_count,
                       count(*)::int as total_count
                from panel_manufacturing_execution_steps step
                where step.execution_id = execution.id
            ) step_counts on true
            left join lateral (
                select coalesce(
                    jsonb_agg(
                        jsonb_build_object(
                            'SequenceNumber', step.sequence_number,
                            'StepName', step.step_name,
                            'Checked', step.checked_at_utc is not null
                        )
                        order by step.sequence_number
                    ),
                    '[]'::jsonb
                )::text as steps_json
                from panel_manufacturing_execution_steps step
                where @include_batch_steps
                  and step.execution_id = execution.id
            ) batch_steps on true
            left join pending_issues pending on pending.id = execution.active_stop_pending_id
            where project.deleted_at_utc is null
              and project.status <> 'Cancelled'
              and (@has_read_all or project.project_key = any(@project_keys))
              {(projectId is null ? string.Empty : "and project.id = @project_id")}
            order by project.project_code, project.id, panel.sequence_number;
            """);
        command.Parameters.AddWithValue("has_read_all", accessScope.HasProjectReadAll);
        command.Parameters.AddWithValue("project_keys", accessScope.ProjectKeys.ToArray());
        command.Parameters.AddWithValue("include_batch_steps", projectId is not null);
        if (projectId is not null)
        {
            command.Parameters.AddWithValue("project_id", projectId.Value);
        }

        var builders = new Dictionary<Guid, ProjectBuilder>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var currentProjectId = reader.GetGuid(0);
            if (!builders.TryGetValue(currentProjectId, out var builder))
            {
                builder = new ProjectBuilder(currentProjectId, reader.GetString(1), reader.GetString(2));
                builders[currentProjectId] = builder;
            }

            var startedBy = reader.IsDBNull(17) ? (Guid?)null : reader.GetGuid(17);
            var showTimes = canReadAllTimes || (actorUserId is not null && startedBy == actorUserId);
            builder.Panels.Add(new ManufacturingPanelResponse(
                reader.GetGuid(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetString(6),
                reader.GetBoolean(20),
                reader.GetGuid(7),
                reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetGuid(9),
                reader.GetString(10),
                reader.GetInt32(11),
                reader.GetInt32(12),
                reader.GetInt32(13),
                reader.IsDBNull(14) ? null : reader.GetGuid(14),
                reader.IsDBNull(15) ? null : reader.GetInt64(15),
                reader.IsDBNull(16) ? null : reader.GetString(16),
                showTimes && !reader.IsDBNull(18) ? reader.GetFieldValue<DateTimeOffset>(18) : null,
                showTimes && !reader.IsDBNull(19) ? reader.GetFieldValue<DateTimeOffset>(19) : null,
                canMutate,
                reader.IsDBNull(21)
                    ? []
                    : JsonSerializer.Deserialize<IReadOnlyList<ManufacturingBatchStepResponse>>(reader.GetString(21)) ?? []));
        }

        return new ManufacturingQueueResponse(builders.Values.Select(builder => builder.ToResponse()).ToList());
    }

    public async Task<ManufacturingReleaseQueueResponse> ListReleaseCandidatesAsync(
        ProjectAccessScope accessScope,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand($"""
            select
                project.id,
                project.project_code,
                project.project_title,
                readiness.active_item_count,
                readiness.completed_item_count,
                panel.id,
                panel.display_code,
                panel.panel_name,
                panel.panel_info_completed,
                completion.id is not null,
                work_item.id is not null,
                work_item.status,
                work_item.created_at_utc
            from projects project
            cross join lateral (
                select count(*)::int as active_item_count,
                       count(*) filter (where item.receipt_completed)::int as completed_item_count
                from project_procurement_items item
                where item.project_id = project.id
                  and item.status = 'Active'
            ) readiness
            join panel_placeholders panel
              on panel.project_id = project.id
             and panel.status = 'Active'
            left join panel_kitting_completions completion on completion.panel_id = panel.id
            left join work_items work_item
              on work_item.idempotency_key = 'kitting:panel:' || panel.id::text || ':manufacturing'
             and work_item.target_type = 'Panel'
             and work_item.target_id = panel.id
            where project.deleted_at_utc is null
              and project.status = 'Active'
              and (@has_read_all or project.project_key = any(@project_keys))
              {(projectId is null ? string.Empty : "and project.id = @project_id")}
            order by project.project_code, project.id, panel.sequence_number;
            """);
        command.Parameters.AddWithValue("has_read_all", accessScope.HasProjectReadAll);
        command.Parameters.AddWithValue("project_keys", accessScope.ProjectKeys.ToArray());
        if (projectId is not null)
        {
            command.Parameters.AddWithValue("project_id", projectId.Value);
        }

        var builders = new Dictionary<Guid, ReleaseProjectBuilder>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var currentProjectId = reader.GetGuid(0);
            if (!builders.TryGetValue(currentProjectId, out var builder))
            {
                builder = new ReleaseProjectBuilder(
                    currentProjectId,
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3),
                    reader.GetInt32(4));
                builders[currentProjectId] = builder;
            }

            var panelInfoCompleted = reader.GetBoolean(8);
            var released = reader.GetBoolean(10);
            builder.Panels.Add(new ManufacturingReleasePanelResponse(
                reader.GetGuid(5),
                reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                panelInfoCompleted,
                reader.GetBoolean(9),
                released,
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12),
                panelInfoCompleted && !released));
        }

        return new ManufacturingReleaseQueueResponse(builders.Values.Select(builder => builder.ToResponse()).ToList());
    }

    public async Task<ManufacturingMutationResult<ManufacturingReleaseResponse>> ReleaseAsync(
        ReleaseManufacturingRequest request,
        Guid actorUserId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        var errors = ValidateRelease(request);
        if (errors.Count > 0)
        {
            return ManufacturingMutationResult<ManufacturingReleaseResponse>.Validation(errors);
        }

        var panelIds = request.PanelIds!.OrderBy(panelId => panelId).ToArray();
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var project = await LockProjectAsync(connection, transaction, request.ProjectId, accessScope, cancellationToken);
            if (project is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ManufacturingMutationResult<ManufacturingReleaseResponse>.NotFound();
            }

            var replay = await ReadReleaseOperationAsync(connection, transaction, request.OperationId, cancellationToken);
            if (replay is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                if (replay.ProjectId != request.ProjectId
                    || replay.RequestedByUserId != actorUserId
                    || !replay.PanelIds.SequenceEqual(panelIds))
                {
                    return ManufacturingMutationResult<ManufacturingReleaseResponse>.Conflict(
                        "같은 작업 식별자를 다른 제조 투입 요청에 사용할 수 없습니다. 최신 내용을 다시 불러와 주세요.");
                }

                return ManufacturingMutationResult<ManufacturingReleaseResponse>.Success(new ManufacturingReleaseResponse(
                    request.OperationId,
                    replay.ReleasedPanelCount,
                    replay.GeneratedWorkItemCount,
                    true));
            }

            if (!string.Equals(project.Status, "Active", StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(cancellationToken);
                return ManufacturingMutationResult<ManufacturingReleaseResponse>.Conflict("진행 중인 프로젝트에서만 제조 투입을 요청할 수 있습니다.");
            }

            var panels = await LockReleasePanelsAsync(connection, transaction, request.ProjectId, panelIds, cancellationToken);
            if (panels.Count != panelIds.Length)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ManufacturingMutationResult<ManufacturingReleaseResponse>.Validation(
                    new Dictionary<string, string[]> { [nameof(request.PanelIds)] = ["선택한 패널은 이 프로젝트의 활성 패널이어야 합니다."] });
            }

            var incompletePanels = panels.Where(panel => !panel.PanelInfoCompleted).Select(panel => panel.DisplayCode).ToList();
            if (incompletePanels.Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ManufacturingMutationResult<ManufacturingReleaseResponse>.Validation(
                    new Dictionary<string, string[]> { [nameof(request.PanelIds)] = [$"패널정보를 먼저 완료해 주세요: {string.Join(", ", incompletePanels)}"] });
            }

            var releasedPanels = panels.Where(panel => panel.Released).Select(panel => panel.DisplayCode).ToList();
            if (releasedPanels.Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ManufacturingMutationResult<ManufacturingReleaseResponse>.Conflict(
                    $"이미 제조 투입 요청된 패널이 포함되어 있습니다: {string.Join(", ", releasedPanels)}. 최신 내용을 다시 불러와 주세요.");
            }

            var assignee = await ResolveManufacturingAssigneeAsync(connection, transaction, request.ProjectId, cancellationToken);
            if (assignee is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ManufacturingMutationResult<ManufacturingReleaseResponse>.Validation(
                    new Dictionary<string, string[]> { ["manufacturingAssignee"] = ["제조 담당자를 지정한 뒤 다시 시도해 주세요."] });
            }

            var readiness = await ReadMaterialReadinessAsync(connection, transaction, request.ProjectId, cancellationToken);
            var generatedCount = 0;
            foreach (var panel in panels)
            {
                generatedCount += await InsertManufacturingReleaseWorkItemAsync(
                    connection,
                    transaction,
                    request.ProjectId,
                    panel,
                    readiness,
                    assignee.Value,
                    actorUserId,
                    cancellationToken);
            }
            if (generatedCount != panels.Count)
            {
                throw new InvalidOperationException("The manufacturing release work item count changed during the transaction.");
            }

            await InsertReleaseOperationAsync(
                connection,
                transaction,
                request.OperationId,
                request.ProjectId,
                actorUserId,
                panelIds,
                panels.Count,
                generatedCount,
                cancellationToken);
            await WorkflowStore.EnsureEffectiveKittingStageCompletedAsync(
                connection,
                transaction,
                request.ProjectId,
                "ManufacturingRelease",
                request.OperationId,
                request.OperationId,
                actorUserId,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ManufacturingMutationResult<ManufacturingReleaseResponse>.Success(new ManufacturingReleaseResponse(
                request.OperationId,
                panels.Count,
                generatedCount,
                false));
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ManufacturingMutationResult<ManufacturingReleaseResponse>.Conflict(
                "다른 요청이 먼저 제조 투입 상태를 변경했습니다. 최신 내용을 다시 불러와 주세요.");
        }
    }

    public async Task<ManufacturingExecutionDetailResponse?> GetDetailAsync(
        Guid panelId,
        ProjectAccessScope accessScope,
        Guid? actorUserId,
        bool canReadAllTimes,
        bool canMutate,
        CancellationToken cancellationToken)
    {
        var queue = await ListAsync(accessScope, actorUserId, canReadAllTimes, canMutate, null, cancellationToken);
        var panel = queue.Projects.SelectMany(project => project.Panels).SingleOrDefault(candidate => candidate.PanelId == panelId);
        if (panel is null)
        {
            return null;
        }
        if (panel.ExecutionId is null)
        {
            return new ManufacturingExecutionDetailResponse(panel, [], []);
        }

        await using var dataSource = CreateDataSource();
        var steps = new List<ManufacturingStepResponse>();
        await using (var command = dataSource.CreateCommand("""
            select step.id, step.sequence_number, step.step_name, step.checked_by_user_id,
                   checked_by.display_name,
                   case when @can_read_all or step.checked_by_user_id = @actor_user_id
                        then step.checked_at_utc else null end
            from panel_manufacturing_execution_steps step
            left join qms_users checked_by on checked_by.id = step.checked_by_user_id
            where step.execution_id = @execution_id
            order by step.sequence_number;
            """))
        {
            command.Parameters.AddWithValue("execution_id", panel.ExecutionId.Value);
            command.Parameters.AddWithValue("can_read_all", canReadAllTimes);
            AddNullableUuid(command, "actor_user_id", actorUserId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                steps.Add(new ManufacturingStepResponse(
                    reader.GetGuid(0),
                    reader.GetInt32(1),
                    reader.GetString(2),
                    !reader.IsDBNull(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5)));
            }
        }

        var events = new List<ManufacturingEventResponse>();
        await using (var command = dataSource.CreateCommand("""
            select event.id, event.event_type, event.stop_reason_code, event.stop_description,
                   event.pending_issue_id, actor.display_name,
                   case when @can_read_all or event.actor_user_id = @actor_user_id
                        then event.created_at_utc else null end
            from panel_manufacturing_events event
            join qms_users actor on actor.id = event.actor_user_id
            where event.execution_id = @execution_id
            order by event.created_at_utc, event.id;
            """))
        {
            command.Parameters.AddWithValue("execution_id", panel.ExecutionId.Value);
            command.Parameters.AddWithValue("can_read_all", canReadAllTimes);
            AddNullableUuid(command, "actor_user_id", actorUserId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                events.Add(new ManufacturingEventResponse(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    EventLabel(reader.GetString(1)),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetGuid(4),
                    reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6)));
            }
        }

        return new ManufacturingExecutionDetailResponse(panel, steps, events);
    }

    public async Task<IReadOnlyList<ManufacturingActionDepartmentResponse>> ListActionDepartmentsAsync(
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select department.code, department.name, users.id, users.display_name
            from departments department
            left join lateral (
                select distinct user_account.id, user_account.display_name
                from qms_users user_account
                join user_roles user_role on user_role.user_id = user_account.id
                join role_permissions role_permission on role_permission.role_id = user_role.role_id
                join permissions permission on permission.id = role_permission.permission_id
                where user_account.department_id = department.id
                  and user_account.is_active = true
                  and permission.code = 'Pending.Manage'
            ) users on true
            where department.is_active = true
            order by department.sort_order, department.code, users.display_name, users.id;
            """);

        var departments = new Dictionary<string, (string Name, List<ManufacturingActionOwnerResponse> Assignees)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = reader.GetString(0);
            if (!departments.TryGetValue(code, out var value))
            {
                value = (reader.GetString(1), []);
                departments[code] = value;
            }
            if (!reader.IsDBNull(2))
            {
                value.Assignees.Add(new ManufacturingActionOwnerResponse(reader.GetGuid(2), reader.GetString(3)));
            }
        }

        return departments.Select(pair => new ManufacturingActionDepartmentResponse(
            pair.Key,
            pair.Value.Name,
            pair.Value.Assignees)).ToList();
    }

    public async Task<ManufacturingMutationResult<ManufacturingMutationResponse>> StartAsync(
        StartManufacturingRequest request,
        Guid actorUserId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        var errors = ValidateOperation(request.OperationId);
        if (request.ProjectId == Guid.Empty)
        {
            errors[nameof(request.ProjectId)] = ["프로젝트를 선택해 주세요."];
        }
        if (request.PanelId == Guid.Empty)
        {
            errors[nameof(request.PanelId)] = ["패널을 선택해 주세요."];
        }
        if (errors.Count > 0)
        {
            return ManufacturingMutationResult<ManufacturingMutationResponse>.Validation(errors);
        }

        var fingerprint = Fingerprint("Start", request.ProjectId, request.PanelId);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var project = await LockProjectAsync(connection, transaction, request.ProjectId, accessScope, cancellationToken);
            if (project is null)
            {
                return await RollbackNotFound(transaction, cancellationToken);
            }
            var replay = await ReadReplayAsync(connection, transaction, request.OperationId, "Start", fingerprint, actorUserId, cancellationToken);
            if (replay.Result is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return replay.Result;
            }
            if (replay.Conflict is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ManufacturingMutationResult<ManufacturingMutationResponse>.Conflict(replay.Conflict);
            }
            if (!string.Equals(project.Status, "Active", StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(cancellationToken);
                return ManufacturingMutationResult<ManufacturingMutationResponse>.Conflict("진행 중인 프로젝트에서만 제조 작업을 시작할 수 있습니다.");
            }

            var panel = await LockReadyPanelAsync(connection, transaction, request.ProjectId, request.PanelId, cancellationToken);
            if (panel is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ManufacturingMutationResult<ManufacturingMutationResponse>.Validation(
                    new Dictionary<string, string[]> { ["panel"] = ["생산관리에서 제조 투입 요청한 활성 패널만 제조 작업을 시작할 수 있습니다."] });
            }
            if (!string.Equals(panel.WorkflowStage, "BeforeManufacturing", StringComparison.Ordinal)
                || !string.Equals(panel.WorkItemStatus, "Requested", StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(cancellationToken);
                return ManufacturingMutationResult<ManufacturingMutationResponse>.Conflict("이미 제조가 시작됐거나 처리할 수 없는 패널입니다. 최신 내용을 다시 불러와 주세요.");
            }

            if (!await IsUl891CurrentDesignReadyAsync(connection, transaction, request.PanelId, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return ManufacturingMutationResult<ManufacturingMutationResponse>.Conflict("설계 탭에서 이 패널의 현재 UL891 설계를 먼저 저장해 주세요.");
            }

            var template = await LockStepTemplateForProjectAsync(connection, transaction, request.ProjectId, cancellationToken);
            if (template is null || template.Value.Steps.Count == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ManufacturingMutationResult<ManufacturingMutationResponse>.Conflict("활성 제조 작업 양식이 없습니다. 양식 관리자에게 확인해 주세요.");
            }
            var qualityAssignee = await ResolveQualityAssigneeAsync(connection, transaction, request.ProjectId, cancellationToken);
            if (qualityAssignee is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ManufacturingMutationResult<ManufacturingMutationResponse>.Validation(
                    new Dictionary<string, string[]>
                    {
                        ["qualityAssignee"] = ["제조 시작과 함께 LQC를 진행할 품질 담당자를 먼저 지정해 주세요."]
                    });
            }

            var executionId = Guid.NewGuid();
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    insert into panel_manufacturing_executions (
                        id, project_id, panel_id, status, started_by_user_id, template_version_id
                    )
                    values (@id, @project_id, @panel_id, 'InProgress', @actor_id, @template_version_id);

                    update panel_placeholders
                    set workflow_stage = 'ManufacturingInProgress', updated_at_utc = now()
                    where id = @panel_id and workflow_stage = 'BeforeManufacturing';

                    update work_items
                    set status = 'InProgress', started_at_utc = coalesce(started_at_utc, now())
                    where id = @work_item_id and status = 'Requested';
                    """;
                command.Parameters.AddWithValue("id", executionId);
                command.Parameters.AddWithValue("project_id", request.ProjectId);
                command.Parameters.AddWithValue("panel_id", request.PanelId);
                command.Parameters.AddWithValue("actor_id", actorUserId);
                command.Parameters.Add("template_version_id", NpgsqlDbType.Uuid).Value =
                    template.Value.LegacyVersionId ?? (object)DBNull.Value;
                command.Parameters.AddWithValue("work_item_id", panel.WorkItemId);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            for (var index = 0; index < template.Value.Steps.Count; index++)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    insert into panel_manufacturing_execution_steps (
                        execution_id,sequence_number,step_name,
                        project_manufacturing_step_id,definition_key
                    )
                    values (
                        @execution_id,@sequence_number,@step_name,
                        @project_step_id,@definition_key
                    );
                    """;
                command.Parameters.AddWithValue("execution_id", executionId);
                command.Parameters.AddWithValue("sequence_number", index + 1);
                command.Parameters.AddWithValue("step_name", template.Value.Steps[index].Label);
                command.Parameters.Add("project_step_id", NpgsqlDbType.Uuid).Value =
                    template.Value.Steps[index].ProjectStepId ?? (object)DBNull.Value;
                command.Parameters.Add("definition_key", NpgsqlDbType.Uuid).Value =
                    template.Value.Steps[index].DefinitionKey ?? (object)DBNull.Value;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            var lqcCreated = await InsertLqcWorkItemAsync(
                connection,
                transaction,
                request.ProjectId,
                request.PanelId,
                panel.DisplayCode,
                qualityAssignee.Value,
                actorUserId,
                cancellationToken);
            await InsertEventAsync(connection, transaction, executionId, "Started", null, null, null, null, actorUserId, cancellationToken);
            var response = new ManufacturingMutationResponse(
                request.OperationId, request.ProjectId, request.PanelId, executionId,
                ManufacturingExecutionStatuses.InProgress, 1, 0, template.Value.Steps.Count,
                null, null, lqcCreated, false, false);
            await InsertOperationAsync(connection, transaction, request.OperationId, "Start", request.ProjectId, request.PanelId, executionId, actorUserId, fingerprint, response, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ManufacturingMutationResult<ManufacturingMutationResponse>.Success(response);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ManufacturingMutationResult<ManufacturingMutationResponse>.Conflict("다른 요청이 먼저 제조 작업을 시작했습니다. 최신 내용을 다시 불러와 주세요.");
        }
    }

    public async Task<ManufacturingMutationResult<ManufacturingMutationResponse>> CheckStepAsync(
        Guid executionId,
        CheckManufacturingStepRequest request,
        Guid actorUserId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        var errors = ValidateVersioned(request.OperationId, request.ExpectedVersion);
        if (request.StepId == Guid.Empty)
        {
            errors[nameof(request.StepId)] = ["확인할 단계를 선택해 주세요."];
        }
        return errors.Count > 0
            ? ManufacturingMutationResult<ManufacturingMutationResponse>.Validation(errors)
            : await MutateExistingAsync(
                executionId,
                request.OperationId,
                "CheckStep",
                Fingerprint("CheckStep", executionId, request.StepId, request.ExpectedVersion),
                actorUserId,
                accessScope,
                request.ExpectedVersion,
                async (connection, transaction, snapshot) =>
                {
                    if (!string.Equals(snapshot.Status, ManufacturingExecutionStatuses.InProgress, StringComparison.Ordinal))
                    {
                        return MutationActionResult.Conflict("진행 중인 제조 작업에서만 단계를 확인할 수 있습니다.");
                    }
                    var steps = await LockStepsAsync(connection, transaction, executionId, cancellationToken);
                    var next = steps.FirstOrDefault(step => step.CheckedAtUtc is null);
                    if (next is null || next.StepId != request.StepId)
                    {
                        return MutationActionResult.Conflict(next is null
                            ? "모든 제조 단계가 이미 확인되었습니다."
                            : $"{next.StepName} 단계를 먼저 확인해 주세요.");
                    }
                    await using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = """
                            update panel_manufacturing_execution_steps
                            set checked_by_user_id = @actor_id, checked_at_utc = now()
                            where id = @step_id and checked_at_utc is null;
                            """;
                        command.Parameters.AddWithValue("actor_id", actorUserId);
                        command.Parameters.AddWithValue("step_id", request.StepId);
                        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                        {
                            return MutationActionResult.Conflict("다른 요청이 먼저 이 단계를 확인했습니다. 최신 내용을 다시 불러와 주세요.");
                        }
                    }
                    await InsertEventAsync(connection, transaction, executionId, "StepChecked", request.StepId, null, null, null, actorUserId, cancellationToken);
                    var version = await IncrementVersionAsync(connection, transaction, executionId, cancellationToken);
                    return MutationActionResult.Success(new ManufacturingMutationResponse(
                        request.OperationId, snapshot.ProjectId, snapshot.PanelId, executionId,
                        snapshot.Status, version, steps.Count(step => step.CheckedAtUtc is not null) + 1, steps.Count,
                        null, null, false, false, false));
                },
                cancellationToken);
    }

    public async Task<ManufacturingMutationResult<StepBatchManufacturingResponse>> CompleteStepBatchAsync(
        StepBatchManufacturingRequest request,
        Guid actorUserId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        var errors = ValidateStepBatch(request);
        if (errors.Count > 0)
        {
            return ManufacturingMutationResult<StepBatchManufacturingResponse>.Validation(errors);
        }

        var targetStepName = request.TargetStepName!.Trim();
        var requestedPanels = request.Panels!
            .OrderBy(panel => panel.PanelId)
            .ToArray();
        var panelIds = requestedPanels.Select(panel => panel.PanelId).ToArray();
        var fingerprint = Fingerprint(
            "StepBatch",
            request.ProjectId,
            request.TargetStepSequence,
            targetStepName,
            string.Join(",", requestedPanels.Select(panel =>
                $"{panel.PanelId:D}:{panel.ExecutionId:D}:{panel.ExpectedVersion}")));

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var project = await LockProjectAsync(connection, transaction, request.ProjectId, accessScope, cancellationToken);
            if (project is null)
            {
                return await RollbackNotFound<StepBatchManufacturingResponse>(transaction, cancellationToken);
            }

            var replay = await ReadStepBatchOperationAsync(
                connection,
                transaction,
                request.OperationId,
                cancellationToken);
            if (replay is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                if (replay.ProjectId != request.ProjectId
                    || replay.RequestedByUserId != actorUserId
                    || !string.Equals(replay.PayloadFingerprint, fingerprint, StringComparison.Ordinal)
                    || !replay.PanelIds.SequenceEqual(panelIds))
                {
                    return ManufacturingMutationResult<StepBatchManufacturingResponse>.Conflict(
                        "같은 작업 식별자를 다른 제조 단계 완료 요청에 사용할 수 없습니다. 최신 내용을 다시 불러와 주세요.");
                }

                return ManufacturingMutationResult<StepBatchManufacturingResponse>.Success(
                    new StepBatchManufacturingResponse(
                        request.OperationId,
                        request.ProjectId,
                        replay.CompletedPanelCount,
                        replay.CheckedStepCount,
                        true));
            }

            if (!string.Equals(project.Status, "Active", StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(cancellationToken);
                return ManufacturingMutationResult<StepBatchManufacturingResponse>.Conflict(
                    "진행 중인 프로젝트에서만 제조 단계를 완료할 수 있습니다.");
            }

            var targets = new List<StepBatchTarget>(requestedPanels.Length);
            var failures = new List<StepBatchFailure>();
            foreach (var requestedPanel in requestedPanels)
            {
                var snapshot = await LockExecutionAsync(
                    connection,
                    transaction,
                    requestedPanel.ExecutionId,
                    cancellationToken);
                if (snapshot is null
                    || snapshot.ProjectId != request.ProjectId
                    || snapshot.PanelId != requestedPanel.PanelId)
                {
                    failures.Add(new StepBatchFailure("선택 패널", "NOT_FOUND_IN_PROJECT"));
                    continue;
                }
                if (!snapshot.PanelActive)
                {
                    failures.Add(new StepBatchFailure(snapshot.DisplayCode, "NOT_FOUND_IN_PROJECT"));
                    continue;
                }
                if (string.Equals(snapshot.Status, ManufacturingExecutionStatuses.Blocked, StringComparison.Ordinal))
                {
                    failures.Add(new StepBatchFailure(snapshot.DisplayCode, "BLOCKED_PENDING"));
                    continue;
                }
                if (!string.Equals(snapshot.Status, ManufacturingExecutionStatuses.InProgress, StringComparison.Ordinal))
                {
                    failures.Add(new StepBatchFailure(snapshot.DisplayCode, "NOT_IN_PROGRESS"));
                    continue;
                }
                if (snapshot.Version != requestedPanel.ExpectedVersion)
                {
                    failures.Add(new StepBatchFailure(snapshot.DisplayCode, "STALE_VERSION"));
                    continue;
                }

                var steps = await LockStepsAsync(
                    connection,
                    transaction,
                    requestedPanel.ExecutionId,
                    cancellationToken);
                var targetStep = steps.SingleOrDefault(step =>
                    step.SequenceNumber == request.TargetStepSequence
                    && string.Equals(step.StepName, targetStepName, StringComparison.Ordinal));
                if (targetStep is null)
                {
                    failures.Add(new StepBatchFailure(snapshot.DisplayCode, "STEP_NOT_FOUND_OR_CHANGED"));
                    continue;
                }
                if (targetStep.CheckedAtUtc is not null)
                {
                    failures.Add(new StepBatchFailure(snapshot.DisplayCode, "STEP_ALREADY_COMPLETED"));
                    continue;
                }

                targets.Add(new StepBatchTarget(
                    requestedPanel.ExecutionId,
                    snapshot.PanelId,
                    snapshot.DisplayCode,
                    targetStep));
            }

            if (failures.Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ManufacturingMutationResult<StepBatchManufacturingResponse>.Conflict(
                    FormatStepBatchFailures(failures));
            }

            var checkedStepCount = targets.Count;
            await InsertStepBatchOperationAsync(
                connection,
                transaction,
                request.OperationId,
                request.ProjectId,
                actorUserId,
                panelIds,
                fingerprint,
                targets.Count,
                checkedStepCount,
                cancellationToken);

            foreach (var target in targets)
            {
                var step = target.StepToCheck;
                await using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = """
                        update panel_manufacturing_execution_steps
                        set checked_by_user_id = @actor_id, checked_at_utc = now()
                        where id = @step_id and checked_at_utc is null;
                        """;
                    command.Parameters.AddWithValue("actor_id", actorUserId);
                    command.Parameters.AddWithValue("step_id", step.StepId);
                    if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return ManufacturingMutationResult<StepBatchManufacturingResponse>.Conflict(
                            "다른 사용자가 먼저 제조 단계를 변경했습니다. 최신 내용을 다시 불러와 주세요.");
                    }
                }

                await InsertEventAsync(
                    connection,
                    transaction,
                    target.ExecutionId,
                    "StepChecked",
                    step.StepId,
                    null,
                    null,
                    null,
                    actorUserId,
                    cancellationToken,
                    request.OperationId);
                await IncrementVersionAsync(
                    connection,
                    transaction,
                    target.ExecutionId,
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return ManufacturingMutationResult<StepBatchManufacturingResponse>.Success(
                new StepBatchManufacturingResponse(
                    request.OperationId,
                    request.ProjectId,
                    targets.Count,
                    checkedStepCount,
                    false));
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ManufacturingMutationResult<StepBatchManufacturingResponse>.Conflict(
                "다른 요청이 먼저 제조 단계를 변경했습니다. 최신 내용을 다시 불러와 주세요.");
        }
    }

    public async Task<ManufacturingMutationResult<ManufacturingMutationResponse>> StopAsync(
        Guid executionId,
        StopManufacturingRequest request,
        Guid actorUserId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        var errors = ValidateVersioned(request.OperationId, request.ExpectedVersion);
        var reason = request.ReasonCode?.Trim() ?? "";
        var description = request.Description?.Trim() ?? "";
        var department = request.ActionDepartmentCode?.Trim() ?? "";
        if (!ManufacturingStopReasons.All.Contains(reason))
        {
            errors[nameof(request.ReasonCode)] = ["중단 사유를 선택해 주세요."];
        }
        if (description.Length is < 10 or > 1000)
        {
            errors[nameof(request.Description)] = ["중단 설명을 10~1,000자로 입력해 주세요."];
        }
        if (department.Length is < 2 or > 80)
        {
            errors[nameof(request.ActionDepartmentCode)] = ["조치 담당 부서를 선택해 주세요."];
        }
        if (errors.Count > 0)
        {
            return ManufacturingMutationResult<ManufacturingMutationResponse>.Validation(errors);
        }

        return await MutateExistingAsync(
            executionId,
            request.OperationId,
            "Stop",
            Fingerprint("Stop", executionId, reason, description, department, request.AssigneeUserId, request.ExpectedVersion),
            actorUserId,
            accessScope,
            request.ExpectedVersion,
            async (connection, transaction, snapshot) =>
            {
                if (!string.Equals(snapshot.Status, ManufacturingExecutionStatuses.InProgress, StringComparison.Ordinal))
                {
                    return MutationActionResult.Conflict("진행 중인 제조 작업만 중단할 수 있습니다.");
                }
                if (!await ValidateActionOwnerAsync(connection, transaction, department, request.AssigneeUserId, cancellationToken))
                {
                    return MutationActionResult.Validation(new Dictionary<string, string[]>
                    {
                        [nameof(request.AssigneeUserId)] = ["선택한 부서에서 Pending 조치 권한이 있는 활성 담당자를 선택해 주세요."]
                    });
                }
                var pending = await pendingStore.CreateManufacturingStopAsync(
                    connection,
                    transaction,
                    snapshot.ProjectId,
                    snapshot.PanelId,
                    snapshot.DisplayCode,
                    reason,
                    description,
                    department,
                    request.AssigneeUserId,
                    actorUserId,
                    request.OperationId.ToString("D"),
                    cancellationToken);
                await using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = """
                        update panel_manufacturing_executions
                        set status = 'Blocked', active_stop_pending_id = @pending_id,
                            version = version + 1, updated_at_utc = now()
                        where id = @execution_id;
                        """;
                    command.Parameters.AddWithValue("pending_id", pending.PendingId);
                    command.Parameters.AddWithValue("execution_id", executionId);
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
                await InsertEventAsync(connection, transaction, executionId, "Stopped", null, pending.PendingId, reason, description, actorUserId, cancellationToken);
                await CreateProductionReferenceNotificationAsync(connection, transaction, snapshot.ProjectId, snapshot.PanelId, request.OperationId, pending.PendingId, cancellationToken);
                return MutationActionResult.Success(new ManufacturingMutationResponse(
                    request.OperationId, snapshot.ProjectId, snapshot.PanelId, executionId,
                    ManufacturingExecutionStatuses.Blocked, snapshot.Version + 1, snapshot.CheckedStepCount, snapshot.TotalStepCount,
                    pending.PendingId, pending.IssueNumber, false, false, false));
            },
            cancellationToken);
    }

    public Task<ManufacturingMutationResult<ManufacturingMutationResponse>> ResumeAsync(
        Guid executionId,
        ResumeManufacturingRequest request,
        Guid actorUserId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        var errors = ValidateVersioned(request.OperationId, request.ExpectedVersion);
        return errors.Count > 0
            ? Task.FromResult(ManufacturingMutationResult<ManufacturingMutationResponse>.Validation(errors))
            : MutateExistingAsync(
                executionId,
                request.OperationId,
                "Resume",
                Fingerprint("Resume", executionId, request.ExpectedVersion),
                actorUserId,
                accessScope,
                request.ExpectedVersion,
                async (connection, transaction, snapshot) =>
                {
                    if (!string.Equals(snapshot.Status, ManufacturingExecutionStatuses.Blocked, StringComparison.Ordinal)
                        || snapshot.ActivePendingId is null)
                    {
                        return MutationActionResult.Conflict("중단된 제조 작업만 재개할 수 있습니다.");
                    }
                    var pending = await ReadPendingAsync(connection, transaction, snapshot.ActivePendingId.Value, cancellationToken);
                    if (pending is null || !string.Equals(pending.Status, PendingStatuses.Closed, StringComparison.Ordinal))
                    {
                        return MutationActionResult.Conflict("연결된 Pending을 종결한 뒤 제조 작업을 재개해 주세요.");
                    }
                    await using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = """
                            update panel_manufacturing_executions
                            set status = 'InProgress', active_stop_pending_id = null,
                                version = version + 1, updated_at_utc = now()
                            where id = @execution_id;
                            """;
                        command.Parameters.AddWithValue("execution_id", executionId);
                        await command.ExecuteNonQueryAsync(cancellationToken);
                    }
                    await InsertEventAsync(connection, transaction, executionId, "Resumed", null, snapshot.ActivePendingId, null, null, actorUserId, cancellationToken);
                    return MutationActionResult.Success(new ManufacturingMutationResponse(
                        request.OperationId, snapshot.ProjectId, snapshot.PanelId, executionId,
                        ManufacturingExecutionStatuses.InProgress, snapshot.Version + 1, snapshot.CheckedStepCount, snapshot.TotalStepCount,
                        null, null, false, false, false));
                },
                cancellationToken);
    }

    public async Task<ManufacturingMutationResult<ManufacturingMutationResponse>> CompleteAsync(
        Guid executionId,
        CompleteManufacturingRequest request,
        Guid actorUserId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        var errors = ValidateVersioned(request.OperationId, request.ExpectedVersion);
        return errors.Count > 0
            ? ManufacturingMutationResult<ManufacturingMutationResponse>.Validation(errors)
            : await MutateExistingAsync(
                executionId,
                request.OperationId,
                "Complete",
                Fingerprint("Complete", executionId, request.ExpectedVersion),
                actorUserId,
                accessScope,
                request.ExpectedVersion,
                async (connection, transaction, snapshot) =>
                {
                    if (!string.Equals(snapshot.Status, ManufacturingExecutionStatuses.InProgress, StringComparison.Ordinal))
                    {
                        return MutationActionResult.Conflict("진행 중인 제조 작업만 종료할 수 있습니다.");
                    }
                    if (snapshot.TotalStepCount == 0 || snapshot.CheckedStepCount != snapshot.TotalStepCount)
                    {
                        return MutationActionResult.Conflict($"제조 단계 {snapshot.TotalStepCount}개를 모두 확인한 뒤 종료해 주세요.");
                    }
                    await using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = """
                            update panel_manufacturing_executions
                            set status = 'Completed', completed_by_user_id = @actor_id,
                                completed_at_utc = now(), version = version + 1, updated_at_utc = now()
                            where id = @execution_id;

                            update panel_placeholders
                            set workflow_stage = 'ManufacturingCompleted', updated_at_utc = now()
                            where id = @panel_id and workflow_stage = 'ManufacturingInProgress';

                            update work_items
                            set status = 'Completed', completed_at_utc = coalesce(completed_at_utc, now())
                            where id = @work_item_id and status = 'InProgress';
                            """;
                        command.Parameters.AddWithValue("actor_id", actorUserId);
                        command.Parameters.AddWithValue("execution_id", executionId);
                        command.Parameters.AddWithValue("panel_id", snapshot.PanelId);
                        command.Parameters.AddWithValue("work_item_id", snapshot.WorkItemId);
                        await command.ExecuteNonQueryAsync(cancellationToken);
                    }
                    var oqcHandoff = await QualityInspectionStore.TryOpenOqcAfterManufacturingAndLqcAsync(
                        connection,
                        transaction,
                        snapshot.ProjectId,
                        snapshot.PanelId,
                        actorUserId,
                        request.OperationId,
                        cancellationToken);
                    if (oqcHandoff.ConflictMessage is not null)
                    {
                        return MutationActionResult.Conflict(oqcHandoff.ConflictMessage);
                    }
                    await InsertEventAsync(connection, transaction, executionId, "Completed", null, null, null, null, actorUserId, cancellationToken);
                    var projectCompleted = await IsProjectManufacturingCompletedAsync(connection, transaction, snapshot.ProjectId, cancellationToken);
                    if (projectCompleted)
                    {
                        await EnsureManufacturingStageEventAsync(connection, transaction, snapshot.ProjectId, executionId, request.OperationId, actorUserId, cancellationToken);
                    }
                    var response = new ManufacturingMutationResponse(
                        request.OperationId, snapshot.ProjectId, snapshot.PanelId, executionId,
                        ManufacturingExecutionStatuses.Completed, snapshot.Version + 1, snapshot.CheckedStepCount, snapshot.TotalStepCount,
                        null, null, false, projectCompleted, false);
                    return MutationActionResult.Success(response);
                },
                cancellationToken);
    }

    internal static async Task CancelActiveExecutionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid panelId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        Guid? executionId = null;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update panel_manufacturing_executions
                set status = 'Cancelled', cancelled_by_user_id = @actor_id,
                    cancelled_at_utc = now(), active_stop_pending_id = null,
                    version = version + 1, updated_at_utc = now()
                where panel_id = @panel_id and status in ('InProgress', 'Blocked')
                returning id;
                """;
            command.Parameters.AddWithValue("actor_id", actorUserId);
            command.Parameters.AddWithValue("panel_id", panelId);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            executionId = result is Guid value ? value : null;
        }
        if (executionId is not null)
        {
            await InsertEventAsync(connection, transaction, executionId.Value, "Cancelled", null, null, null, null, actorUserId, cancellationToken);
        }
    }

    internal static async Task CancelProjectExecutionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var executionIds = new List<Guid>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update panel_manufacturing_executions
                set status = 'Cancelled', cancelled_by_user_id = @actor_id,
                    cancelled_at_utc = now(), active_stop_pending_id = null,
                    version = version + 1, updated_at_utc = now()
                where project_id = @project_id and status in ('InProgress', 'Blocked')
                returning id;
                """;
            command.Parameters.AddWithValue("actor_id", actorUserId);
            command.Parameters.AddWithValue("project_id", projectId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                executionIds.Add(reader.GetGuid(0));
            }
        }
        foreach (var executionId in executionIds)
        {
            await InsertEventAsync(connection, transaction, executionId, "Cancelled", null, null, null, null, actorUserId, cancellationToken);
        }
    }

    private async Task<ManufacturingMutationResult<ManufacturingMutationResponse>> MutateExistingAsync(
        Guid executionId,
        Guid operationId,
        string action,
        string fingerprint,
        Guid actorUserId,
        ProjectAccessScope accessScope,
        int expectedVersion,
        Func<NpgsqlConnection, NpgsqlTransaction, ExecutionSnapshot, Task<MutationActionResult>> actionHandler,
        CancellationToken cancellationToken)
    {
        if (executionId == Guid.Empty)
        {
            return ManufacturingMutationResult<ManufacturingMutationResponse>.Validation(
                new Dictionary<string, string[]> { [nameof(executionId)] = ["제조 실행을 선택해 주세요."] });
        }
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var identity = await ReadExecutionIdentityAsync(connection, transaction, executionId, cancellationToken);
            if (identity is null)
            {
                return await RollbackNotFound(transaction, cancellationToken);
            }
            var project = await LockProjectAsync(connection, transaction, identity.ProjectId, accessScope, cancellationToken);
            if (project is null)
            {
                return await RollbackNotFound(transaction, cancellationToken);
            }
            var replay = await ReadReplayAsync(connection, transaction, operationId, action, fingerprint, actorUserId, cancellationToken);
            if (replay.Result is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return replay.Result;
            }
            if (replay.Conflict is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ManufacturingMutationResult<ManufacturingMutationResponse>.Conflict(replay.Conflict);
            }
            var snapshot = await LockExecutionAsync(connection, transaction, executionId, cancellationToken);
            if (snapshot is null || !string.Equals(project.Status, "Active", StringComparison.Ordinal) || !snapshot.PanelActive)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ManufacturingMutationResult<ManufacturingMutationResponse>.Conflict("활성 프로젝트와 패널의 제조 작업만 변경할 수 있습니다.");
            }
            if (snapshot.Version != expectedVersion)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ManufacturingMutationResult<ManufacturingMutationResponse>.Conflict("다른 사용자가 먼저 변경했습니다. 최신 내용을 다시 불러와 주세요.");
            }
            var actionResult = await actionHandler(connection, transaction, snapshot);
            if (actionResult.Errors is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ManufacturingMutationResult<ManufacturingMutationResponse>.Validation(actionResult.Errors);
            }
            if (actionResult.ConflictMessage is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ManufacturingMutationResult<ManufacturingMutationResponse>.Conflict(actionResult.ConflictMessage);
            }
            var response = actionResult.Response!;
            await InsertOperationAsync(connection, transaction, operationId, action, snapshot.ProjectId, snapshot.PanelId, executionId, actorUserId, fingerprint, response, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ManufacturingMutationResult<ManufacturingMutationResponse>.Success(response);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ManufacturingMutationResult<ManufacturingMutationResponse>.Conflict("다른 요청이 먼저 제조 작업을 변경했습니다. 최신 내용을 다시 불러와 주세요.");
        }
    }

    private static Dictionary<string, string[]> ValidateRelease(ReleaseManufacturingRequest request)
    {
        var errors = ValidateOperation(request.OperationId);
        if (request.ProjectId == Guid.Empty)
        {
            errors[nameof(request.ProjectId)] = ["프로젝트를 선택해 주세요."];
        }
        if (request.PanelIds is null || request.PanelIds.Count == 0)
        {
            errors[nameof(request.PanelIds)] = ["제조 투입할 패널을 하나 이상 선택해 주세요."];
        }
        else if (request.PanelIds.Count > MaxReleasePanelCount)
        {
            errors[nameof(request.PanelIds)] = [$"한 번에 최대 {MaxReleasePanelCount}개 패널까지 요청할 수 있습니다."];
        }
        else if (request.PanelIds.Any(panelId => panelId == Guid.Empty)
                 || request.PanelIds.Distinct().Count() != request.PanelIds.Count)
        {
            errors[nameof(request.PanelIds)] = ["패널 선택값을 다시 확인해 주세요."];
        }
        return errors;
    }

    private static async Task<ReleaseOperationSnapshot?> ReadReleaseOperationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select project_id, requested_by_user_id, panel_ids,
                   released_panel_count, generated_work_item_count
            from panel_manufacturing_release_operations
            where operation_id = @operation_id;
            """;
        command.Parameters.AddWithValue("operation_id", operationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ReleaseOperationSnapshot(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetFieldValue<Guid[]>(2),
                reader.GetInt32(3),
                reader.GetInt32(4))
            : null;
    }

    private static async Task<IReadOnlyList<ReleasePanelSnapshot>> LockReleasePanelsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        IReadOnlyList<Guid> panelIds,
        CancellationToken cancellationToken)
    {
        var panels = new List<ReleasePanelSnapshot>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select panel.id, panel.display_code, panel.panel_name, panel.panel_info_completed,
                   completion.id is not null, work_item.id is not null
            from panel_placeholders panel
            left join panel_kitting_completions completion on completion.panel_id = panel.id
            left join work_items work_item
              on work_item.idempotency_key = 'kitting:panel:' || panel.id::text || ':manufacturing'
             and work_item.target_type = 'Panel'
             and work_item.target_id = panel.id
            where panel.project_id = @project_id
              and panel.id = any(@panel_ids)
              and panel.status = 'Active'
            order by panel.id
            for update of panel;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("panel_ids", panelIds.ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            panels.Add(new ReleasePanelSnapshot(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetBoolean(3),
                reader.GetBoolean(4),
                reader.GetBoolean(5)));
        }
        return panels;
    }

    private static async Task<MaterialReadinessSnapshot> ReadMaterialReadinessAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select count(*)::int,
                   count(*) filter (where receipt_completed)::int
            from project_procurement_items
            where project_id = @project_id
              and status = 'Active';
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new MaterialReadinessSnapshot(reader.GetInt32(0), reader.GetInt32(1));
    }

    private static async Task<ReleaseAssigneeSnapshot?> ResolveManufacturingAssigneeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var responsibilityTypes = new[] { "ManufacturingPrimary", "ManufacturingSecondary", "Manufacturing" };
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select pa.assigned_user_id,
                   pa.responsibility_type,
                   role.code
            from project_assignees pa
            join qms_users users on users.id = pa.assigned_user_id and users.is_active = true
            left join user_roles user_role on user_role.user_id = users.id
            left join roles role on role.id = user_role.role_id
            where pa.project_id = @project_id
              and pa.responsibility_type = any(@responsibility_types)
              and exists (
                  select 1
                  from user_roles allowed_user_role
                  join role_permissions allowed_role_permission on allowed_role_permission.role_id = allowed_user_role.role_id
                  join permissions allowed_permission on allowed_permission.id = allowed_role_permission.permission_id
                  where allowed_user_role.user_id = users.id
                    and allowed_permission.code = @permission_code
              )
            order by array_position(@responsibility_types, pa.responsibility_type), users.display_name, users.id
            limit 1;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("responsibility_types", responsibilityTypes);
        command.Parameters.AddWithValue("permission_code", QmsPermissions.ManufacturingUpdate);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ReleaseAssigneeSnapshot(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2))
            : null;
    }

    private static async Task<int> InsertManufacturingReleaseWorkItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        ReleasePanelSnapshot panel,
        MaterialReadinessSnapshot readiness,
        ReleaseAssigneeSnapshot assignee,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = $"kitting:panel:{panel.PanelId}:manufacturing";
        var title = $"제조 투입 요청 · {panel.DisplayCode}";
        var kittingLabel = panel.KittingCompleted ? "키팅 완료" : "키팅 미보고";
        var materialLabel = readiness.ActiveItemCount == 0
            ? "구매품목 없음"
            : $"자재 입고 {readiness.CompletedItemCount}/{readiness.ActiveItemCount}";
        var description = $"{kittingLabel} · {materialLabel}. 패널 제조 작업을 시작해 주세요.";

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into work_items (
                project_id, target_type, target_id, workflow_stage_code, responsibility_type,
                assigned_user_id, assigned_role_code, title, description, status, priority,
                idempotency_key, created_by_user_id
            )
            values (
                @project_id, 'Panel', @panel_id, 'ManufacturingWork', @responsibility_type,
                @assignee_id, @role_code, @title, @description, 'Requested', 'Normal',
                @idempotency_key, @actor_id
            );
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("panel_id", panel.PanelId);
        command.Parameters.AddWithValue("responsibility_type", assignee.ResponsibilityType);
        command.Parameters.AddWithValue("assignee_id", assignee.UserId);
        AddNullableText(command, "role_code", assignee.RoleCode);
        command.Parameters.AddWithValue("title", title);
        command.Parameters.AddWithValue("description", $"{description} /manufacturing/work?project={projectId}&panel={panel.PanelId}");
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        var inserted = await command.ExecuteNonQueryAsync(cancellationToken);

        await using var readCommand = connection.CreateCommand();
        readCommand.Transaction = transaction;
        readCommand.CommandText = "select id from work_items where idempotency_key = @key;";
        readCommand.Parameters.AddWithValue("key", idempotencyKey);
        if (await readCommand.ExecuteScalarAsync(cancellationToken) is Guid workItemId)
        {
            await WorkAssignmentNotificationWriter.UpsertAsync(
                connection,
                transaction,
                projectId,
                workItemId,
                assignee.UserId,
                ["ManufacturingSecondary"],
                title,
                description,
                $"/manufacturing/work?project={projectId}&panel={panel.PanelId}",
                $"{idempotencyKey}:notification",
                cancellationToken);
        }
        return inserted;
    }

    private static async Task InsertReleaseOperationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid operationId,
        Guid projectId,
        Guid actorUserId,
        IReadOnlyList<Guid> panelIds,
        int releasedPanelCount,
        int generatedWorkItemCount,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into panel_manufacturing_release_operations (
                operation_id, project_id, requested_by_user_id, panel_ids,
                released_panel_count, generated_work_item_count
            )
            values (
                @operation_id, @project_id, @actor_id, @panel_ids,
                @released_panel_count, @generated_work_item_count
            );
            """;
        command.Parameters.AddWithValue("operation_id", operationId);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        command.Parameters.AddWithValue("panel_ids", panelIds.ToArray());
        command.Parameters.AddWithValue("released_panel_count", releasedPanelCount);
        command.Parameters.AddWithValue("generated_work_item_count", generatedWorkItemCount);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<ProjectSnapshot?> LockProjectAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, status
            from projects
            where id = @project_id
              and deleted_at_utc is null
              and (@has_read_all or project_key = any(@project_keys))
            for update;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("has_read_all", accessScope.HasProjectReadAll);
        command.Parameters.AddWithValue("project_keys", accessScope.ProjectKeys.ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? new ProjectSnapshot(reader.GetGuid(0), reader.GetString(1)) : null;
    }

    private static async Task<ReadyPanelSnapshot?> LockReadyPanelAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid panelId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select panel.id, panel.display_code, panel.panel_name, panel.workflow_stage,
                   work_item.id, work_item.status
            from panel_placeholders panel
            join work_items work_item
              on work_item.idempotency_key = 'kitting:panel:' || panel.id::text || ':manufacturing'
            where panel.id = @panel_id
              and panel.project_id = @project_id
              and panel.status = 'Active'
            for update of panel, work_item;
            """;
        command.Parameters.AddWithValue("panel_id", panelId);
        command.Parameters.AddWithValue("project_id", projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ReadyPanelSnapshot(
                reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3), reader.GetGuid(4), reader.GetString(5))
            : null;
    }

    private static async Task<ManufacturingStepTemplateSnapshot?> LockStepTemplateForProjectAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using (var mode = connection.CreateCommand())
        {
            mode.Transaction = transaction;
            mode.CommandText = """
                select coalesce(plan.model_version,'LEGACY')
                from projects project
                left join project_production_plans plan on plan.project_id=project.id
                where project.id=@project_id;
                """;
            mode.Parameters.AddWithValue("project_id", projectId);
            if (string.Equals(Convert.ToString(await mode.ExecuteScalarAsync(cancellationToken)), "LINKED_V1", StringComparison.Ordinal))
            {
                var projectSteps = new List<ManufacturingStartStep>();
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    select id,definition_key,step_name_snapshot
                    from project_manufacturing_step_snapshots
                    where project_id=@project_id and is_active
                    order by sequence_number
                    for share;
                    """;
                command.Parameters.AddWithValue("project_id", projectId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    projectSteps.Add(new(
                        reader.GetGuid(0),
                        reader.GetGuid(1),
                        reader.GetString(2)));
                }
                return projectSteps.Count == 0 ? null : new(null, projectSteps);
            }
        }

        Guid versionId;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select version.id
                from manufacturing_step_template_versions version
                join manufacturing_step_templates template on template.id = version.template_id
                where template.template_code = 'PANEL_MANUFACTURING'
                  and version.lifecycle_status = 'Active'
                  and version.is_active = true
                for share of version;
                """;
            var value = await command.ExecuteScalarAsync(cancellationToken);
            if (value is not Guid activeVersionId)
            {
                return null;
            }
            versionId = activeVersionId;
        }

        var steps = new List<ManufacturingStartStep>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select label
                from manufacturing_step_template_items
                where template_version_id = @version_id
                order by display_order;
                """;
            command.Parameters.AddWithValue("version_id", versionId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                steps.Add(new(null, null, reader.GetString(0)));
            }
        }

        return new(versionId, steps);
    }

    private static async Task<ExecutionIdentity?> ReadExecutionIdentityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid executionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select project_id, panel_id from panel_manufacturing_executions where id = @id;";
        command.Parameters.AddWithValue("id", executionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? new ExecutionIdentity(reader.GetGuid(0), reader.GetGuid(1)) : null;
    }

    private static async Task<ExecutionSnapshot?> LockExecutionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid executionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select execution.project_id, execution.panel_id, execution.status, execution.version,
                   execution.active_stop_pending_id, panel.display_code, panel.panel_name,
                   panel.status = 'Active', panel.workflow_stage, work_item.id, work_item.status,
                   step_counts.checked_count,
                   step_counts.total_count
            from panel_manufacturing_executions execution
            join panel_placeholders panel on panel.id = execution.panel_id
            join work_items work_item
              on work_item.idempotency_key = 'kitting:panel:' || panel.id::text || ':manufacturing'
            cross join lateral (
                select
                    count(*) filter (where checked_at_utc is not null)::int as checked_count,
                    count(*)::int as total_count
                from panel_manufacturing_execution_steps
                where execution_id = execution.id
            ) step_counts
            where execution.id = @execution_id
            for update of execution, panel, work_item;
            """;
        command.Parameters.AddWithValue("execution_id", executionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ExecutionSnapshot(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetBoolean(7), reader.GetString(8), reader.GetGuid(9), reader.GetString(10), reader.GetInt32(11), reader.GetInt32(12))
            : null;
    }

    private static async Task<IReadOnlyList<StepSnapshot>> LockStepsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid executionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, sequence_number, step_name, checked_at_utc
            from panel_manufacturing_execution_steps
            where execution_id = @execution_id
            order by sequence_number
            for update;
            """;
        command.Parameters.AddWithValue("execution_id", executionId);
        var rows = new List<StepSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new StepSnapshot(reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3)));
        }
        return rows;
    }

    private static async Task<int> IncrementVersionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid executionId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update panel_manufacturing_executions
            set version = version + 1, updated_at_utc = now()
            where id = @execution_id
            returning version;
            """;
        command.Parameters.AddWithValue("execution_id", executionId);
        return (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
    }

    private static async Task InsertEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid executionId,
        string eventType,
        Guid? stepId,
        Guid? pendingId,
        string? reasonCode,
        string? description,
        Guid actorUserId,
        CancellationToken cancellationToken,
        Guid? batchOperationId = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into panel_manufacturing_events (
                execution_id, event_type, step_id, pending_issue_id,
                stop_reason_code, stop_description, actor_user_id, batch_operation_id
            )
            values (
                @execution_id, @event_type, @step_id, @pending_id,
                @reason_code, @description, @actor_id, @batch_operation_id
            );
            """;
        command.Parameters.AddWithValue("execution_id", executionId);
        command.Parameters.AddWithValue("event_type", eventType);
        AddNullableUuid(command, "step_id", stepId);
        AddNullableUuid(command, "pending_id", pendingId);
        AddNullableText(command, "reason_code", reasonCode);
        AddNullableText(command, "description", description);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        AddNullableUuid(command, "batch_operation_id", batchOperationId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<StepBatchOperationSnapshot?> ReadStepBatchOperationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select project_id, requested_by_user_id, panel_ids, payload_fingerprint,
                   completed_panel_count, checked_step_count
            from panel_manufacturing_assembly_batch_operations
            where operation_id = @operation_id
            for update;
            """;
        command.Parameters.AddWithValue("operation_id", operationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new StepBatchOperationSnapshot(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetFieldValue<Guid[]>(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetInt32(5))
            : null;
    }

    private static async Task InsertStepBatchOperationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid operationId,
        Guid projectId,
        Guid actorUserId,
        IReadOnlyList<Guid> panelIds,
        string payloadFingerprint,
        int completedPanelCount,
        int checkedStepCount,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into panel_manufacturing_assembly_batch_operations (
                operation_id, project_id, requested_by_user_id, panel_ids,
                payload_fingerprint, completed_panel_count, checked_step_count
            )
            values (
                @operation_id, @project_id, @actor_id, @panel_ids,
                @payload_fingerprint, @completed_panel_count, @checked_step_count
            );
            """;
        command.Parameters.AddWithValue("operation_id", operationId);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        command.Parameters.Add(new NpgsqlParameter<Guid[]>("panel_ids", panelIds.ToArray()));
        command.Parameters.AddWithValue("payload_fingerprint", payloadFingerprint);
        command.Parameters.AddWithValue("completed_panel_count", completedPanelCount);
        command.Parameters.AddWithValue("checked_step_count", checkedStepCount);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<ReplaySnapshot> ReadReplayAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid operationId,
        string action,
        string fingerprint,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select action, payload_fingerprint, requested_by_user_id, result_projection::text
            from panel_manufacturing_operations
            where operation_id = @operation_id
            for update;
            """;
        command.Parameters.AddWithValue("operation_id", operationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new ReplaySnapshot(null, null);
        }
        if (!string.Equals(reader.GetString(0), action, StringComparison.Ordinal)
            || !string.Equals(reader.GetString(1), fingerprint, StringComparison.Ordinal)
            || reader.GetGuid(2) != actorUserId)
        {
            return new ReplaySnapshot(null, "같은 작업 식별자를 다른 요청에 사용할 수 없습니다. 최신 내용을 다시 불러와 주세요.");
        }
        var response = JsonSerializer.Deserialize<ManufacturingMutationResponse>(reader.GetString(3));
        return response is null
            ? new ReplaySnapshot(null, "저장된 작업 결과를 읽을 수 없습니다. 최신 내용을 다시 불러와 주세요.")
            : new ReplaySnapshot(ManufacturingMutationResult<ManufacturingMutationResponse>.Success(response with { Replayed = true }), null);
    }

    private static async Task InsertOperationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid operationId,
        string action,
        Guid projectId,
        Guid panelId,
        Guid? executionId,
        Guid actorUserId,
        string fingerprint,
        ManufacturingMutationResponse response,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into panel_manufacturing_operations (
                operation_id, action, project_id, panel_id, execution_id,
                requested_by_user_id, payload_fingerprint, result_projection
            )
            values (
                @operation_id, @action, @project_id, @panel_id, @execution_id,
                @actor_id, @fingerprint, @projection::jsonb
            );
            """;
        command.Parameters.AddWithValue("operation_id", operationId);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("panel_id", panelId);
        AddNullableUuid(command, "execution_id", executionId);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        command.Parameters.AddWithValue("fingerprint", fingerprint);
        command.Parameters.AddWithValue("projection", JsonSerializer.Serialize(response));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> ValidateActionOwnerAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string departmentCode,
        Guid? assigneeUserId,
        CancellationToken cancellationToken)
    {
        await using var departmentCommand = connection.CreateCommand();
        departmentCommand.Transaction = transaction;
        departmentCommand.CommandText = "select exists (select 1 from departments where code = @code and is_active = true);";
        departmentCommand.Parameters.AddWithValue("code", departmentCode);
        if (await departmentCommand.ExecuteScalarAsync(cancellationToken) is not true)
        {
            return false;
        }
        if (assigneeUserId is null)
        {
            return true;
        }
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists (
                select 1
                from qms_users users
                join departments department on department.id = users.department_id
                join user_roles user_role on user_role.user_id = users.id
                join role_permissions role_permission on role_permission.role_id = user_role.role_id
                join permissions permission on permission.id = role_permission.permission_id
                where users.id = @user_id
                  and users.is_active = true
                  and department.code = @department_code
                  and permission.code = 'Pending.Manage'
            );
            """;
        command.Parameters.AddWithValue("user_id", assigneeUserId.Value);
        command.Parameters.AddWithValue("department_code", departmentCode);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<PendingSnapshot?> ReadPendingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid pendingId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select status, issue_number from pending_issues where id = @id for update;";
        command.Parameters.AddWithValue("id", pendingId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? new PendingSnapshot(reader.GetString(0), reader.GetInt64(1)) : null;
    }

    private static async Task<AssigneeSnapshot?> ResolveQualityAssigneeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            with candidates as (
                select assignee.assigned_user_id as user_id,
                       role.code as role_code,
                       array_position(@responsibilities, assignee.responsibility_type) as priority,
                       users.display_name
                from project_assignees assignee
                join qms_users users on users.id = assignee.assigned_user_id and users.is_active = true
                left join user_roles user_role on user_role.user_id = users.id
                left join roles role on role.id = user_role.role_id
                where assignee.project_id = @project_id
                  and assignee.responsibility_type = any(@responsibilities)
                  and exists (
                      select 1 from user_roles allowed_role
                      join role_permissions role_permission on role_permission.role_id = allowed_role.role_id
                      join permissions permission on permission.id = role_permission.permission_id
                      where allowed_role.user_id = users.id and permission.code = 'quality.inspect'
                  )
                union all
                select users.id, role.code, 100, users.display_name
                from qms_users users
                join user_roles user_role on user_role.user_id = users.id
                join roles role on role.id = user_role.role_id and role.code = 'quality'
                join role_permissions role_permission on role_permission.role_id = role.id
                join permissions permission on permission.id = role_permission.permission_id and permission.code = 'quality.inspect'
                where users.is_active = true
            )
            select user_id, role_code from candidates order by priority, display_name, user_id limit 1;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("responsibilities", new[] { "QualityLQC", "QualityLQCSecondary" });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new AssigneeSnapshot(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetString(1))
            : null;
    }

    private static async Task<bool> InsertLqcWorkItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid panelId,
        string panelDisplayCode,
        AssigneeSnapshot assignee,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
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
                @idempotency_key, @actor_id
            )
            on conflict (idempotency_key) do nothing;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("panel_id", panelId);
        command.Parameters.AddWithValue("assignee_id", assignee.UserId);
        AddNullableText(command, "role_code", assignee.RoleCode);
        var title = $"LQC 입력 · {panelDisplayCode}";
        var description = "제조 시작과 함께 현재 진행 중인 단계의 LQC를 진행해 주세요.";
        var idempotencyKey = $"manufacturing:panel:{panelId}:lqc";
        command.Parameters.AddWithValue("title", title);
        command.Parameters.AddWithValue("description", $"{description} /quality/inspections?stage=LQC&project={projectId}&panel={panelId}");
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        var inserted = await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        await using var readCommand = connection.CreateCommand();
        readCommand.Transaction = transaction;
        readCommand.CommandText = "select id from work_items where idempotency_key=@key;";
        readCommand.Parameters.AddWithValue("key", idempotencyKey);
        var value = await readCommand.ExecuteScalarAsync(cancellationToken);
        if (value is Guid workItemId)
        {
            await WorkAssignmentNotificationWriter.UpsertAsync(
                connection, transaction, projectId, workItemId, assignee.UserId,
                ["QualityLQCSecondary"], title, description,
                $"/quality/inspections?stage=LQC&project={projectId}&panel={panelId}",
                $"{idempotencyKey}:notification", cancellationToken);
        }
        return inserted;
    }

    private static async Task<bool> IsProjectManufacturingCompletedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select count(*) > 0
               and count(*) = count(*) filter (where workflow_stage in (
                    'ManufacturingCompleted', 'InspectionInProgress', 'InspectionCompleted',
                    'PackingCompleted', 'ShipmentCompleted'
               ))
            from panel_placeholders
            where project_id = @project_id and status = 'Active';
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task EnsureManufacturingStageEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid executionId,
        Guid operationId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into project_workflow_events (
                project_id, stage_code, event_type, event_status, source_type,
                source_id, correlation_id, created_by_user_id, note
            )
            select @project_id, 'ManufacturingWork', 'StageCompleted', 'Succeeded',
                   'PanelManufacturingExecution', @execution_id, @correlation_id, @actor_id,
                   '모든 활성 패널 제조 작업 완료'
            where not exists (
                select 1 from project_workflow_events
                where project_id = @project_id
                  and stage_code = 'ManufacturingWork'
                  and event_type = 'StageCompleted'
                  and event_status = 'Succeeded'
            );
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("execution_id", executionId);
        command.Parameters.AddWithValue("correlation_id", operationId.ToString("D"));
        command.Parameters.AddWithValue("actor_id", actorUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CreateProductionReferenceNotificationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid panelId,
        Guid operationId,
        Guid pendingId,
        CancellationToken cancellationToken)
    {
        Guid notificationId;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into notifications (
                    project_id, notification_type, severity, title, message, link_url, idempotency_key
                )
                values (
                    @project_id, 'Blocking', 'Critical', '제조 중단이 등록되었습니다.',
                    '패널 제조 중단 사유와 조치 담당 부서를 확인해 주세요.',
                    '/pending/' || @pending_id::text, @idempotency_key
                )
                on conflict (idempotency_key) do update set title = excluded.title
                returning id;
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("pending_id", pendingId);
            command.Parameters.AddWithValue("idempotency_key", $"manufacturing:operation:{operationId}:production-reference");
            notificationId = (Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty);
        }
        await using var recipientCommand = connection.CreateCommand();
        recipientCommand.Transaction = transaction;
        recipientCommand.CommandText = """
            insert into notification_recipients (notification_id, user_id)
            select @notification_id, assigned_user_id
            from project_assignees
            where project_id = @project_id
              and responsibility_type in ('ProductionPlanningPrimary', 'ProductionPlanningSecondary')
              and assigned_user_id is not null
            on conflict (notification_id, user_id) do nothing;
            """;
        recipientCommand.Parameters.AddWithValue("notification_id", notificationId);
        recipientCommand.Parameters.AddWithValue("project_id", projectId);
        await recipientCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> IsUl891CurrentDesignReadyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid panelId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select case
                when project.structure_mode <> 'Ul891Set' or panel.set_instance_id is null then true
                else panel.design_slot_id is not null and slot.status = 'Active'
            end
            from panel_placeholders panel
            join projects project on project.id=panel.project_id
            left join ul891_set_design_slots slot on slot.id=panel.design_slot_id
            where panel.id=@panel_id;
            """;
        command.Parameters.AddWithValue("panel_id", panelId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static Dictionary<string, string[]> ValidateOperation(Guid operationId)
    {
        var errors = new Dictionary<string, string[]>();
        if (operationId == Guid.Empty)
        {
            errors[nameof(operationId)] = ["작업 식별자가 필요합니다."];
        }
        return errors;
    }

    private static Dictionary<string, string[]> ValidateStepBatch(
        StepBatchManufacturingRequest request)
    {
        var errors = ValidateOperation(request.OperationId);
        if (request.ProjectId == Guid.Empty)
        {
            errors[nameof(request.ProjectId)] = ["프로젝트 식별자가 필요합니다."];
        }
        if (request.TargetStepSequence is < 1 or > 50)
        {
            errors[nameof(request.TargetStepSequence)] = ["완료할 제조 단계를 선택해 주세요."];
        }
        if (string.IsNullOrWhiteSpace(request.TargetStepName) || request.TargetStepName.Trim().Length > 100)
        {
            errors[nameof(request.TargetStepName)] = ["완료할 제조 단계명을 확인해 주세요."];
        }

        if (request.Panels is null || request.Panels.Count is < 1 or > MaxReleasePanelCount)
        {
            errors[nameof(request.Panels)] = [$"패널을 1~{MaxReleasePanelCount}면 선택해 주세요."];
            return errors;
        }

        if (request.Panels.Any(panel =>
                panel.PanelId == Guid.Empty
                || panel.ExecutionId == Guid.Empty
                || panel.ExpectedVersion < 1))
        {
            errors[nameof(request.Panels)] = ["선택 패널의 제조 실행 정보와 최신 버전을 확인해 주세요."];
            return errors;
        }

        if (request.Panels.Select(panel => panel.PanelId).Distinct().Count() != request.Panels.Count
            || request.Panels.Select(panel => panel.ExecutionId).Distinct().Count() != request.Panels.Count)
        {
            errors[nameof(request.Panels)] = ["같은 패널을 한 요청에 두 번 포함할 수 없습니다."];
        }

        return errors;
    }

    private static string FormatStepBatchFailures(IReadOnlyList<StepBatchFailure> failures)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["NOT_FOUND_IN_PROJECT"] = "선택 프로젝트의 활성 제조 실행이 아닙니다",
            ["NOT_IN_PROGRESS"] = "제조 진행 중 상태가 아닙니다",
            ["BLOCKED_PENDING"] = "중단 Pending 처리가 필요합니다",
            ["STALE_VERSION"] = "다른 사용자가 먼저 변경했습니다",
            ["STEP_NOT_FOUND_OR_CHANGED"] = "선택한 제조 단계가 없거나 이름이 다릅니다",
            ["STEP_ALREADY_COMPLETED"] = "선택한 제조 단계가 이미 완료되었습니다"
        };
        var visible = failures
            .Take(20)
            .Select(failure =>
                $"{failure.PanelDisplayCode}: {labels.GetValueOrDefault(failure.ReasonCode, failure.ReasonCode)}");
        var suffix = failures.Count > 20 ? $" 외 {failures.Count - 20}면" : "";
        return $"선택 패널을 일괄 처리하지 않았습니다. {string.Join(", ", visible)}{suffix}. 최신 내용을 다시 불러와 주세요.";
    }

    private static Dictionary<string, string[]> ValidateVersioned(Guid operationId, int expectedVersion)
    {
        var errors = ValidateOperation(operationId);
        if (expectedVersion < 1)
        {
            errors[nameof(expectedVersion)] = ["최신 제조 실행 버전이 필요합니다."];
        }
        return errors;
    }

    private static string Fingerprint(string action, params object?[] values)
    {
        var normalized = string.Join("|", new[] { action }.Concat(values.Select(value => value switch
        {
            null => "null",
            Guid guid => guid.ToString("D"),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? ""
        })));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }

    private static async Task<ManufacturingMutationResult<ManufacturingMutationResponse>> RollbackNotFound(
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        return ManufacturingMutationResult<ManufacturingMutationResponse>.NotFound();
    }

    private static async Task<ManufacturingMutationResult<T>> RollbackNotFound<T>(
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        return ManufacturingMutationResult<T>.NotFound();
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

    private static string EventLabel(string value) => value switch
    {
        "Started" => "작업 시작",
        "StepChecked" => "단계 확인",
        "Stopped" => "제조 중단",
        "Resumed" => "작업 재개",
        "Completed" => "작업 종료",
        "Cancelled" => "작업 취소",
        _ => value
    };

    private static void AddNullableText(NpgsqlCommand command, string name, string? value)
        => command.Parameters.Add(name, NpgsqlDbType.Text).Value = value ?? (object)DBNull.Value;

    private static void AddNullableUuid(NpgsqlCommand command, string name, Guid? value)
        => command.Parameters.Add(name, NpgsqlDbType.Uuid).Value = value ?? (object)DBNull.Value;

    private sealed class ProjectBuilder(Guid projectId, string projectCode, string projectTitle)
    {
        public List<ManufacturingPanelResponse> Panels { get; } = [];

        public ManufacturingProjectResponse ToResponse() => new(
            projectId,
            projectCode,
            projectTitle,
            Panels.Count(panel => panel.Status == "Ready"),
            Panels.Count(panel => panel.Status == ManufacturingExecutionStatuses.InProgress),
            Panels.Count(panel => panel.Status == ManufacturingExecutionStatuses.Blocked),
            Panels.Count(panel => panel.Status == ManufacturingExecutionStatuses.Completed),
            Panels);
    }

    private sealed class ReleaseProjectBuilder(
        Guid projectId,
        string projectCode,
        string projectTitle,
        int activeItemCount,
        int completedItemCount)
    {
        public List<ManufacturingReleasePanelResponse> Panels { get; } = [];

        public ManufacturingReleaseProjectResponse ToResponse() => new(
            projectId,
            projectCode,
            projectTitle,
            activeItemCount,
            completedItemCount,
            Panels);
    }

    private sealed record ProjectSnapshot(Guid ProjectId, string Status);
    private sealed record ReleaseOperationSnapshot(
        Guid ProjectId,
        Guid RequestedByUserId,
        IReadOnlyList<Guid> PanelIds,
        int ReleasedPanelCount,
        int GeneratedWorkItemCount);
    private sealed record ReleasePanelSnapshot(
        Guid PanelId,
        string DisplayCode,
        string? PanelName,
        bool PanelInfoCompleted,
        bool KittingCompleted,
        bool Released);
    private sealed record MaterialReadinessSnapshot(int ActiveItemCount, int CompletedItemCount);
    private readonly record struct ReleaseAssigneeSnapshot(Guid UserId, string ResponsibilityType, string? RoleCode);
    private sealed record ReadyPanelSnapshot(Guid PanelId, string DisplayCode, string? PanelName, string WorkflowStage, Guid WorkItemId, string WorkItemStatus);
    private readonly record struct ManufacturingStepTemplateSnapshot(
        Guid? LegacyVersionId,
        IReadOnlyList<ManufacturingStartStep> Steps);
    private sealed record ManufacturingStartStep(
        Guid? ProjectStepId,
        Guid? DefinitionKey,
        string Label);
    private sealed record ExecutionIdentity(Guid ProjectId, Guid PanelId);
    private sealed record ExecutionSnapshot(
        Guid ProjectId,
        Guid PanelId,
        string Status,
        int Version,
        Guid? ActivePendingId,
        string DisplayCode,
        string? PanelName,
        bool PanelActive,
        string WorkflowStage,
        Guid WorkItemId,
        string WorkItemStatus,
        int CheckedStepCount,
        int TotalStepCount);
    private sealed record StepSnapshot(Guid StepId, int SequenceNumber, string StepName, DateTimeOffset? CheckedAtUtc);
    private sealed record PendingSnapshot(string Status, long IssueNumber);
    private readonly record struct AssigneeSnapshot(Guid UserId, string? RoleCode);
    private sealed record ReplaySnapshot(ManufacturingMutationResult<ManufacturingMutationResponse>? Result, string? Conflict);
    private sealed record StepBatchOperationSnapshot(
        Guid ProjectId,
        Guid RequestedByUserId,
        IReadOnlyList<Guid> PanelIds,
        string PayloadFingerprint,
        int CompletedPanelCount,
        int CheckedStepCount);
    private sealed record StepBatchTarget(
        Guid ExecutionId,
        Guid PanelId,
        string DisplayCode,
        StepSnapshot StepToCheck);
    private sealed record StepBatchFailure(string PanelDisplayCode, string ReasonCode);
    private sealed record MutationActionResult(
        ManufacturingMutationResponse? Response,
        string? ConflictMessage,
        IReadOnlyDictionary<string, string[]>? Errors)
    {
        public static MutationActionResult Success(ManufacturingMutationResponse response) => new(response, null, null);
        public static MutationActionResult Conflict(string message) => new(null, message, null);
        public static MutationActionResult Validation(IReadOnlyDictionary<string, string[]> errors) => new(null, null, errors);
    }
}
