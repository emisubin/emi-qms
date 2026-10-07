using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.OsanProjects;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class BusinessUnitIsolationTests
{
    [Fact]
    public async Task OsanUserProjectCreateMigration_BackfillsExistingEligibleUsersOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var databases = await IsolationDatabaseSet.CreateAsync(ct);
        var provider = new DatabaseConnectionStringProvider(databases.Configuration);
        var catalog = new DatabaseMigrationCatalog(new TestEnvironment(databases.RepositoryRoot));
        await BootstrapTargetsAsync(new DatabaseRoleBootstrapper(databases.Configuration,
            new DatabaseRuntimePrivilegeManager(), NullLogger<DatabaseRoleBootstrapper>.Instance), ct);
        await ApplyCommonSchemaBeforeSeparationAsync(databases, catalog, BusinessUnitCodes.Osan, ct);
        await using (var connection = await databases.OpenAsync(
                         BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration, ct))
        {
            await BusinessUnitDatabaseIdentity.BindOrVerifyAsync(
                connection, databases.BusinessUnits.GetBusiness(BusinessUnitCodes.Osan), ct);
        }

        var existingSales = Guid.NewGuid();
        var existingPlanning = Guid.NewGuid();
        var existingManufacturing = Guid.NewGuid();
        var inactiveSales = Guid.NewGuid();
        await databases.ExecuteAsync(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration, $"""
            insert into qms_users(id,development_user_key,display_name,department_id,is_active)
            values
              ('{existingSales:D}','existing-sales','Existing Sales',(select id from departments where code='sales'),true),
              ('{existingPlanning:D}','existing-planning','Existing Planning',(select id from departments where code='production-planning'),true),
              ('{existingManufacturing:D}','existing-manufacturing','Existing Manufacturing',(select id from departments where code='manufacturing'),true),
              ('{inactiveSales:D}','inactive-sales','Inactive Sales',(select id from departments where code='sales'),false);
            """, ct);

        var runner = new DatabaseMigrationRunner(provider, catalog, new DatabaseRuntimePrivilegeManager(),
            databases.Configuration, NullLogger<DatabaseMigrationRunner>.Instance);
        await runner.ApplyAndVerifyAsync(BusinessUnitCodes.Osan, ct);

        Assert.Equal(2L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "select count(*) from osan_user_project_create_permissions where allowed", ct));
        Assert.Equal(1L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"select count(*) from osan_user_project_create_permissions where user_id='{existingSales:D}' and allowed and version=1", ct));
        Assert.Equal(1L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"select count(*) from osan_user_project_create_permissions where user_id='{existingPlanning:D}' and allowed and version=1", ct));
        Assert.Equal(0L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"select count(*) from osan_user_project_create_permissions where user_id in ('{existingManufacturing:D}','{inactiveSales:D}')", ct));
    }

    [Fact]
    public async Task OsanUserProjectCreatePermission_IsAdminOnlyDefaultsOffAndSerializesExpectedVersion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var databases = await IsolationDatabaseSet.CreateAsync(ct);
        var provider = new DatabaseConnectionStringProvider(databases.Configuration);
        var environment = new TestEnvironment(databases.RepositoryRoot);
        var catalog = new DatabaseMigrationCatalog(environment);
        await BootstrapTargetsAsync(new DatabaseRoleBootstrapper(databases.Configuration,
            new DatabaseRuntimePrivilegeManager(), NullLogger<DatabaseRoleBootstrapper>.Instance), ct);
        await MigrateTargetsAsync(new DatabaseMigrationRunner(provider, catalog,
            new DatabaseRuntimePrivilegeManager(), databases.Configuration,
            NullLogger<DatabaseMigrationRunner>.Instance), ct);
        await new DevelopmentIdentitySeeder(provider, databases.Configuration, environment,
            NullLogger<DevelopmentIdentitySeeder>.Instance, new MigrationLedgerInspector(catalog)).SeedAsync(ct);
        databases.ConfigurationValues["DevelopmentData:SeedEnabled"] = "false";
        await databases.ExecuteAsync("DIRECTORY", BusinessUnitConnectionPurpose.Migration, $"""
            insert into directory_identities(user_id,auth_provider,external_subject,display_name,is_active)
            values('{AdminUserId:D}','Dev','dev-admin','Admin',true),
                  ('{SalesUserId:D}','Dev','dev-sales','Sales',true)
            on conflict(user_id) do nothing;
            insert into directory_business_unit_memberships(user_id,business_unit_code,is_active)
            values('{AdminUserId:D}','OSAN',true),('{SalesUserId:D}','OSAN',true)
            on conflict(user_id,business_unit_code) do update set is_active=true;
            insert into directory_overall_administrators(user_id,is_active)
            values('{AdminUserId:D}',true) on conflict(user_id) do update set is_active=true;
            """, ct);
        var customerId = Guid.NewGuid();
        await databases.ExecuteAsync(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration, $"""
            insert into osan_customers(id,name) values('{customerId:D}','Synthetic Customer');
            insert into osan_customer_assignments(user_id,customer_id)
            values('{SalesUserId:D}','{customerId:D}');
            """, ct);

        using var factory = QmsWebApplicationFactory.Create(
            DevelopmentFeaturePolicy.TestingEnvironmentName,
            databases.ConfigurationValues,
            includeDefaultDevelopmentAuthentication: true);
        using var client = factory.CreateClient();
        const string path = "/api/osan/admin/user-project-create-permissions";

        using (var denied = Request(HttpMethod.Get, path, "dev-sales", BusinessUnitCodes.Osan))
        using (var response = await client.SendAsync(denied, ct))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using (var wrongUnit = Request(HttpMethod.Get, path, "dev-admin", BusinessUnitCodes.Cheongju))
        using (var response = await client.SendAsync(wrongUnit, ct))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using (var get = Request(HttpMethod.Get, path, "dev-admin", BusinessUnitCodes.Osan))
        using (var response = await client.SendAsync(get, ct))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var items = body.RootElement.GetProperty("items").EnumerateArray().ToArray();
            var administrator = items.Single(item => item.GetProperty("userId").GetGuid() == AdminUserId);
            Assert.True(administrator.GetProperty("allowed").GetBoolean());
            Assert.True(administrator.GetProperty("isAdministrator").GetBoolean());
            var newSalesUser = items.Single(item => item.GetProperty("userId").GetGuid() == SalesUserId);
            Assert.False(newSalesUser.GetProperty("allowed").GetBoolean());
            Assert.Equal(0, newSalesUser.GetProperty("version").GetInt64());
        }

        using (var malformed = Request(HttpMethod.Put, path, "dev-admin", BusinessUnitCodes.Osan))
        {
            malformed.Content = JsonContent.Create(new { items = new object?[] { null } });
            using var response = await client.SendAsync(malformed, ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using (var deniedSingle = await SendSingleCreateAsync(
                   client, customerId, "PERMISSION-SINGLE-DENIED", ct))
            Assert.Equal(HttpStatusCode.Forbidden, deniedSingle.StatusCode);
        using (var deniedExcel = Request(HttpMethod.Get, "/api/osan/projects/import/template",
                   "dev-sales", BusinessUnitCodes.Osan))
        using (var response = await client.SendAsync(deniedExcel, ct))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        async Task<HttpResponseMessage> GrantAsync(Guid userId, bool allowed, long expectedVersion)
        {
            using var request = Request(HttpMethod.Put, path, "dev-admin", BusinessUnitCodes.Osan);
            request.Content = JsonContent.Create(new
            {
                items = new[] { new { userId, allowed, expectedVersion } }
            });
            return await client.SendAsync(request, ct);
        }

        var racing = await Task.WhenAll(
            GrantAsync(SalesUserId, true, 0),
            GrantAsync(SalesUserId, true, 0));
        try
        {
            Assert.Single(racing, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(racing, response => response.StatusCode == HttpStatusCode.Conflict);
        }
        finally
        {
            foreach (var response in racing) response.Dispose();
        }

        using (var created = await SendSingleCreateAsync(
                   client, customerId, "PERMISSION-SINGLE-ALLOWED", ct))
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var workbook = CreateOsanImportWorkbook();
        string fileSha256;
        using (var preview = Request(HttpMethod.Post, "/api/osan/projects/import/preview",
                   "dev-sales", BusinessUnitCodes.Osan))
        {
            preview.Content = CreateOsanImportContent(workbook);
            using var response = await client.SendAsync(preview, ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = Assert.IsType<OsanProjectExcelPreviewResponse>(
                await response.Content.ReadFromJsonAsync<OsanProjectExcelPreviewResponse>(ct));
            Assert.Equal(0, body.ErrorCount);
            fileSha256 = body.FileSha256;
        }
        using (var apply = Request(HttpMethod.Post, "/api/osan/projects/import/apply",
                   "dev-sales", BusinessUnitCodes.Osan))
        {
            apply.Content = CreateOsanImportContent(workbook, fileSha256, Guid.NewGuid());
            using var response = await client.SendAsync(apply, ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = Assert.IsType<OsanProjectExcelApplyResponse>(
                await response.Content.ReadFromJsonAsync<OsanProjectExcelApplyResponse>(ct));
            Assert.Equal(2, body.CreatedCount);
        }

        using (var stale = await GrantAsync(SalesUserId, false, 0))
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using (var fixedAdmin = await GrantAsync(AdminUserId, false, 0))
            Assert.Equal(HttpStatusCode.BadRequest, fixedAdmin.StatusCode);
        using (var revoke = await GrantAsync(SalesUserId, false, 1))
            Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        using (var deniedSingle = await SendSingleCreateAsync(
                   client, customerId, "PERMISSION-SINGLE-REVOKED", ct))
            Assert.Equal(HttpStatusCode.Forbidden, deniedSingle.StatusCode);
        using (var deniedExcel = Request(HttpMethod.Get, "/api/osan/projects/import/template",
                   "dev-sales", BusinessUnitCodes.Osan))
        using (var response = await client.SendAsync(deniedExcel, ct))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        Assert.Equal(1L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"select count(*) from osan_user_project_create_permission_events where user_id='{SalesUserId:D}' and actor_user_id='{AdminUserId:D}' and not before_allowed and after_allowed and version=1", ct));
        var appendOnly = await Assert.ThrowsAsync<PostgresException>(() => databases.ExecuteAsync(
            BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
            $"update osan_user_project_create_permission_events set after_allowed=false where user_id='{SalesUserId:D}'", ct));
        Assert.Equal("P0001", appendOnly.SqlState);
        Assert.Equal(0L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            "select count(*) from information_schema.tables where table_schema='public' and table_name='osan_user_project_create_permissions'", ct));
    }

    private static async Task<HttpResponseMessage> SendSingleCreateAsync(
        HttpClient client,
        Guid customerId,
        string projectCode,
        CancellationToken ct)
    {
        using var request = Request(HttpMethod.Post, "/api/osan/projects", "dev-sales", BusinessUnitCodes.Osan);
        request.Content = JsonContent.Create(new CreateOsanProjectRequest(
            "Permission Contract Project",
            projectCode,
            "Synthetic Customer",
            "PO-001",
            "WO-001",
            new DateOnly(2026, 12, 31),
            "Synthetic Product",
            1,
            Guid.NewGuid(),
            customerId));
        return await client.SendAsync(request, ct);
    }
}

public sealed class OsanUserProjectCreatePermissionPolicyTests
{
    [Fact]
    public void SingleAndExcelProjectCreationUseTheSamePolicy()
    {
        using var factory = new QmsWebApplicationFactory();
        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>().ToArray();
        foreach (var (method, path) in new[]
        {
            (HttpMethods.Post, "/api/osan/projects/"),
            (HttpMethods.Get, "/api/osan/projects/import/template"),
            (HttpMethods.Post, "/api/osan/projects/import/preview"),
            (HttpMethods.Post, "/api/osan/projects/import/apply")
        })
        {
            var route = Assert.Single(routes, endpoint => endpoint.RoutePattern.RawText == path
                && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method) == true);
            Assert.Contains(route.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                authorization => string.Equals(authorization.Policy, QmsPolicies.ProjectCreate, StringComparison.Ordinal));
        }
    }
}
