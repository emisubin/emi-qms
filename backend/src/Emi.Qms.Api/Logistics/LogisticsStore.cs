using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Emi.Qms.Api.Notifications;
using Emi.Qms.Api.Projects;
using Emi.Qms.Api.Workflow;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Logistics;

public sealed class LogisticsStore(DatabaseConnectionStringProvider connectionStringProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaxPhotoBytes = 5 * 1024 * 1024;
    private const int MaxDocumentBytes = 10 * 1024 * 1024;

    public static async Task CancelPanelDraftsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid panelId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update logistics_batch_panels membership
            set active = false
            from logistics_batches batch
            where membership.batch_id = batch.id
              and batch.status = 'Draft'
              and membership.panel_id = @panel_id
              and membership.active;
            update logistics_batch_units membership
            set active = false
            from logistics_batches batch
            where membership.batch_id = batch.id
              and batch.status = 'Draft'
              and not exists (
                  select 1
                  from logistics_batch_panels selected
                  where selected.batch_id = batch.id
                    and selected.packing_unit_id = membership.packing_unit_id
                    and selected.active
              );
            update logistics_batches batch
            set status = 'Cancelled', version = version + 1,
                cancelled_by_user_id = @actor_id, cancelled_at_utc = now()
            where batch.status = 'Draft'
              and not exists (select 1 from logistics_batch_panels membership where membership.batch_id = batch.id and membership.active);
            update logistics_packing_unit_panels membership
            set active = false
            from logistics_packing_units unit
            where membership.packing_unit_id = unit.id
              and membership.panel_id = @panel_id and membership.active and unit.status = 'Draft';
            update logistics_packing_units unit
            set status = 'Cancelled', version = version + 1,
                cancelled_by_user_id = @actor_id, cancelled_at_utc = now()
            where unit.status = 'Draft'
              and not exists (select 1 from logistics_packing_unit_panels membership where membership.packing_unit_id = unit.id and membership.active);
            update work_items set status = 'Cancelled', cancelled_at_utc = coalesce(cancelled_at_utc, now())
            where target_type = 'Panel' and target_id = @panel_id
              and workflow_stage_code in ('PackingCompleted', 'DepartureProcessed', 'DeliveryCompleted')
              and status in ('Requested', 'InProgress');
            """;
        command.Parameters.AddWithValue("panel_id", panelId);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task CancelProjectDraftsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update logistics_batch_panels membership set active = false
            from logistics_batches batch
            where membership.batch_id = batch.id and batch.project_id = @project_id and batch.status = 'Draft';
            update logistics_batch_units membership set active = false
            from logistics_batches batch
            where membership.batch_id = batch.id and batch.project_id = @project_id and batch.status = 'Draft';
            update logistics_packing_unit_panels membership set active = false
            from logistics_packing_units unit
            where membership.packing_unit_id = unit.id and unit.project_id = @project_id and unit.status = 'Draft';
            update logistics_batches set status = 'Cancelled', version = version + 1,
                cancelled_by_user_id = @actor_id, cancelled_at_utc = now()
            where project_id = @project_id and status = 'Draft';
            update logistics_packing_units set status = 'Cancelled', version = version + 1,
                cancelled_by_user_id = @actor_id, cancelled_at_utc = now()
            where project_id = @project_id and status = 'Draft';
            update work_items set status = 'Cancelled', cancelled_at_utc = coalesce(cancelled_at_utc, now())
            where project_id = @project_id and target_type = 'Panel'
              and workflow_stage_code in ('PackingCompleted', 'DepartureProcessed', 'DeliveryCompleted')
              and status in ('Requested', 'InProgress');
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<LogisticsQueueResponse> ListAsync(
        string? stage,
        Guid? projectId,
        ProjectAccessScope scope,
        Guid? actorUserId,
        bool canShip,
        CancellationToken cancellationToken)
    {
        var normalized = LogisticsStages.Normalize(stage) ?? LogisticsStages.Packing;
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand(QueueSql(normalized, projectId is not null));
        AddScope(command, scope);
        command.Parameters.AddWithValue("actor_id", actorUserId ?? Guid.Empty);
        command.Parameters.AddWithValue("can_ship", canShip);
        if (projectId is not null) command.Parameters.AddWithValue("project_id", projectId.Value);

        var projects = new Dictionary<Guid, QueueProjectBuilder>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var currentProjectId = reader.GetGuid(0);
            if (!projects.TryGetValue(currentProjectId, out var builder))
            {
                builder = new QueueProjectBuilder(currentProjectId, reader.GetString(1), reader.GetString(2));
                projects[currentProjectId] = builder;
            }
            builder.Items.Add(new LogisticsQueueItem(
                reader.GetGuid(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7),
                reader.GetFieldValue<Guid[]>(8), reader.GetFieldValue<string[]>(9), reader.GetInt32(10), reader.GetString(11),
                reader.GetBoolean(12), reader.GetBoolean(13)));
        }

        var responseProjects = projects.Values.Select(value => value.Build()).ToList();
        var items = responseProjects.SelectMany(value => value.Items).ToList();
        var drafts = await ReadDraftSummariesAsync(
            dataSource, normalized, projectId, scope, actorUserId, canShip, cancellationToken);
        return new LogisticsQueueResponse(
            normalized, items.Count, items.Count(value => value.HasOpenPending), responseProjects, drafts);
    }

    private static async Task<IReadOnlyList<LogisticsDraftSummary>> ReadDraftSummariesAsync(
        NpgsqlDataSource dataSource,
        string stage,
        Guid? projectId,
        ProjectAccessScope scope,
        Guid? actorUserId,
        bool canShip,
        CancellationToken cancellationToken)
    {
        var projectFilter = projectId is null ? string.Empty : "and project.id = @project_id";
        var sql = stage == LogisticsStages.Packing
            ? $"""
                select unit.id, unit.project_id, project.project_code, project.project_title,
                       'packing', 'PU-' || lpad(unit.unit_number::text, 3, '0'),
                       unit.version,
                       (select count(*)::int from logistics_evidence evidence where evidence.packing_unit_id=unit.id),
                       unit.created_at_utc
                from logistics_packing_units unit
                join projects project on project.id=unit.project_id
                  and project.deleted_at_utc is null and project.status <> 'Cancelled'
                where unit.status='Draft'
                  and (@has_read_all or project.project_key=any(@project_keys))
                  and @can_ship
                  and (
                    unit.created_by_user_id=@actor_id
                    or exists(
                      select 1 from project_assignees assignee
                      where assignee.project_id=project.id
                        and assignee.assigned_user_id=@actor_id
                        and assignee.responsibility_type in ('LogisticsPrimary','LogisticsSecondary')
                    )
                  )
                  {projectFilter}
                order by unit.created_at_utc desc, unit.id desc;
                """
            : $"""
                select batch.id, batch.project_id, project.project_code, project.project_title,
                       @stage,
                       case batch.stage_code when 'DepartureProcessed' then 'DP-' else 'DL-' end
                         || lpad(batch.batch_number::text, 3, '0'),
                       batch.version,
                       (select count(*)::int from logistics_evidence evidence where evidence.batch_id=batch.id),
                       batch.created_at_utc
                from logistics_batches batch
                join projects project on project.id=batch.project_id
                  and project.deleted_at_utc is null and project.status <> 'Cancelled'
                where batch.status='Draft' and batch.stage_code=@work_stage
                  and (@has_read_all or project.project_key=any(@project_keys))
                  and @can_ship
                  and (
                    batch.created_by_user_id=@actor_id
                    or exists(
                      select 1 from project_assignees assignee
                      where assignee.project_id=project.id
                        and assignee.assigned_user_id=@actor_id
                        and assignee.responsibility_type in ('LogisticsPrimary','LogisticsSecondary')
                    )
                  )
                  {projectFilter}
                order by batch.created_at_utc desc, batch.id desc;
                """;
        await using var command = dataSource.CreateCommand(sql);
        AddScope(command, scope);
        command.Parameters.AddWithValue("actor_id", actorUserId ?? Guid.Empty);
        command.Parameters.AddWithValue("can_ship", canShip);
        if (projectId is not null) command.Parameters.AddWithValue("project_id", projectId.Value);
        if (stage != LogisticsStages.Packing)
        {
            command.Parameters.AddWithValue("stage", stage);
            command.Parameters.AddWithValue("work_stage", LogisticsStages.WorkStage(stage));
        }

        var result = new List<LogisticsDraftSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new LogisticsDraftSummary(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.GetInt32(6), reader.GetInt32(7),
                reader.GetFieldValue<DateTimeOffset>(8)));
        }
        return result;
    }

    public async Task<LogisticsMutationResult<LogisticsProjectHistoryResponse>> GetProjectHistoryAsync(
        Guid projectId,
        ProjectAccessScope scope,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using (var access = connection.CreateCommand())
        {
            access.CommandText = """
                select exists(
                    select 1 from projects
                    where id=@project_id and deleted_at_utc is null
                      and (@has_read_all or project_key=any(@project_keys))
                );
                """;
            access.Parameters.AddWithValue("project_id", projectId);
            AddScope(access, scope);
            if (await access.ExecuteScalarAsync(cancellationToken) is not true)
                return LogisticsMutationResult<LogisticsProjectHistoryResponse>.NotFound();
        }

        var owners = new List<LogisticsHistoryOwner>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select unit.id, 'packing', 'PU-' || lpad(unit.unit_number::text, 3, '0'),
                       unit.status, unit.version, unit.note, unit.specification, unit.weight_text, null::date,
                       created.display_name, unit.created_at_utc,
                       finalized.display_name, unit.finalized_at_utc,
                       cancelled.display_name, unit.cancelled_at_utc
                from logistics_packing_units unit
                join qms_users created on created.id=unit.created_by_user_id
                left join qms_users finalized on finalized.id=unit.finalized_by_user_id
                left join qms_users cancelled on cancelled.id=unit.cancelled_by_user_id
                where unit.project_id=@project_id
                union all
                select batch.id,
                       case batch.stage_code when 'DepartureProcessed' then 'departure' else 'delivery' end,
                       case batch.stage_code when 'DepartureProcessed' then 'DP-' else 'DL-' end || lpad(batch.batch_number::text, 3, '0'),
                       batch.status, batch.version, null::text, null::text, null::text, batch.departure_date,
                       created.display_name, batch.created_at_utc,
                       finalized.display_name, batch.finalized_at_utc,
                       cancelled.display_name, batch.cancelled_at_utc
                from logistics_batches batch
                join qms_users created on created.id=batch.created_by_user_id
                left join qms_users finalized on finalized.id=batch.finalized_by_user_id
                left join qms_users cancelled on cancelled.id=batch.cancelled_by_user_id
                where batch.project_id=@project_id
                order by 2, 3;
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                owners.Add(new LogisticsHistoryOwner(
                    reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : DateOnly.FromDateTime(reader.GetDateTime(8)),
                    reader.GetString(9), reader.GetFieldValue<DateTimeOffset>(10), reader.IsDBNull(11) ? null : reader.GetString(11),
                    reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12), reader.IsDBNull(13) ? null : reader.GetString(13),
                    reader.IsDBNull(14) ? null : reader.GetFieldValue<DateTimeOffset>(14)));
            }
        }

        var items = new List<LogisticsProjectHistoryItem>();
        foreach (var owner in owners)
        {
            var panelCodes = new List<string>();
            var unitCodes = new List<string>();
            await using (var membership = connection.CreateCommand())
            {
                membership.CommandText = owner.Stage == LogisticsStages.Packing
                    ? """
                        select panel.display_code, null::text
                        from logistics_packing_unit_panels member
                        join panel_placeholders panel on panel.id=member.panel_id
                        where member.packing_unit_id=@id order by panel.sequence_number;
                        """
                    : """
                        select panel.display_code, 'PU-' || lpad(unit.unit_number::text, 3, '0')
                        from logistics_batch_panels member
                        join logistics_packing_units unit on unit.id=member.packing_unit_id
                        join panel_placeholders panel on panel.id=member.panel_id
                        where member.batch_id=@id order by unit.unit_number,panel.sequence_number;
                        """;
                membership.Parameters.AddWithValue("id", owner.TargetId);
                await using var reader = await membership.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var panelCode = reader.GetString(0);
                    if (!panelCodes.Contains(panelCode)) panelCodes.Add(panelCode);
                    if (!reader.IsDBNull(1))
                    {
                        var unitCode = reader.GetString(1);
                        if (!unitCodes.Contains(unitCode)) unitCodes.Add(unitCode);
                    }
                }
            }

            var evidence = new List<LogisticsEvidenceResponse>();
            await using (var evidenceCommand = connection.CreateCommand())
            {
                evidenceCommand.CommandText = owner.Stage == LogisticsStages.Packing
                    ? """
                        select id,owner_type,display_name,normalized_mime,byte_size,alt_text,created_at_utc
                        from logistics_evidence where packing_unit_id=@id order by created_at_utc,id;
                        """
                    : """
                        select id,owner_type,display_name,normalized_mime,byte_size,alt_text,created_at_utc
                        from logistics_evidence where batch_id=@id order by created_at_utc,id;
                        """;
                evidenceCommand.Parameters.AddWithValue("id", owner.TargetId);
                await using var reader = await evidenceCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    evidence.Add(new LogisticsEvidenceResponse(
                        reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4),
                        reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6)));
                }
            }

            items.Add(new LogisticsProjectHistoryItem(
                owner.TargetId, owner.Stage, owner.DisplayCode, owner.Status, owner.Version,
                owner.Note, owner.Specification, owner.WeightText, owner.DepartureDate,
                panelCodes, unitCodes, evidence, owner.CreatedByName, owner.CreatedAtUtc,
                owner.FinalizedByName, owner.FinalizedAtUtc, owner.CancelledByName, owner.CancelledAtUtc));
        }

        return LogisticsMutationResult<LogisticsProjectHistoryResponse>.Success(new(projectId, items));
    }

    public async Task<LogisticsMutationResult<LogisticsDraftResponse>> GetDraftAsync(
        Guid targetId,
        string stage,
        ProjectAccessScope scope,
        CancellationToken cancellationToken)
    {
        var normalized = LogisticsStages.Normalize(stage);
        if (normalized is null) return LogisticsMutationResult<LogisticsDraftResponse>.NotFound();

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var ownerCommand = connection.CreateCommand();
        ownerCommand.CommandText = normalized == LogisticsStages.Packing
            ? """
                select unit.project_id, 'PU-' || lpad(unit.unit_number::text, 3, '0'), unit.status, unit.version, null::date
                from logistics_packing_units unit
                join projects project on project.id=unit.project_id and project.deleted_at_utc is null
                where unit.id=@id and (@has_read_all or project.project_key=any(@project_keys));
                """
            : """
                select batch.project_id,
                       case batch.stage_code when 'DepartureProcessed' then 'DP-' else 'DL-' end || lpad(batch.batch_number::text, 3, '0'),
                       batch.status, batch.version, batch.departure_date
                from logistics_batches batch
                join projects project on project.id=batch.project_id and project.deleted_at_utc is null
                where batch.id=@id and batch.stage_code=@stage
                  and (@has_read_all or project.project_key=any(@project_keys));
                """;
        ownerCommand.Parameters.AddWithValue("id", targetId);
        if (normalized != LogisticsStages.Packing) ownerCommand.Parameters.AddWithValue("stage", LogisticsStages.WorkStage(normalized));
        AddScope(ownerCommand, scope);

        Guid projectId;
        string displayCode;
        string status;
        int version;
        DateOnly? departureDate;
        await using (var reader = await ownerCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) return LogisticsMutationResult<LogisticsDraftResponse>.NotFound();
            projectId = reader.GetGuid(0);
            displayCode = reader.GetString(1);
            status = reader.GetString(2);
            version = reader.GetInt32(3);
            departureDate = reader.IsDBNull(4) ? null : DateOnly.FromDateTime(reader.GetDateTime(4));
        }

        var panelIds = new List<Guid>();
        var unitIds = new List<Guid>();
        await using (var membershipCommand = connection.CreateCommand())
        {
            membershipCommand.CommandText = normalized == LogisticsStages.Packing
                ? "select panel_id from logistics_packing_unit_panels where packing_unit_id=@id and active order by panel_id"
                : """
                    select distinct membership.packing_unit_id, membership.panel_id
                    from logistics_batch_panels membership
                    where membership.batch_id=@id and membership.active
                    order by membership.packing_unit_id, membership.panel_id;
                    """;
            membershipCommand.Parameters.AddWithValue("id", targetId);
            await using var reader = await membershipCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (normalized == LogisticsStages.Packing)
                {
                    panelIds.Add(reader.GetGuid(0));
                }
                else
                {
                    var unitId = reader.GetGuid(0);
                    if (!unitIds.Contains(unitId)) unitIds.Add(unitId);
                    var panelId = reader.GetGuid(1);
                    if (!panelIds.Contains(panelId)) panelIds.Add(panelId);
                }
            }
        }

        var evidence = new List<LogisticsEvidenceResponse>();
        await using (var evidenceCommand = connection.CreateCommand())
        {
            evidenceCommand.CommandText = normalized == LogisticsStages.Packing
                ? """
                    select id,owner_type,display_name,normalized_mime,byte_size,alt_text,created_at_utc
                    from logistics_evidence where packing_unit_id=@id order by created_at_utc,id;
                    """
                : """
                    select id,owner_type,display_name,normalized_mime,byte_size,alt_text,created_at_utc
                    from logistics_evidence where batch_id=@id order by created_at_utc,id;
                    """;
            evidenceCommand.Parameters.AddWithValue("id", targetId);
            await using var reader = await evidenceCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                evidence.Add(new LogisticsEvidenceResponse(
                    reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6)));
            }
        }

        return LogisticsMutationResult<LogisticsDraftResponse>.Success(new LogisticsDraftResponse(
            targetId, projectId, normalized, displayCode, status, version, departureDate, panelIds, unitIds, evidence));
    }

    public async Task<LogisticsMutationResult<LogisticsMutationResponse>> CreatePackingUnitAsync(
        CreatePackingUnitRequest request,
        Guid actorId,
        ProjectAccessScope scope,
        CancellationToken cancellationToken)
    {
        var panelIds = NormalizeIds(request.PanelIds);
        if (panelIds.Count == 0) return LogisticsMutationResult<LogisticsMutationResponse>.Validation("panelIds", "포장할 패널을 한 개 이상 선택해 주세요.");
        if (panelIds.Count > 100) return LogisticsMutationResult<LogisticsMutationResponse>.Validation("panelIds", "한 번에 최대 100개 패널을 선택할 수 있습니다.");
        if (!ValidOptional(request.Note, 500) || !ValidOptional(request.Specification, 120) || !ValidOptional(request.WeightText, 80))
            return LogisticsMutationResult<LogisticsMutationResponse>.Validation("details", "비고·규격·중량 입력 길이를 확인해 주세요.");

        var fingerprint = Fingerprint("create-packing", request.ProjectId, panelIds, request.Note, request.Specification, request.WeightText);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            if (!await LockProjectInScopeAsync(connection, transaction, request.ProjectId, scope, cancellationToken)) return await RollbackNotFound(transaction, cancellationToken);
            var replay = await ReadReplayAsync(connection, transaction, request.OperationId, fingerprint, cancellationToken);
            if (replay.Result is not null) { await transaction.CommitAsync(cancellationToken); return replay.Result; }
            if (!await ValidatePanelsAsync(connection, transaction, request.ProjectId, panelIds, "PackingCompleted", cancellationToken))
                return await RollbackConflict(transaction, "선택한 패널은 같은 프로젝트의 포장 대기 상태여야 합니다.", cancellationToken);
            if (!await ActorCanMutateAsync(connection, transaction, request.ProjectId, panelIds, "PackingCompleted", actorId, cancellationToken))
                return await RollbackForbidden(transaction, cancellationToken);
            if (await HasOpenPendingAsync(connection, transaction, request.ProjectId, panelIds, cancellationToken))
                return await RollbackConflict(transaction, "열린 Pending을 먼저 처리한 뒤 포장을 진행해 주세요.", cancellationToken);

            var targetId = Guid.NewGuid();
            int unitNumber;
            await using (var number = connection.CreateCommand())
            {
                number.Transaction = transaction;
                number.CommandText = "select coalesce(max(unit_number), 0) + 1 from logistics_packing_units where project_id = @project_id;";
                number.Parameters.AddWithValue("project_id", request.ProjectId);
                unitNumber = Convert.ToInt32(await number.ExecuteScalarAsync(cancellationToken));
            }
            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = """
                    insert into logistics_packing_units (
                        id, project_id, unit_number, note, specification, weight_text, created_by_user_id
                    ) values (@id, @project_id, @number, @note, @specification, @weight, @actor_id);
                    """;
                insert.Parameters.AddWithValue("id", targetId);
                insert.Parameters.AddWithValue("project_id", request.ProjectId);
                insert.Parameters.AddWithValue("number", unitNumber);
                AddNullableText(insert, "note", NormalizeOptional(request.Note));
                AddNullableText(insert, "specification", NormalizeOptional(request.Specification));
                AddNullableText(insert, "weight", NormalizeOptional(request.WeightText));
                insert.Parameters.AddWithValue("actor_id", actorId);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            await InsertPackingMembershipsAsync(connection, transaction, targetId, panelIds, actorId, cancellationToken);
            var response = new LogisticsMutationResponse(request.OperationId, request.ProjectId, targetId, LogisticsStages.Packing, "Draft", 1, "evidence", false);
            await InsertReceiptAsync(connection, transaction, request.OperationId, "CreatePackingUnit", request.ProjectId, targetId, null, actorId, fingerprint, response, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return LogisticsMutationResult<LogisticsMutationResponse>.Success(response);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await RollbackQuietly(transaction, cancellationToken);
            return LogisticsMutationResult<LogisticsMutationResponse>.Conflict("다른 요청이 먼저 물류 대상을 구성했습니다. 새로고침 후 다시 시도해 주세요.");
        }
        catch { await RollbackQuietly(transaction, cancellationToken); throw; }
    }

    public async Task<LogisticsMutationResult<LogisticsMutationResponse>> ReplacePackingPanelsAsync(
        Guid unitId, ReplacePackingPanelsRequest request, Guid actorId, ProjectAccessScope scope, CancellationToken cancellationToken)
    {
        var panelIds = NormalizeIds(request.PanelIds);
        if (request.ExpectedVersion is null) return LogisticsMutationResult<LogisticsMutationResponse>.Validation("expectedVersion", "현재 버전을 입력해 주세요.");
        var fingerprint = Fingerprint("replace-packing-panels", unitId, request.ExpectedVersion, panelIds);
        return await MutateDraftMembershipAsync(unitId, LogisticsStages.Packing, request.OperationId, request.ExpectedVersion.Value,
            panelIds, null, fingerprint, actorId, scope, cancellationToken);
    }

    public async Task<LogisticsMutationResult<LogisticsMutationResponse>> CreateBatchAsync(
        string stage, CreateLogisticsBatchRequest request, Guid actorId, ProjectAccessScope scope, CancellationToken cancellationToken)
    {
        var normalized = LogisticsStages.Normalize(stage);
        if (normalized is not (LogisticsStages.Departure or LogisticsStages.Delivery))
            return LogisticsMutationResult<LogisticsMutationResponse>.Validation("stage", "출발 또는 납품 단계를 선택해 주세요.");
        var unitIds = NormalizeIds(request.UnitIds);
        var requestedPanelIds = NormalizeIds(request.PanelIds);
        if (unitIds.Count == 0 && requestedPanelIds.Count == 0)
            return LogisticsMutationResult<LogisticsMutationResponse>.Validation("panelIds", "처리할 패널을 한 개 이상 선택해 주세요.");
        var fingerprint = Fingerprint("create-batch", normalized, request.ProjectId, unitIds, requestedPanelIds, request.DepartureDate);

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            if (!await LockProjectInScopeAsync(connection, transaction, request.ProjectId, scope, cancellationToken)) return await RollbackNotFound(transaction, cancellationToken);
            var replay = await ReadReplayAsync(connection, transaction, request.OperationId, fingerprint, cancellationToken);
            if (replay.Result is not null) { await transaction.CommitAsync(cancellationToken); return replay.Result; }
            var selection = await ReadBatchSelectionAsync(
                connection, transaction, request.ProjectId, unitIds, requestedPanelIds, normalized, null, cancellationToken);
            if (selection is null)
                return await RollbackConflict(transaction, "같은 프로젝트에서 현재 단계에 처리 가능한 패널만 선택해 주세요.", cancellationToken);
            var workStage = LogisticsStages.WorkStage(normalized);
            if (!await ActorCanMutateAsync(connection, transaction, request.ProjectId, selection.PanelIds, workStage, actorId, cancellationToken))
                return await RollbackForbidden(transaction, cancellationToken);
            if (await HasOpenPendingAsync(connection, transaction, request.ProjectId, selection.PanelIds, cancellationToken))
                return await RollbackConflict(transaction, "열린 Pending을 먼저 처리한 뒤 다음 단계를 진행해 주세요.", cancellationToken);

            var targetId = Guid.NewGuid();
            var workStageCode = LogisticsStages.WorkStage(normalized);
            int batchNumber;
            await using (var number = connection.CreateCommand())
            {
                number.Transaction = transaction;
                number.CommandText = "select coalesce(max(batch_number), 0) + 1 from logistics_batches where project_id = @project_id and stage_code = @stage_code;";
                number.Parameters.AddWithValue("project_id", request.ProjectId);
                number.Parameters.AddWithValue("stage_code", workStageCode);
                batchNumber = Convert.ToInt32(await number.ExecuteScalarAsync(cancellationToken));
            }
            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = """
                    insert into logistics_batches (id, project_id, stage_code, batch_number, departure_date, created_by_user_id)
                    values (@id, @project_id, @stage_code, @number, @departure_date, @actor_id);
                    """;
                insert.Parameters.AddWithValue("id", targetId);
                insert.Parameters.AddWithValue("project_id", request.ProjectId);
                insert.Parameters.AddWithValue("stage_code", workStageCode);
                insert.Parameters.AddWithValue("number", batchNumber);
                AddNullableDate(insert, "departure_date", normalized == LogisticsStages.Departure ? request.DepartureDate : null);
                insert.Parameters.AddWithValue("actor_id", actorId);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            await InsertBatchMembershipsAsync(
                connection, transaction, targetId, selection.UnitIds, selection.PanelIds, workStageCode, actorId, cancellationToken);
            var response = new LogisticsMutationResponse(request.OperationId, request.ProjectId, targetId, normalized, "Draft", 1, "evidence", false);
            await InsertReceiptAsync(connection, transaction, request.OperationId, "CreateLogisticsBatch", request.ProjectId, null, targetId, actorId, fingerprint, response, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return LogisticsMutationResult<LogisticsMutationResponse>.Success(response);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await RollbackQuietly(transaction, cancellationToken);
            return LogisticsMutationResult<LogisticsMutationResponse>.Conflict("다른 요청이 먼저 물류 대상을 구성했습니다. 새로고침 후 다시 시도해 주세요.");
        }
        catch { await RollbackQuietly(transaction, cancellationToken); throw; }
    }

    public async Task<LogisticsMutationResult<LogisticsMutationResponse>> ReplaceBatchUnitsAsync(
        Guid batchId, string stage, ReplaceLogisticsBatchUnitsRequest request, Guid actorId, ProjectAccessScope scope, CancellationToken cancellationToken)
    {
        var normalized = LogisticsStages.Normalize(stage);
        if (normalized is not (LogisticsStages.Departure or LogisticsStages.Delivery))
            return LogisticsMutationResult<LogisticsMutationResponse>.Validation("stage", "출발 또는 납품 단계를 선택해 주세요.");
        if (request.ExpectedVersion is null) return LogisticsMutationResult<LogisticsMutationResponse>.Validation("expectedVersion", "현재 버전을 입력해 주세요.");
        var unitIds = NormalizeIds(request.UnitIds);
        var panelIds = NormalizeIds(request.PanelIds);
        var fingerprint = Fingerprint(
            "replace-batch-panels", batchId, normalized, request.ExpectedVersion, unitIds, panelIds, request.DepartureDate);
        return await MutateDraftBatchMembershipAsync(
            batchId, normalized, request.OperationId, request.ExpectedVersion.Value, unitIds, panelIds,
            request.DepartureDate, fingerprint, actorId, scope, cancellationToken);
    }

    public async Task<LogisticsMutationResult<LogisticsMutationResponse>> AddEvidenceAsync(
        Guid targetId, string stage, Guid operationId, int? expectedVersion, string? altText,
        byte[] content, Guid actorId, ProjectAccessScope scope, CancellationToken cancellationToken)
    {
        var normalized = LogisticsStages.Normalize(stage);
        if (normalized is null) return LogisticsMutationResult<LogisticsMutationResponse>.Validation("stage", "물류 단계를 확인해 주세요.");
        if (expectedVersion is null) return LogisticsMutationResult<LogisticsMutationResponse>.Validation("expectedVersion", "현재 버전을 입력해 주세요.");
        var sniff = Sniff(normalized, content);
        if (sniff is null) return LogisticsMutationResult<LogisticsMutationResponse>.Validation("file", normalized == LogisticsStages.Delivery
            ? "서명본은 10MB 이하 JPEG, PNG 또는 PDF 파일이어야 합니다."
            : "사진은 5MB 이하 JPEG 또는 PNG 파일이어야 합니다.");
        if (normalized != LogisticsStages.Delivery && !ValidAlt(altText))
            return LogisticsMutationResult<LogisticsMutationResponse>.Validation("altText", "사진 설명을 2~160자로 입력해 주세요.");
        var contentHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var fingerprint = Fingerprint("add-evidence", targetId, normalized, expectedVersion, NormalizeOptional(altText), contentHash);

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var owner = await LockOwnerAsync(connection, transaction, targetId, normalized, scope, cancellationToken);
            if (owner is null) return await RollbackNotFound(transaction, cancellationToken);
            var replay = await ReadReplayAsync(connection, transaction, operationId, fingerprint, cancellationToken);
            if (replay.Result is not null) { await transaction.CommitAsync(cancellationToken); return replay.Result; }
            if (owner.Status != "Draft") return await RollbackConflict(transaction, "확정되거나 취소된 작업의 증빙은 변경할 수 없습니다.", cancellationToken);
            if (owner.Version != expectedVersion.Value) return await RollbackConflict(transaction, "다른 사용자가 먼저 수정했습니다. 새로고침 후 다시 시도해 주세요.", cancellationToken);
            var panelIds = await ReadOwnerPanelsAsync(connection, transaction, owner, cancellationToken);
            if (!await ActorCanMutateAsync(connection, transaction, owner.ProjectId, panelIds, LogisticsStages.WorkStage(normalized), actorId, cancellationToken))
                return await RollbackForbidden(transaction, cancellationToken);
            var ownerType = normalized switch { LogisticsStages.Packing => "PackingPhoto", LogisticsStages.Departure => "DeparturePhoto", _ => "DeliveryDocument" };
            int count;
            await using (var countCommand = connection.CreateCommand())
            {
                countCommand.Transaction = transaction;
                countCommand.CommandText = normalized == LogisticsStages.Packing
                    ? "select count(*) from logistics_evidence where packing_unit_id = @id"
                    : "select count(*) from logistics_evidence where batch_id = @id";
                countCommand.Parameters.AddWithValue("id", targetId);
                count = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
            }
            var maxCount = normalized == LogisticsStages.Delivery ? 3 : 5;
            if (count >= maxCount) return await RollbackConflict(transaction, $"증빙은 최대 {maxCount}개까지 등록할 수 있습니다.", cancellationToken);
            var evidenceId = Guid.NewGuid();
            var suffix = sniff.Value.Mime switch { "image/jpeg" => "jpg", "image/png" => "png", _ => "pdf" };
            var prefix = normalized switch { LogisticsStages.Packing => "packing-photo", LogisticsStages.Departure => "departure-photo", _ => "delivery-document" };
            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = """
                    insert into logistics_evidence (
                        id, owner_type, packing_unit_id, batch_id, display_name, normalized_mime,
                        byte_size, sha256, alt_text, content, created_by_user_id
                    ) values (@id, @owner_type, @unit_id, @batch_id, @name, @mime, @size, @sha, @alt, @content, @actor_id);
                    """;
                insert.Parameters.AddWithValue("id", evidenceId);
                insert.Parameters.AddWithValue("owner_type", ownerType);
                AddNullableUuid(insert, "unit_id", normalized == LogisticsStages.Packing ? targetId : null);
                AddNullableUuid(insert, "batch_id", normalized == LogisticsStages.Packing ? null : targetId);
                insert.Parameters.AddWithValue("name", $"{prefix}-{count + 1}.{suffix}");
                insert.Parameters.AddWithValue("mime", sniff.Value.Mime);
                insert.Parameters.AddWithValue("size", content.Length);
                insert.Parameters.AddWithValue("sha", contentHash);
                AddNullableText(insert, "alt", normalized == LogisticsStages.Delivery ? NormalizeOptional(altText) : altText!.Trim());
                insert.Parameters.AddWithValue("content", content);
                insert.Parameters.AddWithValue("actor_id", actorId);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            var version = await IncrementOwnerVersionAsync(connection, transaction, owner, expectedVersion.Value, cancellationToken);
            var response = new LogisticsMutationResponse(operationId, owner.ProjectId, targetId, normalized, "Draft", version, "confirm", false);
            await InsertReceiptAsync(connection, transaction, operationId, "AddLogisticsEvidence", owner.ProjectId,
                normalized == LogisticsStages.Packing ? targetId : null, normalized == LogisticsStages.Packing ? null : targetId,
                actorId, fingerprint, response, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return LogisticsMutationResult<LogisticsMutationResponse>.Success(response);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await RollbackQuietly(transaction, cancellationToken);
            return LogisticsMutationResult<LogisticsMutationResponse>.Conflict("다른 요청이 먼저 증빙을 등록했습니다. 새로고침 후 다시 시도해 주세요.");
        }
        catch { await RollbackQuietly(transaction, cancellationToken); throw; }
    }

    public async Task<LogisticsMutationResult<LogisticsMutationResponse>> RemoveEvidenceAsync(
        Guid targetId, Guid evidenceId, string stage, Guid operationId, int? expectedVersion,
        Guid actorId, ProjectAccessScope scope, CancellationToken cancellationToken)
    {
        var normalized = LogisticsStages.Normalize(stage);
        if (normalized is null) return LogisticsMutationResult<LogisticsMutationResponse>.Validation("stage", "물류 단계를 확인해 주세요.");
        if (expectedVersion is null) return LogisticsMutationResult<LogisticsMutationResponse>.Validation("expectedVersion", "현재 버전을 입력해 주세요.");
        var fingerprint = Fingerprint("remove-evidence", targetId, evidenceId, normalized, expectedVersion);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var owner = await LockOwnerAsync(connection, transaction, targetId, normalized, scope, cancellationToken);
            if (owner is null) return await RollbackNotFound(transaction, cancellationToken);
            var replay = await ReadReplayAsync(connection, transaction, operationId, fingerprint, cancellationToken);
            if (replay.Result is not null) { await transaction.CommitAsync(cancellationToken); return replay.Result; }
            if (owner.Status != "Draft" || owner.Version != expectedVersion.Value)
                return await RollbackConflict(transaction, "최신 draft에서만 증빙을 삭제할 수 있습니다.", cancellationToken);
            var panelIds = await ReadOwnerPanelsAsync(connection, transaction, owner, cancellationToken);
            if (!await ActorCanMutateAsync(connection, transaction, owner.ProjectId, panelIds, LogisticsStages.WorkStage(normalized), actorId, cancellationToken))
                return await RollbackForbidden(transaction, cancellationToken);
            await using var delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = normalized == LogisticsStages.Packing
                ? "delete from logistics_evidence where id = @evidence_id and packing_unit_id = @target_id"
                : "delete from logistics_evidence where id = @evidence_id and batch_id = @target_id";
            delete.Parameters.AddWithValue("evidence_id", evidenceId);
            delete.Parameters.AddWithValue("target_id", targetId);
            if (await delete.ExecuteNonQueryAsync(cancellationToken) == 0) return await RollbackNotFound(transaction, cancellationToken);
            var version = await IncrementOwnerVersionAsync(connection, transaction, owner, expectedVersion.Value, cancellationToken);
            var response = new LogisticsMutationResponse(operationId, owner.ProjectId, targetId, normalized, "Draft", version, "evidence", false);
            await InsertReceiptAsync(connection, transaction, operationId, "RemoveLogisticsEvidence", owner.ProjectId,
                normalized == LogisticsStages.Packing ? targetId : null, normalized == LogisticsStages.Packing ? null : targetId,
                actorId, fingerprint, response, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return LogisticsMutationResult<LogisticsMutationResponse>.Success(response);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await RollbackQuietly(transaction, cancellationToken);
            return LogisticsMutationResult<LogisticsMutationResponse>.Conflict("다른 요청이 먼저 증빙을 변경했습니다. 새로고침 후 다시 시도해 주세요.");
        }
        catch { await RollbackQuietly(transaction, cancellationToken); throw; }
    }

    public async Task<LogisticsMutationResult<LogisticsMutationResponse>> FinalizeAsync(
        Guid targetId, string stage, FinalizeLogisticsRequest request, Guid actorId, ProjectAccessScope scope, CancellationToken cancellationToken)
    {
        var normalized = LogisticsStages.Normalize(stage);
        if (normalized is null) return LogisticsMutationResult<LogisticsMutationResponse>.Validation("stage", "물류 단계를 확인해 주세요.");
        if (request.ExpectedVersion is null) return LogisticsMutationResult<LogisticsMutationResponse>.Validation("expectedVersion", "현재 버전을 입력해 주세요.");
        var fingerprint = Fingerprint("finalize", targetId, normalized, request.ExpectedVersion);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var ownerProjectId = await ReadOwnerProjectInScopeAsync(connection, transaction, targetId, normalized, scope, cancellationToken);
            if (ownerProjectId is null) return await RollbackNotFound(transaction, cancellationToken);
            if (!await LockActiveProjectAsync(connection, transaction, ownerProjectId.Value, cancellationToken))
                return await RollbackConflict(transaction, "프로젝트 상태가 변경되었습니다. 목록을 새로 확인해 주세요.", cancellationToken);
            var owner = await LockOwnerAsync(connection, transaction, targetId, normalized, scope, cancellationToken);
            if (owner is null) return await RollbackNotFound(transaction, cancellationToken);
            var replay = await ReadReplayAsync(connection, transaction, request.OperationId, fingerprint, cancellationToken);
            if (replay.Result is not null) { await transaction.CommitAsync(cancellationToken); return replay.Result; }
            if (owner.Status != "Draft" || owner.Version != request.ExpectedVersion.Value)
                return await RollbackConflict(transaction, "작업이 이미 확정되었거나 버전이 변경되었습니다. 새로고침해 주세요.", cancellationToken);
            var panelIds = await ReadOwnerPanelsAsync(connection, transaction, owner, cancellationToken);
            if (panelIds.Count == 0) return await RollbackConflict(transaction, "확정할 대상을 한 개 이상 선택해 주세요.", cancellationToken);
            var workStage = LogisticsStages.WorkStage(normalized);
            if (!await ActorCanMutateAsync(connection, transaction, owner.ProjectId, panelIds, workStage, actorId, cancellationToken))
                return await RollbackForbidden(transaction, cancellationToken);
            if (await HasOpenPendingAsync(connection, transaction, owner.ProjectId, panelIds, cancellationToken))
                return await RollbackConflict(transaction, "열린 Pending을 먼저 처리한 뒤 확정해 주세요.", cancellationToken);
            if (!await HasRequiredEvidenceAsync(connection, transaction, owner, cancellationToken))
                return await RollbackConflict(transaction, normalized == LogisticsStages.Delivery ? "거래명세서 서명본을 한 개 이상 등록해 주세요." : "필수 사진을 한 장 이상 등록해 주세요.", cancellationToken);
            if (normalized == LogisticsStages.Departure && owner.DepartureDate is null)
                return await RollbackConflict(transaction, "출발일을 입력해 주세요.", cancellationToken);
            if (!await AllWorkOpenAsync(connection, transaction, owner.ProjectId, panelIds, workStage, cancellationToken))
                return await RollbackConflict(transaction, "선택 대상의 현재 물류 업무 상태가 변경되었습니다.", cancellationToken);

            if (normalized == LogisticsStages.Packing)
            {
                var next = await ResolveAssigneeAsync(connection, transaction, owner.ProjectId, "DepartureProcessed", cancellationToken);
                if (next is null) return await RollbackConflict(transaction, "다음 출발 처리 담당자를 지정한 뒤 다시 시도해 주세요.", cancellationToken);
                await CompleteWorkAsync(connection, transaction, owner.ProjectId, panelIds, "PackingCompleted", actorId, cancellationToken);
                await AdvancePanelsAsync(connection, transaction, panelIds, "PackingCompleted", cancellationToken);
                await CreatePanelWorkAsync(connection, transaction, owner.ProjectId, panelIds, "DepartureProcessed", next.Value, actorId, request.OperationId, cancellationToken);
                await FinalizeOwnerAsync(connection, transaction, owner, actorId, cancellationToken);
                await EnsureProjectEventAsync(connection, transaction, owner.ProjectId, "PackingCompleted", targetId, request.OperationId, actorId, cancellationToken);
            }
            else if (normalized == LogisticsStages.Departure)
            {
                var next = await ResolveAssigneeAsync(connection, transaction, owner.ProjectId, "DeliveryCompleted", cancellationToken);
                if (next is null) return await RollbackConflict(transaction, "다음 납품 완료 담당자를 지정한 뒤 다시 시도해 주세요.", cancellationToken);
                await CompleteWorkAsync(connection, transaction, owner.ProjectId, panelIds, "DepartureProcessed", actorId, cancellationToken);
                await CreatePanelWorkAsync(connection, transaction, owner.ProjectId, panelIds, "DeliveryCompleted", next.Value, actorId, request.OperationId, cancellationToken);
                await FinalizeOwnerAsync(connection, transaction, owner, actorId, cancellationToken);
                await EnsureProjectEventAsync(connection, transaction, owner.ProjectId, "DepartureProcessed", targetId, request.OperationId, actorId, cancellationToken);
            }
            else
            {
                var willCompleteProject = await WillCompleteDeliveryAsync(connection, transaction, owner.ProjectId, panelIds, cancellationToken);
                Assignee? sales = null;
                if (willCompleteProject)
                {
                    sales = await ResolveAssigneeAsync(connection, transaction, owner.ProjectId, "SalesSettlementCompleted", cancellationToken);
                    if (sales is null) return await RollbackConflict(transaction, "영업 정산 담당자를 지정한 뒤 다시 시도해 주세요.", cancellationToken);
                }
                await InsertDeliveryResultsAsync(connection, transaction, owner, panelIds, actorId, cancellationToken);
                await CompleteWorkAsync(connection, transaction, owner.ProjectId, panelIds, "DeliveryCompleted", actorId, cancellationToken);
                await AdvancePanelsAsync(connection, transaction, panelIds, "ShipmentCompleted", cancellationToken);
                await FinalizeOwnerAsync(connection, transaction, owner, actorId, cancellationToken);
                if (willCompleteProject && sales is not null)
                {
                    await EnsureProjectEventAsync(connection, transaction, owner.ProjectId, "DeliveryCompleted", targetId, request.OperationId, actorId, cancellationToken);
                    await CreateProjectDeliveryCompletedNotificationAsync(connection, transaction, owner.ProjectId, cancellationToken);
                    await CreateSalesSettlementWorkAsync(connection, transaction, owner.ProjectId, sales.Value, actorId, cancellationToken);
                }
            }

            var nextStage = normalized switch { LogisticsStages.Packing => LogisticsStages.Departure, LogisticsStages.Departure => LogisticsStages.Delivery, _ => "sales-settlement" };
            var response = new LogisticsMutationResponse(request.OperationId, owner.ProjectId, targetId, normalized, "Finalized", owner.Version + 1, nextStage, false);
            await InsertReceiptAsync(connection, transaction, request.OperationId, $"Finalize{normalized}", owner.ProjectId,
                normalized == LogisticsStages.Packing ? targetId : null, normalized == LogisticsStages.Packing ? null : targetId,
                actorId, fingerprint, response, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return LogisticsMutationResult<LogisticsMutationResponse>.Success(response);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await RollbackQuietly(transaction, cancellationToken);
            return LogisticsMutationResult<LogisticsMutationResponse>.Conflict("다른 요청이 먼저 물류 단계를 확정했습니다. 새로고침 후 다시 시도해 주세요.");
        }
        catch { await RollbackQuietly(transaction, cancellationToken); throw; }
    }

    public async Task<LogisticsMutationResult<LogisticsMutationResponse>> CancelDraftAsync(
        Guid targetId, string stage, CancelLogisticsDraftRequest request, Guid actorId, ProjectAccessScope scope, CancellationToken cancellationToken)
    {
        var normalized = LogisticsStages.Normalize(stage);
        if (normalized is null) return LogisticsMutationResult<LogisticsMutationResponse>.Validation("stage", "물류 단계를 확인해 주세요.");
        if (request.ExpectedVersion is null) return LogisticsMutationResult<LogisticsMutationResponse>.Validation("expectedVersion", "현재 버전을 입력해 주세요.");
        var fingerprint = Fingerprint("cancel-draft", targetId, normalized, request.ExpectedVersion);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var owner = await LockOwnerAsync(connection, transaction, targetId, normalized, scope, cancellationToken);
            if (owner is null) return await RollbackNotFound(transaction, cancellationToken);
            var replay = await ReadReplayAsync(connection, transaction, request.OperationId, fingerprint, cancellationToken);
            if (replay.Result is not null) { await transaction.CommitAsync(cancellationToken); return replay.Result; }
            if (owner.Status != "Draft" || owner.Version != request.ExpectedVersion.Value)
                return await RollbackConflict(transaction, "최신 draft만 취소할 수 있습니다.", cancellationToken);
            var panelIds = await ReadOwnerPanelsAsync(connection, transaction, owner, cancellationToken);
            if (panelIds.Count > 0 && !await ActorCanMutateAsync(connection, transaction, owner.ProjectId, panelIds, LogisticsStages.WorkStage(normalized), actorId, cancellationToken))
                return await RollbackForbidden(transaction, cancellationToken);
            await using (var membership = connection.CreateCommand())
            {
                membership.Transaction = transaction;
                membership.CommandText = normalized == LogisticsStages.Packing
                    ? "update logistics_packing_unit_panels set active = false where packing_unit_id = @id and active"
                    : """
                      update logistics_batch_panels set active = false where batch_id = @id and active;
                      update logistics_batch_units set active = false where batch_id = @id and active;
                      """;
                membership.Parameters.AddWithValue("id", targetId);
                await membership.ExecuteNonQueryAsync(cancellationToken);
            }
            await using (var cancel = connection.CreateCommand())
            {
                cancel.Transaction = transaction;
                cancel.CommandText = normalized == LogisticsStages.Packing
                    ? "update logistics_packing_units set status='Cancelled', version=version+1, cancelled_by_user_id=@actor_id, cancelled_at_utc=now() where id=@id"
                    : "update logistics_batches set status='Cancelled', version=version+1, cancelled_by_user_id=@actor_id, cancelled_at_utc=now() where id=@id";
                cancel.Parameters.AddWithValue("id", targetId);
                cancel.Parameters.AddWithValue("actor_id", actorId);
                await cancel.ExecuteNonQueryAsync(cancellationToken);
            }
            var response = new LogisticsMutationResponse(request.OperationId, owner.ProjectId, targetId, normalized, "Cancelled", owner.Version + 1, normalized, false);
            await InsertReceiptAsync(connection, transaction, request.OperationId, "CancelLogisticsDraft", owner.ProjectId,
                normalized == LogisticsStages.Packing ? targetId : null, normalized == LogisticsStages.Packing ? null : targetId,
                actorId, fingerprint, response, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return LogisticsMutationResult<LogisticsMutationResponse>.Success(response);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await RollbackQuietly(transaction, cancellationToken);
            return LogisticsMutationResult<LogisticsMutationResponse>.Conflict("다른 요청이 먼저 물류 작업을 변경했습니다. 새로고침 후 다시 시도해 주세요.");
        }
        catch { await RollbackQuietly(transaction, cancellationToken); throw; }
    }

    public async Task<LogisticsMutationResult<LogisticsEvidenceContent>> GetEvidenceAsync(
        Guid evidenceId, ProjectAccessScope scope, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select evidence.content, evidence.normalized_mime, evidence.display_name
            from logistics_evidence evidence
            left join logistics_packing_units unit on unit.id = evidence.packing_unit_id
            left join logistics_batches batch on batch.id = evidence.batch_id
            join projects project on project.id = coalesce(unit.project_id, batch.project_id)
            where evidence.id = @id and project.deleted_at_utc is null
              and (@has_read_all or project.project_key = any(@project_keys));
            """);
        command.Parameters.AddWithValue("id", evidenceId);
        AddScope(command, scope);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? LogisticsMutationResult<LogisticsEvidenceContent>.Success(new(reader.GetFieldValue<byte[]>(0), reader.GetString(1), reader.GetString(2)))
            : LogisticsMutationResult<LogisticsEvidenceContent>.NotFound();
    }

    private async Task<LogisticsMutationResult<LogisticsMutationResponse>> MutateDraftMembershipAsync(
        Guid targetId, string stage, Guid operationId, int expectedVersion, IReadOnlyList<Guid> ids, DateOnly? departureDate,
        string fingerprint, Guid actorId, ProjectAccessScope scope, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var owner = await LockOwnerAsync(connection, transaction, targetId, stage, scope, cancellationToken);
            if (owner is null) return await RollbackNotFound(transaction, cancellationToken);
            var replay = await ReadReplayAsync(connection, transaction, operationId, fingerprint, cancellationToken);
            if (replay.Result is not null) { await transaction.CommitAsync(cancellationToken); return replay.Result; }
            if (owner.Status != "Draft" || owner.Version != expectedVersion)
                return await RollbackConflict(transaction, "다른 사용자가 먼저 수정했습니다. 새로고침 후 다시 시도해 주세요.", cancellationToken);
            if (stage != LogisticsStages.Packing)
                return await RollbackConflict(transaction, "포장 구성 변경 경로를 확인해 주세요.", cancellationToken);
            if (ids.Count > 0 && !await ValidatePanelsAsync(connection, transaction, owner.ProjectId, ids, "PackingCompleted", cancellationToken))
                return await RollbackConflict(transaction, "같은 프로젝트의 포장 대기 패널만 선택해 주세요.", cancellationToken);
            if (ids.Count > 0 && !await ActorCanMutateAsync(connection, transaction, owner.ProjectId, ids, LogisticsStages.WorkStage(stage), actorId, cancellationToken))
                return await RollbackForbidden(transaction, cancellationToken);

            await using (var clear = connection.CreateCommand())
            {
                clear.Transaction = transaction;
                clear.CommandText = "delete from logistics_packing_unit_panels where packing_unit_id = @id";
                clear.Parameters.AddWithValue("id", targetId);
                await clear.ExecuteNonQueryAsync(cancellationToken);
            }
            await InsertPackingMembershipsAsync(connection, transaction, targetId, ids, actorId, cancellationToken);
            await using (var update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = "update logistics_packing_units set version=version+1 where id=@id and version=@expected returning version";
                update.Parameters.AddWithValue("id", targetId);
                update.Parameters.AddWithValue("expected", expectedVersion);
                var version = Convert.ToInt32(await update.ExecuteScalarAsync(cancellationToken));
                var response = new LogisticsMutationResponse(operationId, owner.ProjectId, targetId, stage, "Draft", version, ids.Count == 0 ? "target" : "evidence", false);
                await InsertReceiptAsync(connection, transaction, operationId, "ReplaceLogisticsMembership", owner.ProjectId,
                    stage == LogisticsStages.Packing ? targetId : null, stage == LogisticsStages.Packing ? null : targetId,
                    actorId, fingerprint, response, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return LogisticsMutationResult<LogisticsMutationResponse>.Success(response);
            }
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await RollbackQuietly(transaction, cancellationToken);
            return LogisticsMutationResult<LogisticsMutationResponse>.Conflict("다른 요청이 먼저 물류 구성을 변경했습니다. 새로고침 후 다시 시도해 주세요.");
        }
        catch { await RollbackQuietly(transaction, cancellationToken); throw; }
    }

    private async Task<LogisticsMutationResult<LogisticsMutationResponse>> MutateDraftBatchMembershipAsync(
        Guid targetId,
        string stage,
        Guid operationId,
        int expectedVersion,
        IReadOnlyList<Guid> requestedUnitIds,
        IReadOnlyList<Guid> requestedPanelIds,
        DateOnly? departureDate,
        string fingerprint,
        Guid actorId,
        ProjectAccessScope scope,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var owner = await LockOwnerAsync(connection, transaction, targetId, stage, scope, cancellationToken);
            if (owner is null) return await RollbackNotFound(transaction, cancellationToken);
            var replay = await ReadReplayAsync(connection, transaction, operationId, fingerprint, cancellationToken);
            if (replay.Result is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return replay.Result;
            }
            if (owner.Status != "Draft" || owner.Version != expectedVersion)
                return await RollbackConflict(transaction, "다른 사용자가 먼저 수정했습니다. 새로고침 후 다시 시도해 주세요.", cancellationToken);

            var selection = requestedUnitIds.Count == 0 && requestedPanelIds.Count == 0
                ? new BatchSelection([], [], [])
                : await ReadBatchSelectionAsync(
                    connection, transaction, owner.ProjectId, requestedUnitIds, requestedPanelIds, stage, targetId, cancellationToken);
            if (selection is null)
                return await RollbackConflict(transaction, "같은 프로젝트의 현재 단계 패널만 선택해 주세요.", cancellationToken);
            if (selection.PanelIds.Count > 0
                && !await ActorCanMutateAsync(
                    connection, transaction, owner.ProjectId, selection.PanelIds, LogisticsStages.WorkStage(stage), actorId, cancellationToken))
                return await RollbackForbidden(transaction, cancellationToken);

            await using (var clearPanels = connection.CreateCommand())
            {
                clearPanels.Transaction = transaction;
                clearPanels.CommandText = "delete from logistics_batch_panels where batch_id=@id";
                clearPanels.Parameters.AddWithValue("id", targetId);
                await clearPanels.ExecuteNonQueryAsync(cancellationToken);
            }
            await using (var clearUnits = connection.CreateCommand())
            {
                clearUnits.Transaction = transaction;
                clearUnits.CommandText = "delete from logistics_batch_units where batch_id=@id";
                clearUnits.Parameters.AddWithValue("id", targetId);
                await clearUnits.ExecuteNonQueryAsync(cancellationToken);
            }
            if (selection.PanelIds.Count > 0)
            {
                await InsertBatchMembershipsAsync(
                    connection, transaction, targetId, selection.UnitIds, selection.PanelIds,
                    LogisticsStages.WorkStage(stage), actorId, cancellationToken);
            }
            await using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                update logistics_batches
                set version=version+1, departure_date=@departure_date
                where id=@id and version=@expected
                returning version;
                """;
            update.Parameters.AddWithValue("id", targetId);
            update.Parameters.AddWithValue("expected", expectedVersion);
            AddNullableDate(update, "departure_date", stage == LogisticsStages.Departure ? departureDate : null);
            var version = Convert.ToInt32(await update.ExecuteScalarAsync(cancellationToken));
            var response = new LogisticsMutationResponse(
                operationId, owner.ProjectId, targetId, stage, "Draft", version,
                selection.PanelIds.Count == 0 ? "target" : "evidence", false);
            await InsertReceiptAsync(
                connection, transaction, operationId, "ReplaceLogisticsMembership", owner.ProjectId,
                null, targetId, actorId, fingerprint, response, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return LogisticsMutationResult<LogisticsMutationResponse>.Success(response);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await RollbackQuietly(transaction, cancellationToken);
            return LogisticsMutationResult<LogisticsMutationResponse>.Conflict(
                "다른 요청이 먼저 물류 구성을 변경했습니다. 새로고침 후 다시 시도해 주세요.");
        }
        catch
        {
            await RollbackQuietly(transaction, cancellationToken);
            throw;
        }
    }

    private static string QueueSql(string stage, bool filterProject)
    {
        var projectFilter = filterProject ? "and project.id = @project_id" : string.Empty;
        if (stage == LogisticsStages.Packing)
        {
            return $"""
                select project.id, project.project_code, project.project_title,
                       panel.id, 'Panel', panel.display_code, coalesce(panel.panel_name, panel.display_code),
                       '품질 완료 · 포장 대기', array[panel.id]::uuid[], array[panel.display_code]::text[],
                       0, work.status,
                       exists(select 1 from pending_issues pending where pending.status <> 'Closed' and pending.project_id=project.id and (pending.target_type='Project' or (pending.target_type='Panel' and pending.target_id=panel.id))),
                       (@can_ship and (exists(select 1 from project_assignees pa where pa.project_id=project.id and pa.assigned_user_id=@actor_id and pa.responsibility_type in ('LogisticsPrimary','LogisticsSecondary')) or work.assigned_user_id=@actor_id))
                from work_items work
                join panel_placeholders panel on panel.id=work.target_id and panel.status='Active'
                join projects project on project.id=work.project_id and project.deleted_at_utc is null and project.status <> 'Cancelled'
                where work.target_type='Panel' and work.workflow_stage_code='PackingCompleted' and work.status in ('Requested','InProgress')
                  and not exists(select 1 from logistics_packing_unit_panels membership where membership.panel_id=panel.id and membership.active)
                  and (@has_read_all or project.project_key = any(@project_keys)) {projectFilter}
                order by project.project_code, panel.sequence_number;
                """;
        }
        var workStage = LogisticsStages.WorkStage(stage);
        var prerequisite = stage == LogisticsStages.Departure
            ? "unit.status='Finalized'"
            : """
              unit.status='Finalized'
              and exists(
                select 1
                from logistics_batch_panels departed
                join logistics_batches departure
                  on departure.id=departed.batch_id
                 and departure.stage_code='DepartureProcessed'
                 and departure.status='Finalized'
                where departed.panel_id=panel.id and departed.active
              )
              """;
        var waitLabel = stage == LogisticsStages.Departure ? "출발 대기" : "납품 대기";
        return $"""
            select project.id, project.project_code, project.project_title,
                   panel.id, 'Panel', panel.display_code, coalesce(panel.panel_name, panel.display_code),
                   'PU-' || lpad(unit.unit_number::text, 3, '0') || ' · 포장 완료 · {waitLabel}',
                   array[panel.id]::uuid[], array[panel.display_code]::text[],
                   unit.version, work.status,
                   exists(select 1 from pending_issues pending where pending.status <> 'Closed' and pending.project_id=project.id
                     and (pending.target_type='Project' or (pending.target_type='Panel' and pending.target_id=panel.id))),
                   (@can_ship and (exists(select 1 from project_assignees pa where pa.project_id=project.id and pa.assigned_user_id=@actor_id and pa.responsibility_type in ('LogisticsPrimary','LogisticsSecondary')) or work.assigned_user_id=@actor_id))
            from logistics_packing_units unit
            join projects project on project.id=unit.project_id and project.deleted_at_utc is null and project.status <> 'Cancelled'
            join logistics_packing_unit_panels membership on membership.packing_unit_id=unit.id and membership.active
            join panel_placeholders panel on panel.id=membership.panel_id and panel.status='Active'
            join work_items work on work.project_id=project.id and work.target_type='Panel' and work.target_id=panel.id
              and work.workflow_stage_code='{workStage}' and work.status in ('Requested','InProgress')
            where {prerequisite}
              and not exists(select 1 from logistics_batch_panels used where used.panel_id=panel.id and used.stage_code='{workStage}' and used.active)
              and (@has_read_all or project.project_key=any(@project_keys)) {projectFilter}
            order by project.project_code, unit.unit_number, panel.sequence_number;
            """;
    }

    private sealed record LogisticsHistoryOwner(
        Guid TargetId,
        string Stage,
        string DisplayCode,
        string Status,
        int Version,
        string? Note,
        string? Specification,
        string? WeightText,
        DateOnly? DepartureDate,
        string CreatedByName,
        DateTimeOffset CreatedAtUtc,
        string? FinalizedByName,
        DateTimeOffset? FinalizedAtUtc,
        string? CancelledByName,
        DateTimeOffset? CancelledAtUtc);

    private static async Task<bool> LockProjectInScopeAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, ProjectAccessScope scope, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select 1 from projects where id=@project_id and deleted_at_utc is null and status <> 'Cancelled'
              and (@has_read_all or project_key=any(@project_keys)) for update;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        AddScope(command, scope);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<Guid?> ReadOwnerProjectInScopeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid targetId,
        string stage,
        ProjectAccessScope scope,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = stage == LogisticsStages.Packing
            ? """
                select unit.project_id from logistics_packing_units unit
                join projects project on project.id=unit.project_id
                where unit.id=@id and (@has_read_all or project.project_key=any(@project_keys));
                """
            : """
                select batch.project_id from logistics_batches batch
                join projects project on project.id=batch.project_id
                where batch.id=@id and batch.stage_code=@stage
                  and (@has_read_all or project.project_key=any(@project_keys));
                """;
        command.Parameters.AddWithValue("id", targetId);
        if (stage != LogisticsStages.Packing) command.Parameters.AddWithValue("stage", LogisticsStages.WorkStage(stage));
        AddScope(command, scope);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid projectId ? projectId : null;
    }

    private static async Task<bool> LockActiveProjectAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select id from projects where id=@project_id and deleted_at_utc is null and status <> 'Cancelled' for update";
        command.Parameters.AddWithValue("project_id", projectId);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<bool> ValidatePanelsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, IReadOnlyList<Guid> panelIds, string workStage, CancellationToken cancellationToken)
    {
        if (panelIds.Count == 0) return true;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select panel.id from panel_placeholders panel
            where panel.id=any(@panel_ids) and panel.project_id=@project_id and panel.status='Active'
              and exists(select 1 from work_items work where work.target_type='Panel' and work.target_id=panel.id and work.workflow_stage_code=@stage and work.status in ('Requested','InProgress'))
            order by panel.id for update of panel;
            """;
        command.Parameters.AddWithValue("panel_ids", panelIds.ToArray());
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("stage", workStage);
        var count = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) count++;
        return count == panelIds.Count;
    }

    private static async Task<bool> ActorCanMutateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, IReadOnlyList<Guid> panelIds, string stage, Guid actorId, CancellationToken cancellationToken)
    {
        if (panelIds.Count == 0) return false;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists(
                select 1 from project_assignees where project_id=@project_id and assigned_user_id=@actor_id
                  and responsibility_type in ('LogisticsPrimary','LogisticsSecondary')
            ) or (
                select count(distinct target_id)=cardinality(@panel_ids)
                from work_items where project_id=@project_id and target_type='Panel' and target_id=any(@panel_ids)
                  and workflow_stage_code=@stage and status in ('Requested','InProgress') and assigned_user_id=@actor_id
            );
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("actor_id", actorId);
        command.Parameters.AddWithValue("panel_ids", panelIds.ToArray());
        command.Parameters.AddWithValue("stage", stage);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<bool> HasOpenPendingAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, IReadOnlyList<Guid> panelIds, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists(select 1 from pending_issues where project_id=@project_id and status <> 'Closed'
              and (target_type='Project' or (target_type='Panel' and target_id=any(@panel_ids))));
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("panel_ids", panelIds.ToArray());
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<BatchSelection?> ReadBatchSelectionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        IReadOnlyList<Guid> requestedUnitIds,
        IReadOnlyList<Guid> requestedPanelIds,
        string stage,
        Guid? existingBatchId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var prerequisite = stage == LogisticsStages.Departure
            ? "unit.status='Finalized'"
            : """
              unit.status='Finalized'
              and exists(
                select 1
                from logistics_batch_panels departed
                join logistics_batches departure
                  on departure.id=departed.batch_id
                 and departure.status='Finalized'
                 and departure.stage_code='DepartureProcessed'
                where departed.panel_id=panel.id and departed.active
              )
              """;
        command.CommandText = $"""
            select unit.id, panel.id, panel.display_code
            from logistics_packing_units unit
            join logistics_packing_unit_panels membership on membership.packing_unit_id=unit.id and membership.active
            join panel_placeholders panel on panel.id=membership.panel_id and panel.status='Active'
            where unit.project_id=@project_id
              and (
                (cardinality(@panel_ids) > 0 and panel.id=any(@panel_ids))
                or (cardinality(@panel_ids) = 0 and unit.id=any(@unit_ids))
              )
              and {prerequisite}
              and not exists(
                select 1 from logistics_batch_panels used
                where used.panel_id=panel.id and used.stage_code=@work_stage and used.active
                  and (@batch_id is null or used.batch_id<>@batch_id)
              )
              and exists(
                select 1 from work_items work
                where work.project_id=@project_id and work.target_type='Panel' and work.target_id=panel.id
                  and work.workflow_stage_code=@work_stage and work.status in ('Requested','InProgress')
              )
            order by unit.id, panel.id
            for update of unit, panel;
            """;
        command.Parameters.AddWithValue("unit_ids", requestedUnitIds.ToArray());
        command.Parameters.AddWithValue("panel_ids", requestedPanelIds.ToArray());
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("work_stage", LogisticsStages.WorkStage(stage));
        AddNullableUuid(command, "batch_id", existingBatchId);
        var unitIds = new List<Guid>();
        var panelIds = new List<Guid>();
        var panelCodes = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var unitId = reader.GetGuid(0);
            var panelId = reader.GetGuid(1);
            if (!unitIds.Contains(unitId)) unitIds.Add(unitId);
            if (!panelIds.Contains(panelId))
            {
                panelIds.Add(panelId);
                panelCodes.Add(reader.GetString(2));
            }
        }
        if (requestedPanelIds.Count > 0 && panelIds.Count != requestedPanelIds.Count) return null;
        if (requestedPanelIds.Count == 0 && unitIds.Count != requestedUnitIds.Count) return null;
        return panelIds.Count == 0 ? null : new BatchSelection(unitIds, panelIds, panelCodes);
    }

    private static async Task<Owner?> LockOwnerAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid targetId, string stage, ProjectAccessScope scope, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = stage == LogisticsStages.Packing
            ? """
                select unit.id, unit.project_id, unit.status, unit.version, null::date
                from logistics_packing_units unit join projects project on project.id=unit.project_id
                where unit.id=@id and (@has_read_all or project.project_key=any(@project_keys)) for update of unit;
                """
            : """
                select batch.id, batch.project_id, batch.status, batch.version, batch.departure_date
                from logistics_batches batch join projects project on project.id=batch.project_id
                where batch.id=@id and batch.stage_code=@stage and (@has_read_all or project.project_key=any(@project_keys)) for update of batch;
                """;
        command.Parameters.AddWithValue("id", targetId);
        if (stage != LogisticsStages.Packing) command.Parameters.AddWithValue("stage", LogisticsStages.WorkStage(stage));
        AddScope(command, scope);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new Owner(reader.GetGuid(0), reader.GetGuid(1), stage, reader.GetString(2), reader.GetInt32(3), reader.IsDBNull(4) ? null : DateOnly.FromDateTime(reader.GetDateTime(4)));
    }

    private static async Task<IReadOnlyList<Guid>> ReadOwnerPanelsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Owner owner, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = owner.Stage == LogisticsStages.Packing
            ? "select panel_id from logistics_packing_unit_panels where packing_unit_id=@id and active order by panel_id for update"
            : "select panel_id from logistics_batch_panels where batch_id=@id and active order by panel_id for update";
        command.Parameters.AddWithValue("id", owner.Id);
        var result = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(reader.GetGuid(0));
        return result;
    }

    private static async Task InsertPackingMembershipsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid unitId, IReadOnlyList<Guid> panelIds, Guid actorId, CancellationToken cancellationToken)
    {
        foreach (var panelId in panelIds)
        {
            await using var command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText = "insert into logistics_packing_unit_panels (packing_unit_id,panel_id,added_by_user_id) values (@unit_id,@panel_id,@actor_id)";
            command.Parameters.AddWithValue("unit_id", unitId); command.Parameters.AddWithValue("panel_id", panelId); command.Parameters.AddWithValue("actor_id", actorId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task InsertBatchMembershipsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid batchId,
        IReadOnlyList<Guid> unitIds,
        IReadOnlyList<Guid> panelIds,
        string stage,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        foreach (var unitId in unitIds)
        {
            await using var command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText = "insert into logistics_batch_units (batch_id,packing_unit_id,stage_code,added_by_user_id) values (@batch_id,@unit_id,@stage,@actor_id)";
            command.Parameters.AddWithValue("batch_id", batchId); command.Parameters.AddWithValue("unit_id", unitId); command.Parameters.AddWithValue("stage", stage); command.Parameters.AddWithValue("actor_id", actorId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        foreach (var panelId in panelIds)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into logistics_batch_panels (
                    batch_id, packing_unit_id, panel_id, stage_code, added_by_user_id
                )
                select @batch_id, membership.packing_unit_id, membership.panel_id, @stage, @actor_id
                from logistics_packing_unit_panels membership
                where membership.panel_id=@panel_id and membership.active;
                """;
            command.Parameters.AddWithValue("batch_id", batchId);
            command.Parameters.AddWithValue("panel_id", panelId);
            command.Parameters.AddWithValue("stage", stage);
            command.Parameters.AddWithValue("actor_id", actorId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<int> IncrementOwnerVersionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Owner owner, int expected, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = owner.Stage == LogisticsStages.Packing
            ? "update logistics_packing_units set version=version+1 where id=@id and version=@expected returning version"
            : "update logistics_batches set version=version+1 where id=@id and version=@expected returning version";
        command.Parameters.AddWithValue("id", owner.Id); command.Parameters.AddWithValue("expected", expected);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<bool> HasRequiredEvidenceAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Owner owner, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = owner.Stage == LogisticsStages.Packing
            ? "select count(*) between 1 and 5 from logistics_evidence where packing_unit_id=@id and owner_type='PackingPhoto'"
            : owner.Stage == LogisticsStages.Departure
                ? "select count(*) between 1 and 5 from logistics_evidence where batch_id=@id and owner_type='DeparturePhoto'"
                : "select count(*) between 1 and 3 from logistics_evidence where batch_id=@id and owner_type='DeliveryDocument'";
        command.Parameters.AddWithValue("id", owner.Id);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<bool> AllWorkOpenAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, IReadOnlyList<Guid> panels, string stage, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            select target_id from work_items
            where project_id=@project_id and target_type='Panel' and target_id=any(@panel_ids)
              and workflow_stage_code=@stage and status in ('Requested','InProgress') order by target_id for update;
            """;
        command.Parameters.AddWithValue("project_id", projectId); command.Parameters.AddWithValue("panel_ids", panels.ToArray()); command.Parameters.AddWithValue("stage", stage);
        var found = new HashSet<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) found.Add(reader.GetGuid(0));
        return found.Count == panels.Count;
    }

    private static async Task CompleteWorkAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        IReadOnlyList<Guid> panels,
        string stage,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            update work_items set status='Completed', started_at_utc=coalesce(started_at_utc,now()), completed_at_utc=coalesce(completed_at_utc,now())
            where project_id=@project_id and target_type='Panel' and target_id=any(@panel_ids) and workflow_stage_code=@stage and status in ('Requested','InProgress');
            """;
        command.Parameters.AddWithValue("project_id", projectId); command.Parameters.AddWithValue("panel_ids", panels.ToArray()); command.Parameters.AddWithValue("stage", stage);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await WorkItemFallbackCompletion.SynchronizeForPanelStageAsync(
            connection,
            transaction,
            projectId,
            panels,
            stage,
            actorUserId,
            cancellationToken);
    }

    private static async Task AdvancePanelsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyList<Guid> panels, string stage, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = stage == "PackingCompleted"
            ? "update panel_placeholders set workflow_stage='PackingCompleted', updated_at_utc=now() where id=any(@panel_ids) and workflow_stage <> 'ShipmentCompleted'"
            : "update panel_placeholders set workflow_stage='ShipmentCompleted', updated_at_utc=now() where id=any(@panel_ids)";
        command.Parameters.AddWithValue("panel_ids", panels.ToArray()); await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Assignee?> ResolveAssigneeAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, string stage, CancellationToken cancellationToken)
    {
        var responsibilities = stage == "SalesSettlementCompleted" ? new[] { "SalesPrimary", "SalesSecondary" } : new[] { "LogisticsPrimary", "LogisticsSecondary", "Logistics" };
        var departmentCode = stage == "SalesSettlementCompleted" ? "sales" : "logistics";
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            with candidates as (
              select pa.assigned_user_id user_id, role.code role_code, array_position(@responsibilities,pa.responsibility_type) priority, users.display_name
              from project_assignees pa join qms_users users on users.id=pa.assigned_user_id and users.is_active
              left join user_roles ur on ur.user_id=users.id left join roles role on role.id=ur.role_id
              where pa.project_id=@project_id and pa.responsibility_type=any(@responsibilities)
              union all
              select users.id, role.code, 100, users.display_name
              from qms_users users
              join departments department on department.id=users.department_id and department.code=@department_code and department.is_active
              left join lateral (
                select roles.code
                from user_roles user_role join roles on roles.id=user_role.role_id
                where user_role.user_id=users.id
                order by roles.code limit 1
              ) role on true
              where users.is_active and users.is_department_head
            ) select user_id, role_code from candidates order by priority,display_name,user_id limit 1;
            """;
        command.Parameters.AddWithValue("project_id", projectId); command.Parameters.AddWithValue("responsibilities", responsibilities); command.Parameters.AddWithValue("department_code", departmentCode);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetString(1));
        }
        throw new DepartmentHeadRequiredException(departmentCode);
    }

    private static async Task CreatePanelWorkAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, IReadOnlyList<Guid> panels, string stage, Assignee assignee, Guid actorId, Guid operationId, CancellationToken cancellationToken)
    {
        foreach (var panelId in panels)
        {
            await using var command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText = """
                insert into work_items (project_id,target_type,target_id,workflow_stage_code,responsibility_type,assigned_user_id,assigned_role_code,title,description,status,priority,idempotency_key,created_by_user_id)
                select @project_id,'Panel',panel.id,@stage,'LogisticsPrimary',@assignee_id,@role_code,
                       case @stage when 'DepartureProcessed' then '출발 처리 · ' else '납품 완료 · ' end || panel.display_code,
                       case @stage when 'DepartureProcessed' then '상차사진과 출발일을 등록해 주세요.' else '거래명세서 서명본을 등록해 주세요.' end,
                       'Requested','Normal','logistics:panel:' || panel.id || case @stage when 'DepartureProcessed' then ':departure' else ':delivery' end,@actor_id
                from panel_placeholders panel where panel.id=@panel_id on conflict (idempotency_key) do nothing;
                """;
            command.Parameters.AddWithValue("project_id", projectId); command.Parameters.AddWithValue("panel_id", panelId); command.Parameters.AddWithValue("stage", stage);
            command.Parameters.AddWithValue("assignee_id", assignee.UserId); AddNullableText(command,"role_code",assignee.RoleCode); command.Parameters.AddWithValue("actor_id", actorId);
            await command.ExecuteNonQueryAsync(cancellationToken);
            var key = $"logistics:panel:{panelId}:{(stage == "DepartureProcessed" ? "departure" : "delivery")}";
            await using var readCommand = connection.CreateCommand();
            readCommand.Transaction = transaction;
            readCommand.CommandText = "select id from work_items where idempotency_key=@key;";
            readCommand.Parameters.AddWithValue("key", key);
            var value = await readCommand.ExecuteScalarAsync(cancellationToken);
            if (value is Guid workItemId)
            {
                var title = stage == "DepartureProcessed" ? "출발 처리" : "납품 완료";
                var message = stage == "DepartureProcessed" ? "상차사진과 출발일을 등록해 주세요." : "거래명세서 서명본을 등록해 주세요.";
                var link = $"/logistics?stage={(stage == "DepartureProcessed" ? "departure" : "delivery")}&project={projectId}&panel={panelId}";
                await WorkAssignmentNotificationWriter.UpsertAsync(
                    connection, transaction, projectId, workItemId, assignee.UserId,
                    ["LogisticsSecondary"], $"{title} · 패널", message, link,
                    $"logistics:operation:{operationId}:stage:{stage}:assignee:{assignee.UserId}:notification", cancellationToken);
            }
        }
    }

    private static async Task FinalizeOwnerAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Owner owner, Guid actorId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = owner.Stage == LogisticsStages.Packing
            ? "update logistics_packing_units set status='Finalized',version=version+1,finalized_by_user_id=@actor_id,finalized_at_utc=now() where id=@id"
            : "update logistics_batches set status='Finalized',version=version+1,finalized_by_user_id=@actor_id,finalized_at_utc=now() where id=@id";
        command.Parameters.AddWithValue("id", owner.Id); command.Parameters.AddWithValue("actor_id", actorId); await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureProjectEventAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, string stage, Guid sourceId, Guid operationId, Guid actorId, CancellationToken cancellationToken)
    {
        var completion = stage switch
        {
            "PackingCompleted" => "exists(select 1 from logistics_packing_unit_panels membership join logistics_packing_units unit on unit.id=membership.packing_unit_id and unit.status='Finalized' where membership.panel_id=panel.id and membership.active)",
            "DepartureProcessed" => "exists(select 1 from logistics_batch_panels membership join logistics_batches batch on batch.id=membership.batch_id and batch.stage_code='DepartureProcessed' and batch.status='Finalized' where membership.panel_id=panel.id and membership.active)",
            _ => "exists(select 1 from logistics_delivery_results result where result.panel_id=panel.id)"
        };
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = $"""
            insert into project_workflow_events (project_id,stage_code,event_type,event_status,source_type,source_id,correlation_id,created_by_user_id,note)
            select @project_id,@stage,'StageCompleted','Succeeded','Logistics',@source_id,@correlation_id,@actor_id,'모든 활성 패널 물류 단계 완료'
            where exists(select 1 from panel_placeholders panel where panel.project_id=@project_id and panel.status='Active')
              and not exists(select 1 from panel_placeholders panel where panel.project_id=@project_id and panel.status='Active' and not ({completion}))
              and not exists(select 1 from project_workflow_events where project_id=@project_id and stage_code=@stage and event_type='StageCompleted' and event_status='Succeeded');
            """;
        command.Parameters.AddWithValue("project_id",projectId); command.Parameters.AddWithValue("stage",stage); command.Parameters.AddWithValue("source_id",sourceId);
        command.Parameters.AddWithValue("correlation_id",operationId.ToString("D")); command.Parameters.AddWithValue("actor_id",actorId); await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> WillCompleteDeliveryAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, IReadOnlyList<Guid> currentPanels, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            select not exists(select 1 from panel_placeholders panel where panel.project_id=@project_id and panel.status='Active'
              and panel.id <> all(@current_panels) and not exists(select 1 from logistics_delivery_results result where result.panel_id=panel.id));
            """;
        command.Parameters.AddWithValue("project_id",projectId); command.Parameters.AddWithValue("current_panels",currentPanels.ToArray());
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task CreateProjectDeliveryCompletedNotificationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into notifications (
                    id, project_id, notification_type, severity, title, message, link_url,
                    idempotency_key, visibility_scope, source_kind)
                values (
                    @id, @project_id, 'Reference', 'Info', '프로젝트 납품 완료',
                    '모든 활성 패널의 납품이 완료되었습니다.',
                    '/projects/' || @project_id || '/logistics',
                    'logistics:project:' || @project_id || ':delivery-completed',
                    'RecipientOnly', 'ProjectDeliveryCompleted')
                on conflict (idempotency_key) do nothing;
                """;
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("project_id", projectId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var recipients = connection.CreateCommand();
        recipients.Transaction = transaction;
        recipients.CommandText = """
            insert into notification_recipients (notification_id, user_id)
            select notification.id, target.user_id
            from notifications notification
            cross join lateral (
                select project.sales_owner_user_id as user_id
                from projects project
                where project.id = @project_id
                union
                select assignee.assigned_user_id
                from project_assignees assignee
                where assignee.project_id = @project_id
                  and assignee.assigned_user_id is not null
            ) target
            join qms_users users on users.id = target.user_id and users.is_active = true
            where notification.idempotency_key = 'logistics:project:' || @project_id || ':delivery-completed'
            on conflict (notification_id, user_id) do nothing;
            """;
        recipients.Parameters.AddWithValue("project_id", projectId);
        await recipients.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertDeliveryResultsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Owner owner, IReadOnlyList<Guid> panels, Guid actorId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            insert into logistics_delivery_results (batch_id,packing_unit_id,panel_id,delivered_by_user_id)
            select @batch_id,membership.packing_unit_id,membership.panel_id,@actor_id
            from logistics_batch_panels membership
            where membership.batch_id=@batch_id and membership.active and membership.panel_id=any(@panel_ids)
            on conflict (panel_id) do nothing;
            """;
        command.Parameters.AddWithValue("batch_id",owner.Id); command.Parameters.AddWithValue("actor_id",actorId); command.Parameters.AddWithValue("panel_ids",panels.ToArray());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CreateSalesSettlementWorkAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, Assignee sales, Guid actorId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            insert into work_items (project_id,target_type,target_id,workflow_stage_code,responsibility_type,assigned_user_id,assigned_role_code,title,description,status,priority,idempotency_key,created_by_user_id)
            values (@project_id,'Project',@project_id,'SalesSettlementCompleted','SalesPrimary',@assignee_id,@role_code,'세금계산서 발행요청 준비','모든 활성 패널의 납품이 완료되었습니다. 회계팀 발행요청 자료를 준비해 주세요.','Requested','Normal','logistics:project:' || @project_id || ':sales-settlement',@actor_id)
            on conflict (idempotency_key) do nothing;
            """;
        command.Parameters.AddWithValue("project_id",projectId); command.Parameters.AddWithValue("assignee_id",sales.UserId); AddNullableText(command,"role_code",sales.RoleCode); command.Parameters.AddWithValue("actor_id",actorId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        var key = $"logistics:project:{projectId}:sales-settlement";
        await using var readCommand = connection.CreateCommand();
        readCommand.Transaction = transaction;
        readCommand.CommandText = "select id from work_items where idempotency_key=@key;";
        readCommand.Parameters.AddWithValue("key", key);
        var value = await readCommand.ExecuteScalarAsync(cancellationToken);
        if (value is Guid workItemId)
        {
            await WorkAssignmentNotificationWriter.UpsertAsync(
                connection, transaction, projectId, workItemId, sales.UserId,
                ["SalesSecondary"], "세금계산서 발행요청 준비",
                "모든 활성 패널의 납품이 완료되었습니다. 회계팀 발행요청 자료를 준비해 주세요.",
                $"/projects/{projectId}/settlement", $"{key}:notification", cancellationToken);
        }
    }

    private static async Task<(LogisticsMutationResult<LogisticsMutationResponse>? Result, bool Exists)> ReadReplayAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid operationId, string fingerprint, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "select payload_fingerprint,result_projection::text from logistics_operations where operation_id=@operation_id";
        command.Parameters.AddWithValue("operation_id",operationId); await using var reader=await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return (null,false);
        if (!string.Equals(reader.GetString(0),fingerprint,StringComparison.Ordinal)) return (LogisticsMutationResult<LogisticsMutationResponse>.Conflict("같은 operationId에 다른 요청 내용이 사용되었습니다."),true);
        var value=JsonSerializer.Deserialize<LogisticsMutationResponse>(reader.GetString(1),JsonOptions);
        return value is null ? (LogisticsMutationResult<LogisticsMutationResponse>.Conflict("이전 작업 결과를 복구할 수 없습니다."),true)
            : (LogisticsMutationResult<LogisticsMutationResponse>.Success(value with { Replayed=true }),true);
    }

    private static async Task InsertReceiptAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid operationId,string action,Guid projectId,Guid? unitId,Guid? batchId,Guid actorId,string fingerprint,LogisticsMutationResponse response,CancellationToken cancellationToken)
    {
        await using var command=connection.CreateCommand(); command.Transaction=transaction;
        command.CommandText="insert into logistics_operations(operation_id,action,project_id,packing_unit_id,batch_id,actor_user_id,payload_fingerprint,result_projection) values (@operation_id,@action,@project_id,@unit_id,@batch_id,@actor_id,@fingerprint,@projection::jsonb)";
        command.Parameters.AddWithValue("operation_id",operationId); command.Parameters.AddWithValue("action",action); command.Parameters.AddWithValue("project_id",projectId);
        AddNullableUuid(command,"unit_id",unitId); AddNullableUuid(command,"batch_id",batchId); command.Parameters.AddWithValue("actor_id",actorId); command.Parameters.AddWithValue("fingerprint",fingerprint);
        command.Parameters.AddWithValue("projection",JsonSerializer.Serialize(response,JsonOptions)); await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static (string Mime,string Extension)? Sniff(string stage,byte[] content)
    {
        var max=stage==LogisticsStages.Delivery?MaxDocumentBytes:MaxPhotoBytes;
        if(content.Length is < 1 || content.Length>max)return null;
        if(content.Length>=3&&content[0]==0xff&&content[1]==0xd8&&content[2]==0xff)return("image/jpeg","jpg");
        if(content.Length>=8&&content.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))return("image/png","png");
        if(stage==LogisticsStages.Delivery&&content.Length>=4&&content.AsSpan(0,4).SequenceEqual("%PDF"u8))return("application/pdf","pdf");
        return null;
    }

    private static string Fingerprint(params object?[] values)
    {
        var text=string.Join("|",values.Select(value=>value switch{null=>"null",IEnumerable<Guid> ids=>string.Join(",",ids.Order()),_=>value.ToString()?.Trim()??""}));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }
    private static List<Guid> NormalizeIds(IReadOnlyList<Guid>? ids)=>ids?.Where(id=>id!=Guid.Empty).Distinct().Order().ToList()??[];
    private static string? NormalizeOptional(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
    private static bool ValidOptional(string? value,int max)=>string.IsNullOrWhiteSpace(value)||value.Trim().Length<=max;
    private static bool ValidAlt(string? value)=>!string.IsNullOrWhiteSpace(value)&&value.Trim().Length is >=2 and <=160;
    private static void AddScope(NpgsqlCommand command,ProjectAccessScope scope){command.Parameters.AddWithValue("has_read_all",scope.HasProjectReadAll);command.Parameters.AddWithValue("project_keys",scope.ProjectKeys.ToArray());}
    private static void AddNullableText(NpgsqlCommand command,string name,string? value)=>command.Parameters.Add(name,NpgsqlDbType.Text).Value=value??(object)DBNull.Value;
    private static void AddNullableUuid(NpgsqlCommand command,string name,Guid? value)=>command.Parameters.Add(name,NpgsqlDbType.Uuid).Value=value??(object)DBNull.Value;
    private static void AddNullableDate(NpgsqlCommand command,string name,DateOnly? value)=>command.Parameters.Add(name,NpgsqlDbType.Date).Value=value??(object)DBNull.Value;
    private NpgsqlDataSource CreateDataSource(){var value=connectionStringProvider.GetConnectionString();if(string.IsNullOrWhiteSpace(value))throw new InvalidOperationException("QMS database connection string is not configured.");return NpgsqlDataSource.Create(value);}
    private static async Task<LogisticsMutationResult<LogisticsMutationResponse>> RollbackNotFound(NpgsqlTransaction transaction,CancellationToken token){await transaction.RollbackAsync(token);return LogisticsMutationResult<LogisticsMutationResponse>.NotFound();}
    private static async Task<LogisticsMutationResult<LogisticsMutationResponse>> RollbackForbidden(NpgsqlTransaction transaction,CancellationToken token){await transaction.RollbackAsync(token);return LogisticsMutationResult<LogisticsMutationResponse>.Forbidden();}
    private static async Task<LogisticsMutationResult<LogisticsMutationResponse>> RollbackConflict(NpgsqlTransaction transaction,string message,CancellationToken token){await transaction.RollbackAsync(token);return LogisticsMutationResult<LogisticsMutationResponse>.Conflict(message);}
    private static async Task RollbackQuietly(NpgsqlTransaction transaction,CancellationToken token){try{await transaction.RollbackAsync(token);}catch{}}
    private sealed record Owner(Guid Id,Guid ProjectId,string Stage,string Status,int Version,DateOnly? DepartureDate);
    private sealed record BatchSelection(
        IReadOnlyList<Guid> UnitIds,
        IReadOnlyList<Guid> PanelIds,
        IReadOnlyList<string> PanelCodes);
    private readonly record struct Assignee(Guid UserId,string? RoleCode);
    private sealed class QueueProjectBuilder(Guid id,string code,string title){public List<LogisticsQueueItem> Items{get;}=[];public LogisticsProjectQueue Build()=>new(id,code,title,Items);}
}
