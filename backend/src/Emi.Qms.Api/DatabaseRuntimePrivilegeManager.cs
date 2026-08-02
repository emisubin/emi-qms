using Emi.Qms.Api.Security;
using Npgsql;

namespace Emi.Qms.Api;

public sealed class DatabaseRuntimePrivilegeManager
{
    public const long MaintenanceAdvisoryLockKey = 2026073101L;

    public async Task ConfigureBootstrapPrivilegesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string databaseName,
        string migrationRoleName,
        string runtimeRoleName,
        CancellationToken cancellationToken)
    {
        var database = QuoteIdentifier(databaseName);
        var migrator = QuoteIdentifier(migrationRoleName);
        var runtime = QuoteIdentifier(runtimeRoleName);

        await ExecuteAsync(
            connection,
            transaction,
            $"""
            revoke all privileges on database {database} from public;
            grant connect, temporary on database {database} to {migrator};
            grant connect on database {database} to {runtime};

            revoke create on schema public from public;
            grant usage, create on schema public to {migrator};
            revoke all privileges on schema public from {runtime};
            grant usage on schema public to {runtime};

            create extension if not exists "uuid-ossp";
            revoke execute on all functions in schema public from public;
            grant execute on all functions in schema public to {migrator}, {runtime};

            alter default privileges for role {migrator} in schema public
                revoke execute on functions from public;
            alter default privileges for role {migrator} in schema public
                grant select, insert, update, delete on tables to {runtime};
            alter default privileges for role {migrator} in schema public
                grant usage, select on sequences to {runtime};
            alter default privileges for role {migrator} in schema public
                grant execute on functions to {runtime};
            """,
            cancellationToken);
    }

    public async Task ReconcileAfterMigrationAsync(
        NpgsqlConnection connection,
        string? configuredMigrationRoleName,
        string? configuredRuntimeRoleName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuredMigrationRoleName)
            && string.IsNullOrWhiteSpace(configuredRuntimeRoleName))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(configuredMigrationRoleName)
            || string.IsNullOrWhiteSpace(configuredRuntimeRoleName)
            || string.Equals(
                configuredMigrationRoleName,
                configuredRuntimeRoleName,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Distinct migration and runtime database roles are required.");
        }

        var currentRole = await ReadScalarAsync(
            connection,
            "select current_user;",
            cancellationToken);
        if (!string.Equals(
            currentRole,
            configuredMigrationRoleName,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Runtime database privileges can only be reconciled by the migration role.");
        }

        var runtime = QuoteIdentifier(configuredRuntimeRoleName);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await ExecuteAsync(
            connection,
            transaction,
            $"""
            revoke all privileges on all tables in schema public from {runtime};
            grant select, insert, update, delete on all tables in schema public to {runtime};
            revoke insert, update, delete, truncate, references, trigger
                on table public.schema_migrations from {runtime};
            grant select on table public.schema_migrations to {runtime};

            revoke all privileges on all sequences in schema public from {runtime};
            grant usage, select on all sequences in schema public to {runtime};

            alter default privileges in schema public
                revoke execute on functions from public;
            alter default privileges in schema public
                grant select, insert, update, delete on tables to {runtime};
            alter default privileges in schema public
                grant usage, select on sequences to {runtime};
            alter default privileges in schema public
                grant execute on functions to {runtime};
            """,
            cancellationToken);

        var functionGrantStatements = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select format(
                    'revoke all privileges on function %s from %I; grant execute on function %s to %I;',
                    function_row.oid::regprocedure,
                    @runtime_role,
                    function_row.oid::regprocedure,
                    @runtime_role)
                from pg_proc function_row
                join pg_namespace namespace_row on namespace_row.oid = function_row.pronamespace
                where namespace_row.nspname = 'public'
                  and function_row.proowner = current_user::regrole;
                """;
            command.Parameters.AddWithValue(
                "runtime_role",
                configuredRuntimeRoleName);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                functionGrantStatements.Add(reader.GetString(0));
            }
        }

        foreach (var statement in functionGrantStatements)
        {
            await ExecuteAsync(connection, transaction, statement, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string?> ReadScalarAsync(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (await command.ExecuteScalarAsync(cancellationToken))?.ToString();
    }

    private static string QuoteIdentifier(string value)
    {
        return new NpgsqlCommandBuilder().QuoteIdentifier(value);
    }
}
