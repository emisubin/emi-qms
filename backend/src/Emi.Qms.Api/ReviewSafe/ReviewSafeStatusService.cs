using Npgsql;

namespace Emi.Qms.Api.ReviewSafe;

public sealed class ReviewSafeStatusService(
    DatabaseConnectionStringProvider connectionStringProvider,
    DatabaseMigrationCatalog migrationCatalog,
    MigrationLedgerInspector migrationLedgerInspector,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    public async Task<ReviewSafeRuntimeStatus> CheckAsync(CancellationToken cancellationToken)
    {
        var enabled = ReviewSafeMode.IsEnabled(configuration);
        var catalogState = ReadCatalogState();
        if (!enabled)
        {
            return new ReviewSafeRuntimeStatus(
                "Development",
                false,
                true,
                true,
                true,
                false,
                true,
                environment.EnvironmentName,
                true,
                "not_applicable",
                catalogState.LatestVersion,
                null,
                null,
                catalogState.ExpectedCount,
                null,
                [],
                [],
                [],
                false,
                false);
        }

        if (!catalogState.Valid)
        {
            return ReviewFailure(
                catalogState.Reason,
                catalogState.LatestVersion,
                catalogState.ExpectedCount,
                catalogState.LedgerStatus);
        }

        var connectionString = connectionStringProvider.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return ReviewFailure(
                "database_not_configured",
                catalogState.LatestVersion,
                catalogState.ExpectedCount,
                MigrationLedgerInspector.UnavailableStatus);
        }

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(connectionString);
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

            var readOnly = await ReadSettingAsync(connection, "transaction_read_only", cancellationToken);
            var applicationName = await ReadSettingAsync(connection, "application_name", cancellationToken);
            var ledger = await migrationLedgerInspector.InspectAsync(connection, cancellationToken);
            var databaseReadOnly = string.Equals(readOnly, "on", StringComparison.OrdinalIgnoreCase);
            var expectedApplicationName = ReviewSafeMode.ResolveDatabaseApplicationName(configuration);
            var applicationNameMatches = string.Equals(
                applicationName,
                expectedApplicationName,
                StringComparison.Ordinal);
            var isReady = databaseReadOnly && applicationNameMatches && ledger.MigrationLedgerReady;
            var reason = !databaseReadOnly
                ? "database_not_read_only"
                : !applicationNameMatches
                    ? "database_application_name_mismatch"
                    : ledger.Reason;

            return new ReviewSafeRuntimeStatus(
                "ReviewSafe",
                true,
                false,
                false,
                false,
                databaseReadOnly,
                false,
                environment.EnvironmentName,
                isReady,
                reason,
                ledger.ExpectedLatestMigration ?? catalogState.LatestVersion,
                ledger.ActualLatestMigration,
                ledger.Status,
                ledger.ExpectedMigrationCount,
                ledger.ActualMigrationCount,
                ledger.MissingMigrations,
                ledger.UnexpectedMigrations,
                ledger.ApprovedLegacyMigrations,
                ledger.MigrationSchemaCompatible,
                ledger.MigrationLedgerReady);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return ReviewFailure(
                "database_unreachable",
                catalogState.LatestVersion,
                catalogState.ExpectedCount,
                MigrationLedgerInspector.UnavailableStatus);
        }
    }

    private (bool Valid, int ExpectedCount, string LatestVersion, string Reason, string LedgerStatus) ReadCatalogState()
    {
        try
        {
            var snapshot = migrationCatalog.GetSnapshot();
            return (true, snapshot.ExpectedCount, snapshot.LatestVersion, "ready", MigrationLedgerInspector.ExactStatus);
        }
        catch (MigrationCatalogException)
        {
            return (false, 0, string.Empty, "migration_catalog_invalid", MigrationLedgerInspector.MismatchStatus);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (false, 0, string.Empty, "migration_ledger_unavailable", MigrationLedgerInspector.UnavailableStatus);
        }
    }

    private ReviewSafeRuntimeStatus ReviewFailure(
        string reason,
        string expectedMigration,
        int expectedMigrationCount,
        string ledgerStatus)
    {
        return new ReviewSafeRuntimeStatus(
            "ReviewSafe",
            true,
            false,
            false,
            false,
            false,
            false,
            environment.EnvironmentName,
            false,
            reason,
            expectedMigration,
            null,
            ledgerStatus,
            expectedMigrationCount,
            null,
            [],
            [],
            [],
            false,
            false);
    }

    private static async Task<string?> ReadSettingAsync(
        NpgsqlConnection connection,
        string setting,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"show {setting};";
        return (await command.ExecuteScalarAsync(cancellationToken))?.ToString();
    }
}

public sealed record ReviewSafeRuntimeStatus(
    string Mode,
    bool ReviewSafe,
    bool MutationAllowed,
    bool BackgroundWorkersEnabled,
    bool ExternalProvidersEnabled,
    bool DatabaseReadOnly,
    bool MigrationExecutionEnabled,
    string Environment,
    bool Ready,
    string Reason,
    string ExpectedMigration,
    string? ActualMigration,
    string? MigrationLedgerStatus,
    int ExpectedMigrationCount,
    int? ActualMigrationCount,
    IReadOnlyList<string> MissingMigrations,
    IReadOnlyList<string> UnexpectedMigrations,
    IReadOnlyList<string> ApprovedLegacyMigrations,
    bool MigrationSchemaCompatible,
    bool MigrationLedgerReady);
