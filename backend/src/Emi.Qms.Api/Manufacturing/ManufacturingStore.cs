using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Pending;
using Emi.Qms.Api.Projects;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Manufacturing;

public sealed class ManufacturingStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    PendingStore pendingStore)
{
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
                execution.completed_at_utc
            from projects project
            join panel_placeholders panel
              on panel.project_id = project.id
             and panel.status = 'Active'
            join panel_kitting_completions completion on completion.panel_id = panel.id
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
            left join pending_issues pending on pending.id = execution.active_stop_pending_id
            where project.deleted_at_utc is null
              and project.status <> 'Cancelled'
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
                canMutate));
        }

        return new ManufacturingQueueResponse(builders.Values.Select(builder => builder.ToResponse()).ToList());
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
                    new Dictionary<string, string[]> { ["panel"] = ["키팅 완료된 활성 패널만 제조 작업을 시작할 수 있습니다."] });
            }
            if (!string.Equals(panel.WorkflowStage, "BeforeManufacturing", StringComparison.Ordinal)
                || !string.Equals(panel.WorkItemStatus, "Requested", StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(cancellationToken);
                return ManufacturingMutationResult<ManufacturingMutationResponse>.Conflict("이미 제조가 시작됐거나 처리할 수 없는 패널입니다. 최신 내용을 다시 불러와 주세요.");
            }

            var template = await LockActiveStepTemplateAsync(connection, transaction, cancellationToken);
            if (template is null || template.Value.Steps.Count == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ManufacturingMutationResult<ManufacturingMutationResponse>.Conflict("활성 제조 작업 양식이 없습니다. 양식 관리자에게 확인해 주세요.");
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
                command.Parameters.AddWithValue("template_version_id", template.Value.VersionId);
                command.Parameters.AddWithValue("work_item_id", panel.WorkItemId);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            for (var index = 0; index < template.Value.Steps.Count; index++)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    insert into panel_manufacturing_execution_steps (execution_id, sequence_number, step_name)
                    values (@execution_id, @sequence_number, @step_name);
                    """;
                command.Parameters.AddWithValue("execution_id", executionId);
                command.Parameters.AddWithValue("sequence_number", index + 1);
                command.Parameters.AddWithValue("step_name", template.Value.Steps[index]);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            await InsertEventAsync(connection, transaction, executionId, "Started", null, null, null, null, actorUserId, cancellationToken);
            var response = new ManufacturingMutationResponse(
                request.OperationId, request.ProjectId, request.PanelId, executionId,
                ManufacturingExecutionStatuses.InProgress, 1, 0, template.Value.Steps.Count,
                null, null, false, false, false);
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
                    var qualityAssignee = await ResolveQualityAssigneeAsync(connection, transaction, snapshot.ProjectId, cancellationToken);
                    if (qualityAssignee is null)
                    {
                        return MutationActionResult.Validation(new Dictionary<string, string[]>
                        {
                            ["qualityAssignee"] = ["LQC 품질 담당자를 지정한 뒤 다시 시도해 주세요."]
                        });
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
                    var lqcCreated = await InsertLqcWorkItemAsync(connection, transaction, snapshot, qualityAssignee.Value, actorUserId, cancellationToken);
                    await InsertEventAsync(connection, transaction, executionId, "Completed", null, null, null, null, actorUserId, cancellationToken);
                    var projectCompleted = await IsProjectManufacturingCompletedAsync(connection, transaction, snapshot.ProjectId, cancellationToken);
                    if (projectCompleted)
                    {
                        await EnsureManufacturingStageEventAsync(connection, transaction, snapshot.ProjectId, executionId, request.OperationId, actorUserId, cancellationToken);
                    }
                    var response = new ManufacturingMutationResponse(
                        request.OperationId, snapshot.ProjectId, snapshot.PanelId, executionId,
                        ManufacturingExecutionStatuses.Completed, snapshot.Version + 1, snapshot.CheckedStepCount, snapshot.TotalStepCount,
                        null, null, lqcCreated, projectCompleted, false);
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
            join panel_kitting_completions completion on completion.panel_id = panel.id
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

    private static async Task<(Guid VersionId, IReadOnlyList<string> Steps)?> LockActiveStepTemplateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
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

        var steps = new List<string>();
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
                steps.Add(reader.GetString(0));
            }
        }

        return (versionId, steps);
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
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into panel_manufacturing_events (
                execution_id, event_type, step_id, pending_issue_id,
                stop_reason_code, stop_description, actor_user_id
            )
            values (
                @execution_id, @event_type, @step_id, @pending_id,
                @reason_code, @description, @actor_id
            );
            """;
        command.Parameters.AddWithValue("execution_id", executionId);
        command.Parameters.AddWithValue("event_type", eventType);
        AddNullableUuid(command, "step_id", stepId);
        AddNullableUuid(command, "pending_id", pendingId);
        AddNullableText(command, "reason_code", reasonCode);
        AddNullableText(command, "description", description);
        command.Parameters.AddWithValue("actor_id", actorUserId);
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
        ExecutionSnapshot snapshot,
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
        command.Parameters.AddWithValue("project_id", snapshot.ProjectId);
        command.Parameters.AddWithValue("panel_id", snapshot.PanelId);
        command.Parameters.AddWithValue("assignee_id", assignee.UserId);
        AddNullableText(command, "role_code", assignee.RoleCode);
        command.Parameters.AddWithValue("title", $"LQC 입력 · {snapshot.DisplayCode}");
        command.Parameters.AddWithValue("description", "제조 완료 패널의 LQC 입력을 진행해 주세요. 상세 검사 화면은 TASK-012A에서 연결됩니다.");
        command.Parameters.AddWithValue("idempotency_key", $"manufacturing:panel:{snapshot.PanelId}:lqc");
        command.Parameters.AddWithValue("actor_id", actorUserId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
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

    private static Dictionary<string, string[]> ValidateOperation(Guid operationId)
    {
        var errors = new Dictionary<string, string[]>();
        if (operationId == Guid.Empty)
        {
            errors[nameof(operationId)] = ["작업 식별자가 필요합니다."];
        }
        return errors;
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

    private sealed record ProjectSnapshot(Guid ProjectId, string Status);
    private sealed record ReadyPanelSnapshot(Guid PanelId, string DisplayCode, string? PanelName, string WorkflowStage, Guid WorkItemId, string WorkItemStatus);
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
