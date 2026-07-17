using System.Security.Cryptography;
using System.Text;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Projects;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Materials;

public sealed class PanelKittingStore(DatabaseConnectionStringProvider connectionStringProvider)
{
    private const int ReadinessPredicateVersion = 1;
    private const int MaxPanelCountPerOperation = 500;

    public async Task<PanelKittingQueueResponse> ListAsync(
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
                completion.completed_at_utc,
                completed_by.display_name
            from projects project
            cross join lateral (
                select
                    count(*)::int as active_item_count,
                    count(*) filter (where item.receipt_completed)::int as completed_item_count
                from project_procurement_items item
                where item.project_id = project.id
                  and item.status = 'Active'
            ) readiness
            left join panel_placeholders panel
              on panel.project_id = project.id
             and panel.status = 'Active'
            left join panel_kitting_completions completion
              on completion.panel_id = panel.id
            left join qms_users completed_by
              on completed_by.id = completion.completed_by_user_id
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
                    reader.GetInt32(3),
                    reader.GetInt32(4));
                builders[currentProjectId] = builder;
            }

            if (reader.IsDBNull(5))
            {
                continue;
            }

            var completedAt = reader.IsDBNull(9)
                ? (DateTimeOffset?)null
                : reader.GetFieldValue<DateTimeOffset>(9);
            builder.Panels.Add(new PanelKittingPanelResponse(
                reader.GetGuid(5),
                reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetBoolean(8),
                completedAt is not null,
                completedAt,
                reader.IsDBNull(10) ? null : reader.GetString(10),
                builder.Ready && reader.GetBoolean(8) && completedAt is null));
        }

        return new PanelKittingQueueResponse(builders.Values
            .Select(builder => builder.ToResponse())
            .ToList());
    }

    public async Task<MaterialsMutationResult<PanelKittingCompletionResponse>> CompleteAsync(
        CompletePanelKittingRequest request,
        Guid actorUserId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        var validation = ValidateRequest(request);
        if (validation.Count > 0)
        {
            return MaterialsMutationResult<PanelKittingCompletionResponse>.Validation(validation);
        }

        var panelIds = request.PanelIds!
            .Distinct()
            .OrderBy(panelId => panelId)
            .ToList();
        var fingerprint = ComputePanelSetFingerprint(panelIds);

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var project = await LockProjectAsync(
                connection,
                transaction,
                request.ProjectId,
                accessScope,
                cancellationToken);
            if (project is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return MaterialsMutationResult<PanelKittingCompletionResponse>.NotFound();
            }
            if (!string.Equals(project.Status, "Active", StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(cancellationToken);
                return MaterialsMutationResult<PanelKittingCompletionResponse>.Conflict("진행 중인 프로젝트에서만 키팅을 완료할 수 있습니다.");
            }

            var replay = await ReadBatchAsync(connection, transaction, request.OperationId, cancellationToken);
            if (replay is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                if (replay.ProjectId != request.ProjectId
                    || replay.RequestedByUserId != actorUserId
                    || !string.Equals(replay.PanelSetFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    return MaterialsMutationResult<PanelKittingCompletionResponse>.Conflict(
                        "같은 작업 식별자를 다른 요청에 사용할 수 없습니다. 최신 내용을 다시 불러와 주세요.");
                }

                return MaterialsMutationResult<PanelKittingCompletionResponse>.Success(
                    new PanelKittingCompletionResponse(
                        request.OperationId,
                        replay.CompletedPanelCount,
                        replay.GeneratedWorkItemCount,
                        replay.ProjectKittingCompleted,
                        true));
            }

            var panels = await LockPanelsAsync(
                connection,
                transaction,
                request.ProjectId,
                panelIds,
                cancellationToken);
            if (panels.Count != panelIds.Count)
            {
                await transaction.RollbackAsync(cancellationToken);
                return MaterialsMutationResult<PanelKittingCompletionResponse>.Validation(
                    new Dictionary<string, string[]> { [nameof(request.PanelIds)] = ["선택한 패널은 이 프로젝트의 활성 패널이어야 합니다."] });
            }

            var incompleteInfo = panels.Where(panel => !panel.PanelInfoCompleted).Select(panel => panel.DisplayCode).ToList();
            if (incompleteInfo.Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return MaterialsMutationResult<PanelKittingCompletionResponse>.Validation(
                    new Dictionary<string, string[]> { [nameof(request.PanelIds)] = [$"패널정보를 먼저 완료해 주세요: {string.Join(", ", incompleteInfo)}"] });
            }

            var alreadyCompleted = panels.Where(panel => panel.KittingCompleted).Select(panel => panel.DisplayCode).ToList();
            if (alreadyCompleted.Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return MaterialsMutationResult<PanelKittingCompletionResponse>.Conflict(
                    $"이미 키팅 완료된 패널이 포함되어 있습니다: {string.Join(", ", alreadyCompleted)}. 최신 내용을 다시 불러와 주세요.");
            }

            var readiness = await ReadReadinessAsync(connection, transaction, request.ProjectId, cancellationToken);
            if (!readiness.Ready)
            {
                await transaction.RollbackAsync(cancellationToken);
                return MaterialsMutationResult<PanelKittingCompletionResponse>.Validation(
                    new Dictionary<string, string[]> { ["readiness"] = [readiness.ActiveItemCount == 0
                        ? "활성 구매품목이 하나 이상 있어야 키팅을 완료할 수 있습니다."
                        : $"입고가 완료되지 않은 구매품목이 {readiness.ActiveItemCount - readiness.CompletedItemCount}건 있습니다."] });
            }

            var assignee = await ResolveManufacturingAssigneeAsync(
                connection,
                transaction,
                request.ProjectId,
                cancellationToken);
            if (assignee is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return MaterialsMutationResult<PanelKittingCompletionResponse>.Validation(
                    new Dictionary<string, string[]> { ["manufacturingAssignee"] = ["제조 담당자를 지정한 뒤 다시 시도해 주세요."] });
            }

            var panelCounts = await ReadPanelCountsAsync(connection, transaction, request.ProjectId, cancellationToken);
            var projectKittingCompleted = panelCounts.ActivePanelCount > 0
                && panelCounts.CompletedPanelCount + panels.Count >= panelCounts.ActivePanelCount;
            var existingWorkItemCount = await CountExistingManufacturingWorkItemsAsync(
                connection,
                transaction,
                panels.Select(panel => panel.PanelId).ToList(),
                cancellationToken);
            var generatedWorkItemCount = panels.Count - existingWorkItemCount;
            var batchId = Guid.NewGuid();

            await InsertBatchAsync(
                connection,
                transaction,
                batchId,
                request,
                actorUserId,
                fingerprint,
                panels.Count,
                generatedWorkItemCount,
                projectKittingCompleted,
                readiness,
                cancellationToken);

            var actualGeneratedCount = 0;
            foreach (var panel in panels)
            {
                await InsertCompletionAsync(
                    connection,
                    transaction,
                    batchId,
                    request.ProjectId,
                    panel.PanelId,
                    actorUserId,
                    cancellationToken);
                actualGeneratedCount += await InsertManufacturingWorkItemAsync(
                    connection,
                    transaction,
                    request.ProjectId,
                    panel,
                    assignee.Value,
                    actorUserId,
                    cancellationToken);
            }

            if (actualGeneratedCount != generatedWorkItemCount)
            {
                throw new InvalidOperationException("The manufacturing work item count changed during the kitting transaction.");
            }

            Guid? stageEventId = null;
            if (projectKittingCompleted)
            {
                var eventId = await EnsureStageCompletedEventAsync(
                    connection,
                    transaction,
                    request.ProjectId,
                    batchId,
                    request.OperationId,
                    actorUserId,
                    cancellationToken);
                stageEventId = eventId.EventId;
                await CompleteKittingWorkItemAsync(connection, transaction, request.ProjectId, cancellationToken);
            }

            await CreateBatchReferenceNotificationAsync(
                connection,
                transaction,
                request.ProjectId,
                request.OperationId,
                panels.Count,
                stageEventId,
                assignee.Value.UserId,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return MaterialsMutationResult<PanelKittingCompletionResponse>.Success(
                new PanelKittingCompletionResponse(
                    request.OperationId,
                    panels.Count,
                    generatedWorkItemCount,
                    projectKittingCompleted,
                    false));
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            return MaterialsMutationResult<PanelKittingCompletionResponse>.Conflict(
                "다른 요청이 먼저 키팅을 완료했습니다. 최신 내용을 다시 불러와 주세요.");
        }
    }

    internal static async Task EnsureKittingWorkItemIfReadyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var readiness = await ReadReadinessAsync(connection, transaction, projectId, cancellationToken);
        if (!readiness.Ready)
        {
            return;
        }

        await using (var stageCommand = connection.CreateCommand())
        {
            stageCommand.Transaction = transaction;
            stageCommand.CommandText = """
                select exists (
                    select 1
                    from project_workflow_events
                    where project_id = @project_id
                      and stage_code = 'KittingCompleted'
                      and event_type = 'StageCompleted'
                      and event_status = 'Succeeded'
                );
                """;
            stageCommand.Parameters.AddWithValue("project_id", projectId);
            if ((bool)(await stageCommand.ExecuteScalarAsync(cancellationToken) ?? false))
            {
                return;
            }
        }

        var assignee = await ResolveMaterialsAssigneeAsync(connection, transaction, projectId, cancellationToken);
        if (assignee is null)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into work_items (
                project_id, target_type, target_id, workflow_stage_code, responsibility_type,
                assigned_user_id, assigned_role_code, title, description, status, priority,
                idempotency_key, created_by_user_id
            )
            values (
                @project_id, 'Project', @project_id, 'KittingCompleted', 'MaterialsPrimary',
                @assignee_id, @role_code, '패널 키팅 완료',
                '입고 준비가 끝난 프로젝트의 패널 키팅을 완료해 주세요. /materials/kitting?project=' || @project_id::text,
                'Requested', 'Normal', @idempotency_key, @actor_id
            )
            on conflict (idempotency_key) do nothing;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("assignee_id", assignee.Value.UserId);
        AddNullableText(command, "role_code", assignee.Value.RoleCode);
        command.Parameters.AddWithValue("idempotency_key", $"materials:kitting:{projectId}");
        command.Parameters.AddWithValue("actor_id", actorUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Dictionary<string, string[]> ValidateRequest(CompletePanelKittingRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.OperationId == Guid.Empty)
        {
            errors[nameof(request.OperationId)] = ["작업 식별자가 필요합니다."];
        }
        if (request.ProjectId == Guid.Empty)
        {
            errors[nameof(request.ProjectId)] = ["프로젝트를 선택해 주세요."];
        }
        if (request.PanelIds is null || request.PanelIds.Count == 0)
        {
            errors[nameof(request.PanelIds)] = ["키팅 완료할 패널을 하나 이상 선택해 주세요."];
        }
        else if (request.PanelIds.Count > MaxPanelCountPerOperation)
        {
            errors[nameof(request.PanelIds)] = [$"한 번에 최대 {MaxPanelCountPerOperation}개 패널까지 처리할 수 있습니다."];
        }
        else if (request.PanelIds.Any(panelId => panelId == Guid.Empty)
                 || request.PanelIds.Distinct().Count() != request.PanelIds.Count)
        {
            errors[nameof(request.PanelIds)] = ["패널 선택값을 다시 확인해 주세요."];
        }
        return errors;
    }

    private static string ComputePanelSetFingerprint(IReadOnlyList<Guid> panelIds)
    {
        var payload = string.Join("\n", panelIds.Select(panelId => panelId.ToString("D")));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
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
            select project.id, project.status
            from projects project
            where project.id = @project_id
              and project.deleted_at_utc is null
              and (@has_read_all or project.project_key = any(@project_keys))
            for update;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("has_read_all", accessScope.HasProjectReadAll);
        command.Parameters.AddWithValue("project_keys", accessScope.ProjectKeys.ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ProjectSnapshot(reader.GetGuid(0), reader.GetString(1))
            : null;
    }

    private static async Task<BatchSnapshot?> ReadBatchAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select project_id, requested_by_user_id, panel_set_fingerprint,
                   completed_panel_count, generated_work_item_count, project_kitting_completed
            from panel_kitting_batches
            where operation_id = @operation_id;
            """;
        command.Parameters.AddWithValue("operation_id", operationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new BatchSnapshot(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2),
                reader.GetInt32(3), reader.GetInt32(4), reader.GetBoolean(5))
            : null;
    }

    private static async Task<IReadOnlyList<PanelSnapshot>> LockPanelsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        IReadOnlyList<Guid> panelIds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select panel.id, panel.display_code, panel.panel_name, panel.panel_info_completed,
                   panel.status, completion.id is not null
            from panel_placeholders panel
            left join panel_kitting_completions completion on completion.panel_id = panel.id
            where panel.project_id = @project_id
              and panel.id = any(@panel_ids)
              and panel.status = 'Active'
            order by panel.sequence_number
            for update of panel;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.Add(new NpgsqlParameter<Guid[]>("panel_ids", panelIds.ToArray()));
        var panels = new List<PanelSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            panels.Add(new PanelSnapshot(
                reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetBoolean(3), reader.GetBoolean(5)));
        }
        return panels;
    }

    private static async Task<ReadinessSnapshot> ReadReadinessAsync(
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
        return new ReadinessSnapshot(reader.GetInt32(0), reader.GetInt32(1));
    }

    private static async Task<PanelCounts> ReadPanelCountsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select count(*)::int,
                   count(completion.id)::int
            from panel_placeholders panel
            left join panel_kitting_completions completion on completion.panel_id = panel.id
            where panel.project_id = @project_id
              and panel.status = 'Active';
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new PanelCounts(reader.GetInt32(0), reader.GetInt32(1));
    }

    private static async Task<AssigneeSnapshot?> ResolveManufacturingAssigneeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
        => await ResolveAssigneeAsync(
            connection,
            transaction,
            projectId,
            ["ManufacturingPrimary", "ManufacturingSecondary", "Manufacturing", "SalesPrimary", "SalesSecondary"],
            QmsPermissions.ManufacturingUpdate,
            "manufacturing",
            cancellationToken);

    private static async Task<AssigneeSnapshot?> ResolveMaterialsAssigneeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
        => await ResolveAssigneeAsync(
            connection,
            transaction,
            projectId,
            ["MaterialsPrimary", "MaterialsSecondary", "SalesPrimary", "SalesSecondary"],
            QmsPermissions.MaterialReceiptUpdate,
            "materials",
            cancellationToken);

    private static async Task<AssigneeSnapshot?> ResolveAssigneeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        IReadOnlyList<string> responsibilityTypes,
        string permissionCode,
        string operationalRoleCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            with candidates as (
                select pa.assigned_user_id as user_id,
                       role.code as role_code,
                       array_position(@responsibility_types, pa.responsibility_type) as priority,
                       users.display_name
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
                union all
                select users.id, role.code, 100, users.display_name
                from qms_users users
                join user_roles user_role on user_role.user_id = users.id
                join roles role on role.id = user_role.role_id
                join role_permissions role_permission on role_permission.role_id = role.id
                join permissions permission on permission.id = role_permission.permission_id
                where users.is_active = true
                  and role.code = @operational_role_code
                  and permission.code = @permission_code
                union all
                select users.id, role.code, 200, users.display_name
                from qms_users users
                join user_roles user_role on user_role.user_id = users.id
                join roles role on role.id = user_role.role_id
                join role_permissions role_permission on role_permission.role_id = role.id
                join permissions permission on permission.id = role_permission.permission_id
                where users.is_active = true
                  and role.code = 'system-administrator'
                  and permission.code = @permission_code
            )
            select user_id, role_code
            from candidates
            order by priority, display_name, user_id
            limit 1;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("responsibility_types", responsibilityTypes.ToArray());
        command.Parameters.AddWithValue("permission_code", permissionCode);
        command.Parameters.AddWithValue("operational_role_code", operationalRoleCode);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new AssigneeSnapshot(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetString(1))
            : null;
    }

    private static async Task<int> CountExistingManufacturingWorkItemsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<Guid> panelIds,
        CancellationToken cancellationToken)
    {
        var keys = panelIds.Select(panelId => $"kitting:panel:{panelId}:manufacturing").ToArray();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select count(*)::int from work_items where idempotency_key = any(@keys);";
        command.Parameters.AddWithValue("keys", keys);
        return (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
    }

    private static async Task InsertBatchAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid batchId,
        CompletePanelKittingRequest request,
        Guid actorUserId,
        string fingerprint,
        int completedPanelCount,
        int generatedWorkItemCount,
        bool projectKittingCompleted,
        ReadinessSnapshot readiness,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into panel_kitting_batches (
                id, project_id, operation_id, requested_by_user_id, panel_set_fingerprint,
                completed_panel_count, generated_work_item_count, project_kitting_completed,
                readiness_active_item_count, readiness_completed_item_count,
                readiness_predicate_version, readiness_verified_at_utc
            )
            values (
                @id, @project_id, @operation_id, @actor_id, @fingerprint,
                @completed_panel_count, @generated_work_item_count, @project_kitting_completed,
                @active_item_count, @completed_item_count, @predicate_version, now()
            );
            """;
        command.Parameters.AddWithValue("id", batchId);
        command.Parameters.AddWithValue("project_id", request.ProjectId);
        command.Parameters.AddWithValue("operation_id", request.OperationId);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        command.Parameters.AddWithValue("fingerprint", fingerprint);
        command.Parameters.AddWithValue("completed_panel_count", completedPanelCount);
        command.Parameters.AddWithValue("generated_work_item_count", generatedWorkItemCount);
        command.Parameters.AddWithValue("project_kitting_completed", projectKittingCompleted);
        command.Parameters.AddWithValue("active_item_count", readiness.ActiveItemCount);
        command.Parameters.AddWithValue("completed_item_count", readiness.CompletedItemCount);
        command.Parameters.AddWithValue("predicate_version", ReadinessPredicateVersion);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertCompletionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid batchId,
        Guid projectId,
        Guid panelId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into panel_kitting_completions (
                batch_id, project_id, panel_id, completed_by_user_id
            )
            values (@batch_id, @project_id, @panel_id, @actor_id);
            """;
        command.Parameters.AddWithValue("batch_id", batchId);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("panel_id", panelId);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> InsertManufacturingWorkItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        PanelSnapshot panel,
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
                @project_id, 'Panel', @panel_id, 'ManufacturingWork', 'ManufacturingPrimary',
                @assignee_id, @role_code, @title, @description, 'Requested', 'Normal',
                @idempotency_key, @actor_id
            )
            on conflict (idempotency_key) do nothing;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("panel_id", panel.PanelId);
        command.Parameters.AddWithValue("assignee_id", assignee.UserId);
        AddNullableText(command, "role_code", assignee.RoleCode);
        command.Parameters.AddWithValue("title", $"제조 작업 · {panel.DisplayCode}");
        command.Parameters.AddWithValue("description", $"키팅 완료 패널의 제조 작업을 확인해 주세요. /materials/kitting?project={projectId}&panel={panel.PanelId}");
        command.Parameters.AddWithValue("idempotency_key", $"kitting:panel:{panel.PanelId}:manufacturing");
        command.Parameters.AddWithValue("actor_id", actorUserId);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<(Guid EventId, bool Created)> EnsureStageCompletedEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid batchId,
        Guid operationId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using (var readCommand = connection.CreateCommand())
        {
            readCommand.Transaction = transaction;
            readCommand.CommandText = """
                select id
                from project_workflow_events
                where project_id = @project_id
                  and stage_code = 'KittingCompleted'
                  and event_type = 'StageCompleted'
                  and event_status = 'Succeeded'
                order by created_at_utc
                limit 1;
                """;
            readCommand.Parameters.AddWithValue("project_id", projectId);
            var existing = await readCommand.ExecuteScalarAsync(cancellationToken);
            if (existing is Guid existingId)
            {
                return (existingId, false);
            }
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into project_workflow_events (
                project_id, stage_code, event_type, event_status, source_type, source_id,
                correlation_id, created_by_user_id, note
            )
            values (
                @project_id, 'KittingCompleted', 'StageCompleted', 'Succeeded',
                'PanelKittingBatch', @batch_id, @correlation_id, @actor_id,
                '모든 활성 패널 키팅 완료'
            )
            returning id;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("batch_id", batchId);
        command.Parameters.AddWithValue("correlation_id", operationId.ToString("D"));
        command.Parameters.AddWithValue("actor_id", actorUserId);
        return ((Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty), true);
    }

    private static async Task CompleteKittingWorkItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update work_items
            set status = 'Completed', completed_at_utc = coalesce(completed_at_utc, now())
            where idempotency_key = @idempotency_key
              and status in ('Requested', 'InProgress');
            """;
        command.Parameters.AddWithValue("idempotency_key", $"materials:kitting:{projectId}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CreateBatchReferenceNotificationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid operationId,
        int panelCount,
        Guid? eventId,
        Guid manufacturingAssigneeId,
        CancellationToken cancellationToken)
    {
        var recipientIds = new HashSet<Guid> { manufacturingAssigneeId };
        await using (var recipientCommand = connection.CreateCommand())
        {
            recipientCommand.Transaction = transaction;
            recipientCommand.CommandText = """
                select assigned_user_id
                from project_assignees
                where project_id = @project_id
                  and responsibility_type in (
                      'ProductionPlanningPrimary', 'ProductionPlanningSecondary',
                      'ManufacturingPrimary', 'ManufacturingSecondary', 'Manufacturing'
                  )
                  and assigned_user_id is not null;
                """;
            recipientCommand.Parameters.AddWithValue("project_id", projectId);
            await using var reader = await recipientCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                recipientIds.Add(reader.GetGuid(0));
            }
        }

        Guid notificationId;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into notifications (
                    project_id, notification_type, severity, title, message, link_url,
                    generated_by_event_id, idempotency_key
                )
                values (
                    @project_id, 'Reference', 'Info', '패널 키팅이 완료되었습니다.',
                    @message, @link_url, @event_id, @idempotency_key
                )
                on conflict (idempotency_key) do update set title = excluded.title
                returning id;
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("message", $"이번 작업에서 패널 {panelCount}건의 키팅이 완료되어 제조 업무가 생성되었습니다.");
            command.Parameters.AddWithValue("link_url", $"/materials/kitting?project={projectId}");
            command.Parameters.Add("event_id", NpgsqlDbType.Uuid).Value = eventId ?? (object)DBNull.Value;
            command.Parameters.AddWithValue("idempotency_key", $"kitting:operation:{operationId}:reference");
            notificationId = (Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty);
        }

        foreach (var recipientId in recipientIds)
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

    private NpgsqlDataSource CreateDataSource()
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("QMS database connection string is not configured.");
        }
        return NpgsqlDataSource.Create(connectionString);
    }

    private static void AddNullableText(NpgsqlCommand command, string name, string? value)
        => command.Parameters.Add(name, NpgsqlDbType.Text).Value = value ?? (object)DBNull.Value;

    private sealed class ProjectBuilder(
        Guid projectId,
        string projectCode,
        string projectTitle,
        int activeItemCount,
        int completedItemCount)
    {
        public bool Ready => activeItemCount > 0 && activeItemCount == completedItemCount;
        public List<PanelKittingPanelResponse> Panels { get; } = [];

        public PanelKittingProjectResponse ToResponse()
        {
            var completedPanels = Panels.Count(panel => panel.KittingCompleted);
            return new PanelKittingProjectResponse(
                projectId,
                projectCode,
                projectTitle,
                activeItemCount,
                completedItemCount,
                Ready,
                Panels.Count - completedPanels,
                completedPanels,
                Panels);
        }
    }

    private sealed record ProjectSnapshot(Guid ProjectId, string Status);
    private sealed record BatchSnapshot(
        Guid ProjectId,
        Guid RequestedByUserId,
        string PanelSetFingerprint,
        int CompletedPanelCount,
        int GeneratedWorkItemCount,
        bool ProjectKittingCompleted);
    private sealed record PanelSnapshot(
        Guid PanelId,
        string DisplayCode,
        string? PanelName,
        bool PanelInfoCompleted,
        bool KittingCompleted);
    private sealed record ReadinessSnapshot(int ActiveItemCount, int CompletedItemCount)
    {
        public bool Ready => ActiveItemCount > 0 && ActiveItemCount == CompletedItemCount;
    }
    private sealed record PanelCounts(int ActivePanelCount, int CompletedPanelCount);
    private readonly record struct AssigneeSnapshot(Guid UserId, string? RoleCode);
}
