using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.ReviewSafe;
using Npgsql;

namespace Emi.Qms.Api;

public sealed class DatabaseHealthChecker(
    DatabaseConnectionStringProvider connectionStringProvider,
    MigrationLedgerInspector migrationLedgerInspector,
    BusinessUnitDirectoryMigrationCatalog directoryMigrationCatalog)
{
    public async Task<DatabaseHealthResult> CheckAsync(CancellationToken cancellationToken)
    {
        if (!connectionStringProvider.BusinessUnits.Enabled)
        {
            return await CheckLegacyAsync(cancellationToken);
        }

        var allReady = true;
        foreach (var target in connectionStringProvider.BusinessUnits.AllTargets())
        {
            try
            {
                var connectionString = connectionStringProvider.GetConnectionString(
                    target,
                    BusinessUnitConnectionPurpose.Runtime);
                await using var dataSource = NpgsqlDataSource.Create(connectionString);
                await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
                if (!await BusinessUnitDatabaseIdentity.IsExpectedAsync(
                        connection,
                        target,
                        cancellationToken))
                {
                    allReady = false;
                    continue;
                }

                var ledger = target.Kind == BusinessUnitDatabaseKind.Directory
                    ? await directoryMigrationCatalog.InspectAsync(connection, cancellationToken)
                    : await migrationLedgerInspector.InspectAsync(connection, cancellationToken);
                if (!ledger.MigrationLedgerReady)
                {
                    allReady = false;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Continue checking the remaining isolated targets. A failed target
                // is never substituted with another configured database.
                allReady = false;
            }
        }

        return allReady
            ? new DatabaseHealthResult(true, "reachable")
            : new DatabaseHealthResult(false, "business_unit_database_unready");
    }

    private async Task<DatabaseHealthResult> CheckLegacyAsync(CancellationToken cancellationToken)
    {
        var connectionString = connectionStringProvider.GetConnectionString();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new DatabaseHealthResult(false, "not_configured");
        }

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(connectionString);
            await using var command = dataSource.CreateCommand("select 1");
            var value = await command.ExecuteScalarAsync(cancellationToken);

            return Convert.ToInt32(value) == 1
                ? new DatabaseHealthResult(true, "reachable")
                : new DatabaseHealthResult(false, "unexpected_response");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new DatabaseHealthResult(false, "unreachable");
        }
    }
}
