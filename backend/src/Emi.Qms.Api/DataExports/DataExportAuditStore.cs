using Npgsql;

namespace Emi.Qms.Api.DataExports;

public sealed class DataExportAuditStore(DatabaseConnectionStringProvider connectionStringProvider)
{
    public async Task AppendSuccessAsync(
        Guid actorUserId,
        string exportKind,
        int rowCount,
        bool filtersApplied,
        bool sensitiveSalesAmountIncluded,
        CancellationToken cancellationToken)
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("QMS database connection string is not configured.");
        }

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand("""
            insert into data_export_events (
                id,
                actor_user_id,
                export_kind,
                row_count,
                filters_applied,
                sensitive_sales_amount_included,
                succeeded_at_utc
            ) values (
                @id,
                @actor_user_id,
                @export_kind,
                @row_count,
                @filters_applied,
                @sensitive_sales_amount_included,
                now()
            );
            """);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("actor_user_id", actorUserId);
        command.Parameters.AddWithValue("export_kind", exportKind);
        command.Parameters.AddWithValue("row_count", rowCount);
        command.Parameters.AddWithValue("filters_applied", filtersApplied);
        command.Parameters.AddWithValue("sensitive_sales_amount_included", sensitiveSalesAmountIncluded);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
