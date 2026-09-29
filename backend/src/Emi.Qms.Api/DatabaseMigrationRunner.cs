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

    public async Task ApplyAsync(string targetCode, CancellationToken cancellationToken)
    {
        _ = await ApplyAndVerifyAsync(targetCode, cancellationToken);
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

        throw new InvalidOperationException("business_unit_migration_target_required");
    }

    public async Task<MigrationLedgerInspection> ApplyAndVerifyAsync(
        string targetCode,
        CancellationToken cancellationToken)
    {
        if (ReviewSafeMode.IsEnabled(configuration))
        {
            throw new InvalidOperationException("Database migrations are disabled in review-safe UAT mode.");
        }

        var businessUnits = connectionStringProvider.BusinessUnits;
        if (!businessUnits.Enabled)
        {
            throw new InvalidOperationException("business_unit_migration_target_not_enabled");
        }
        businessUnits.ThrowIfInvalid();
        var normalizedTargetCode = targetCode.Trim().ToUpperInvariant();
        var target = businessUnits.AllTargets()
            .SingleOrDefault(candidate => string.Equals(
                candidate.Code,
                normalizedTargetCode,
                StringComparison.Ordinal))
            ?? throw new InvalidOperationException("business_unit_migration_target_invalid");
        var operationErrors = businessUnits
            .ValidateOperationConnections(
                configuration,
                BusinessUnitConnectionPurpose.Migration,
                [target])
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (operationErrors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Business-unit migration configuration is invalid ({operationErrors.Count} validation error(s)).");
        }

        try
        {
            return target.Kind == BusinessUnitDatabaseKind.Directory
                ? await ApplyDirectoryMigrationsAsync(target, cancellationToken)
                : await ApplyBusinessMigrationsAsync(
                    connectionStringProvider.GetConnectionString(
                        target,
                        BusinessUnitConnectionPurpose.Migration),
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
            logger.LogError(
                "Database migration target failed. Target={Target} ExceptionType={ExceptionType}.",
                target.Code,
                exception.GetType().Name);
            throw;
        }
    }

    private async Task<MigrationLedgerInspection> ApplyBusinessMigrationsAsync(
        string connectionString,
        BusinessUnitDatabaseTarget? target,
        string? migrationRoleName,
        string? runtimeRoleName,
        CancellationToken cancellationToken)
    {
        var migrationFiles = target is null
            ? migrationCatalog.GetMigrationFiles()
            : migrationCatalog.GetMigrationFiles(target.Code);
        int? commonMigrationCount = target is null
            ? null
            : migrationCatalog.GetCommonMigrationFiles().Count;
        return await ApplyTargetAsync(
            connectionString,
            migrationFiles,
            async (connection, token) =>
            {
                if (target is not null)
                {
                    await BusinessUnitDatabaseIdentity.BindOrVerifyAsync(connection, target, token);
                }
                var inspector = new MigrationLedgerInspector(migrationCatalog);
                return target is null
                    ? await inspector.InspectAsync(connection, token)
                    : await inspector.InspectAsync(connection, target.Code, token);
            },
            target,
            migrationRoleName,
            runtimeRoleName,
            BusinessUnitDatabaseKind.Business,
            commonMigrationCount,
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
            bindTargetAtMigrationIndex: null,
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
        int? bindTargetAtMigrationIndex,
        CancellationToken cancellationToken)
    {
        var separationApproval = configuration["Database:BusinessSchemaSeparationApproved"];
        if (separationApproval is not null && !bool.TryParse(separationApproval, out _))
            throw new InvalidOperationException("business_schema_approval_invalid");
        var separationApproved = bool.TryParse(separationApproval, out var approved) && approved;
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

            for (var migrationIndex = 0; migrationIndex < migrationFiles.Count; migrationIndex += 1)
            {
                if (bindTargetAtMigrationIndex == migrationIndex && target is not null)
                {
                    await BusinessUnitDatabaseIdentity.BindOrVerifyAsync(
                        connection,
                        target,
                        cancellationToken);
                }

                var migrationFile = migrationFiles[migrationIndex];
                var version = Path.GetFileNameWithoutExtension(migrationFile);
                if (await IsMigrationAppliedAsync(connection, version, cancellationToken)) continue;

                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                if (target?.Kind == BusinessUnitDatabaseKind.Business
                    && version == $"0131_{target.Code.ToLowerInvariant()}_business_schema")
                {
                    // Approval belongs to this exact migration transaction. Override
                    // any stale connection/session option, including on denial.
                    await using var consent = connection.CreateCommand();
                    consent.Transaction = transaction;
                    consent.CommandText = "select set_config('emi_qms.business_schema_separation', @target, true);";
                    consent.Parameters.AddWithValue("target", separationApproved ? target.Code : string.Empty);
                    await consent.ExecuteNonQueryAsync(cancellationToken);
                }
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
                databaseKind,
                target?.Code);
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
