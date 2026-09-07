using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.ReviewSafe;
using Npgsql;

namespace Emi.Qms.Api;

public sealed class DatabaseRoleBootstrapper(
    IConfiguration configuration,
    DatabaseRuntimePrivilegeManager privilegeManager,
    ILogger<DatabaseRoleBootstrapper> logger)
{
    public async Task BootstrapAsync(CancellationToken cancellationToken)
    {
        if (ReviewSafeMode.IsEnabled(configuration))
        {
            throw new InvalidOperationException("Database role bootstrap is disabled in review-safe UAT mode.");
        }

        var businessUnits = BusinessUnitConfiguration.Read(configuration);
        if (!businessUnits.Enabled)
        {
            await BootstrapLegacyAsync(cancellationToken);
            return;
        }

        businessUnits.ThrowIfInvalid();
        var purposes = new[]
        {
            BusinessUnitConnectionPurpose.Runtime,
            BusinessUnitConnectionPurpose.Migration,
            BusinessUnitConnectionPurpose.Administrator
        };
        var errors = purposes
            .SelectMany(purpose => businessUnits.ValidateOperationConnections(configuration, purpose))
            .Concat(businessUnits.ValidateSameServerAcrossPurposes(configuration, purposes))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Business-unit database role bootstrap configuration is invalid ({errors.Count} validation error(s)).");
        }

        var targets = businessUnits.AllTargets();
        var roleCredentials = targets
            .SelectMany(target => new[]
            {
                ReadRoleCredential(target, BusinessUnitConnectionPurpose.Migration),
                ReadRoleCredential(target, BusinessUnitConnectionPurpose.Runtime)
            })
            .ToList();
        var allBoundedRoleNames = roleCredentials
            .Select(credential => credential.RoleName)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Roles are cluster-scoped, so create every bounded role before revoking its
        // access to databases owned by another target.
        var roleAdministrator = ReadConnection(targets[0], BusinessUnitConnectionPurpose.Administrator);
        await WithMaintenanceLockAsync(
            roleAdministrator.ConnectionString,
            async (connection, transaction) =>
            {
                await ThrowIfBoundedRoleMembershipExistsAsync(
                    connection,
                    transaction,
                    allBoundedRoleNames,
                    cancellationToken);
                foreach (var credential in roleCredentials)
                {
                    await EnsureLoginRoleAsync(
                        connection,
                        transaction,
                        credential.RoleName,
                        credential.Password,
                        cancellationToken);
                }
            },
            cancellationToken);

        var failures = new List<string>();
        foreach (var target in targets)
        {
            try
            {
                var administrator = ReadConnection(target, BusinessUnitConnectionPurpose.Administrator);
                await WithMaintenanceLockAsync(
                    administrator.ConnectionString,
                    async (connection, transaction) =>
                    {
                        await privilegeManager.ConfigureBootstrapPrivilegesAsync(
                            connection,
                            transaction,
                            target.ExpectedDatabaseName,
                            target.MigrationRoleName,
                            target.RuntimeRoleName,
                            target.Kind,
                            allBoundedRoleNames,
                            cancellationToken);
                    },
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
                    "Database role bootstrap target failed. Target={Target} ExceptionType={ExceptionType}.",
                    target.Code,
                    exception.GetType().Name);
            }
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                $"Business-unit database role bootstrap failed for {failures.Count} target(s); no target fallback was used.");
        }

        logger.LogInformation(
            "Database roles were bootstrapped for {TargetCount} isolated database targets.",
            targets.Count);
    }

    private static async Task ThrowIfBoundedRoleMembershipExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<string> boundedRoleNames,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists (
                select 1
                from pg_auth_members membership
                join pg_roles member_role on member_role.oid = membership.member
                where member_role.rolname = any(@bounded_role_names));
            """;
        command.Parameters.AddWithValue("bounded_role_names", boundedRoleNames.ToArray());
        if (await command.ExecuteScalarAsync(cancellationToken) is true)
        {
            throw new InvalidOperationException(
                "Configured bounded database roles must not participate in PostgreSQL role memberships.");
        }
    }

    private async Task BootstrapLegacyAsync(CancellationToken cancellationToken)
    {
        var administrator = RequiredConnection("QmsDatabaseAdmin");
        var migrator = RequiredConnection("QmsDatabaseMigration");
        var runtime = RequiredConnection("QmsDatabaseRuntime");
        var administratorDatabase = RequiredValue(administrator.Database, "administrator database");
        var migrationRoleName = RequiredValue(migrator.Username, "migration username");
        var migrationPassword = RequiredValue(migrator.Password, "migration password");
        var runtimeRoleName = RequiredValue(runtime.Username, "runtime username");
        var runtimePassword = RequiredValue(runtime.Password, "runtime password");

        await WithMaintenanceLockAsync(
            administrator.ConnectionString,
            async (connection, transaction) =>
            {
                await EnsureLoginRoleAsync(
                    connection,
                    transaction,
                    migrationRoleName,
                    migrationPassword,
                    cancellationToken);
                await EnsureLoginRoleAsync(
                    connection,
                    transaction,
                    runtimeRoleName,
                    runtimePassword,
                    cancellationToken);
                await privilegeManager.ConfigureBootstrapPrivilegesAsync(
                    connection,
                    transaction,
                    administratorDatabase,
                    migrationRoleName,
                    runtimeRoleName,
                    cancellationToken);
            },
            cancellationToken);

        logger.LogInformation("Database runtime and migration roles were bootstrapped with bounded privileges.");
    }

    private async Task WithMaintenanceLockAsync(
        string connectionString,
        Func<NpgsqlConnection, NpgsqlTransaction, Task> action,
        CancellationToken cancellationToken)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using (var lockCommand = connection.CreateCommand())
        {
            lockCommand.CommandText = "select pg_advisory_lock(@lock_key);";
            lockCommand.Parameters.AddWithValue(
                "lock_key",
                DatabaseRuntimePrivilegeManager.MaintenanceAdvisoryLockKey);
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await action(connection, transaction);
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            try
            {
                await using var unlockCommand = connection.CreateCommand();
                unlockCommand.CommandText = "select pg_advisory_unlock(@lock_key);";
                unlockCommand.Parameters.AddWithValue(
                    "lock_key",
                    DatabaseRuntimePrivilegeManager.MaintenanceAdvisoryLockKey);
                await unlockCommand.ExecuteNonQueryAsync(CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    "The database role bootstrap lock could not be explicitly released ({ExceptionType}); connection disposal will release it.",
                    exception.GetType().Name);
            }
        }
    }

    private (string RoleName, string Password) ReadRoleCredential(
        BusinessUnitDatabaseTarget target,
        BusinessUnitConnectionPurpose purpose)
    {
        var builder = ReadConnection(target, purpose);
        return (
            RequiredValue(builder.Username, $"{target.Code} {purpose} username"),
            RequiredValue(builder.Password, $"{target.Code} {purpose} password"));
    }

    private NpgsqlConnectionStringBuilder ReadConnection(
        BusinessUnitDatabaseTarget target,
        BusinessUnitConnectionPurpose purpose)
    {
        var name = purpose switch
        {
            BusinessUnitConnectionPurpose.Runtime => target.RuntimeConnectionName,
            BusinessUnitConnectionPurpose.Migration => target.MigrationConnectionName,
            BusinessUnitConnectionPurpose.Administrator => target.AdministratorConnectionName,
            _ => throw new ArgumentOutOfRangeException(nameof(purpose))
        };
        var builder = RequiredConnection(name);
        if (!string.Equals(builder.Database, target.ExpectedDatabaseName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Database role bootstrap target identity is invalid.");
        }
        return builder;
    }

    private NpgsqlConnectionStringBuilder RequiredConnection(string name)
    {
        var connectionString = configuration.GetConnectionString(name);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"The {name} connection is required for database role bootstrap.");
        }

        return new NpgsqlConnectionStringBuilder(connectionString);
    }

    private static string RequiredValue(string? value, string label)
    {
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"The {label} is required for database role bootstrap.");
    }

    private static async Task EnsureLoginRoleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string roleName,
        string password,
        CancellationToken cancellationToken)
    {
        bool exists;
        await using (var existsCommand = connection.CreateCommand())
        {
            existsCommand.Transaction = transaction;
            existsCommand.CommandText = "select exists(select 1 from pg_roles where rolname = @role_name);";
            existsCommand.Parameters.AddWithValue("role_name", roleName);
            exists = (bool)(await existsCommand.ExecuteScalarAsync(cancellationToken) ?? false);
        }

        string statement;
        await using (var formatCommand = connection.CreateCommand())
        {
            formatCommand.Transaction = transaction;
            formatCommand.CommandText = exists
                ? "select format('alter role %I with login inherit nosuperuser nocreatedb nocreaterole noreplication nobypassrls password %L', @role_name, @password);"
                : "select format('create role %I with login inherit nosuperuser nocreatedb nocreaterole noreplication nobypassrls password %L', @role_name, @password);";
            formatCommand.Parameters.AddWithValue("role_name", roleName);
            formatCommand.Parameters.AddWithValue("password", password);
            statement = (string)(await formatCommand.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("Database role command generation failed."));
        }

        await using var roleCommand = connection.CreateCommand();
        roleCommand.Transaction = transaction;
        roleCommand.CommandText = statement;
        await roleCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
