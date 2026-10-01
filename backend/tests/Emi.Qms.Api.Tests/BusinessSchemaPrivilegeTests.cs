using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class BusinessUnitIsolationTests
{
    [Fact]
    public async Task DirectoryDefaultPrivileges_KeepFutureMigratorObjectsReadOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var databases = await IsolationDatabaseSet.CreateAsync(ct);
        var privilegeManager = new DatabaseRuntimePrivilegeManager();
        var bootstrapper = new DatabaseRoleBootstrapper(
            databases.Configuration,
            privilegeManager,
            NullLogger<DatabaseRoleBootstrapper>.Instance);
        await BootstrapTargetsAsync(bootstrapper, ct);

        var directory = Assert.IsType<BusinessUnitDatabaseTarget>(databases.BusinessUnits.Directory);
        var unrelatedRuntimeRole = databases.BusinessUnits
            .GetBusiness(BusinessUnitCodes.Cheongju)
            .RuntimeRoleName;
        await CreateDirectoryPrivilegeProbeAsync(databases, "bootstrap_future", ct);
        await AssertDirectoryPrivilegeProbeAsync(
            databases, directory.RuntimeRoleName, unrelatedRuntimeRole, "bootstrap_future", ct);
        await databases.ExecuteAsync("DIRECTORY", BusinessUnitConnectionPurpose.Migration,
            "drop table bootstrap_future_table; drop sequence bootstrap_future_sequence; " +
            "drop function bootstrap_future_function();", ct);

        var catalog = new DatabaseMigrationCatalog(new TestEnvironment(databases.RepositoryRoot));
        var runner = new DatabaseMigrationRunner(
            new DatabaseConnectionStringProvider(databases.Configuration),
            catalog,
            privilegeManager,
            databases.Configuration,
            NullLogger<DatabaseMigrationRunner>.Instance);
        await runner.ApplyAndVerifyAsync("DIRECTORY", ct);

        var runtime = QuoteTestIdentifier(directory.RuntimeRoleName);
        await databases.ExecuteAsync("DIRECTORY", BusinessUnitConnectionPurpose.Migration, $"""
            alter default privileges
                grant insert, update, delete on tables to {runtime};
            alter default privileges in schema public
                grant insert, update, delete on tables to {runtime};
            alter default privileges
                grant usage, select on sequences to {runtime};
            alter default privileges in schema public
                grant usage, select on sequences to {runtime};
            alter default privileges
                grant execute on functions to public;
            alter default privileges in schema public
                grant execute on functions to public;
            """, ct);

        await using (var migration = await databases.OpenAsync(
                         "DIRECTORY", BusinessUnitConnectionPurpose.Migration, ct))
        {
            await privilegeManager.ReconcileAfterMigrationAsync(
                migration,
                directory.MigrationRoleName,
                directory.RuntimeRoleName,
                ct,
                BusinessUnitDatabaseKind.Directory);
        }

        await CreateDirectoryPrivilegeProbeAsync(databases, "reconciled_future", ct);
        await AssertDirectoryPrivilegeProbeAsync(
            databases, directory.RuntimeRoleName, unrelatedRuntimeRole, "reconciled_future", ct);
    }

    private static async Task CreateDirectoryPrivilegeProbeAsync(
        IsolationDatabaseSet databases,
        string prefix,
        CancellationToken ct)
    {
        await databases.ExecuteAsync("DIRECTORY", BusinessUnitConnectionPurpose.Migration, $"""
            create table {prefix}_table(id integer primary key);
            insert into {prefix}_table(id) values(1);
            create sequence {prefix}_sequence;
            create function {prefix}_function() returns integer language sql as 'select 1';
            """, ct);
    }

    private static async Task AssertDirectoryPrivilegeProbeAsync(
        IsolationDatabaseSet databases,
        string runtimeRoleName,
        string unrelatedRuntimeRoleName,
        string prefix,
        CancellationToken ct)
    {
        await using (var migration = await databases.OpenAsync(
                         "DIRECTORY", BusinessUnitConnectionPurpose.Migration, ct))
        await using (var privileges = migration.CreateCommand())
        {
            privileges.CommandText = $"""
                select has_table_privilege(@runtime_role, 'public.{prefix}_table', 'select')
                   and not has_table_privilege(@runtime_role, 'public.{prefix}_table', 'insert')
                   and not has_table_privilege(@runtime_role, 'public.{prefix}_table', 'update')
                   and not has_table_privilege(@runtime_role, 'public.{prefix}_table', 'delete')
                   and not has_sequence_privilege(@runtime_role, 'public.{prefix}_sequence', 'usage')
                   and not has_sequence_privilege(@runtime_role, 'public.{prefix}_sequence', 'select')
                   and has_function_privilege(@runtime_role, 'public.{prefix}_function()', 'execute')
                   and not has_function_privilege(@unrelated_runtime_role, 'public.{prefix}_function()', 'execute')
                   and not exists (
                       select 1
                       from pg_proc function_row
                       cross join lateral aclexplode(
                           coalesce(function_row.proacl, acldefault('f', function_row.proowner))) privilege
                       where function_row.oid = 'public.{prefix}_function()'::regprocedure
                         and privilege.grantee = 0
                         and privilege.privilege_type = 'EXECUTE');
                """;
            privileges.Parameters.AddWithValue("runtime_role", runtimeRoleName);
            privileges.Parameters.AddWithValue("unrelated_runtime_role", unrelatedRuntimeRoleName);
            Assert.True(await privileges.ExecuteScalarAsync(ct) is true);
        }

        await using var runtime = await databases.OpenAsync(
            "DIRECTORY", BusinessUnitConnectionPurpose.Runtime, ct);
        await using (var read = runtime.CreateCommand())
        {
            read.CommandText = $"select count(*) from {prefix}_table;";
            Assert.Equal(1L, Convert.ToInt64(await read.ExecuteScalarAsync(ct)));
        }
        await using (var write = runtime.CreateCommand())
        {
            write.CommandText = $"insert into {prefix}_table(id) values(2);";
            var error = await Assert.ThrowsAsync<PostgresException>(() => write.ExecuteNonQueryAsync(ct));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        }
        await using (var sequence = runtime.CreateCommand())
        {
            sequence.CommandText = $"select nextval('public.{prefix}_sequence');";
            var error = await Assert.ThrowsAsync<PostgresException>(() => sequence.ExecuteScalarAsync(ct));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        }
        await using (var function = runtime.CreateCommand())
        {
            function.CommandText = $"select {prefix}_function();";
            Assert.Equal(1, Convert.ToInt32(await function.ExecuteScalarAsync(ct)));
        }
    }

    private static string QuoteTestIdentifier(string value) =>
        new NpgsqlCommandBuilder().QuoteIdentifier(value);
}
