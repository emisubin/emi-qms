using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.ReviewSafe;
using Npgsql;

namespace Emi.Qms.Api;

public sealed class DatabaseHealthChecker(
    DatabaseConnectionStringProvider connectionStringProvider,
    MigrationLedgerInspector migrationLedgerInspector,
    BusinessUnitDirectoryMigrationCatalog directoryMigrationCatalog)
{
    public async Task<DatabaseHealthResult> CheckTargetAsync(BusinessUnitDatabaseTarget target, CancellationToken ct)
    {
        try
        {
            await using var source = NpgsqlDataSource.Create(connectionStringProvider.GetConnectionString(target));
            await using var connection = await source.OpenConnectionAsync(ct);
            if (!await BusinessUnitDatabaseIdentity.IsExpectedAsync(connection, target, ct))
                return new(false, "business_unit_database_identity_mismatch");
            var ledger = target.Kind == BusinessUnitDatabaseKind.Directory
                ? await directoryMigrationCatalog.InspectAsync(connection, ct)
                : await migrationLedgerInspector.InspectAsync(connection, target.Code, ct);
            return new(ledger.MigrationLedgerReady, ledger.MigrationLedgerReady ? "reachable" : "business_unit_database_unready");
        }
        catch (OperationCanceledException) { throw; }
        catch { return new(false, "business_unit_database_unready"); }
    }

    public async Task<DatabaseHealthResult> CheckAsync(CancellationToken cancellationToken)
    {
        if (!connectionStringProvider.BusinessUnits.Enabled)
        {
            return await CheckLegacyAsync(cancellationToken);
        }

        var states = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var target in connectionStringProvider.BusinessUnits.AllTargets())
        {
            states[target.Code] = false;
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
                    continue;
                }

                var ledger = target.Kind == BusinessUnitDatabaseKind.Directory
                    ? await directoryMigrationCatalog.InspectAsync(connection, cancellationToken)
                    : await migrationLedgerInspector.InspectAsync(connection, target.Code, cancellationToken);
                states[target.Code] = ledger.MigrationLedgerReady;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Continue checking the remaining isolated targets. A failed target
                // is never substituted with another configured database.
                states[target.Code] = false;
            }
        }

        var directoryReady = states.GetValueOrDefault("DIRECTORY");
        var businessStates = connectionStringProvider.BusinessUnits.Businesses
            .ToDictionary(target => target.Code, target => states[target.Code], StringComparer.Ordinal);
        var canServeRequests = directoryReady && businessStates.Values.Any(ready => ready);
        return new DatabaseHealthResult(canServeRequests,
            states.Values.All(ready => ready) ? "reachable" : "business_unit_database_unready", businessStates);
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
