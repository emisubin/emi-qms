using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.ReviewSafe;
using Npgsql;

namespace Emi.Qms.Api;

public sealed class DatabaseMigrationRunner(
    DatabaseConnectionStringProvider connectionStringProvider,
    DatabaseMigrationCatalog migrationCatalog,
    DatabaseRuntimePrivilegeManager runtimePrivilegeManager,
    IConfiguration configuration,
    ILogger<DatabaseMigrationRunner> logger)
{
    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        _ = await ApplyAndVerifyAsync(cancellationToken);
    }

    public async Task<MigrationLedgerInspection> ApplyAndVerifyAsync(CancellationToken cancellationToken)
    {
        if (ReviewSafeMode.IsEnabled(configuration))
        {
            throw new InvalidOperationException("Database migrations are disabled in review-safe UAT mode.");
        }

        if (!connectionStringProvider.BusinessUnits.Enabled)
        {
            var connectionString = connectionStringProvider.GetConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("QMS database connection string is not configured.");
            }
            return await ApplyBusinessMigrationsAsync(
                connectionString,
                target: null,
                configuration["Database:MigrationRoleName"],
                configuration["Database:RuntimeRoleName"],
                cancellationToken);
        }

        var businessUnits = connectionStringProvider.BusinessUnits;
        businessUnits.ThrowIfInvalid();
        var operationErrors = businessUnits
            .ValidateOperationConnections(configuration, BusinessUnitConnectionPurpose.Migration)
            .Concat(businessUnits.ValidateSameServer(configuration, BusinessUnitConnectionPurpose.Migration))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (operationErrors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Business-unit migration configuration is invalid ({operationErrors.Count} validation error(s)).");
        }

        var failures = new List<string>();
        MigrationLedgerInspection? lastBusinessInspection = null;
        var directory = businessUnits.Directory
            ?? throw new InvalidOperationException("Business-unit directory target is not configured.");
        try
        {
            await ApplyDirectoryMigrationsAsync(directory, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            failures.Add(directory.Code);
            logger.LogError(
                "Database migration target failed. Target={Target} ExceptionType={ExceptionType}.",
                directory.Code,
                exception.GetType().Name);
        }

        foreach (var target in businessUnits.Businesses)
        {
            try
            {
                var connectionString = connectionStringProvider.GetConnectionString(
                    target,
                    BusinessUnitConnectionPurpose.Migration);
                lastBusinessInspection = await ApplyBusinessMigrationsAsync(
                    connectionString,
                    target,
                    target.MigrationRoleName,
                    target.RuntimeRoleName,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                failures.Add(target.Code);
                logger.LogError(
                    "Database migration target failed. Target={Target} ExceptionType={ExceptionType}.",
                    target.Code,
                    exception.GetType().Name);
            }
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                $"Business-unit migration failed for {failures.Count} target(s); no target fallback was used.");
        }

        return lastBusinessInspection
            ?? throw new InvalidOperationException("No business-unit migration target was processed.");
    }

    private async Task<MigrationLedgerInspection> ApplyBusinessMigrationsAsync(
        string connectionString,
        BusinessUnitDatabaseTarget? target,
        string? migrationRoleName,
        string? runtimeRoleName,
        CancellationToken cancellationToken)
    {
        return await ApplyTargetAsync(
            connectionString,
            migrationCatalog.GetMigrationFiles(),
            async (connection, token) =>
            {
                if (target is not null)
                {
                    await BusinessUnitDatabaseIdentity.BindOrVerifyAsync(connection, target, token);
                }
                return await new MigrationLedgerInspector(migrationCatalog).InspectAsync(connection, token);
            },
            target,
            migrationRoleName,
            runtimeRoleName,
            BusinessUnitDatabaseKind.Business,
            cancellationToken);
    }

    private async Task<MigrationLedgerInspection> ApplyDirectoryMigrationsAsync(
        BusinessUnitDatabaseTarget target,
        CancellationToken cancellationToken)
    {
        var directoryCatalog = new BusinessUnitDirectoryMigrationCatalog(migrationCatalog);
        var connectionString = connectionStringProvider.GetConnectionString(
            target,
            BusinessUnitConnectionPurpose.Migration);
        return await ApplyTargetAsync(
            connectionString,
            directoryCatalog.Catalog.GetMigrationFiles(),
            async (connection, token) =>
            {
                await BusinessUnitDatabaseIdentity.BindOrVerifyAsync(connection, target, token);
                return await directoryCatalog.InspectAsync(connection, token);
            },
            target,
            target.MigrationRoleName,
            target.RuntimeRoleName,
            BusinessUnitDatabaseKind.Directory,
            cancellationToken);
    }

    private async Task<MigrationLedgerInspection> ApplyTargetAsync(
        string connectionString,
        IReadOnlyList<string> migrationFiles,
        Func<NpgsqlConnection, CancellationToken, Task<MigrationLedgerInspection>> inspect,
        BusinessUnitDatabaseTarget? target,
        string? migrationRoleName,
        string? runtimeRoleName,
        BusinessUnitDatabaseKind databaseKind,
        CancellationToken cancellationToken)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using (var lockCommand = connection.CreateCommand())
        {
            lockCommand.CommandText = "select pg_advisory_lock(@lock_key);";
            lockCommand.Parameters.AddWithValue("lock_key", DatabaseRuntimePrivilegeManager.MaintenanceAdvisoryLockKey);
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            if (target is not null)
            {
                await BusinessUnitDatabaseIdentity.PreflightMigrationAsync(
                    connection,
                    target,
                    cancellationToken);
                await ThrowIfMigrationLedgerIsNotKnownPrefixAsync(
                    connection,
                    migrationFiles,
                    databaseKind,
                    cancellationToken);
            }

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    create table if not exists schema_migrations (
                        version text primary key,
                        applied_at_utc timestamptz not null default now()
                    );
                    """;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var migrationFile in migrationFiles)
            {
                var version = Path.GetFileNameWithoutExtension(migrationFile);
                if (await IsMigrationAppliedAsync(connection, version, cancellationToken)) continue;

                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                await using (var migrationCommand = connection.CreateCommand())
                {
                    migrationCommand.Transaction = transaction;
                    migrationCommand.CommandText = await File.ReadAllTextAsync(migrationFile, cancellationToken);
                    await migrationCommand.ExecuteNonQueryAsync(cancellationToken);
                }
                await using (var recordCommand = connection.CreateCommand())
                {
                    recordCommand.Transaction = transaction;
                    recordCommand.CommandText = "insert into schema_migrations (version) values (@version);";
                    recordCommand.Parameters.AddWithValue("version", version);
                    await recordCommand.ExecuteNonQueryAsync(cancellationToken);
                }
                await transaction.CommitAsync(cancellationToken);
                logger.LogInformation(
                    "Applied database migration {MigrationVersion} to {Target}.",
                    version,
                    target?.Code ?? "LEGACY");
            }

            var inspection = await inspect(connection, cancellationToken);
            if (!inspection.MigrationLedgerReady)
            {
                throw new InvalidOperationException(
                    $"Database migration verification failed: {inspection.Reason}.");
            }

            await runtimePrivilegeManager.ReconcileAfterMigrationAsync(
                connection,
                migrationRoleName,
                runtimeRoleName,
                cancellationToken,
                databaseKind);
            return inspection;
        }
        finally
        {
            try
            {
                await using var unlockCommand = connection.CreateCommand();
                unlockCommand.CommandText = "select pg_advisory_unlock(@lock_key);";
                unlockCommand.Parameters.AddWithValue("lock_key", DatabaseRuntimePrivilegeManager.MaintenanceAdvisoryLockKey);
                await unlockCommand.ExecuteNonQueryAsync(CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    "The database migration advisory lock could not be explicitly released; connection disposal will release it. ExceptionType={ExceptionType}.",
                    exception.GetType().Name);
            }
        }
    }

    private static async Task<bool> IsMigrationAppliedAsync(
        NpgsqlConnection connection,
        string version,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select exists (select 1 from schema_migrations where version = @version);";
        command.Parameters.AddWithValue("version", version);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task ThrowIfMigrationLedgerIsNotKnownPrefixAsync(
        NpgsqlConnection connection,
        IReadOnlyList<string> migrationFiles,
        BusinessUnitDatabaseKind databaseKind,
        CancellationToken cancellationToken)
    {
        await using (var existsCommand = connection.CreateCommand())
        {
            existsCommand.CommandText = "select to_regclass('public.schema_migrations') is not null;";
            if (await existsCommand.ExecuteScalarAsync(cancellationToken) is not true)
            {
                return;
            }
        }

        var appliedVersions = new HashSet<string>(StringComparer.Ordinal);
        await using (var ledgerCommand = connection.CreateCommand())
        {
            ledgerCommand.CommandText = "select version from schema_migrations;";
            await using var reader = await ledgerCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                appliedVersions.Add(reader.GetString(0));
            }
        }

        var expectedVersions = migrationFiles
            .Select(path => Path.GetFileNameWithoutExtension(path)
                ?? throw new InvalidOperationException("database_migration_version_missing"))
            .ToList();
        var expectedVersionSet = expectedVersions.ToHashSet(StringComparer.Ordinal);
        var approvedLegacy = MigrationLedgerCompatibilityPolicy.ApprovedLegacyMigrations.Single();
        var hasApprovedLegacy = databaseKind == BusinessUnitDatabaseKind.Business
            && appliedVersions.Contains(approvedLegacy.LegacyVersion);
        if (hasApprovedLegacy
            && !appliedVersions.Contains(approvedLegacy.CanonicalSuccessor))
        {
            throw new InvalidOperationException("migration_ledger_legacy_successor_missing");
        }

        var unexpectedVersions = appliedVersions
            .Where(version => !expectedVersionSet.Contains(version))
            .ToList();
        if (unexpectedVersions.Count > 0
            && (!hasApprovedLegacy
                || unexpectedVersions.Count != 1
                || !string.Equals(
                    unexpectedVersions[0],
                    approvedLegacy.LegacyVersion,
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("migration_ledger_unexpected");
        }

        var appliedCanonicalVersions = appliedVersions
            .Where(expectedVersionSet.Contains)
            .ToHashSet(StringComparer.Ordinal);
        var prefixLength = 0;
        while (prefixLength < expectedVersions.Count
               && appliedCanonicalVersions.Contains(expectedVersions[prefixLength]))
        {
            prefixLength++;
        }

        if (appliedCanonicalVersions.Count != prefixLength)
        {
            throw new InvalidOperationException("database_migration_ledger_not_known_prefix");
        }

        if (databaseKind == BusinessUnitDatabaseKind.Business
            && appliedCanonicalVersions.Contains(approvedLegacy.CanonicalSuccessor)
            && !await MigrationLedgerInspector.ProbeTeamsActivitySchemaAsync(
                connection,
                appliedCanonicalVersions.Contains(MigrationLedgerInspector.WebPushMigrationVersion),
                cancellationToken))
        {
            throw new InvalidOperationException(
                hasApprovedLegacy
                    ? "migration_ledger_legacy_schema_mismatch"
                    : "migration_ledger_schema_mismatch");
        }
    }
}
