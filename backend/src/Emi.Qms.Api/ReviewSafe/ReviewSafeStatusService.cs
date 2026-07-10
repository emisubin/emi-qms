using Npgsql;

namespace Emi.Qms.Api.ReviewSafe;

public sealed class ReviewSafeStatusService(
    DatabaseConnectionStringProvider connectionStringProvider,
    DatabaseMigrationCatalog migrationCatalog,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    public async Task<ReviewSafeRuntimeStatus> CheckAsync(CancellationToken cancellationToken)
    {
        var enabled = ReviewSafeMode.IsEnabled(configuration);
        var expectedMigration = migrationCatalog.GetExpectedLatestVersion();
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
                expectedMigration,
                null);
        }

        var connectionString = connectionStringProvider.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return ReviewFailure("database_not_configured", expectedMigration);
        }

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(connectionString);
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

            var readOnly = await ReadSettingAsync(connection, "transaction_read_only", cancellationToken);
            var applicationName = await ReadSettingAsync(connection, "application_name", cancellationToken);
            var actualMigration = await ReadActualMigrationAsync(connection, cancellationToken);
            var databaseReadOnly = string.Equals(readOnly, "on", StringComparison.OrdinalIgnoreCase);
            var applicationNameMatches = string.Equals(
                applicationName,
                ReviewSafeMode.DatabaseApplicationName,
                StringComparison.Ordinal);
            var migrationMatches = string.Equals(actualMigration, expectedMigration, StringComparison.Ordinal);
            var isReady = databaseReadOnly && applicationNameMatches && migrationMatches;
            var reason = !databaseReadOnly
                ? "database_not_read_only"
                : !applicationNameMatches
                    ? "database_application_name_mismatch"
                    : !migrationMatches
                        ? "schema_mismatch"
                        : "ready";

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
                expectedMigration,
                actualMigration);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return ReviewFailure("database_unreachable", expectedMigration);
        }
    }

    private ReviewSafeRuntimeStatus ReviewFailure(string reason, string expectedMigration)
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
            null);
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

    private static async Task<string?> ReadActualMigrationAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select version
            from schema_migrations
            order by version desc
            limit 1;
            """;
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
    string? ActualMigration);
