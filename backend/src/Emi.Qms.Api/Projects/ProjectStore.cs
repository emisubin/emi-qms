using System.Data;
using Emi.Qms.Api.PanelInformation;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Projects;

public enum ProjectMutationStatus
{
    Success,
    NotFound,
    Conflict,
    ValidationFailed
}

public sealed record ProjectMutationResult<T>(
    ProjectMutationStatus Status,
    T? Value = default,
    string? Message = null,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static ProjectMutationResult<T> Success(T value)
    {
        return new ProjectMutationResult<T>(ProjectMutationStatus.Success, value);
    }

    public static ProjectMutationResult<T> NotFound()
    {
        return new ProjectMutationResult<T>(ProjectMutationStatus.NotFound);
    }

    public static ProjectMutationResult<T> Conflict(string message)
    {
        return new ProjectMutationResult<T>(ProjectMutationStatus.Conflict, Message: message);
    }

    public static ProjectMutationResult<T> Validation(IReadOnlyDictionary<string, string[]> errors)
    {
        return new ProjectMutationResult<T>(ProjectMutationStatus.ValidationFailed, Errors: errors);
    }
}

public sealed class ProjectStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    IEnumerable<IProjectDeletionGuard> deletionGuards,
    ProjectExcelParser projectExcelParser)
{
    public async Task<IReadOnlyList<SalesOwnerResponse>> GetSalesOwnersAsync(CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select qms_users.id, qms_users.display_name
            from qms_users
            join user_roles on user_roles.user_id = qms_users.id
            join roles on roles.id = user_roles.role_id
            where qms_users.is_active = true
              and roles.code = 'sales'
            order by qms_users.display_name;
            """);

        var owners = new List<SalesOwnerResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            owners.Add(new SalesOwnerResponse(reader.GetGuid(0), reader.GetString(1)));
        }

        return owners;
    }

    public async Task<ProjectListResponse> ListProjectsAsync(
        ProjectListQuery query,
        ProjectAccessScope accessScope,
        bool includeSalesAmount,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        var where = new List<string>();
        var parameters = new List<NpgsqlParameter>();
        where.Add("projects.deleted_at_utc is null");

        if (!string.IsNullOrWhiteSpace(query.Status) && !string.Equals(query.Status, "All", StringComparison.OrdinalIgnoreCase))
        {
            where.Add("projects.status = @status");
            parameters.Add(new NpgsqlParameter("status", query.Status));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            where.Add("""
                (
                    projects.customer_name ilike @search
                    or projects.item ilike @search
                    or projects.project_code ilike @search
                    or projects.project_title ilike @search
                )
                """);
            parameters.Add(new NpgsqlParameter("search", $"%{query.Search.Trim()}%"));
        }

        if (query.SalesOwnerUserId is not null)
        {
            where.Add("projects.sales_owner_user_id = @sales_owner_user_id");
            parameters.Add(new NpgsqlParameter("sales_owner_user_id", query.SalesOwnerUserId.Value));
        }

        if (query.DeliveryDateFrom is not null)
        {
            where.Add("projects.delivery_date >= @delivery_date_from");
            parameters.Add(new NpgsqlParameter("delivery_date_from", query.DeliveryDateFrom.Value));
        }

        if (query.DeliveryDateTo is not null)
        {
            where.Add("projects.delivery_date <= @delivery_date_to");
            parameters.Add(new NpgsqlParameter("delivery_date_to", query.DeliveryDateTo.Value));
        }

        AddAccessScope(where, parameters, accessScope);

        var whereSql = where.Count == 0 ? "" : $"where {string.Join(" and ", where)}";
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        await using var command = dataSource.CreateCommand($"""
            select
                projects.id,
                coalesce(projects.customer_name, ''),
                coalesce(projects.item, ''),
                coalesce(projects.project_code, projects.project_number),
                coalesce(projects.project_title, projects.name),
                coalesce(active_panels.active_panel_count, 0),
                coalesce(projects.delivery_date, current_date),
                projects.sales_owner_user_id,
                coalesce(qms_users.display_name, ''),
                projects.packaging_method,
                projects.delivery_location,
                projects.status,
                projects.created_at_utc,
                projects.updated_at_utc,
                projects.sales_amount,
                projects.currency_code,
                project_workflow.project_work_status,
                project_workflow.project_progress_percent,
                count(*) over() as total_count
            from projects
            left join qms_users on qms_users.id = projects.sales_owner_user_id
            left join lateral (
                select count(*)::integer as active_panel_count,
                       coalesce(sum(case when workflow_stage in ('ManufacturingInProgress', 'ManufacturingCompleted', 'InspectionInProgress', 'InspectionCompleted', 'PackingCompleted', 'ShipmentCompleted') then 1 else 0 end), 0)::integer as manufacturing_started_count,
                       coalesce(sum(case when workflow_stage in ('ManufacturingCompleted', 'InspectionInProgress', 'InspectionCompleted', 'PackingCompleted', 'ShipmentCompleted') then 1 else 0 end), 0)::integer as manufacturing_completed_count,
                       coalesce(sum(case when workflow_stage in ('InspectionInProgress', 'InspectionCompleted', 'PackingCompleted', 'ShipmentCompleted') then 1 else 0 end), 0)::integer as inspection_started_count,
                       coalesce(sum(case when workflow_stage in ('InspectionCompleted', 'PackingCompleted', 'ShipmentCompleted') then 1 else 0 end), 0)::integer as inspection_completed_count,
                       coalesce(sum(case when workflow_stage = 'PackingCompleted' then 1 else 0 end), 0)::integer as packing_count,
                       coalesce(sum(case when workflow_stage = 'ShipmentCompleted' then 1 else 0 end), 0)::integer as shipment_count,
                       coalesce(sum(case workflow_stage
                           when 'BeforeManufacturing' then 0
                           when 'ManufacturingInProgress' then 20
                           when 'ManufacturingCompleted' then 40
                           when 'InspectionInProgress' then 60
                           when 'InspectionCompleted' then 75
                           when 'PackingCompleted' then 90
                           when 'ShipmentCompleted' then 100
                           else 0
                       end), 0)::integer as progress_score_sum
                from panel_placeholders
                where panel_placeholders.project_id = projects.id
                  and panel_placeholders.status = 'Active'
            ) active_panels on true
            left join lateral (
                select case
                           when projects.status in ('OnHold', 'Cancelled', 'Completed') then projects.status
                           when active_panels.active_panel_count > 0 and active_panels.shipment_count = active_panels.active_panel_count then 'ShipmentCompleted'
                           when active_panels.packing_count > 0 then 'ReadyForShipment'
                           when active_panels.active_panel_count > 0 and active_panels.inspection_completed_count = active_panels.active_panel_count and active_panels.packing_count = 0 and active_panels.shipment_count = 0 then 'InspectionCompleted'
                           when active_panels.inspection_started_count > 0 and active_panels.inspection_completed_count < active_panels.active_panel_count then 'InspectionInProgress'
                           when active_panels.active_panel_count > 0 and active_panels.manufacturing_completed_count = active_panels.active_panel_count and active_panels.inspection_started_count = 0 then 'ManufacturingCompleted'
                           when active_panels.manufacturing_started_count > 0 and active_panels.manufacturing_completed_count < active_panels.active_panel_count then 'ManufacturingInProgress'
                           else 'BeforeManufacturing'
                       end as project_work_status,
                       case
                           when active_panels.active_panel_count = 0 then null
                           else round(active_panels.progress_score_sum::numeric / active_panels.active_panel_count)::integer
                       end as project_progress_percent
            ) project_workflow on true
            {whereSql}
            order by
                case projects.status
                    when 'Active' then 0
                    when 'OnHold' then 1
                    when 'Completed' then 2
                    when 'Cancelled' then 3
                    else 4
                end,
                projects.delivery_date asc nulls last,
                projects.created_at_utc desc
            limit @limit offset @offset;
            """);

        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        command.Parameters.AddWithValue("limit", pageSize);
        command.Parameters.AddWithValue("offset", offset);

        var items = new List<ProjectListItemResponse>();
        long totalCount = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            totalCount = reader.GetInt64(18);
            items.Add(ReadProjectListItem(reader, includeSalesAmount));
        }

        return new ProjectListResponse(items, page, pageSize, totalCount);
    }

    public async Task<ProjectDashboardSummaryResponse> GetProjectDashboardSummaryAsync(
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        var where = new List<string> { "projects.deleted_at_utc is null" };
        var parameters = new List<NpgsqlParameter>();
        AddAccessScope(where, parameters, accessScope);
        var whereSql = $"where {string.Join(" and ", where)}";

        await using var command = dataSource.CreateCommand($"""
            with visible_projects as (
                select projects.id, projects.status
                from projects
                {whereSql}
            ),
            project_panel_summary as (
                select vp.id,
                       vp.status,
                       count(pp.id) filter (where pp.status = 'Active')::int as active_panel_count,
                       coalesce(sum(case when pp.status = 'Active' and pp.workflow_stage in ('ManufacturingCompleted', 'InspectionInProgress', 'InspectionCompleted', 'PackingCompleted', 'ShipmentCompleted') then 1 else 0 end), 0)::int as manufacturing_completed_count,
                       coalesce(sum(case when pp.status = 'Active' and pp.workflow_stage in ('InspectionCompleted', 'PackingCompleted', 'ShipmentCompleted') then 1 else 0 end), 0)::int as inspection_completed_count
                from visible_projects vp
                left join panel_placeholders pp on pp.project_id = vp.id
                group by vp.id, vp.status
            ),
            active_panels as (
                select vp.id as project_id,
                       vp.status as project_status,
                       pp.panel_name,
                       pp.workflow_stage
                from visible_projects vp
                join panel_placeholders pp on pp.project_id = vp.id
                where pp.status = 'Active'
            )
            select
                (select count(*)::int from visible_projects where status <> 'Completed'),
                (select count(*)::int from visible_projects where status = 'Active'),
                (select count(*)::int from visible_projects where status = 'OnHold'),
                (select count(*)::int from visible_projects where status = 'Completed'),
                (select count(*)::int from visible_projects where status = 'Cancelled'),
                (select count(*)::int
                 from active_panels
                 where project_status = 'Active'
                   and panel_name is not null
                   and btrim(panel_name) <> ''),
                (select count(*)::int
                 from active_panels
                 where workflow_stage in ('ManufacturingCompleted', 'InspectionInProgress', 'InspectionCompleted', 'PackingCompleted', 'ShipmentCompleted')),
                (select count(*)::int
                 from active_panels
                 where workflow_stage in ('InspectionCompleted', 'PackingCompleted', 'ShipmentCompleted')),
                (select count(*)::int
                 from project_panel_summary
                 where status = 'Active'
                   and active_panel_count > 0
                   and manufacturing_completed_count = active_panel_count),
                (select count(*)::int
                 from project_panel_summary
                 where status = 'Active'
                   and active_panel_count > 0
                   and inspection_completed_count = active_panel_count);
            """);

        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new ProjectDashboardSummaryResponse(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        return new ProjectDashboardSummaryResponse(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5),
            reader.GetInt32(6),
            reader.GetInt32(7),
            reader.GetInt32(8),
            reader.GetInt32(9));
    }

    public async Task<ProjectDetailResponse?> GetProjectAsync(
        Guid projectId,
        bool includeSalesAmount,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select
                projects.id,
                coalesce(projects.customer_name, ''),
                coalesce(projects.item, ''),
                coalesce(projects.project_code, projects.project_number),
                coalesce(projects.project_title, projects.name),
                coalesce(active_panels.active_panel_count, 0),
                coalesce(projects.delivery_date, current_date),
                projects.sales_owner_user_id,
                coalesce(qms_users.display_name, ''),
                projects.packaging_method,
                projects.delivery_location,
                projects.status,
                projects.created_at_utc,
                projects.updated_at_utc,
                projects.sales_amount,
                projects.currency_code,
                project_workflow.project_work_status,
                project_workflow.project_progress_percent,
                projects.status_reason
            from projects
            left join qms_users on qms_users.id = projects.sales_owner_user_id
            left join lateral (
                select count(*)::integer as active_panel_count,
                       coalesce(sum(case when workflow_stage in ('ManufacturingInProgress', 'ManufacturingCompleted', 'InspectionInProgress', 'InspectionCompleted', 'PackingCompleted', 'ShipmentCompleted') then 1 else 0 end), 0)::integer as manufacturing_started_count,
                       coalesce(sum(case when workflow_stage in ('ManufacturingCompleted', 'InspectionInProgress', 'InspectionCompleted', 'PackingCompleted', 'ShipmentCompleted') then 1 else 0 end), 0)::integer as manufacturing_completed_count,
                       coalesce(sum(case when workflow_stage in ('InspectionInProgress', 'InspectionCompleted', 'PackingCompleted', 'ShipmentCompleted') then 1 else 0 end), 0)::integer as inspection_started_count,
                       coalesce(sum(case when workflow_stage in ('InspectionCompleted', 'PackingCompleted', 'ShipmentCompleted') then 1 else 0 end), 0)::integer as inspection_completed_count,
                       coalesce(sum(case when workflow_stage = 'PackingCompleted' then 1 else 0 end), 0)::integer as packing_count,
                       coalesce(sum(case when workflow_stage = 'ShipmentCompleted' then 1 else 0 end), 0)::integer as shipment_count,
                       coalesce(sum(case workflow_stage
                           when 'BeforeManufacturing' then 0
                           when 'ManufacturingInProgress' then 20
                           when 'ManufacturingCompleted' then 40
                           when 'InspectionInProgress' then 60
                           when 'InspectionCompleted' then 75
                           when 'PackingCompleted' then 90
                           when 'ShipmentCompleted' then 100
                           else 0
                       end), 0)::integer as progress_score_sum
                from panel_placeholders
                where panel_placeholders.project_id = projects.id
                  and panel_placeholders.status = 'Active'
            ) active_panels on true
            left join lateral (
                select case
                           when projects.status in ('OnHold', 'Cancelled', 'Completed') then projects.status
                           when active_panels.active_panel_count > 0 and active_panels.shipment_count = active_panels.active_panel_count then 'ShipmentCompleted'
                           when active_panels.packing_count > 0 then 'ReadyForShipment'
                           when active_panels.active_panel_count > 0 and active_panels.inspection_completed_count = active_panels.active_panel_count and active_panels.packing_count = 0 and active_panels.shipment_count = 0 then 'InspectionCompleted'
                           when active_panels.inspection_started_count > 0 and active_panels.inspection_completed_count < active_panels.active_panel_count then 'InspectionInProgress'
                           when active_panels.active_panel_count > 0 and active_panels.manufacturing_completed_count = active_panels.active_panel_count and active_panels.inspection_started_count = 0 then 'ManufacturingCompleted'
                           when active_panels.manufacturing_started_count > 0 and active_panels.manufacturing_completed_count < active_panels.active_panel_count then 'ManufacturingInProgress'
                           else 'BeforeManufacturing'
                       end as project_work_status,
                       case
                           when active_panels.active_panel_count = 0 then null
                           else round(active_panels.progress_score_sum::numeric / active_panels.active_panel_count)::integer
                       end as project_progress_percent
            ) project_workflow on true
            where projects.id = @project_id
              and projects.deleted_at_utc is null;
            """);
        command.Parameters.AddWithValue("project_id", projectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var baseItem = ReadProjectListItem(reader, includeSalesAmount);
        var statusReason = reader.IsDBNull(18) ? null : reader.GetString(18);
        await reader.DisposeAsync();
        var panelInfoSummary = await ReadPanelInformationSummaryAsync(dataSource, projectId, cancellationToken);
        return new ProjectDetailResponse
        {
            ProjectId = baseItem.ProjectId,
            CustomerName = baseItem.CustomerName,
            Item = baseItem.Item,
            ProjectCode = baseItem.ProjectCode,
            ProjectTitle = baseItem.ProjectTitle,
            ActivePanelCount = baseItem.ActivePanelCount,
            DeliveryDate = baseItem.DeliveryDate,
            SalesOwnerUserId = baseItem.SalesOwnerUserId,
            SalesOwnerName = baseItem.SalesOwnerName,
            PackagingMethod = baseItem.PackagingMethod,
            DeliveryLocation = baseItem.DeliveryLocation,
            Status = baseItem.Status,
            ProjectWorkStatus = baseItem.ProjectWorkStatus,
            ProjectProgressPercent = baseItem.ProjectProgressPercent,
            CreatedAt = baseItem.CreatedAt,
            UpdatedAt = baseItem.UpdatedAt,
            SalesAmount = baseItem.SalesAmount,
            CurrencyCode = baseItem.CurrencyCode,
            StatusReason = statusReason,
            PanelInfoCompletedCount = panelInfoSummary.CompletedCount,
            PanelInfoPendingCount = panelInfoSummary.PendingCount,
            QrEligibleCount = panelInfoSummary.QrEligibleCount,
            ManufacturingCompletedCount = panelInfoSummary.ManufacturingCompletedCount,
            InspectionCompletedCount = panelInfoSummary.InspectionCompletedCount,
            DuplicatePanelNameGroupCount = panelInfoSummary.DuplicatePanelNameGroupCount,
            ProjectPanelInformationCompleted = panelInfoSummary.ProjectPanelInformationCompleted
        };
    }

    public async Task<ProjectAccessRecord?> GetProjectAccessRecordAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select id, project_key
            from projects
            where id = @project_id
              and deleted_at_utc is null;
            """);
        command.Parameters.AddWithValue("project_id", projectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ProjectAccessRecord(reader.GetGuid(0), reader.GetString(1))
            : null;
    }

    public async Task<ProjectMutationResult<ProjectDetailResponse>> CreateProjectAsync(
        NormalizedCreateProjectInput input,
        Guid changedByUserId,
        string correlationId,
        bool includeSalesAmount,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        if (!await IsActiveSalesUserAsync(connection, null, input.SalesOwnerUserId, cancellationToken))
        {
            return ProjectMutationResult<ProjectDetailResponse>.Validation(
                new Dictionary<string, string[]> { [nameof(CreateProjectRequest.SalesOwnerUserId)] = ["활성 Sales 사용자만 영업담당자로 선택할 수 있습니다."] });
        }

        if (await ProjectTitleExistsAsync(connection, null, input.ProjectTitleNormalized, null, cancellationToken))
        {
            return ProjectMutationResult<ProjectDetailResponse>.Conflict("동일한 PJT Title이 이미 존재합니다.");
        }

        var projectId = Guid.NewGuid();
        var projectKey = projectId.ToString("N");

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    insert into projects (
                        id,
                        project_key,
                        project_number,
                        name,
                        customer_name,
                        item,
                        project_code,
                        project_title,
                        project_title_normalized,
                        packaging_method,
                        delivery_date,
                        sales_owner_user_id,
                        sales_amount,
                        currency_code,
                        delivery_location,
                        status,
                        created_by_user_id,
                        updated_at_utc
                    )
                    values (
                        @id,
                        @project_key,
                        @project_code,
                        @project_title,
                        @customer_name,
                        @item,
                        @project_code,
                        @project_title,
                        @project_title_normalized,
                        @packaging_method,
                        @delivery_date,
                        @sales_owner_user_id,
                        @sales_amount,
                        @currency_code,
                        @delivery_location,
                        'Active',
                        @created_by_user_id,
                        now()
                    );
                    """;
                AddProjectParameters(command, projectId, projectKey, input, changedByUserId);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await InsertAuditEventAsync(
                connection,
                transaction,
                projectId,
                "Project",
                projectId,
                "ProjectCreated",
                null,
                null,
                input.ProjectTitle,
                null,
                changedByUserId,
                correlationId,
                false,
                cancellationToken);

            for (var sequence = 1; sequence <= input.PanelCount; sequence++)
            {
                var panelId = Guid.NewGuid();
                var displayCode = ProjectInputNormalizer.FormatPanelDisplayCode(sequence);
                await InsertPanelAsync(connection, transaction, panelId, projectId, sequence, displayCode, cancellationToken);
                await InsertAuditEventAsync(
                    connection,
                    transaction,
                    projectId,
                    "PanelPlaceholder",
                    panelId,
                    "PanelCreated",
                    "DisplayCode",
                    null,
                    displayCode,
                    null,
                    changedByUserId,
                    correlationId,
                    false,
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ProjectMutationResult<ProjectDetailResponse>.Conflict("동일한 PJT Title이 이미 존재합니다.");
        }

        var detail = await GetProjectAsync(projectId, includeSalesAmount, cancellationToken);
        if (detail is null)
        {
            return ProjectMutationResult<ProjectDetailResponse>.NotFound();
        }

        return ProjectMutationResult<ProjectDetailResponse>.Success(detail!);
    }

    public async Task<ProjectMutationResult<ProjectDetailResponse>> UpdateProjectAsync(
        Guid projectId,
        NormalizedUpdateProjectInput input,
        Guid changedByUserId,
        string correlationId,
        bool includeSalesAmount,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var existing = await ReadProjectEditSnapshotAsync(connection, transaction, projectId, true, cancellationToken);
            if (existing is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ProjectMutationResult<ProjectDetailResponse>.NotFound();
            }

            if (!await IsActiveSalesUserAsync(connection, transaction, input.SalesOwnerUserId, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return ProjectMutationResult<ProjectDetailResponse>.Validation(
                    new Dictionary<string, string[]> { [nameof(UpdateProjectRequest.SalesOwnerUserId)] = ["활성 Sales 사용자만 영업담당자로 선택할 수 있습니다."] });
            }

            if (!string.Equals(existing.ProjectTitleNormalized, input.ProjectTitleNormalized, StringComparison.Ordinal)
                && await ProjectTitleExistsAsync(connection, transaction, input.ProjectTitleNormalized, projectId, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return ProjectMutationResult<ProjectDetailResponse>.Conflict("동일한 PJT Title이 이미 존재합니다.");
            }

            var changes = existing.CollectChanges(input);

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    update projects
                    set project_number = @project_code,
                        name = @project_title,
                        customer_name = @customer_name,
                        item = @item,
                        project_code = @project_code,
                        project_title = @project_title,
                        project_title_normalized = @project_title_normalized,
                        packaging_method = @packaging_method,
                        delivery_date = @delivery_date,
                        sales_owner_user_id = @sales_owner_user_id,
                        sales_amount = @sales_amount,
                        currency_code = @currency_code,
                        delivery_location = @delivery_location,
                        updated_at_utc = now()
                    where id = @project_id;
                    """;
                command.Parameters.AddWithValue("project_id", projectId);
                AddProjectParameters(command, projectId, existing.ProjectKey, input, changedByUserId);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await RecalculatePanelDerivedStateAsync(connection, transaction, projectId, cancellationToken);

            foreach (var change in changes)
            {
                await InsertAuditEventAsync(
                    connection,
                    transaction,
                    projectId,
                    "Project",
                    projectId,
                    "ProjectFieldUpdated",
                    change.FieldName,
                    change.OldValue,
                    change.NewValue,
                    input.Reason,
                    changedByUserId,
                    correlationId,
                    change.IsSensitive,
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ProjectMutationResult<ProjectDetailResponse>.Conflict("동일한 PJT Title이 이미 존재합니다.");
        }

        var detail = await GetProjectAsync(projectId, includeSalesAmount, cancellationToken);
        if (detail is null)
        {
            return ProjectMutationResult<ProjectDetailResponse>.NotFound();
        }

        return ProjectMutationResult<ProjectDetailResponse>.Success(detail!);
    }

    public async Task<ProjectMutationResult<ProjectDetailResponse>> ChangePanelCountAsync(
        Guid projectId,
        NormalizedPanelCountChangeInput input,
        Guid changedByUserId,
        string correlationId,
        bool includeSalesAmount,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            var snapshot = await LockProjectForUpdateAsync(connection, transaction, projectId, cancellationToken);
            if (snapshot is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ProjectMutationResult<ProjectDetailResponse>.NotFound();
            }

            if (snapshot.ActivePanelCount != input.ExpectedActivePanelCount)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ProjectMutationResult<ProjectDetailResponse>.Conflict(ProjectDomainRules.PanelCountConcurrencyMessage);
            }

            if (snapshot.Status == "Cancelled")
            {
                await transaction.RollbackAsync(cancellationToken);
                return ProjectMutationResult<ProjectDetailResponse>.Conflict("취소된 프로젝트의 면수는 변경할 수 없습니다.");
            }

            if (snapshot.ActivePanelCount == input.PanelCount)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ProjectMutationResult<ProjectDetailResponse>.Conflict("이미 요청한 면수와 동일합니다.");
            }

            if (input.PanelCount > snapshot.ActivePanelCount)
            {
                var delta = input.PanelCount - snapshot.ActivePanelCount;
                for (var offset = 1; offset <= delta; offset++)
                {
                    var sequence = snapshot.MaxSequenceNumber + offset;
                    var panelId = Guid.NewGuid();
                    var displayCode = ProjectInputNormalizer.FormatPanelDisplayCode(sequence);
                    await InsertPanelAsync(connection, transaction, panelId, projectId, sequence, displayCode, cancellationToken);
                    await InsertAuditEventAsync(
                        connection,
                        transaction,
                        projectId,
                        "PanelPlaceholder",
                        panelId,
                        "PanelCreated",
                        "DisplayCode",
                        null,
                        displayCode,
                        input.Reason,
                        changedByUserId,
                        correlationId,
                        false,
                        cancellationToken);
                }
            }
            else
            {
                var delta = snapshot.ActivePanelCount - input.PanelCount;
                if (input.CancelPanelIds.Count != delta)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ProjectMutationResult<ProjectDetailResponse>.Validation(
                        new Dictionary<string, string[]> { [nameof(ChangePanelCountRequest.CancelPanelIds)] = ["감소 면수와 동일한 수의 활성 패널을 선택해야 합니다."] });
                }

                var selectedPanels = await ReadSelectedActivePanelsAsync(
                    connection,
                    transaction,
                    projectId,
                    input.CancelPanelIds,
                    cancellationToken);

                if (selectedPanels.Count != input.CancelPanelIds.Count)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ProjectMutationResult<ProjectDetailResponse>.Validation(
                        new Dictionary<string, string[]> { [nameof(ChangePanelCountRequest.CancelPanelIds)] = ["선택한 패널은 해당 프로젝트의 활성 패널이어야 합니다."] });
                }

                foreach (var panel in selectedPanels)
                {
                    await using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = """
                        update panel_placeholders
                        set status = 'Cancelled',
                            updated_at_utc = now(),
                            cancelled_by_user_id = @changed_by_user_id,
                            cancelled_at_utc = now(),
                            cancellation_reason = @reason
                        where id = @panel_id;
                        """;
                    command.Parameters.AddWithValue("changed_by_user_id", changedByUserId);
                    command.Parameters.AddWithValue("reason", input.Reason);
                    command.Parameters.AddWithValue("panel_id", panel.PanelId);
                    await command.ExecuteNonQueryAsync(cancellationToken);

                    await InsertAuditEventAsync(
                        connection,
                        transaction,
                        projectId,
                        "PanelPlaceholder",
                        panel.PanelId,
                        "PanelCancelled",
                        "PanelStatus",
                        "Active",
                        "Cancelled",
                        input.Reason,
                        changedByUserId,
                        correlationId,
                        false,
                        cancellationToken);
                }
            }

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "update projects set updated_at_utc = now() where id = @project_id;";
                command.Parameters.AddWithValue("project_id", projectId);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await RecalculatePanelDerivedStateAsync(connection, transaction, projectId, cancellationToken);

            await InsertAuditEventAsync(
                connection,
                transaction,
                projectId,
                "Project",
                projectId,
                input.PanelCount > snapshot.ActivePanelCount ? "PanelCountIncreased" : "PanelCountDecreased",
                "PanelCount",
                ProjectInputNormalizer.FormatAuditValue(snapshot.ActivePanelCount),
                ProjectInputNormalizer.FormatAuditValue(input.PanelCount),
                input.Reason,
                changedByUserId,
                correlationId,
                false,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            return ProjectMutationResult<ProjectDetailResponse>.Conflict(ProjectDomainRules.PanelCountConcurrencyMessage);
        }

        var detail = await GetProjectAsync(projectId, includeSalesAmount, cancellationToken);
        if (detail is null)
        {
            return ProjectMutationResult<ProjectDetailResponse>.NotFound();
        }

        return ProjectMutationResult<ProjectDetailResponse>.Success(detail!);
    }

    public async Task<ProjectMutationResult<ProjectDetailResponse>> ChangeStatusAsync(
        Guid projectId,
        string action,
        string targetStatus,
        IReadOnlySet<string> allowedSourceStatuses,
        string reason,
        Guid changedByUserId,
        string correlationId,
        bool includeSalesAmount,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var existing = await LockProjectForUpdateAsync(connection, transaction, projectId, cancellationToken);
        if (existing is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ProjectMutationResult<ProjectDetailResponse>.NotFound();
        }

        if (!allowedSourceStatuses.Contains(existing.Status))
        {
            await transaction.RollbackAsync(cancellationToken);
            return ProjectMutationResult<ProjectDetailResponse>.Conflict("현재 상태에서는 요청한 상태 전이를 수행할 수 없습니다.");
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = targetStatus switch
            {
                "OnHold" => """
                    update projects
                    set status = @target_status,
                        status_reason = @reason,
                        held_by_user_id = @changed_by_user_id,
                        held_at_utc = now(),
                        updated_at_utc = now()
                    where id = @project_id;
                    """,
                "Cancelled" => """
                    update projects
                    set status = @target_status,
                        status_reason = @reason,
                        cancelled_by_user_id = @changed_by_user_id,
                        cancelled_at_utc = now(),
                        updated_at_utc = now()
                    where id = @project_id;
                    """,
                _ => """
                    update projects
                    set status = @target_status,
                        status_reason = @reason,
                        updated_at_utc = now()
                    where id = @project_id;
                    """
            };
            command.Parameters.AddWithValue("target_status", targetStatus);
            command.Parameters.AddWithValue("reason", reason);
            command.Parameters.AddWithValue("changed_by_user_id", changedByUserId);
            command.Parameters.AddWithValue("project_id", projectId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await RecalculatePanelDerivedStateAsync(connection, transaction, projectId, cancellationToken);

        await InsertAuditEventAsync(
            connection,
            transaction,
            projectId,
            "Project",
            projectId,
            action,
            "Status",
            existing.Status,
            targetStatus,
            reason,
            changedByUserId,
            correlationId,
            false,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var detail = await GetProjectAsync(projectId, includeSalesAmount, cancellationToken);
        if (detail is null)
        {
            return ProjectMutationResult<ProjectDetailResponse>.NotFound();
        }

        return ProjectMutationResult<ProjectDetailResponse>.Success(detail!);
    }

    public async Task<ProjectMutationResult<DeletedProjectDetailResponse>> DeleteProjectAsync(
        Guid projectId,
        NormalizedDeleteProjectInput input,
        Guid changedByUserId,
        string correlationId,
        bool includeSalesAmount,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var snapshot = await LockProjectDeletionSnapshotAsync(connection, transaction, projectId, cancellationToken);
        if (snapshot is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ProjectMutationResult<DeletedProjectDetailResponse>.NotFound();
        }

        if (snapshot.DeletedAtUtc is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ProjectMutationResult<DeletedProjectDetailResponse>.Conflict("이미 삭제된 프로젝트입니다.");
        }

        if (snapshot.Status == "Completed")
        {
            await transaction.RollbackAsync(cancellationToken);
            return ProjectMutationResult<DeletedProjectDetailResponse>.Conflict("완료 프로젝트는 삭제할 수 없습니다.");
        }

        if (!string.Equals(snapshot.ProjectTitleNormalized, input.ConfirmProjectTitleNormalized, StringComparison.Ordinal))
        {
            await transaction.RollbackAsync(cancellationToken);
            return ProjectMutationResult<DeletedProjectDetailResponse>.Validation(
                new Dictionary<string, string[]> { [nameof(DeleteProjectRequest.ConfirmProjectTitle)] = ["확인 입력이 현재 PJT Title과 일치하지 않습니다."] });
        }

        var deletionContext = new ProjectDeletionContext(
            projectId,
            snapshot.Status,
            snapshot.ProjectTitle,
            snapshot.ProjectTitleNormalized,
            snapshot.PackagingMethod,
            connection,
            transaction,
            correlationId);
        try
        {
            foreach (var deletionGuard in deletionGuards)
            {
                var guardResult = await deletionGuard.CanDeleteAsync(deletionContext, cancellationToken);
                if (!guardResult.IsAllowed)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ProjectMutationResult<DeletedProjectDetailResponse>.Conflict(guardResult.Message ?? ProjectDeletionGuardResult.Blocked().Message!);
                }
            }
        }
        catch
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            return ProjectMutationResult<DeletedProjectDetailResponse>.Conflict("삭제 가능 여부를 확인할 수 없습니다. 잠시 후 다시 시도해 주세요.");
        }

        DateTimeOffset deletedAt;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update projects
                set deleted_at_utc = now(),
                    deleted_by_user_id = @changed_by_user_id,
                    delete_reason = @reason,
                    deleted_correlation_id = @correlation_id,
                    updated_at_utc = now()
                where id = @project_id
                returning deleted_at_utc;
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("changed_by_user_id", changedByUserId);
            command.Parameters.AddWithValue("reason", input.Reason);
            command.Parameters.AddWithValue("correlation_id", correlationId);
            deletedAt = ToDateTimeOffset(await command.ExecuteScalarAsync(cancellationToken));
        }

        await RecalculatePanelDerivedStateAsync(connection, transaction, projectId, cancellationToken);

        await InsertAuditEventAsync(
            connection,
            transaction,
            projectId,
            "Project",
            projectId,
            "ProjectDeleted",
            "DeletedAtUtc",
            null,
            ProjectInputNormalizer.FormatAuditValue(deletedAt),
            input.Reason,
            changedByUserId,
            correlationId,
            false,
            cancellationToken);
        await InsertAuditEventAsync(
            connection,
            transaction,
            projectId,
            "Project",
            projectId,
            "ProjectDeletedSnapshot",
            "Status",
            null,
            snapshot.Status,
            input.Reason,
            changedByUserId,
            correlationId,
            false,
            cancellationToken);
        await InsertAuditEventAsync(
            connection,
            transaction,
            projectId,
            "Project",
            projectId,
            "ProjectDeletedSnapshot",
            "ProjectTitle",
            null,
            snapshot.ProjectTitle,
            input.Reason,
            changedByUserId,
            correlationId,
            false,
            cancellationToken);
        await InsertAuditEventAsync(
            connection,
            transaction,
            projectId,
            "Project",
            projectId,
            "ProjectDeletedSnapshot",
            "PackagingMethod",
            null,
            snapshot.PackagingMethod,
            input.Reason,
            changedByUserId,
            correlationId,
            false,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var detail = await GetDeletedProjectAsync(projectId, includeSalesAmount, true, cancellationToken);
        return ProjectMutationResult<DeletedProjectDetailResponse>.Success(detail!);
    }

    public async Task<DeletedProjectListResponse> ListDeletedProjectsAsync(
        DeletedProjectListQuery query,
        bool includeSalesAmount,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        var where = new List<string> { "projects.deleted_at_utc is not null" };
        var parameters = new List<NpgsqlParameter>();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            where.Add("""
                (
                    projects.customer_name ilike @search
                    or projects.item ilike @search
                    or projects.project_code ilike @search
                    or projects.project_title ilike @search
                )
                """);
            parameters.Add(new NpgsqlParameter("search", $"%{query.Search.Trim()}%"));
        }

        if (query.DeletedByUserId is not null)
        {
            where.Add("projects.deleted_by_user_id = @deleted_by_user_id");
            parameters.Add(new NpgsqlParameter("deleted_by_user_id", query.DeletedByUserId.Value));
        }

        if (query.DeletedAtFrom is not null)
        {
            where.Add("projects.deleted_at_utc >= @deleted_at_from");
            parameters.Add(new NpgsqlParameter("deleted_at_from", query.DeletedAtFrom.Value));
        }

        if (query.DeletedAtTo is not null)
        {
            where.Add("projects.deleted_at_utc <= @deleted_at_to");
            parameters.Add(new NpgsqlParameter("deleted_at_to", query.DeletedAtTo.Value));
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var offset = (page - 1) * pageSize;
        var whereSql = $"where {string.Join(" and ", where)}";

        await using var command = dataSource.CreateCommand($"""
            select
                projects.id,
                coalesce(projects.customer_name, ''),
                coalesce(projects.item, ''),
                coalesce(projects.project_code, projects.project_number),
                coalesce(projects.project_title, projects.name),
                coalesce(active_panels.active_panel_count, 0),
                coalesce(projects.delivery_date, current_date),
                projects.sales_owner_user_id,
                coalesce(owner.display_name, ''),
                projects.packaging_method,
                projects.delivery_location,
                projects.status,
                projects.created_at_utc,
                projects.updated_at_utc,
                projects.sales_amount,
                projects.currency_code,
                projects.status as project_work_status,
                null::integer as project_progress_percent,
                projects.deleted_at_utc,
                projects.deleted_by_user_id,
                deleter.display_name,
                coalesce(projects.delete_reason, ''),
                count(*) over() as total_count
            from projects
            left join qms_users owner on owner.id = projects.sales_owner_user_id
            left join qms_users deleter on deleter.id = projects.deleted_by_user_id
            left join lateral (
                select count(*)::integer as active_panel_count
                from panel_placeholders
                where panel_placeholders.project_id = projects.id
                  and panel_placeholders.status = 'Active'
            ) active_panels on true
            {whereSql}
            order by projects.deleted_at_utc desc nulls last
            limit @limit offset @offset;
            """);

        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        command.Parameters.AddWithValue("limit", pageSize);
        command.Parameters.AddWithValue("offset", offset);

        var items = new List<DeletedProjectListItemResponse>();
        long totalCount = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            totalCount = reader.GetInt64(22);
            items.Add(ReadDeletedProjectListItem(reader, includeSalesAmount));
        }

        return new DeletedProjectListResponse(items, page, pageSize, totalCount);
    }

    public async Task<DeletedProjectDetailResponse?> GetDeletedProjectAsync(
        Guid projectId,
        bool includeSalesAmount,
        bool includeAudit,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select
                projects.id,
                coalesce(projects.customer_name, ''),
                coalesce(projects.item, ''),
                coalesce(projects.project_code, projects.project_number),
                coalesce(projects.project_title, projects.name),
                coalesce(active_panels.active_panel_count, 0),
                coalesce(projects.delivery_date, current_date),
                projects.sales_owner_user_id,
                coalesce(owner.display_name, ''),
                projects.packaging_method,
                projects.delivery_location,
                projects.status,
                projects.created_at_utc,
                projects.updated_at_utc,
                projects.sales_amount,
                projects.currency_code,
                projects.status as project_work_status,
                null::integer as project_progress_percent,
                projects.deleted_at_utc,
                projects.deleted_by_user_id,
                deleter.display_name,
                coalesce(projects.delete_reason, ''),
                projects.status_reason
            from projects
            left join qms_users owner on owner.id = projects.sales_owner_user_id
            left join qms_users deleter on deleter.id = projects.deleted_by_user_id
            left join lateral (
                select count(*)::integer as active_panel_count
                from panel_placeholders
                where panel_placeholders.project_id = projects.id
                  and panel_placeholders.status = 'Active'
            ) active_panels on true
            where projects.id = @project_id
              and projects.deleted_at_utc is not null;
            """);
        command.Parameters.AddWithValue("project_id", projectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var item = ReadDeletedProjectListItem(reader, includeSalesAmount);
        var statusReason = reader.IsDBNull(22) ? null : reader.GetString(22);
        await reader.DisposeAsync();
        var detail = new DeletedProjectDetailResponse
        {
            ProjectId = item.ProjectId,
            CustomerName = item.CustomerName,
            Item = item.Item,
            ProjectCode = item.ProjectCode,
            ProjectTitle = item.ProjectTitle,
            ActivePanelCount = item.ActivePanelCount,
            DeliveryDate = item.DeliveryDate,
            SalesOwnerUserId = item.SalesOwnerUserId,
            SalesOwnerName = item.SalesOwnerName,
            PackagingMethod = item.PackagingMethod,
            DeliveryLocation = item.DeliveryLocation,
            Status = item.Status,
            ProjectWorkStatus = item.ProjectWorkStatus,
            ProjectProgressPercent = item.ProjectProgressPercent,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            SalesAmount = item.SalesAmount,
            CurrencyCode = item.CurrencyCode,
            DeletedAtUtc = item.DeletedAtUtc,
            DeletedByUserId = item.DeletedByUserId,
            DeletedByUserName = item.DeletedByUserName,
            DeleteReason = item.DeleteReason,
            StatusReason = statusReason,
            Panels = await ListDeletedProjectPanelsAsync(dataSource, projectId, cancellationToken),
            AuditHistory = includeAudit ? await ListDeletedProjectAuditHistoryAsync(dataSource, projectId, includeSalesAmount, cancellationToken) : []
        };

        return detail;
    }

    public async Task<ProjectMutationResult<ProjectDetailResponse>> RestoreDeletedProjectAsync(
        Guid projectId,
        string? reason,
        Guid changedByUserId,
        string correlationId,
        bool includeSalesAmount,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            var deleted = await LockDeletedProjectForRestoreAsync(connection, transaction, projectId, cancellationToken);
            if (deleted is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                if (await ProjectExistsAsync(connection, null, projectId, cancellationToken))
                {
                    return ProjectMutationResult<ProjectDetailResponse>.Conflict("삭제 보관함에 있는 프로젝트만 복구할 수 있습니다.");
                }

                return ProjectMutationResult<ProjectDetailResponse>.NotFound();
            }

            if (await ProjectTitleExistsAsync(connection, transaction, deleted.ProjectTitleNormalized, projectId, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return ProjectMutationResult<ProjectDetailResponse>.Conflict("동일한 프로젝트명이 이미 사용 중입니다. 프로젝트명을 정리한 후 다시 시도해 주세요.");
            }

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    update projects
                    set deleted_at_utc = null,
                        deleted_by_user_id = null,
                        delete_reason = null,
                        deleted_correlation_id = null,
                        updated_at_utc = now()
                    where id = @project_id;
                    """;
                command.Parameters.AddWithValue("project_id", projectId);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await RecalculatePanelDerivedStateAsync(connection, transaction, projectId, cancellationToken);
            await InsertAuditEventAsync(
                connection,
                transaction,
                projectId,
                "Project",
                projectId,
                "ProjectRestored",
                "DeletedAtUtc",
                ProjectInputNormalizer.FormatAuditValue(deleted.DeletedAtUtc),
                null,
                string.IsNullOrWhiteSpace(reason) ? "삭제 보관함 복구" : reason,
                changedByUserId,
                correlationId,
                false,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            throw;
        }

        var detail = await GetProjectAsync(projectId, includeSalesAmount, cancellationToken);
        return detail is null
            ? ProjectMutationResult<ProjectDetailResponse>.NotFound()
            : ProjectMutationResult<ProjectDetailResponse>.Success(detail);
    }

    public async Task<ProjectMutationResult<PurgeDeletedProjectsResponse>> PurgeDeletedProjectAsync(
        Guid projectId,
        string? confirmText,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(confirmText, "완전 삭제", StringComparison.Ordinal))
        {
            return ProjectMutationResult<PurgeDeletedProjectsResponse>.Validation(
                new Dictionary<string, string[]> { ["ConfirmText"] = ["확인 문구를 정확히 입력해 주세요."] });
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            var deleted = await ReadDeletedProjectForPurgeAsync(connection, transaction, projectId, cancellationToken);
            if (deleted is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                if (await ProjectExistsAsync(connection, null, projectId, cancellationToken))
                {
                    return ProjectMutationResult<PurgeDeletedProjectsResponse>.Conflict("삭제 보관함에 있는 프로젝트만 완전 삭제할 수 있습니다.");
                }

                return ProjectMutationResult<PurgeDeletedProjectsResponse>.NotFound();
            }

            await PurgeDeletedProjectIdsAsync(connection, transaction, [projectId], cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ProjectMutationResult<PurgeDeletedProjectsResponse>.Success(new PurgeDeletedProjectsResponse(1));
        }
        catch
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            throw;
        }
    }

    public async Task<ProjectMutationResult<PurgeDeletedProjectsResponse>> PurgeAllDeletedProjectsAsync(
        string? confirmText,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(confirmText, "삭제 보관함 비우기", StringComparison.Ordinal))
        {
            return ProjectMutationResult<PurgeDeletedProjectsResponse>.Validation(
                new Dictionary<string, string[]> { ["ConfirmText"] = ["확인 문구를 정확히 입력해 주세요."] });
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            var projectIds = await ReadAllDeletedProjectIdsForPurgeAsync(connection, transaction, cancellationToken);
            if (projectIds.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return ProjectMutationResult<PurgeDeletedProjectsResponse>.Success(new PurgeDeletedProjectsResponse(0));
            }

            await PurgeDeletedProjectIdsAsync(connection, transaction, projectIds, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ProjectMutationResult<PurgeDeletedProjectsResponse>.Success(new PurgeDeletedProjectsResponse(projectIds.Count));
        }
        catch
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<PanelPlaceholderResponse>?> ListPanelsAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (await GetProjectAccessRecordAsync(projectId, cancellationToken) is null)
        {
            return null;
        }

        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select panel_placeholders.id,
                   panel_placeholders.project_id,
                   panel_placeholders.sequence_number,
                   panel_placeholders.display_code,
                   panel_placeholders.panel_name,
                   panel_placeholders.width_mm,
                   panel_placeholders.height_mm,
                   panel_placeholders.depth_mm,
                   panel_placeholders.status,
                   panel_placeholders.workflow_stage,
                   case
                       when projects.packaging_method is null then false
                       when projects.packaging_method = 'WoodenCrate' then
                           panel_placeholders.panel_name is not null
                           and panel_placeholders.width_mm is not null
                           and panel_placeholders.height_mm is not null
                           and panel_placeholders.depth_mm is not null
                       when projects.packaging_method in ('StretchWrap', 'HeavyDutyBox') then
                           panel_placeholders.panel_name is not null
                           and (
                               (panel_placeholders.width_mm is null and panel_placeholders.height_mm is null and panel_placeholders.depth_mm is null)
                               or (panel_placeholders.width_mm is not null and panel_placeholders.height_mm is not null and panel_placeholders.depth_mm is not null)
                           )
                       else false
                   end as panel_info_completed,
                   projects.deleted_at_utc is null
                       and projects.status = 'Active'
                       and panel_placeholders.status = 'Active'
                       and panel_placeholders.panel_name is not null as qr_eligible,
                   panel_placeholders.created_at_utc,
                   panel_placeholders.updated_at_utc
            from panel_placeholders
            join projects on projects.id = panel_placeholders.project_id
            where panel_placeholders.project_id = @project_id
              and projects.deleted_at_utc is null
            order by panel_placeholders.sequence_number;
            """);
        command.Parameters.AddWithValue("project_id", projectId);

        var panels = new List<PanelPlaceholderResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            panels.Add(ReadPanel(reader));
        }

        return panels;
    }

    public async Task<PanelPlaceholderResponse?> GetPanelAsync(
        Guid projectId,
        Guid panelId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select panel_placeholders.id,
                   panel_placeholders.project_id,
                   panel_placeholders.sequence_number,
                   panel_placeholders.display_code,
                   panel_placeholders.panel_name,
                   panel_placeholders.width_mm,
                   panel_placeholders.height_mm,
                   panel_placeholders.depth_mm,
                   panel_placeholders.status,
                   panel_placeholders.workflow_stage,
                   case
                       when projects.packaging_method is null then false
                       when projects.packaging_method = 'WoodenCrate' then
                           panel_placeholders.panel_name is not null
                           and panel_placeholders.width_mm is not null
                           and panel_placeholders.height_mm is not null
                           and panel_placeholders.depth_mm is not null
                       when projects.packaging_method in ('StretchWrap', 'HeavyDutyBox') then
                           panel_placeholders.panel_name is not null
                           and (
                               (panel_placeholders.width_mm is null and panel_placeholders.height_mm is null and panel_placeholders.depth_mm is null)
                               or (panel_placeholders.width_mm is not null and panel_placeholders.height_mm is not null and panel_placeholders.depth_mm is not null)
                           )
                       else false
                   end as panel_info_completed,
                   projects.deleted_at_utc is null
                       and projects.status = 'Active'
                       and panel_placeholders.status = 'Active'
                       and panel_placeholders.panel_name is not null as qr_eligible,
                   panel_placeholders.created_at_utc,
                   panel_placeholders.updated_at_utc
            from panel_placeholders
            join projects on projects.id = panel_placeholders.project_id
            where panel_placeholders.project_id = @project_id
              and panel_placeholders.id = @panel_id
              and projects.deleted_at_utc is null;
            """);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("panel_id", panelId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadPanel(reader) : null;
    }

    public async Task<ProjectAuditHistoryResponse?> GetAuditHistoryAsync(
        Guid projectId,
        bool includeSensitive,
        CancellationToken cancellationToken)
    {
        if (await GetProjectAccessRecordAsync(projectId, cancellationToken) is null)
        {
            return null;
        }

        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand($"""
            select project_audit_events.id,
                   project_audit_events.entity_type,
                   project_audit_events.entity_id,
                   project_audit_events.project_id,
                   project_audit_events.action,
                   project_audit_events.field_name,
                   project_audit_events.old_value,
                   project_audit_events.new_value,
                   project_audit_events.reason,
                   project_audit_events.changed_by_user_id,
                   qms_users.display_name,
                   project_audit_events.changed_at_utc,
                   project_audit_events.correlation_id
            from project_audit_events
            left join qms_users on qms_users.id = project_audit_events.changed_by_user_id
            where project_audit_events.project_id = @project_id
              {(includeSensitive ? "" : "and project_audit_events.is_sensitive = false")}
            order by project_audit_events.changed_at_utc desc, project_audit_events.id desc;
            """);
        command.Parameters.AddWithValue("project_id", projectId);

        var events = new List<ProjectAuditEventResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new ProjectAuditEventResponse
            {
                AuditEventId = reader.GetGuid(0),
                EntityType = reader.GetString(1),
                EntityId = reader.GetGuid(2),
                ProjectId = reader.GetGuid(3),
                Action = reader.GetString(4),
                FieldName = reader.IsDBNull(5) ? null : reader.GetString(5),
                OldValue = reader.IsDBNull(6) ? null : reader.GetString(6),
                NewValue = reader.IsDBNull(7) ? null : reader.GetString(7),
                Reason = reader.IsDBNull(8) ? null : reader.GetString(8),
                ChangedByUserId = reader.IsDBNull(9) ? null : reader.GetGuid(9),
                ChangedByUserName = reader.IsDBNull(10) ? null : reader.GetString(10),
                ChangedAtUtc = reader.GetFieldValue<DateTimeOffset>(11),
                CorrelationId = reader.GetString(12)
            });
        }

        return new ProjectAuditHistoryResponse(events);
    }

    public ProjectExcelTemplate CreateProjectExcelTemplate()
    {
        return new ProjectExcelTemplate(
            projectExcelParser.CreateTemplate(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Project_Create_Template.xlsx");
    }

    public async Task<bool> IsActiveProductionProductTypeCodeAsync(string code, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await IsActiveProductionProductTypeCodeAsync(connection, null, code, cancellationToken);
    }

    public async Task<ProjectMutationResult<ProjectExcelPreviewResponse>> PreviewProjectExcelAsync(
        UploadedExcelFile file,
        CancellationToken cancellationToken)
    {
        var preview = await BuildProjectExcelPreviewAsync(file, cancellationToken);
        return ProjectMutationResult<ProjectExcelPreviewResponse>.Success(preview.Response);
    }

    public async Task<ProjectMutationResult<ProjectExcelApplyResponse>> ApplyProjectExcelAsync(
        UploadedExcelFile file,
        string expectedFileSha256,
        Guid changedByUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(file.FileSha256, expectedFileSha256, StringComparison.OrdinalIgnoreCase))
        {
            return ProjectMutationResult<ProjectExcelApplyResponse>.Conflict("파일이 변경되었습니다. 다시 미리보기를 실행해 주세요.");
        }

        var preview = await BuildProjectExcelPreviewAsync(file, cancellationToken);
        if (preview.Response.ErrorCount > 0 || preview.Response.NeedsReviewCount > 0)
        {
            if (preview.Response.Rows.Any(row => row.ErrorMessages.Any(message => message.Contains("동일한 PJT Title", StringComparison.Ordinal))))
            {
                return ProjectMutationResult<ProjectExcelApplyResponse>.Conflict("동일한 PJT Title이 이미 존재합니다.");
            }

            return ProjectMutationResult<ProjectExcelApplyResponse>.Validation(
                new Dictionary<string, string[]> { ["File"] = ["오류 또는 확인이 필요한 행이 있어 저장할 수 없습니다."] });
        }

        var inputs = preview.Rows
            .Where(row => row.Input is not null)
            .Select(row => row.Input!)
            .ToList();
        if (inputs.Count == 0)
        {
            return ProjectMutationResult<ProjectExcelApplyResponse>.Validation(
                new Dictionary<string, string[]> { ["File"] = ["생성할 프로젝트가 없습니다."] });
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var createdIds = new List<Guid>();
        try
        {
            foreach (var input in inputs)
            {
                if (!await IsActiveSalesUserAsync(connection, transaction, input.SalesOwnerUserId, cancellationToken))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ProjectMutationResult<ProjectExcelApplyResponse>.Validation(
                        new Dictionary<string, string[]> { ["SalesOwner"] = ["활성 Sales 사용자만 영업담당자로 선택할 수 있습니다."] });
                }

                if (await ProjectTitleExistsAsync(connection, transaction, input.ProjectTitleNormalized, null, cancellationToken))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ProjectMutationResult<ProjectExcelApplyResponse>.Conflict("동일한 PJT Title이 이미 존재합니다.");
                }

                createdIds.Add(await InsertProjectWithPanelsAsync(
                    connection,
                    transaction,
                    input,
                    changedByUserId,
                    correlationId,
                    cancellationToken));
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ProjectMutationResult<ProjectExcelApplyResponse>.Conflict("동일한 PJT Title이 이미 존재합니다.");
        }

        return ProjectMutationResult<ProjectExcelApplyResponse>.Success(new ProjectExcelApplyResponse(createdIds.Count, createdIds));
    }

    private async Task<ProjectExcelPreviewBuildResult> BuildProjectExcelPreviewAsync(
        UploadedExcelFile file,
        CancellationToken cancellationToken)
    {
        var parsed = await projectExcelParser.ParseAsync(file, cancellationToken);
        if (parsed.FileErrors.Count > 0)
        {
            var errorRow = new ProjectExcelPreviewRowResponse(
                0,
                "Error",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                parsed.FileErrors);
            return new ProjectExcelPreviewBuildResult(
                new ProjectExcelPreviewResponse(parsed.FileSha256, parsed.TotalRows, 0, 0, 1, [errorRow]),
                []);
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var salesOwners = await ReadSalesOwnerMatchersAsync(connection, cancellationToken);
        var activeProductTypeCodes = await ReadActiveProductionProductTypeCodesAsync(connection, null, cancellationToken);
        var fileTitleCounts = parsed.Rows
            .Where(row => !string.IsNullOrWhiteSpace(row.ProjectTitle))
            .GroupBy(row => ProjectInputNormalizer.NormalizeProjectTitle(row.ProjectTitle!), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var rows = new List<ProjectExcelResolvedRow>();
        foreach (var row in parsed.Rows)
        {
            var errors = new List<string>(row.ErrorMessages);
            var packagingMethod = NormalizeProjectExcelPackaging(row.PackagingMethod);
            var owner = ResolveSalesOwner(row.SalesOwnerText, salesOwners);
            if (!string.IsNullOrWhiteSpace(row.PackagingMethod) && packagingMethod is null)
            {
                errors.Add("포장방식 값을 확인할 수 없습니다.");
            }

            var request = new CreateProjectRequest(
                row.CustomerName,
                row.Item,
                row.ProjectCode,
                row.ProjectTitle,
                row.PanelCount,
                row.DeliveryDate,
                owner?.UserId,
                packagingMethod,
                row.SalesAmount,
                row.CurrencyCode,
                row.DeliveryLocation);
            var (input, validation) = ProjectRequestValidator.ValidateCreate(request);
            foreach (var error in validation.Errors)
            {
                if (string.Equals(error.Key, nameof(CreateProjectRequest.SalesOwnerUserId), StringComparison.Ordinal)
                    && owner is null
                    && !string.IsNullOrWhiteSpace(row.SalesOwnerText))
                {
                    continue;
                }

                errors.AddRange(error.Value);
            }

            if (input is not null && !activeProductTypeCodes.Contains(input.Item))
            {
                errors.Add("Item은 등록된 Item 기준값 중 하나여야 합니다.");
            }

            if (row.PanelCount is < 1 or > ProjectDomainRules.MaxPanelsPerProject)
            {
                errors.Add($"면수는 1 이상 {ProjectDomainRules.MaxPanelsPerProject} 이하의 정수여야 합니다.");
            }

            if (row.SalesAmount is not null)
            {
                var currency = ProjectInputNormalizer.NormalizeCurrencyCode(row.CurrencyCode);
                if (currency is not "KRW" and not "USD")
                {
                    errors.Add("판매금액 입력 시 통화는 KRW 또는 USD만 사용할 수 있습니다.");
                }
            }

            if (!string.IsNullOrWhiteSpace(row.ProjectTitle))
            {
                var normalizedTitle = ProjectInputNormalizer.NormalizeProjectTitle(row.ProjectTitle);
                if (fileTitleCounts.TryGetValue(normalizedTitle, out var count) && count > 1)
                {
                    errors.Add("같은 Excel 파일 안에 동일한 PJT Title이 있습니다.");
                }

                if (await ProjectTitleExistsAsync(connection, null, normalizedTitle, null, cancellationToken))
                {
                    errors.Add("동일한 PJT Title이 이미 존재합니다.");
                }
            }

            var resultType = errors.Count > 0 ? "Error" : owner is null ? "NeedsReview" : "New";
            var previewRow = new ProjectExcelPreviewRowResponse(
                row.ExcelRowNumber,
                resultType,
                row.CustomerName,
                row.Item,
                row.ProjectCode,
                row.ProjectTitle,
                row.PanelCount,
                row.DeliveryDate,
                packagingMethod,
                row.SalesAmount,
                ProjectInputNormalizer.NormalizeCurrencyCode(row.CurrencyCode),
                row.DeliveryLocation,
                row.SalesOwnerText,
                owner?.UserId,
                owner?.DisplayName,
                resultType == "NeedsReview" ? [.. errors, "영업담당자를 확인할 수 없습니다."] : errors);
            rows.Add(new ProjectExcelResolvedRow(previewRow, resultType == "New" ? input : null));
        }

        return new ProjectExcelPreviewBuildResult(
            new ProjectExcelPreviewResponse(
                parsed.FileSha256,
                parsed.TotalRows,
                rows.Count(row => row.Preview.ResultType == "New"),
                rows.Count(row => row.Preview.ResultType == "NeedsReview"),
                rows.Count(row => row.Preview.ResultType == "Error"),
                rows.Select(row => row.Preview).ToList()),
            rows);
    }

    private static string? NormalizeProjectExcelPackaging(string? value)
    {
        var normalized = ProjectInputNormalizer.TrimToNull(value);
        if (normalized is null)
        {
            return null;
        }

        return normalized switch
        {
            "목포장" => "WoodenCrate",
            "청랩포장" => "StretchWrap",
            "고강도박스포장" => "HeavyDutyBox",
            "WoodenCrate" or "StretchWrap" or "HeavyDutyBox" => normalized,
            _ => null
        };
    }

    private static async Task<IReadOnlyList<ProjectExcelSalesOwnerMatcher>> ReadSalesOwnerMatchersAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select qms_users.id,
                   qms_users.display_name,
                   qms_users.development_user_key
            from qms_users
            join user_roles on user_roles.user_id = qms_users.id
            join roles on roles.id = user_roles.role_id
            where qms_users.is_active = true
              and roles.code = 'sales';
            """;

        var owners = new List<ProjectExcelSalesOwnerMatcher>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            owners.Add(new ProjectExcelSalesOwnerMatcher(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return owners;
    }

    private static async Task<bool> IsActiveProductionProductTypeCodeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string code,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists (
                select 1
                from production_product_types
                where code = @code
                  and is_active = true
            );
            """;
        command.Parameters.AddWithValue("code", code);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<IReadOnlySet<string>> ReadActiveProductionProductTypeCodesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select code
            from production_product_types
            where is_active = true;
            """;
        var codes = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            codes.Add(reader.GetString(0));
        }

        return codes;
    }

    private static ProjectExcelSalesOwnerMatcher? ResolveSalesOwner(
        string? value,
        IReadOnlyList<ProjectExcelSalesOwnerMatcher> salesOwners)
    {
        var normalized = ProjectInputNormalizer.TrimToNull(value);
        if (normalized is null)
        {
            return null;
        }

        var key = ProjectInputNormalizer.NormalizeProjectTitle(normalized);
        var matches = salesOwners
            .Where(owner =>
                string.Equals(ProjectInputNormalizer.NormalizeProjectTitle(owner.DisplayName), key, StringComparison.Ordinal)
                || (owner.DevelopmentUserKey is not null
                    && string.Equals(ProjectInputNormalizer.NormalizeProjectTitle(owner.DevelopmentUserKey), key, StringComparison.Ordinal)))
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    private static async Task<Guid> InsertProjectWithPanelsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        NormalizedCreateProjectInput input,
        Guid changedByUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var projectId = Guid.NewGuid();
        var projectKey = projectId.ToString("N");
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into projects (
                    id,
                    project_key,
                    project_number,
                    name,
                    customer_name,
                    item,
                    project_code,
                    project_title,
                    project_title_normalized,
                    packaging_method,
                    delivery_date,
                    sales_owner_user_id,
                    sales_amount,
                    currency_code,
                    delivery_location,
                    status,
                    created_by_user_id,
                    updated_at_utc
                )
                values (
                    @id,
                    @project_key,
                    @project_code,
                    @project_title,
                    @customer_name,
                    @item,
                    @project_code,
                    @project_title,
                    @project_title_normalized,
                    @packaging_method,
                    @delivery_date,
                    @sales_owner_user_id,
                    @sales_amount,
                    @currency_code,
                    @delivery_location,
                    'Active',
                    @created_by_user_id,
                    now()
                );
                """;
            AddProjectParameters(command, projectId, projectKey, input, changedByUserId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertAuditEventAsync(
            connection,
            transaction,
            projectId,
            "Project",
            projectId,
            "ProjectCreated",
            null,
            null,
            input.ProjectTitle,
            null,
            changedByUserId,
            correlationId,
            false,
            cancellationToken);

        for (var sequence = 1; sequence <= input.PanelCount; sequence++)
        {
            var panelId = Guid.NewGuid();
            var displayCode = ProjectInputNormalizer.FormatPanelDisplayCode(sequence);
            await InsertPanelAsync(connection, transaction, panelId, projectId, sequence, displayCode, cancellationToken);
            await InsertAuditEventAsync(
                connection,
                transaction,
                projectId,
                "PanelPlaceholder",
                panelId,
                "PanelCreated",
                "DisplayCode",
                null,
                displayCode,
                null,
                changedByUserId,
                correlationId,
                false,
                cancellationToken);
        }

        return projectId;
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

    private static void AddAccessScope(
        List<string> where,
        List<NpgsqlParameter> parameters,
        ProjectAccessScope accessScope)
    {
        if (accessScope.HasProjectReadAll)
        {
            return;
        }

        if (accessScope.ProjectKeys.Count == 0)
        {
            where.Add("false");
            return;
        }

        where.Add("projects.project_key = any(@project_keys)");
        parameters.Add(new NpgsqlParameter<string[]>("project_keys", accessScope.ProjectKeys.ToArray()));
    }

    private static ProjectListItemResponse ReadProjectListItem(NpgsqlDataReader reader, bool includeSalesAmount)
    {
        return new ProjectListItemResponse
        {
            ProjectId = reader.GetGuid(0),
            CustomerName = reader.GetString(1),
            Item = reader.GetString(2),
            ProjectCode = reader.GetString(3),
            ProjectTitle = reader.GetString(4),
            ActivePanelCount = reader.GetInt32(5),
            DeliveryDate = reader.GetFieldValue<DateOnly>(6),
            SalesOwnerUserId = reader.IsDBNull(7) ? Guid.Empty : reader.GetGuid(7),
            SalesOwnerName = reader.GetString(8),
            PackagingMethod = reader.IsDBNull(9) ? null : reader.GetString(9),
            DeliveryLocation = reader.IsDBNull(10) ? null : reader.GetString(10),
            Status = reader.GetString(11),
            ProjectWorkStatus = reader.GetString(16),
            ProjectProgressPercent = reader.IsDBNull(17) ? null : reader.GetInt32(17),
            CreatedAt = reader.GetFieldValue<DateTimeOffset>(12),
            UpdatedAt = reader.GetFieldValue<DateTimeOffset>(13),
            SalesAmount = includeSalesAmount && !reader.IsDBNull(14) ? reader.GetDecimal(14) : null,
            CurrencyCode = includeSalesAmount && !reader.IsDBNull(15) ? reader.GetString(15) : null
        };
    }

    private static DeletedProjectListItemResponse ReadDeletedProjectListItem(NpgsqlDataReader reader, bool includeSalesAmount)
    {
        var baseItem = ReadProjectListItem(reader, includeSalesAmount);
        return new DeletedProjectListItemResponse
        {
            ProjectId = baseItem.ProjectId,
            CustomerName = baseItem.CustomerName,
            Item = baseItem.Item,
            ProjectCode = baseItem.ProjectCode,
            ProjectTitle = baseItem.ProjectTitle,
            ActivePanelCount = baseItem.ActivePanelCount,
            DeliveryDate = baseItem.DeliveryDate,
            SalesOwnerUserId = baseItem.SalesOwnerUserId,
            SalesOwnerName = baseItem.SalesOwnerName,
            PackagingMethod = baseItem.PackagingMethod,
            DeliveryLocation = baseItem.DeliveryLocation,
            Status = baseItem.Status,
            ProjectWorkStatus = baseItem.ProjectWorkStatus,
            ProjectProgressPercent = baseItem.ProjectProgressPercent,
            CreatedAt = baseItem.CreatedAt,
            UpdatedAt = baseItem.UpdatedAt,
            SalesAmount = baseItem.SalesAmount,
            CurrencyCode = baseItem.CurrencyCode,
            DeletedAtUtc = reader.GetFieldValue<DateTimeOffset>(18),
            DeletedByUserId = reader.IsDBNull(19) ? null : reader.GetGuid(19),
            DeletedByUserName = reader.IsDBNull(20) ? null : reader.GetString(20),
            DeleteReason = reader.GetString(21)
        };
    }

    private static PanelPlaceholderResponse ReadPanel(NpgsqlDataReader reader)
    {
        return new PanelPlaceholderResponse(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetInt32(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetDecimal(5),
            reader.IsDBNull(6) ? null : reader.GetDecimal(6),
            reader.IsDBNull(7) ? null : reader.GetDecimal(7),
            reader.GetString(8),
            reader.GetString(9),
            reader.GetBoolean(10),
            reader.GetBoolean(11),
            reader.GetFieldValue<DateTimeOffset>(12),
            reader.GetFieldValue<DateTimeOffset>(13));
    }

    private static async Task<IReadOnlyList<PanelPlaceholderResponse>> ListDeletedProjectPanelsAsync(
        NpgsqlDataSource dataSource,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            select panel_placeholders.id,
                   panel_placeholders.project_id,
                   panel_placeholders.sequence_number,
                   panel_placeholders.display_code,
                   panel_placeholders.panel_name,
                   panel_placeholders.width_mm,
                   panel_placeholders.height_mm,
                   panel_placeholders.depth_mm,
                   panel_placeholders.status,
                   panel_placeholders.workflow_stage,
                   case
                       when projects.packaging_method is null then false
                       when projects.packaging_method = 'WoodenCrate' then
                           panel_placeholders.panel_name is not null
                           and panel_placeholders.width_mm is not null
                           and panel_placeholders.height_mm is not null
                           and panel_placeholders.depth_mm is not null
                       when projects.packaging_method in ('StretchWrap', 'HeavyDutyBox') then
                           panel_placeholders.panel_name is not null
                           and (
                               (panel_placeholders.width_mm is null and panel_placeholders.height_mm is null and panel_placeholders.depth_mm is null)
                               or (panel_placeholders.width_mm is not null and panel_placeholders.height_mm is not null and panel_placeholders.depth_mm is not null)
                           )
                       else false
                   end as panel_info_completed,
                   false as qr_eligible,
                   panel_placeholders.created_at_utc,
                   panel_placeholders.updated_at_utc
            from panel_placeholders
            join projects on projects.id = panel_placeholders.project_id
            where panel_placeholders.project_id = @project_id
            order by panel_placeholders.sequence_number;
            """);
        command.Parameters.AddWithValue("project_id", projectId);

        var panels = new List<PanelPlaceholderResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            panels.Add(ReadPanel(reader));
        }

        return panels;
    }

    private static async Task<IReadOnlyList<ProjectAuditEventResponse>> ListDeletedProjectAuditHistoryAsync(
        NpgsqlDataSource dataSource,
        Guid projectId,
        bool includeSensitive,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand($"""
            select project_audit_events.id,
                   project_audit_events.entity_type,
                   project_audit_events.entity_id,
                   project_audit_events.project_id,
                   project_audit_events.action,
                   project_audit_events.field_name,
                   project_audit_events.old_value,
                   project_audit_events.new_value,
                   project_audit_events.reason,
                   project_audit_events.changed_by_user_id,
                   qms_users.display_name,
                   project_audit_events.changed_at_utc,
                   project_audit_events.correlation_id
            from project_audit_events
            left join qms_users on qms_users.id = project_audit_events.changed_by_user_id
            where project_audit_events.project_id = @project_id
              {(includeSensitive ? "" : "and project_audit_events.is_sensitive = false")}
            order by project_audit_events.changed_at_utc desc, project_audit_events.id desc;
            """);
        command.Parameters.AddWithValue("project_id", projectId);

        var events = new List<ProjectAuditEventResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new ProjectAuditEventResponse
            {
                AuditEventId = reader.GetGuid(0),
                EntityType = reader.GetString(1),
                EntityId = reader.GetGuid(2),
                ProjectId = reader.GetGuid(3),
                Action = reader.GetString(4),
                FieldName = reader.IsDBNull(5) ? null : reader.GetString(5),
                OldValue = reader.IsDBNull(6) ? null : reader.GetString(6),
                NewValue = reader.IsDBNull(7) ? null : reader.GetString(7),
                Reason = reader.IsDBNull(8) ? null : reader.GetString(8),
                ChangedByUserId = reader.IsDBNull(9) ? null : reader.GetGuid(9),
                ChangedByUserName = reader.IsDBNull(10) ? null : reader.GetString(10),
                ChangedAtUtc = reader.GetFieldValue<DateTimeOffset>(11),
                CorrelationId = reader.GetString(12)
            });
        }

        return events;
    }

    private static async Task<bool> IsActiveSalesUserAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists (
                select 1
                from qms_users
                join user_roles on user_roles.user_id = qms_users.id
                join roles on roles.id = user_roles.role_id
                where qms_users.id = @user_id
                  and qms_users.is_active = true
                  and roles.code = 'sales'
            );
            """;
        command.Parameters.AddWithValue("user_id", userId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is bool result && result;
    }

    private static async Task<bool> ProjectTitleExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string normalizedTitle,
        Guid? excludedProjectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists (
                select 1
                from projects
                where project_title_normalized = @project_title_normalized
                  and deleted_at_utc is null
                  and (@excluded_project_id is null or id <> @excluded_project_id)
            );
            """;
        command.Parameters.AddWithValue("project_title_normalized", normalizedTitle);
        command.Parameters.Add("excluded_project_id", NpgsqlDbType.Uuid).Value = excludedProjectId ?? (object)DBNull.Value;
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is bool result && result;
    }

    private static void AddProjectParameters(
        NpgsqlCommand command,
        Guid projectId,
        string projectKey,
        NormalizedCreateProjectInput input,
        Guid changedByUserId)
    {
        command.Parameters.AddWithValue("id", projectId);
        command.Parameters.AddWithValue("project_key", projectKey);
        AddProjectValueParameters(
            command,
            input.CustomerName,
            input.Item,
            input.ProjectCode,
            input.ProjectTitle,
            input.ProjectTitleNormalized,
            input.PackagingMethod,
            input.DeliveryDate,
            input.SalesOwnerUserId,
            input.SalesAmount,
            input.CurrencyCode,
            input.DeliveryLocation,
            changedByUserId);
    }

    private static void AddProjectParameters(
        NpgsqlCommand command,
        Guid projectId,
        string projectKey,
        NormalizedUpdateProjectInput input,
        Guid changedByUserId)
    {
        command.Parameters.AddWithValue("id", projectId);
        command.Parameters.AddWithValue("project_key", projectKey);
        AddProjectValueParameters(
            command,
            input.CustomerName,
            input.Item,
            input.ProjectCode,
            input.ProjectTitle,
            input.ProjectTitleNormalized,
            input.PackagingMethod,
            input.DeliveryDate,
            input.SalesOwnerUserId,
            input.SalesAmount,
            input.CurrencyCode,
            input.DeliveryLocation,
            changedByUserId);
    }

    private static void AddProjectValueParameters(
        NpgsqlCommand command,
        string customerName,
        string item,
        string projectCode,
        string projectTitle,
        string projectTitleNormalized,
        string packagingMethod,
        DateOnly deliveryDate,
        Guid salesOwnerUserId,
        decimal? salesAmount,
        string? currencyCode,
        string? deliveryLocation,
        Guid changedByUserId)
    {
        command.Parameters.AddWithValue("customer_name", customerName);
        command.Parameters.AddWithValue("item", item);
        command.Parameters.AddWithValue("project_code", projectCode);
        command.Parameters.AddWithValue("project_title", projectTitle);
        command.Parameters.AddWithValue("project_title_normalized", projectTitleNormalized);
        command.Parameters.AddWithValue("packaging_method", packagingMethod);
        command.Parameters.AddWithValue("delivery_date", deliveryDate);
        command.Parameters.AddWithValue("sales_owner_user_id", salesOwnerUserId);
        command.Parameters.Add("sales_amount", NpgsqlDbType.Numeric).Value = salesAmount ?? (object)DBNull.Value;
        command.Parameters.Add("currency_code", NpgsqlDbType.Text).Value = currencyCode ?? (object)DBNull.Value;
        command.Parameters.Add("delivery_location", NpgsqlDbType.Text).Value = deliveryLocation ?? (object)DBNull.Value;
        command.Parameters.AddWithValue("created_by_user_id", changedByUserId);
    }

    private static async Task InsertPanelAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid panelId,
        Guid projectId,
        int sequenceNumber,
        string displayCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into panel_placeholders (
                id,
                project_id,
                sequence_number,
                display_code,
                panel_name,
                width_mm,
                height_mm,
                depth_mm,
                status,
                panel_info_completed,
                qr_eligible,
                updated_at_utc
            )
            values (
                @id,
                @project_id,
                @sequence_number,
                @display_code,
                null,
                null,
                null,
                null,
                'Active',
                false,
                false,
                now()
            );
            """;
        command.Parameters.AddWithValue("id", panelId);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("sequence_number", sequenceNumber);
        command.Parameters.AddWithValue("display_code", displayCode);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAuditEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        string entityType,
        Guid entityId,
        string action,
        string? fieldName,
        string? oldValue,
        string? newValue,
        string? reason,
        Guid changedByUserId,
        string correlationId,
        bool isSensitive,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into project_audit_events (
                project_id,
                entity_type,
                entity_id,
                action,
                field_name,
                old_value,
                new_value,
                reason,
                changed_by_user_id,
                correlation_id,
                is_sensitive
            )
            values (
                @project_id,
                @entity_type,
                @entity_id,
                @action,
                @field_name,
                @old_value,
                @new_value,
                @reason,
                @changed_by_user_id,
                @correlation_id,
                @is_sensitive
            );
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("entity_type", entityType);
        command.Parameters.AddWithValue("entity_id", entityId);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.Add("field_name", NpgsqlDbType.Text).Value = fieldName ?? (object)DBNull.Value;
        command.Parameters.Add("old_value", NpgsqlDbType.Text).Value = oldValue ?? (object)DBNull.Value;
        command.Parameters.Add("new_value", NpgsqlDbType.Text).Value = newValue ?? (object)DBNull.Value;
        command.Parameters.Add("reason", NpgsqlDbType.Text).Value = reason ?? (object)DBNull.Value;
        command.Parameters.AddWithValue("changed_by_user_id", changedByUserId);
        command.Parameters.AddWithValue("correlation_id", correlationId);
        command.Parameters.AddWithValue("is_sensitive", isSensitive);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task RecalculatePanelDerivedStateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update panel_placeholders
            set panel_info_completed = case
                    when projects.packaging_method is null then false
                    when projects.packaging_method = 'WoodenCrate' then
                        panel_placeholders.panel_name is not null
                        and panel_placeholders.width_mm is not null
                        and panel_placeholders.height_mm is not null
                        and panel_placeholders.depth_mm is not null
                    when projects.packaging_method in ('StretchWrap', 'HeavyDutyBox') then
                        panel_placeholders.panel_name is not null
                        and (
                            (panel_placeholders.width_mm is null and panel_placeholders.height_mm is null and panel_placeholders.depth_mm is null)
                            or (panel_placeholders.width_mm is not null and panel_placeholders.height_mm is not null and panel_placeholders.depth_mm is not null)
                        )
                    else false
                end,
                qr_eligible = projects.deleted_at_utc is null
                    and projects.status = 'Active'
                    and panel_placeholders.status = 'Active'
                    and panel_placeholders.panel_name is not null
            from projects
            where projects.id = panel_placeholders.project_id
              and panel_placeholders.project_id = @project_id;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<ProjectPanelInformationSummary> ReadPanelInformationSummaryAsync(
        NpgsqlDataSource dataSource,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            select projects.status,
                   projects.packaging_method,
                   projects.deleted_at_utc,
                   panel_placeholders.panel_name,
                   panel_placeholders.width_mm,
                   panel_placeholders.height_mm,
                   panel_placeholders.depth_mm,
                   panel_placeholders.status,
                   panel_placeholders.workflow_stage
            from projects
            left join panel_placeholders on panel_placeholders.project_id = projects.id
            where projects.id = @project_id
              and projects.deleted_at_utc is null;
            """);
        command.Parameters.AddWithValue("project_id", projectId);

        var activePanelCount = 0;
        var completedCount = 0;
        var qrEligibleCount = 0;
        var manufacturingCompletedCount = 0;
        var inspectionCompletedCount = 0;
        string? projectStatus = null;
        string? packagingMethod = null;
        DateTimeOffset? deletedAtUtc = null;
        var duplicateNames = new Dictionary<string, int>(StringComparer.Ordinal);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            projectStatus ??= reader.GetString(0);
            packagingMethod ??= reader.IsDBNull(1) ? null : reader.GetString(1);
            deletedAtUtc ??= reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2);
            if (reader.IsDBNull(7))
            {
                continue;
            }

            var panelStatus = reader.GetString(7);
            if (panelStatus != "Active")
            {
                continue;
            }

            var panelName = reader.IsDBNull(3) ? null : reader.GetString(3);
            decimal? widthMm = reader.IsDBNull(4) ? null : reader.GetDecimal(4);
            decimal? heightMm = reader.IsDBNull(5) ? null : reader.GetDecimal(5);
            decimal? depthMm = reader.IsDBNull(6) ? null : reader.GetDecimal(6);
            activePanelCount++;

            if (PanelInformationDomain.IsPanelInfoCompleted(packagingMethod, panelName, widthMm, heightMm, depthMm))
            {
                completedCount++;
            }

            if (PanelInformationDomain.IsQrEligible(deletedAtUtc is not null, projectStatus ?? "", panelStatus, panelName))
            {
                qrEligibleCount++;
            }

            var workflowStage = reader.GetString(8);
            if (PanelInformationDomain.IsManufacturingCompletedStage(workflowStage))
            {
                manufacturingCompletedCount++;
            }

            if (PanelInformationDomain.IsInspectionCompletedStage(workflowStage))
            {
                inspectionCompletedCount++;
            }

            var duplicateName = PanelInformationDomain.NormalizeDuplicateName(panelName);
            if (duplicateName is not null)
            {
                duplicateNames[duplicateName] = duplicateNames.TryGetValue(duplicateName, out var count) ? count + 1 : 1;
            }
        }

        var pendingCount = activePanelCount - completedCount;
        return new ProjectPanelInformationSummary(
            completedCount,
            pendingCount,
            qrEligibleCount,
            manufacturingCompletedCount,
            inspectionCompletedCount,
            duplicateNames.Count(item => item.Value > 1),
            activePanelCount > 0 && pendingCount == 0);
    }

    private static async Task<ProjectEditSnapshot?> ReadProjectEditSnapshotAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid projectId,
        bool lockForUpdate,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id,
                   project_key,
                   coalesce(customer_name, ''),
                   coalesce(item, ''),
                   coalesce(project_code, project_number),
                   coalesce(project_title, name),
                   coalesce(project_title_normalized, upper(regexp_replace(btrim(name), '\s+', ' ', 'g'))),
                   packaging_method,
                   coalesce(delivery_date, current_date),
                   sales_owner_user_id,
                   sales_amount,
                   currency_code,
                   delivery_location
            from projects
            where id = @project_id
              and deleted_at_utc is null
            """;
        if (lockForUpdate)
        {
            command.CommandText += " for update";
        }

        command.CommandText += ";";
        command.Parameters.AddWithValue("project_id", projectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ProjectEditSnapshot(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.GetFieldValue<DateOnly>(8),
            reader.IsDBNull(9) ? Guid.Empty : reader.GetGuid(9),
            reader.IsDBNull(10) ? null : reader.GetDecimal(10),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetString(12));
    }

    private static async Task<ProjectLockSnapshot?> LockProjectForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select status
            from projects
            where id = @project_id
              and deleted_at_utc is null
            for update;
            """;
        command.Parameters.AddWithValue("project_id", projectId);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is not string status)
        {
            return null;
        }

        await using var panelCommand = connection.CreateCommand();
        panelCommand.Transaction = transaction;
        panelCommand.CommandText = """
            select coalesce(count(id) filter (where status = 'Active'), 0)::integer,
                   coalesce(max(sequence_number), 0)::integer
            from panel_placeholders
            where project_id = @project_id;
            """;
        panelCommand.Parameters.AddWithValue("project_id", projectId);

        await using var reader = await panelCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new ProjectLockSnapshot(status, 0, 0);
        }

        return new ProjectLockSnapshot(status, reader.GetInt32(0), reader.GetInt32(1));
    }

    private static async Task<ProjectDeletionSnapshot?> LockProjectDeletionSnapshotAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select status,
                   coalesce(project_title, name),
                   coalesce(project_title_normalized, upper(regexp_replace(btrim(name), '\s+', ' ', 'g'))),
                   packaging_method,
                   deleted_at_utc
            from projects
            where id = @project_id
            for update;
            """;
        command.Parameters.AddWithValue("project_id", projectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ProjectDeletionSnapshot(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4));
    }

    private static async Task<IReadOnlyList<SelectedPanelSnapshot>> ReadSelectedActivePanelsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        IReadOnlyList<Guid> panelIds,
        CancellationToken cancellationToken)
    {
        if (panelIds.Count == 0)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, display_code
            from panel_placeholders
            where project_id = @project_id
              and status = 'Active'
              and id = any(@panel_ids)
            order by sequence_number;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.Add(new NpgsqlParameter<Guid[]>("panel_ids", panelIds.ToArray()));

        var panels = new List<SelectedPanelSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            panels.Add(new SelectedPanelSnapshot(reader.GetGuid(0), reader.GetString(1)));
        }

        return panels;
    }

    private sealed record ProjectEditSnapshot(
        Guid ProjectId,
        string ProjectKey,
        string CustomerName,
        string Item,
        string ProjectCode,
        string ProjectTitle,
        string ProjectTitleNormalized,
        string? PackagingMethod,
        DateOnly DeliveryDate,
        Guid SalesOwnerUserId,
        decimal? SalesAmount,
        string? CurrencyCode,
        string? DeliveryLocation)
    {
        public IReadOnlyList<ProjectFieldChange> CollectChanges(NormalizedUpdateProjectInput input)
        {
            var changes = new List<ProjectFieldChange>();
            Add(changes, "CustomerName", CustomerName, input.CustomerName, false);
            Add(changes, "Item", Item, input.Item, false);
            Add(changes, "ProjectCode", ProjectCode, input.ProjectCode, false);
            Add(changes, "ProjectTitle", ProjectTitle, input.ProjectTitle, false);
            Add(changes, "PackagingMethod", PackagingMethod, input.PackagingMethod, false);
            Add(changes, "DeliveryDate", DeliveryDate, input.DeliveryDate, false);
            Add(changes, "SalesOwnerUserId", SalesOwnerUserId, input.SalesOwnerUserId, false);
            Add(changes, "SalesAmount", SalesAmount, input.SalesAmount, true);
            Add(changes, "CurrencyCode", CurrencyCode, input.CurrencyCode, true);
            Add(changes, "DeliveryLocation", DeliveryLocation, input.DeliveryLocation, false);
            return changes;
        }

        private static void Add<T>(
            List<ProjectFieldChange> changes,
            string fieldName,
            T oldValue,
            T newValue,
            bool isSensitive)
        {
            if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
            {
                return;
            }

            changes.Add(new ProjectFieldChange(
                fieldName,
                ProjectInputNormalizer.FormatAuditValue(oldValue),
                ProjectInputNormalizer.FormatAuditValue(newValue),
                isSensitive));
        }
    }

    private sealed record ProjectFieldChange(string FieldName, string OldValue, string NewValue, bool IsSensitive);

    private sealed record DeletedProjectRestoreSnapshot(
        string ProjectTitle,
        string ProjectTitleNormalized,
        DateTimeOffset DeletedAtUtc);

    private sealed record ProjectPanelInformationSummary(
        int CompletedCount,
        int PendingCount,
        int QrEligibleCount,
        int ManufacturingCompletedCount,
        int InspectionCompletedCount,
        int DuplicatePanelNameGroupCount,
        bool ProjectPanelInformationCompleted);

    private static async Task<bool> ProjectExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select exists (select 1 from projects where id = @project_id);";
        command.Parameters.AddWithValue("project_id", projectId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<Guid?> ReadDeletedProjectForPurgeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id
            from projects
            where id = @project_id
              and deleted_at_utc is not null
            for update;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid id ? id : null;
    }

    private static async Task<DeletedProjectRestoreSnapshot?> LockDeletedProjectForRestoreAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select coalesce(project_title, name),
                   coalesce(project_title_normalized, upper(regexp_replace(btrim(name), '\s+', ' ', 'g'))),
                   deleted_at_utc
            from projects
            where id = @project_id
              and deleted_at_utc is not null
            for update;
            """;
        command.Parameters.AddWithValue("project_id", projectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new DeletedProjectRestoreSnapshot(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetFieldValue<DateTimeOffset>(2));
    }

    private static async Task<IReadOnlyList<Guid>> ReadAllDeletedProjectIdsForPurgeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id
            from projects
            where deleted_at_utc is not null
            order by deleted_at_utc, id
            for update;
            """;
        var projectIds = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            projectIds.Add(reader.GetGuid(0));
        }

        return projectIds;
    }

    private static async Task PurgeDeletedProjectIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<Guid> projectIds,
        CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0)
        {
            return;
        }

        await ExecutePurgeCommandAsync(connection, transaction, "delete from project_audit_events where project_id = any(@project_ids);", projectIds, cancellationToken);
        await ExecutePurgeCommandAsync(connection, transaction, "delete from procurement_excel_import_batch_projects where project_id = any(@project_ids);", projectIds, cancellationToken);
        await ExecutePurgeCommandAsync(connection, transaction, "delete from procurement_excel_import_batches where cardinality(@project_ids) >= 0 and not exists (select 1 from procurement_excel_import_batch_projects bp where bp.import_batch_id = procurement_excel_import_batches.id) and not exists (select 1 from project_audit_events a where a.procurement_import_batch_id = procurement_excel_import_batches.id);", projectIds, cancellationToken);
        await ExecutePurgeCommandAsync(connection, transaction, "delete from panel_information_excel_import_batches where project_id = any(@project_ids);", projectIds, cancellationToken);
        await ExecutePurgeCommandAsync(connection, transaction, "delete from project_assignees where project_id = any(@project_ids);", projectIds, cancellationToken);
        await ExecutePurgeCommandAsync(connection, transaction, "delete from project_production_plans where project_id = any(@project_ids);", projectIds, cancellationToken);
        await ExecutePurgeCommandAsync(connection, transaction, "delete from project_procurement_items where project_id = any(@project_ids);", projectIds, cancellationToken);
        await ExecutePurgeCommandAsync(connection, transaction, "delete from panel_placeholders where project_id = any(@project_ids);", projectIds, cancellationToken);
        await ExecutePurgeCommandAsync(connection, transaction, "delete from user_project_access where project_id = any(@project_ids);", projectIds, cancellationToken);
        await ExecutePurgeCommandAsync(connection, transaction, "delete from projects where id = any(@project_ids) and deleted_at_utc is not null;", projectIds, cancellationToken);
    }

    private static async Task ExecutePurgeCommandAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string commandText,
        IReadOnlyList<Guid> projectIds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        command.Parameters.Add(new NpgsqlParameter<Guid[]>("project_ids", projectIds.ToArray()));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DateTimeOffset ToDateTimeOffset(object? value)
    {
        return value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            _ => throw new InvalidOperationException("The database did not return a deletion timestamp.")
        };
    }

    private static async Task RollbackQuietlyAsync(
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private sealed record ProjectLockSnapshot(string Status, int ActivePanelCount, int MaxSequenceNumber);

    private sealed record ProjectDeletionSnapshot(
        string Status,
        string ProjectTitle,
        string ProjectTitleNormalized,
        string? PackagingMethod,
        DateTimeOffset? DeletedAtUtc);

    private sealed record SelectedPanelSnapshot(Guid PanelId, string DisplayCode);
}

public sealed record ProjectAccessRecord(Guid ProjectId, string ProjectKey);

public sealed record ProjectAccessScope(bool HasProjectReadAll, IReadOnlyList<string> ProjectKeys);

public sealed record ProjectListQuery(
    string? Search,
    string? Status,
    Guid? SalesOwnerUserId,
    DateOnly? DeliveryDateFrom,
    DateOnly? DeliveryDateTo,
    bool IncludeCancelled,
    int Page,
    int PageSize);

public sealed record DeletedProjectListQuery(
    string? Search,
    Guid? DeletedByUserId,
    DateTimeOffset? DeletedAtFrom,
    DateTimeOffset? DeletedAtTo,
    int Page,
    int PageSize);

public sealed record ProjectExcelTemplate(byte[] Content, string ContentType, string FileName);

internal sealed record ProjectExcelSalesOwnerMatcher(Guid UserId, string DisplayName, string? DevelopmentUserKey);

internal sealed record ProjectExcelResolvedRow(
    ProjectExcelPreviewRowResponse Preview,
    NormalizedCreateProjectInput? Input);

internal sealed record ProjectExcelPreviewBuildResult(
    ProjectExcelPreviewResponse Response,
    IReadOnlyList<ProjectExcelResolvedRow> Rows);
