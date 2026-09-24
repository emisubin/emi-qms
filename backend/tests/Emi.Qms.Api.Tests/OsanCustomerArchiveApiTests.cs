using System.Net;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class BusinessUnitIsolationTests
{
    [Fact]
    public async Task CustomerArchive_RequiresOsanAdministratorBeforeWriting()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var databases = await IsolationDatabaseSet.CreateAsync(ct);
        var provider = new DatabaseConnectionStringProvider(databases.Configuration);
        var environment = new TestEnvironment(databases.RepositoryRoot);
        var catalog = new DatabaseMigrationCatalog(environment);
        await new DatabaseRoleBootstrapper(databases.Configuration, new DatabaseRuntimePrivilegeManager(),
            NullLogger<DatabaseRoleBootstrapper>.Instance).BootstrapAsync(ct);
        await new DatabaseMigrationRunner(provider, catalog, new DatabaseRuntimePrivilegeManager(),
            databases.Configuration, NullLogger<DatabaseMigrationRunner>.Instance).ApplyAndVerifyAsync(ct);
        await new DevelopmentIdentitySeeder(provider, databases.Configuration, environment,
            NullLogger<DevelopmentIdentitySeeder>.Instance, new MigrationLedgerInspector(catalog)).SeedAsync(ct);
        databases.ConfigurationValues["DevelopmentData:SeedEnabled"] = "false";
        await databases.ExecuteAsync("DIRECTORY", BusinessUnitConnectionPurpose.Migration, $"""
            insert into directory_identities(user_id,auth_provider,external_subject,display_name,is_active)
            values('{AdminUserId}','Dev','dev-admin','Admin',true),('{SalesUserId}','Dev','dev-sales','Sales',true),('50000000-0000-0000-0000-000000000005','Dev','dev-quality','Quality',true)
            on conflict(user_id) do nothing;
            insert into directory_business_unit_memberships(user_id,business_unit_code,is_active)
            values('{AdminUserId}','OSAN',true),('{SalesUserId}','OSAN',true),('50000000-0000-0000-0000-000000000005','OSAN',true)
            on conflict(user_id,business_unit_code) do update set is_active=true;
            insert into directory_overall_administrators(user_id,is_active)
            values('{AdminUserId}',true) on conflict(user_id) do update set is_active=true;
            """, ct);
        using var factory = QmsWebApplicationFactory.Create(DevelopmentFeaturePolicy.TestingEnvironmentName,
            databases.ConfigurationValues, includeDefaultDevelopmentAuthentication: true);
        using var client = factory.CreateClient();

        var customerId = Guid.NewGuid();
        foreach (var unit in new[] { BusinessUnitCodes.Osan, BusinessUnitCodes.Cheongju })
            await databases.ExecuteAsync(unit, BusinessUnitConnectionPurpose.Migration,
                $"insert into osan_customers(id,name) values('{customerId:D}','Synthetic Archive API');", ct);
        var path = $"/api/osan/admin/customers/{customerId}?expectedVersion=1";
        foreach (var (user, unit) in new[]
        {
            ("dev-quality", BusinessUnitCodes.Osan),
            ("dev-admin", BusinessUnitCodes.Cheongju)
        })
        {
            using var request = Request(HttpMethod.Delete, path, user, unit);
            using var response = await client.SendAsync(request, ct);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        Assert.Equal(0L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration, "select count(*) from osan_customers where archived_at_utc is not null", ct));
        using (var request = Request(HttpMethod.Delete, path, "dev-admin", BusinessUnitCodes.Osan))
        using (var response = await client.SendAsync(request, ct))
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var request = Request(HttpMethod.Delete, path, "dev-admin", BusinessUnitCodes.Osan))
        using (var response = await client.SendAsync(request, ct))
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration, "select count(*) from osan_customers where archived_at_utc is not null", ct));
        Assert.Equal(0L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration, "select count(*) from osan_customers where archived_at_utc is not null", ct));
    }
}
