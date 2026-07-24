using System.Security.Cryptography;
using System.Text;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Projects;
using Emi.Qms.Api.Workflow;
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
              {(projectId is null ? "and project.status = 'Active'" : string.Empty)}
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
                reader.GetBoolean(8) && completedAt is null));
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
            var panelCounts = await ReadPanelCountsAsync(connection, transaction, request.ProjectId, cancellationToken);
            var projectKittingCompleted = panelCounts.ActivePanelCount > 0
                && panelCounts.CompletedPanelCount + panels.Count >= panelCounts.ActivePanelCount;
            const int generatedWorkItemCount = 0;
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
            }

            var stageEventId = await WorkflowStore.EnsureEffectiveKittingStageCompletedAsync(
                connection,
                transaction,
                request.ProjectId,
                "PanelKittingBatch",
                batchId,
                request.OperationId,
                actorUserId,
                cancellationToken);

            await CreateBatchReferenceNotificationAsync(
                connection,
                transaction,
                request.ProjectId,
                request.OperationId,
                panels.Count,
                stageEventId,
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
        CancellationToken cancellationToken)
    {
        var recipientIds = new HashSet<Guid>();
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
                    @project_id, 'Reference', 'Info', '패널 키팅 완료 상태가 공유되었습니다.',
                    @message, @link_url, @event_id, @idempotency_key
                )
                on conflict (idempotency_key) do update set title = excluded.title
                returning id;
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("message", $"패널 {panelCount}건의 키팅 완료 상태를 공유했습니다. 제조 투입 여부와 별개인 자재 준비 정보입니다.");
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
}
