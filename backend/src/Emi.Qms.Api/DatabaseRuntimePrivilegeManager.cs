using Emi.Qms.Api.BusinessUnits;
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
        await ConfigureBootstrapPrivilegesAsync(
            connection,
            transaction,
            databaseName,
            migrationRoleName,
            runtimeRoleName,
            BusinessUnitDatabaseKind.Business,
            [],
            cancellationToken);
    }

    public async Task ConfigureBootstrapPrivilegesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string databaseName,
        string migrationRoleName,
        string runtimeRoleName,
        BusinessUnitDatabaseKind databaseKind,
        IReadOnlyCollection<string> deniedRoleNames,
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

        if (databaseKind == BusinessUnitDatabaseKind.Business)
        {
            await ExecuteAsync(
                connection,
                transaction,
                "create extension if not exists \"uuid-ossp\";",
                cancellationToken);
        }

        foreach (var deniedRoleName in deniedRoleNames
                     .Where(role => !string.Equals(role, migrationRoleName, StringComparison.Ordinal)
                                    && !string.Equals(role, runtimeRoleName, StringComparison.Ordinal))
                     .Distinct(StringComparer.Ordinal))
        {
            var deniedRole = QuoteIdentifier(deniedRoleName);
            await ExecuteAsync(
                connection,
                transaction,
                $"revoke connect, temporary on database {database} from {deniedRole};",
                cancellationToken);
        }
    }

    public async Task ReconcileAfterMigrationAsync(
        NpgsqlConnection connection,
        string? configuredMigrationRoleName,
        string? configuredRuntimeRoleName,
        CancellationToken cancellationToken,
        BusinessUnitDatabaseKind databaseKind = BusinessUnitDatabaseKind.Business)
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

        var objectGrants = databaseKind == BusinessUnitDatabaseKind.Directory
            ? $"""
              revoke all privileges on all tables in schema public from {runtime};
              grant select on all tables in schema public to {runtime};

                  revoke insert, update, delete, truncate, references, trigger
                  on table public.schema_migrations, public.qms_database_identity,
                      public.directory_business_units, public.directory_identities,
                      public.directory_business_unit_memberships, public.directory_overall_administrators,
                      public.directory_membership_audit_events, public.directory_user_access_operations
                  from {runtime};
              grant select
                  on table public.schema_migrations, public.qms_database_identity,
                      public.directory_business_units, public.directory_identities,
                      public.directory_business_unit_memberships, public.directory_overall_administrators,
                      public.directory_membership_audit_events, public.directory_user_access_operations
                  to {runtime};

              revoke all privileges on all sequences in schema public from {runtime};
              """
            : $"""
            revoke all privileges on all tables in schema public from {runtime};
            grant select, insert, update, delete on all tables in schema public to {runtime};
            revoke insert, update, delete, truncate, references, trigger
                on table public.schema_migrations from {runtime};
            grant select on table public.schema_migrations to {runtime};

            revoke insert, update, delete, truncate, references, trigger
                on table public.audit_coverage_state, public.audit_events, public.audit_event_changes,
                    public.site_access_coverage_state, public.site_access_sessions
                from {runtime};
            grant select on table public.audit_coverage_state, public.audit_events, public.audit_event_changes,
                    public.site_access_coverage_state, public.site_access_sessions
                to {runtime};

            revoke all privileges on all sequences in schema public from {runtime};
            grant usage, select on all sequences in schema public to {runtime};
            """;

        await ExecuteAsync(
            connection,
            transaction,
            $"""
            {objectGrants}

            alter default privileges in schema public
                revoke execute on functions from public;
            alter default privileges in schema public
                {(databaseKind == BusinessUnitDatabaseKind.Directory
                    ? $"grant select on tables to {runtime};"
                    : $"grant select, insert, update, delete on tables to {runtime};")}
            alter default privileges in schema public
                {(databaseKind == BusinessUnitDatabaseKind.Directory
                    ? $"revoke all privileges on sequences from {runtime};"
                    : $"grant usage, select on sequences to {runtime};")}
            alter default privileges in schema public
                grant execute on functions to {runtime};
            """,
            cancellationToken);

        if (databaseKind == BusinessUnitDatabaseKind.Business)
        {
            await ExecuteAsync(
                connection,
                transaction,
                $"""
                do $database_identity_privileges$
                begin
                    if to_regclass('public.qms_database_identity') is not null then
                        execute 'revoke insert, update, delete, truncate, references, trigger '
                            || 'on table public.qms_database_identity from {runtime}';
                        execute 'grant select on table public.qms_database_identity to {runtime}';
                    end if;
                end
                $database_identity_privileges$;
                """,
                cancellationToken);
        }

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
