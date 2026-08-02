using Emi.Qms.Api.ReviewSafe;
using Emi.Qms.Api.Security;
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

        var administrator = RequiredConnection("QmsDatabaseAdmin");
        var migrator = RequiredConnection("QmsDatabaseMigration");
        var runtime = RequiredConnection("QmsDatabaseRuntime");
        var administratorDatabase = RequiredValue(administrator.Database, "administrator database");
        var migrationRoleName = RequiredValue(migrator.Username, "migration username");
        var migrationPassword = RequiredValue(migrator.Password, "migration password");
        var runtimeRoleName = RequiredValue(runtime.Username, "runtime username");
        var runtimePassword = RequiredValue(runtime.Password, "runtime password");

        await using var dataSource = NpgsqlDataSource.Create(administrator.ConnectionString);
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
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation("Database runtime and migration roles were bootstrapped with bounded privileges.");
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
