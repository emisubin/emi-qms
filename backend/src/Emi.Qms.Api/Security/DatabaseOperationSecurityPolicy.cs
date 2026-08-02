using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Emi.Qms.Api.Security;

public enum DatabaseOperationMode
{
    Migration,
    RoleBootstrap
}

public static class DatabaseOperationSecurityPolicy
{
    public const string MigrationRoleName = "pms_migrator";
    public const string RuntimeRoleName = "pms_app";
    private const int MinimumManagedPasswordLength = 32;

    public static void ThrowIfInvalid(
        IHostEnvironment environment,
        IConfiguration configuration,
        DatabaseOperationMode mode)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        var errors = Evaluate(configuration, mode);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Production database operation configuration is incomplete: {string.Join(" ", errors)}");
        }
    }

    public static IReadOnlyList<string> Evaluate(
        IConfiguration configuration,
        DatabaseOperationMode mode)
    {
        var errors = new List<string>();

        if (mode == DatabaseOperationMode.Migration)
        {
            var migration = ParseAndValidate(
                configuration.GetConnectionString("QmsDatabase"),
                "migration",
                MigrationRoleName,
                errors);
            if (migration is not null
                && (!string.Equals(
                        configuration["Database:MigrationRoleName"],
                        MigrationRoleName,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        configuration["Database:RuntimeRoleName"],
                        RuntimeRoleName,
                        StringComparison.Ordinal)))
            {
                errors.Add("Migration must use and reconcile the fixed least-privileged database roles.");
            }

            return errors;
        }

        var administrator = ParseAndValidate(
            configuration.GetConnectionString("QmsDatabaseAdmin"),
            "administrator",
            expectedUsername: null,
            errors);
        var migrator = ParseAndValidate(
            configuration.GetConnectionString("QmsDatabaseMigration"),
            "migration",
            MigrationRoleName,
            errors);
        var runtime = ParseAndValidate(
            configuration.GetConnectionString("QmsDatabaseRuntime"),
            "runtime",
            RuntimeRoleName,
            errors);

        if (administrator is null || migrator is null || runtime is null)
        {
            return errors;
        }

        if (!SameDatabaseEndpoint(administrator, migrator)
            || !SameDatabaseEndpoint(administrator, runtime))
        {
            errors.Add("Administrator, migration, and runtime connections must target the same database endpoint.");
        }

        if (string.Equals(administrator.Username, migrator.Username, StringComparison.Ordinal)
            || string.Equals(administrator.Username, runtime.Username, StringComparison.Ordinal))
        {
            errors.Add("The database administrator must be distinct from migration and runtime roles.");
        }

        if (string.Equals(administrator.Password, migrator.Password, StringComparison.Ordinal)
            || string.Equals(administrator.Password, runtime.Password, StringComparison.Ordinal)
            || string.Equals(migrator.Password, runtime.Password, StringComparison.Ordinal))
        {
            errors.Add("Administrator, migration, and runtime database passwords must be distinct.");
        }

        return errors;
    }

    private static NpgsqlConnectionStringBuilder? ParseAndValidate(
        string? connectionString,
        string label,
        string? expectedUsername,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            errors.Add($"The {label} database connection string is required.");
            return null;
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(builder.Host)
                || string.IsNullOrWhiteSpace(builder.Database)
                || string.IsNullOrWhiteSpace(builder.Username)
                || string.IsNullOrWhiteSpace(builder.Password))
            {
                errors.Add($"The {label} database connection must include host, database, username, and password.");
            }

            if (builder.SslMode != SslMode.VerifyFull)
            {
                errors.Add($"The {label} database connection must use SSL Mode=VerifyFull with certificate validation.");
            }

            if ((builder.Password?.Length ?? 0) < MinimumManagedPasswordLength)
            {
                errors.Add($"The {label} database password must contain at least {MinimumManagedPasswordLength} characters.");
            }

            if (expectedUsername is not null
                && !string.Equals(builder.Username, expectedUsername, StringComparison.Ordinal))
            {
                errors.Add($"The {label} database connection must use the fixed least-privileged role.");
            }

            return builder;
        }
        catch (ArgumentException)
        {
            errors.Add($"The {label} database connection string is invalid.");
            return null;
        }
    }

    private static bool SameDatabaseEndpoint(
        NpgsqlConnectionStringBuilder left,
        NpgsqlConnectionStringBuilder right)
    {
        return string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase)
            && left.Port == right.Port
            && string.Equals(left.Database, right.Database, StringComparison.Ordinal);
    }
}
