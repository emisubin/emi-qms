using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.ReviewSafe;
namespace Emi.Qms.Api;

public sealed class DatabaseHealthChecker
{
    private static readonly TimeSpan ReadinessProbeBudget = TimeSpan.FromSeconds(4);

    private readonly DatabaseConnectionStringProvider connectionStringProvider;
    private readonly MigrationLedgerInspector? migrationLedgerInspector;
    private readonly BusinessUnitDirectoryMigrationCatalog? directoryMigrationCatalog;
    private readonly Func<BusinessUnitDatabaseTarget, CancellationToken, Task<DatabaseHealthResult>> targetProbe;
    private readonly TimeSpan readinessProbeBudget;

    public DatabaseHealthChecker(
        DatabaseConnectionStringProvider connectionStringProvider,
        MigrationLedgerInspector migrationLedgerInspector,
        BusinessUnitDirectoryMigrationCatalog directoryMigrationCatalog)
    {
        this.connectionStringProvider = connectionStringProvider;
        this.migrationLedgerInspector = migrationLedgerInspector;
        this.directoryMigrationCatalog = directoryMigrationCatalog;
        targetProbe = CheckConfiguredTargetAsync;
        readinessProbeBudget = ReadinessProbeBudget;
    }

    internal DatabaseHealthChecker(
        DatabaseConnectionStringProvider connectionStringProvider,
        Func<BusinessUnitDatabaseTarget, CancellationToken, Task<DatabaseHealthResult>> targetProbe,
        TimeSpan readinessProbeBudget)
    {
        this.connectionStringProvider = connectionStringProvider;
        this.targetProbe = targetProbe;
        this.readinessProbeBudget = readinessProbeBudget;
    }

    public Task<DatabaseHealthResult> CheckTargetAsync(
        BusinessUnitDatabaseTarget target,
        CancellationToken cancellationToken) =>
        targetProbe(target, cancellationToken);

    public async Task<DatabaseHealthResult> CheckAsync(CancellationToken cancellationToken)
    {
        if (!connectionStringProvider.BusinessUnits.Enabled)
        {
            return await CheckLegacyAsync(cancellationToken);
        }

        using var probeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probeCancellation.CancelAfter(readinessProbeBudget);
        var targetStates = await Task.WhenAll(
            connectionStringProvider.BusinessUnits.AllTargets()
                .Select(target => CheckReadinessTargetAsync(target, cancellationToken, probeCancellation.Token)));
        cancellationToken.ThrowIfCancellationRequested();

        var states = targetStates.ToDictionary(
            state => state.Target.Code,
            state => state.IsReady,
            StringComparer.Ordinal);
        var directoryReady = states.GetValueOrDefault("DIRECTORY");
        var businessStates = connectionStringProvider.BusinessUnits.Businesses
            .ToDictionary(target => target.Code, target => states[target.Code], StringComparer.Ordinal);
        var canServeRequests = directoryReady && businessStates.Values.Any(ready => ready);
        return new DatabaseHealthResult(
            canServeRequests,
            states.Values.All(ready => ready) ? "reachable" : "business_unit_database_unready",
            businessStates);
    }

    private async Task<(BusinessUnitDatabaseTarget Target, bool IsReady)> CheckReadinessTargetAsync(
        BusinessUnitDatabaseTarget target,
        CancellationToken callerCancellation,
        CancellationToken probeCancellation)
    {
        try
        {
            var probe = targetProbe(target, probeCancellation);
            _ = probe.ContinueWith(
                static completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            var result = await probe.WaitAsync(probeCancellation);
            return (target, result.IsReady);
        }
        catch (OperationCanceledException) when (!callerCancellation.IsCancellationRequested)
        {
            return (target, false);
        }
    }

    private async Task<DatabaseHealthResult> CheckConfiguredTargetAsync(
        BusinessUnitDatabaseTarget target,
        CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = connectionStringProvider.GetConnectionString(
                target,
                BusinessUnitConnectionPurpose.Runtime);
            await using var dataSource = connectionStringProvider.RentDataSource(connectionString);
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            if (!await BusinessUnitDatabaseIdentity.IsExpectedAsync(connection, target, cancellationToken))
                return new(false, "business_unit_database_identity_mismatch");
            var ledger = target.Kind == BusinessUnitDatabaseKind.Directory
                ? await directoryMigrationCatalog!.InspectAsync(connection, cancellationToken)
                : await migrationLedgerInspector!.InspectAsync(connection, target.Code, cancellationToken);
            return new(ledger.MigrationLedgerReady, ledger.MigrationLedgerReady ? "reachable" : "business_unit_database_unready");
        }
        catch (OperationCanceledException) { throw; }
        catch { return new(false, "business_unit_database_unready"); }
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
            await using var dataSource = connectionStringProvider.RentDataSource(connectionString);
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
