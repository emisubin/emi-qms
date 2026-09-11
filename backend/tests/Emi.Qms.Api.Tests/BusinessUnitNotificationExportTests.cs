using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class BusinessUnitIsolationTests
{
    [Fact]
    public async Task OsanNotificationExport_UsesRecipientScopeAndDoesNotOpenGeneralExports()
    {
        var ct=TestContext.Current.CancellationToken;
        await using var databases=await IsolationDatabaseSet.CreateAsync(ct);
        var provider=new DatabaseConnectionStringProvider(databases.Configuration);
        var environment=new TestEnvironment(databases.RepositoryRoot);
        var catalog=new DatabaseMigrationCatalog(environment);
        await new DatabaseRoleBootstrapper(databases.Configuration,new DatabaseRuntimePrivilegeManager(),
            NullLogger<DatabaseRoleBootstrapper>.Instance).BootstrapAsync(ct);
        await new DatabaseMigrationRunner(provider,catalog,new DatabaseRuntimePrivilegeManager(),
            databases.Configuration,NullLogger<DatabaseMigrationRunner>.Instance).ApplyAndVerifyAsync(ct);
        await new DevelopmentIdentitySeeder(provider,databases.Configuration,environment,
            NullLogger<DevelopmentIdentitySeeder>.Instance,new MigrationLedgerInspector(catalog)).SeedAsync(ct);
        databases.ConfigurationValues["DevelopmentData:SeedEnabled"]="false";
        await databases.ExecuteAsync("DIRECTORY",BusinessUnitConnectionPurpose.Migration,$"""
            insert into directory_identities(user_id,auth_provider,external_subject,display_name,is_active)
            values('{SalesUserId}','Dev','dev-sales','Sales',true) on conflict(user_id) do nothing;
            insert into directory_business_unit_memberships(user_id,business_unit_code,is_active)
            values('{SalesUserId}','OSAN',true) on conflict(user_id,business_unit_code) do update set is_active=true;
            """,ct);
        var own=Guid.NewGuid(); var hidden=Guid.NewGuid(); var otherCampus=Guid.NewGuid();
        await databases.ExecuteAsync(BusinessUnitCodes.Osan,BusinessUnitConnectionPurpose.Migration,$"""
            insert into notifications(id,notification_type,severity,title,message,idempotency_key,visibility_scope,source_kind)
            values('{own}','Info','Info','OSAN-OWN-EXPORT','own record','{own}','RecipientOnly','OsanWorkflow'),
                  ('{hidden}','Info','Info','OSAN-HIDDEN-EXPORT','private record','{hidden}','RecipientOnly','OsanWorkflow');
            insert into notification_recipients(notification_id,user_id)
            values('{own}','{SalesUserId}'),('{hidden}','{AdminUserId}');
            """,ct);
        await databases.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Migration,$"""
            insert into notifications(id,notification_type,severity,title,message,idempotency_key,visibility_scope)
            values('{otherCampus}','Info','Info','CHEONGJU-PRIVATE','other campus','{otherCampus}','RecipientOnly');
            insert into notification_recipients(notification_id,user_id) values('{otherCampus}','{SalesUserId}');
            """,ct);
        using var factory=QmsWebApplicationFactory.Create(DevelopmentFeaturePolicy.TestingEnvironmentName,
            databases.ConfigurationValues,includeDefaultDevelopmentAuthentication:true);
        using var client=factory.CreateClient();
        async Task<HttpResponseMessage> Export(string route,object body)
        {
            using var request=Request(HttpMethod.Post,route,"dev-sales",BusinessUnitCodes.Osan);
            request.Content=JsonContent.Create(body);
            return await client.SendAsync(request,ct);
        }
        using(var response=await Export("/api/notifications/export",new {ids=new[]{own},filters=new {readStatus="unread"}}))
        {
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            Assert.Equal("1",Assert.Single(response.Headers.GetValues("X-Export-Row-Count")));
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",response.Content.Headers.ContentType?.MediaType);
            using var workbook=new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync(ct)));
            var values=workbook.Worksheets.SelectMany(sheet=>sheet.CellsUsed()).Select(cell=>cell.GetString()).ToArray();
            Assert.Contains("OSAN-OWN-EXPORT",values);
            Assert.DoesNotContain("OSAN-HIDDEN-EXPORT",values);
            Assert.DoesNotContain("CHEONGJU-PRIVATE",values);
        }
        foreach(var badIds in new[]{new[]{hidden},new[]{otherCampus},new[]{own,hidden},Array.Empty<Guid>(),new[]{own,own}})
        {
            using var denied=await Export("/api/notifications/export",new {ids=badIds});
            Assert.Equal(HttpStatusCode.UnprocessableEntity,denied.StatusCode);
        }
        using(var screenAttack=await Export("/api/notifications/export",new {ids=new[]{own},screen="projects"}))
            Assert.Equal(HttpStatusCode.UnprocessableEntity,screenAttack.StatusCode);
        using(var general=await Export("/api/data-exports/selected",new {ids=new[]{own},screen="notifications"}))
            Assert.Equal(HttpStatusCode.Forbidden,general.StatusCode);
        using(var filtered=await Export("/api/notifications/export",new {ids=new[]{own},filters=new {readStatus="read"}}))
            Assert.Equal(HttpStatusCode.UnprocessableEntity,filtered.StatusCode);
        Assert.Equal(1L,await databases.ReadScalarAsync<long>(BusinessUnitCodes.Osan,BusinessUnitConnectionPurpose.Migration,
            "select count(*) from data_export_events where export_kind='NotificationsSelected' and row_count=1",ct));
        Assert.Equal(0L,await databases.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Migration,
            "select count(*) from data_export_events where export_kind='NotificationsSelected'",ct));
    }
}
