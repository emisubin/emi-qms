using System.Data;
using Emi.Qms.Api.Projects;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.OsanProjects;

public sealed class OsanDashboardStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    TimeProvider timeProvider)
{
    private static readonly TimeZoneInfo SeoulTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");

    public async Task<OsanDashboardResponse> GetAsync(
        OsanDashboardQuery query,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);
        await using (var readOnly = connection.CreateCommand())
        {
            readOnly.Transaction = transaction;
            readOnly.CommandText = "set transaction read only;";
            await readOnly.ExecuteNonQueryAsync(cancellationToken);
        }

        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), SeoulTimeZone).DateTime);
        var scope = BuildScope(query.Search, query.View, today, accessScope);
        var summary = await ReadSummaryAsync(
            connection,
            transaction,
            scope,
            cancellationToken);
        var totalCount = await ReadFilteredCountAsync(
            connection,
            transaction,
            scope,
            query.Status,
            cancellationToken);
        var projects = await ReadPageAsync(
            connection,
            transaction,
            scope,
            query,
            cancellationToken);
        var stages = await ReadStagesAsync(
            connection,
            transaction,
            projects.Select(project => project.ProjectId).ToArray(),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var items = projects.Select(project => new OsanDashboardProjectResponse(
            project.ProjectId,
            project.Title,
            project.ProjectCode,
            project.CustomerName,
            project.ProductName,
            project.PoNumber,
            project.WorkOrderNumber,
            project.Quantity,
            project.DeliveryDate,
            project.Status,
            project.CompletedStepCount,
            project.TotalStepCount,
            ProgressPercent(project.Status, project.CompletedStepCount, project.TotalStepCount),
            stages.GetValueOrDefault(project.ProjectId, [])))
            .ToArray();
        return new OsanDashboardResponse(summary, items, totalCount, query.Page, query.PageSize);
    }

    private static async Task<OsanDashboardSummaryResponse> ReadSummaryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        QueryScope scope,
        CancellationToken cancellationToken)
    {
        await using var command = CreateScopedCommand(connection, transaction, scope, $"""
            select
                count(*)::bigint,
                count(*) filter (where progress_status = '{OsanDashboardStatuses.NotStarted}')::bigint,
                count(*) filter (where progress_status = '{OsanDashboardStatuses.InProgress}')::bigint,
                count(*) filter (where progress_status = '{OsanDashboardStatuses.Completed}')::bigint
            from scoped_projects;
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new OsanDashboardSummaryResponse(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetInt64(2),
            reader.GetInt64(3));
    }

    private static async Task<long> ReadFilteredCountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        QueryScope scope,
        string status,
        CancellationToken cancellationToken)
    {
        var statusFilter = status == OsanDashboardStatuses.All
            ? string.Empty
            : "where progress_status = @status";
        await using var command = CreateScopedCommand(connection, transaction, scope, $"""
            select count(*)::bigint
            from scoped_projects
            {statusFilter};
            """);
        command.Parameters.AddWithValue("status", status);
        return (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
    }

    private static async Task<List<ProjectRow>> ReadPageAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        QueryScope scope,
        OsanDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var statusFilter = query.Status == OsanDashboardStatuses.All
            ? string.Empty
            : "where progress_status = @status";
        var ordering = query.View == OsanDashboardViews.Home
            ? "delivery_date, project_code, project_id"
            : $"case when progress_status = '{OsanDashboardStatuses.Completed}' then 1 else 0 end, "
                + "delivery_date, project_code, project_id";
        await using var command = CreateScopedCommand(connection, transaction, scope, $"""
            select project_id, title, project_code, customer_name, product_name,
                   po_number, work_order_number, quantity, delivery_date, progress_status,
                   completed_step_count, total_step_count
            from scoped_projects
            {statusFilter}
            order by {ordering}
            limit @page_size offset @offset;
            """);
        command.Parameters.AddWithValue("status", query.Status);
        command.Parameters.AddWithValue("page_size", query.PageSize);
        command.Parameters.AddWithValue("offset", checked((long)(query.Page - 1) * query.PageSize));
        var result = new List<ProjectRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ProjectRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetInt32(7),
                reader.GetFieldValue<DateOnly>(8),
                reader.GetString(9),
                reader.GetInt32(10),
                reader.GetInt32(11)));
        }
        return result;
    }

    private static async Task<Dictionary<Guid, IReadOnlyList<OsanDashboardStageResponse>>> ReadStagesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid[] projectIds,
        CancellationToken cancellationToken)
    {
        if (projectIds.Length == 0)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select step.project_id, step.sequence_number, step.step_code, step.step_name,
                   count(*) filter (where step.status = 'Completed')::integer,
                   count(*)::integer
            from osan_project_target_steps step
            where step.project_id = any(@project_ids)
            group by step.project_id, step.sequence_number, step.step_code, step.step_name
            order by step.project_id, step.sequence_number;
            """;
        command.Parameters.Add(new NpgsqlParameter<Guid[]>("project_ids", projectIds));
        var result = new Dictionary<Guid, IReadOnlyList<OsanDashboardStageResponse>>();
        var current = new List<OsanDashboardStageResponse>();
        Guid? currentProjectId = null;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var projectId = reader.GetGuid(0);
            if (currentProjectId != projectId)
            {
                if (currentProjectId is not null)
                {
                    result.Add(currentProjectId.Value, current.ToArray());
                }
                currentProjectId = projectId;
                current = [];
            }
            current.Add(new OsanDashboardStageResponse(
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetInt32(5)));
        }
        if (currentProjectId is not null)
        {
            result.Add(currentProjectId.Value, current.ToArray());
        }
        return result;
    }

    private static NpgsqlCommand CreateScopedCommand(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        QueryScope scope,
        string statement)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            with scoped_projects as (
                select projects.id as project_id,
                       projects.project_title as title,
                       projects.project_code,
                       projects.customer_name,
                       projects.osan_product_name as product_name,
                       projects.osan_po_number as po_number,
                       projects.osan_work_order_number as work_order_number,
                       projects.osan_quantity as quantity,
                       projects.delivery_date,
                       case
                           when projects.status = 'Completed' then '{OsanDashboardStatuses.Completed}'
                           when progress.completed_step_count > 0 then '{OsanDashboardStatuses.InProgress}'
                           else '{OsanDashboardStatuses.NotStarted}'
                       end as progress_status,
                       progress.completed_step_count,
                       progress.total_step_count
                from projects
                cross join lateral (
                    select count(*) filter (where step.status = 'Completed')::integer as completed_step_count,
                           count(*)::integer as total_step_count
                    from osan_project_target_steps step
                    where step.project_id = projects.id
                ) progress
                where {scope.WhereClause}
            )
            {statement}
            """;
        command.Parameters.AddRange(scope.Parameters.Select(CloneParameter).ToArray());
        return command;
    }

    private static QueryScope BuildScope(
        string search,
        string view,
        DateOnly today,
        ProjectAccessScope accessScope)
    {
        var where = new List<string>
        {
            "projects.project_profile = 'Osan'",
            "projects.deleted_at_utc is null"
        };
        var parameters = new List<NpgsqlParameter>();
        if (view == OsanDashboardViews.Home)
        {
            where.Add("not (projects.delivery_date < @today and projects.status = 'Completed')");
            parameters.Add(new NpgsqlParameter("today", NpgsqlDbType.Date) { Value = today });
        }
        if (!accessScope.HasProjectReadAll)
        {
            if (accessScope.ProjectKeys.Count == 0)
            {
                where.Add("false");
            }
            else
            {
                where.Add("projects.project_key = any(@project_keys)");
                parameters.Add(new NpgsqlParameter<string[]>(
                    "project_keys",
                    accessScope.ProjectKeys.ToArray()));
            }
        }
        if (search.Length > 0)
        {
            where.Add("""
                (projects.project_title ilike @search escape '\'
                 or projects.project_code ilike @search escape '\'
                 or projects.customer_name ilike @search escape '\'
                 or projects.osan_product_name ilike @search escape '\'
                 or coalesce(projects.osan_po_number, '') ilike @search escape '\'
                 or coalesce(projects.osan_work_order_number, '') ilike @search escape '\')
                """);
            parameters.Add(new NpgsqlParameter("search", NpgsqlDbType.Text)
            {
                Value = $"%{EscapeLike(search)}%"
            });
        }
        return new QueryScope(string.Join(" and ", where), parameters);
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static NpgsqlParameter CloneParameter(NpgsqlParameter parameter) =>
        new(parameter.ParameterName, parameter.NpgsqlDbType) { Value = parameter.Value };

    private static int ProgressPercent(string status, int completed, int total)
    {
        if (status == OsanDashboardStatuses.Completed)
        {
            return 100;
        }
        return total == 0 ? 0 : Math.Min(99, completed * 100 / total);
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

    private sealed record QueryScope(string WhereClause, IReadOnlyList<NpgsqlParameter> Parameters);
    private sealed record ProjectRow(
        Guid ProjectId,
        string Title,
        string ProjectCode,
        string CustomerName,
        string ProductName,
        string? PoNumber,
        string? WorkOrderNumber,
        int Quantity,
        DateOnly DeliveryDate,
        string Status,
        int CompletedStepCount,
        int TotalStepCount);
}
