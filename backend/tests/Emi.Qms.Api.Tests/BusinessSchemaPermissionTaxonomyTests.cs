using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class BusinessUnitIsolationTests
{
    private static readonly string[] RequiredOsanPermissionCodes =
    [
        "Project.Create",
        "Project.Delete",
        "Project.Read.All",
        "Project.Update",
        "manufacturing.update",
        "projects.read",
        "users.manage",
    ];

    [Fact]
    public async Task BusinessSchemaPermissionTaxonomy_RejectsUnknownPermissionAndUsedBusbarRoleWithRollback()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var databases = await IsolationDatabaseSet.CreateAsync(ct);
        var (catalog, _) = await PrepareCommonOsanTaxonomyAsync(databases, ct);
        var migration = await File.ReadAllTextAsync(
            catalog.GetBusinessMigrationFiles(BusinessUnitCodes.Osan)
                .Single(path => Path.GetFileName(path).StartsWith("0131_", StringComparison.Ordinal)), ct);
        var mutations = new (string Sql, string Error)[]
        {
            ("insert into permissions(id,code,name) values('30000000-0000-0000-0000-000000000099','custom.permission','Synthetic custom permission')",
                "osan_unexpected_permission_catalog"),
            ("update permissions set name='Changed permission name' where code='projects.read'",
                "osan_unexpected_permission_catalog"),
            ("update roles set name='Changed Busbar Role' where code='interior-busbar-manager'",
                "osan_unexpected_interior_busbar_role"),
            ($"""
                insert into qms_users(id,development_user_key,display_name,is_active)
                values('{Guid.NewGuid():D}','taxonomy-busbar-user','Synthetic Busbar User',true);
                insert into user_roles(user_id,role_id)
                select user_account.id,role.id from qms_users user_account cross join roles role
                where user_account.development_user_key='taxonomy-busbar-user'
                  and role.code='interior-busbar-manager';
                """, "osan_interior_busbar_role_in_use"),
            ("""
                insert into role_permissions(role_id,permission_id)
                select role.id,permission.id from roles role cross join permissions permission
                where role.code='interior-busbar-manager' and permission.code='projects.read';
                """, "osan_interior_busbar_role_in_use"),
        };

        await using var connection = await databases.OpenAsync(
            BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration, ct);
        foreach (var mutation in mutations)
        {
            await using var transaction = await connection.BeginTransactionAsync(ct);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "select set_config('emi_qms.business_schema_separation','OSAN',true)";
            await command.ExecuteNonQueryAsync(ct);
            command.CommandText = mutation.Sql;
            await command.ExecuteNonQueryAsync(ct);
            command.CommandText = migration;
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(ct));
            Assert.Equal("P0001", error.SqlState);
            Assert.Equal(mutation.Error, error.MessageText);
            await transaction.RollbackAsync(ct);

            Assert.Equal(35L, await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
                "select count(*) from permissions", ct));
            Assert.Equal(11L, await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
                "select count(*) from roles", ct));
            Assert.Equal(111L, await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
                "select count(*) from role_permissions", ct));
            Assert.Equal(1L, await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
                "select count(*) from roles where code='interior-busbar-manager'", ct));
            Assert.Equal(0L, await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
                "select count(*) from user_roles assignment join roles role on role.id=assignment.role_id " +
                "where role.code='interior-busbar-manager'", ct));
            Assert.Equal(0L, await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
                "select count(*) from role_permissions assignment join roles role on role.id=assignment.role_id " +
                "where role.code='interior-busbar-manager'", ct));
            Assert.Equal(130L, await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
                "select count(*) from schema_migrations", ct));
            Assert.Equal(209L, await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
                "select count(*) from information_schema.tables where table_schema='public' and table_type='BASE TABLE'", ct));
        }
    }

    [Fact]
    public async Task BusinessSchemaPermissionTaxonomy_PreservesRequiredAndCustomMappingsAndSeederCannotRestoreRemovedPermissions()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var databases = await IsolationDatabaseSet.CreateAsync(ct);
        var (catalog, runner) = await PrepareCommonOsanTaxonomyAsync(databases, ct);
        var retainedUserId = Guid.NewGuid();
        await databases.ExecuteAsync(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration, $"""
            insert into qms_users(id,development_user_key,display_name,is_active)
            values('{retainedUserId:D}','taxonomy-retained-user','Synthetic Retained User',true);
            insert into roles(id,code,name)
            values('{Guid.NewGuid():D}','taxonomy-custom-role','Synthetic Custom Role');
            insert into user_roles(user_id,role_id)
            select '{retainedUserId:D}',id from roles where code='taxonomy-custom-role';
            insert into role_permissions(role_id,permission_id)
            select role.id,permission.id from roles role cross join permissions permission
            where role.code='taxonomy-custom-role' and permission.code='users.manage';
            """, ct);
        var auditEventsBefore = await databases.ReadScalarAsync<long>(
            BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
            "select count(*) from audit_events", ct);

        await runner.ApplyAndVerifyAsync(BusinessUnitCodes.Osan, ct);

        Assert.Equal(RequiredOsanPermissionCodes, await databases.ReadColumnAsync(
            BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
            "select code from permissions order by code collate \"C\"", ct));
        Assert.Equal(30L, await databases.ReadScalarAsync<long>(
            BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
            "select count(*) from role_permissions", ct));
        Assert.True(await databases.ReadScalarAsync<bool>(
            BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
            "select exists(select 1 from role_permissions assignment " +
            "join roles role on role.id=assignment.role_id join permissions permission on permission.id=assignment.permission_id " +
            "where role.code='taxonomy-custom-role' and permission.code='users.manage')", ct));
        Assert.True(await databases.ReadScalarAsync<bool>(
            BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
            $"select exists(select 1 from user_roles assignment join roles role on role.id=assignment.role_id " +
            $"where assignment.user_id='{retainedUserId:D}' and role.code='taxonomy-custom-role')", ct));
        Assert.Equal(auditEventsBefore, await databases.ReadScalarAsync<long>(
            BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
            "select count(*) from audit_events", ct));

        var seedValues = new Dictionary<string, string?>(databases.ConfigurationValues,
            StringComparer.OrdinalIgnoreCase);
        seedValues.Remove("BusinessUnits:DevelopmentSeedUnits:0");
        seedValues.Remove("BusinessUnits:DevelopmentSeedUnits:1");
        seedValues["BusinessUnits:DevelopmentSeedUnits:0"] = BusinessUnitCodes.Osan;
        var seedConfiguration = new ConfigurationBuilder().AddInMemoryCollection(seedValues).Build();
        var seedProvider = new DatabaseConnectionStringProvider(seedConfiguration);
        var environment = new TestEnvironment(databases.RepositoryRoot);
        var seeder = new DevelopmentIdentitySeeder(seedProvider, seedConfiguration, environment,
            NullLogger<DevelopmentIdentitySeeder>.Instance, new MigrationLedgerInspector(catalog));
        await seeder.SeedAsync(ct);
        await seeder.SeedAsync(ct);

        Assert.Equal(RequiredOsanPermissionCodes, await databases.ReadColumnAsync(
            BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
            "select code from permissions order by code collate \"C\"", ct));
        Assert.True(await databases.ReadScalarAsync<bool>(
            BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
            "select exists(select 1 from role_permissions assignment " +
            "join roles role on role.id=assignment.role_id join permissions permission on permission.id=assignment.permission_id " +
            "where role.code='taxonomy-custom-role' and permission.code='users.manage')", ct));
        Assert.True(await databases.ReadScalarAsync<bool>(
            BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
            $"select exists(select 1 from user_roles assignment join roles role on role.id=assignment.role_id " +
            $"where assignment.user_id='{retainedUserId:D}' and role.code='taxonomy-custom-role')", ct));

        var identity = new DbIdentityStore(new OsanDatabase(seedProvider), seedConfiguration);
        var sales = Assert.IsType<UserAuthorizationProfile>(
            await identity.GetProfileByDevelopmentUserKeyAsync("dev-sales", ct));
        Assert.True(sales.HasPermission(QmsPermissions.ProjectRead));
        Assert.True(sales.HasPermission(QmsPermissions.ProjectReadAll));
        Assert.True(sales.HasPermission(QmsPermissions.ProjectCreate));
        Assert.False(sales.HasPermission(QmsPermissions.ManufacturingUpdate));
        var quality = Assert.IsType<UserAuthorizationProfile>(
            await identity.GetProfileByDevelopmentUserKeyAsync("dev-quality", ct));
        Assert.True(quality.HasPermission(QmsPermissions.ProjectRead));
        Assert.True(quality.HasPermission(QmsPermissions.ProjectReadAll));
        Assert.True(quality.HasPermission(QmsPermissions.ManufacturingUpdate));
        Assert.False(quality.HasPermission(QmsPermissions.ProjectCreate));
        var administrator = Assert.IsType<UserAuthorizationProfile>(
            await identity.GetProfileByDevelopmentUserKeyAsync("dev-admin", ct));
        Assert.Equal(RequiredOsanPermissionCodes, administrator.Permissions
            .Select(permission => permission.Code).OrderBy(code => code, StringComparer.Ordinal).ToArray());
        var design = Assert.IsType<UserAuthorizationProfile>(
            await identity.GetProfileByDevelopmentUserKeyAsync("dev-design", ct));
        Assert.False(design.HasPermission(QmsPermissions.UsersManage));
        Assert.False(design.HasPermission(QmsPermissions.ProjectCreate));
        Assert.False(design.HasPermission(QmsPermissions.ManufacturingUpdate));
        var retainedCustomRole = Assert.IsType<UserAuthorizationProfile>(
            await identity.GetProfileByDevelopmentUserKeyAsync("taxonomy-retained-user", ct));
        Assert.True(retainedCustomRole.HasPermission(QmsPermissions.UsersManage));
        Assert.False(retainedCustomRole.HasPermission(QmsPermissions.ProjectCreate));
    }

    private static async Task<(DatabaseMigrationCatalog Catalog, DatabaseMigrationRunner Runner)>
        PrepareCommonOsanTaxonomyAsync(
        IsolationDatabaseSet databases, CancellationToken ct)
    {
        var provider = new DatabaseConnectionStringProvider(databases.Configuration);
        var catalog = new DatabaseMigrationCatalog(new TestEnvironment(databases.RepositoryRoot));
        await BootstrapTargetsAsync(new DatabaseRoleBootstrapper(databases.Configuration,
            new DatabaseRuntimePrivilegeManager(), NullLogger<DatabaseRoleBootstrapper>.Instance), ct);
        await ApplyCommonSchemaBeforeSeparationAsync(databases, catalog, BusinessUnitCodes.Osan, ct);
        await using var connection = await databases.OpenAsync(
            BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration, ct);
        await BusinessUnitDatabaseIdentity.BindOrVerifyAsync(
            connection, databases.BusinessUnits.GetBusiness(BusinessUnitCodes.Osan), ct);
        var runner = new DatabaseMigrationRunner(provider, catalog, new DatabaseRuntimePrivilegeManager(),
            databases.Configuration, NullLogger<DatabaseMigrationRunner>.Instance);
        return (catalog, runner);
    }
}
