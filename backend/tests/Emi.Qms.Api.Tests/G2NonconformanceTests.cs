using System.Data;
using Microsoft.AspNetCore.Http;
using ImageMagick;
using Emi.Qms.Api.Authorization;
using System.Net;
using System.Net.Http.Json;
using Emi.Qms.Api.Identity;
using System.Text.Json;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.G2;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace Emi.Qms.Api.Tests;
public sealed class G2NonconformanceTests
{
    private static readonly G2QualityPhoto[] Photos=CreatePhotos();
    private static G2QualityPhoto[] CreatePhotos() { using var image=new MagickImage(MagickColors.White,2,2);return [new("test.png","image/png",Convert.ToBase64String(image.ToByteArray(MagickFormat.Png)))]; }
    private sealed class Clock:TimeProvider { public DateTimeOffset Now=new(2026,10,12,3,0,0,TimeSpan.Zero); public override DateTimeOffset GetUtcNow()=>Now; }
    [Fact] public async Task RejectInvalidPhotosAndVersion()
    {
        Assert.Throws<ArgumentException>(()=>G2QualityRules.Validate("text",[]));
        Assert.Throws<ArgumentException>(()=>G2QualityRules.Validate("text",[new("x.svg","image/svg+xml","PHN2Zz4=")]));
        Assert.Throws<ArgumentException>(()=>G2QualityRules.Validate("",Photos));
        Assert.Throws<DBConcurrencyException>(()=>G2QualityRules.Version(1,2));
        await G2QualityRules.ValidateImagesAsync("text",Photos,TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ArgumentException>(()=>G2QualityRules.ValidateImagesAsync("text",[new("bad.png","image/png",Convert.ToBase64String(new byte[]{137,80,78,71,13,10,26,10,0}))],TestContext.Current.CancellationToken));
    }
    [Fact] public async Task LifecyclePermissionsProjectionConcurrencyAndPurge()
    {
        var ct=TestContext.Current.CancellationToken;
        await using var db=await BusinessUnitIsolationTests.IsolationDatabaseSet.CreateAsync(ct);
        var provider=new DatabaseConnectionStringProvider(db.Configuration);
        await BusinessUnitIsolationTests.BootstrapTargetsAsync(new DatabaseRoleBootstrapper(db.Configuration,new DatabaseRuntimePrivilegeManager(),NullLogger<DatabaseRoleBootstrapper>.Instance),ct);
        await new DatabaseMigrationRunner(provider,DatabaseMigrationCatalog.FromPaths(Path.Combine(db.RepositoryRoot,"database","migrations"),Path.Combine(db.RepositoryRoot,"database","business-migrations")),new DatabaseRuntimePrivilegeManager(),db.Configuration,NullLogger<DatabaseMigrationRunner>.Instance).ApplyAndVerifyAsync(BusinessUnitCodes.Cheongju,ct);
        var quality=Guid.Parse("50000000-0000-0000-0000-000000000005");var staff=Guid.Parse("50000000-0000-0000-0000-000000000004");var admin=Guid.Parse("50000000-0000-0000-0000-000000000001");
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"""
        insert into qms_users(id,development_user_key,display_name,department_id,is_active) values
        ('50000000-0000-0000-0000-000000000005','quality-test','Synthetic quality',(select id from departments where code='quality'),true),
        ('50000000-0000-0000-0000-000000000004','staff-test','Synthetic staff',(select id from departments where code='manufacturing'),true),
        ('50000000-0000-0000-0000-000000000001','admin-test','Synthetic admin',(select id from departments where code='sales'),true);
        insert into user_roles(user_id,role_id) select '50000000-0000-0000-0000-000000000001',id from roles where code='system-administrator';
        insert into g2_daily_metrics(work_date,metric_code,quantity,created_by_user_id,updated_by_user_id) values ('2026-10-11','Defect',2,'50000000-0000-0000-0000-000000000005','50000000-0000-0000-0000-000000000005');
        """,ct);
        var clock=new Clock(); var store=new G2NonconformanceStore(new CheongjuDatabase(provider),clock);var day=new DateOnly(2026,10,11);
        var request=new SaveG2Quality("SERIAL",day,"production","Issue",false,Photos,null);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.SaveFirstAsync(null,request,staff,ct));
        var first=(await store.SaveFirstAsync(null,request,quality,ct))!;
        Assert.Null(first.Photos[0].Base64); Assert.NotNull(first.Photos[0].Id);
        Assert.NotNull((await store.PhotoAsync(first.Id,first.Photos[0].Id!.Value,ct))!.Base64);
        var initial=(await store.SaveFirstAsync(null,request with {Serial="BACKLOG",InitialStock=true},admin,ct))!;
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"update qms_users set is_active=false where id='50000000-0000-0000-0000-000000000001'",ct);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.SaveFirstAsync(null,request with {Serial="Inactive"},admin,ct));
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"update qms_users set is_active=true where id='50000000-0000-0000-0000-000000000001';delete from user_roles where user_id='50000000-0000-0000-0000-000000000001'",ct);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.SaveFirstAsync(null,request with {Serial="Revoked"},admin,ct));
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"insert into user_roles(user_id,role_id) select '50000000-0000-0000-0000-000000000001',id from roles where code='system-administrator';update g2_quality_settings set activated_on='2026-10-12';insert into g2_defect_inventory_counts(count_date,quantity,created_by_user_id,updated_by_user_id) values('2026-10-10',7,'50000000-0000-0000-0000-000000000005','50000000-0000-0000-0000-000000000005')",ct);
        var operations=new G2OperationsStore(new CheongjuDatabase(provider),clock);
        var range=await operations.GetRangeAsync(day,day.AddDays(1),ct);
        Assert.Equal(3,range.Days[0].Defect!.Quantity); Assert.Equal(9,range.Days[0].DefectInventory);
        var historical=await operations.GetRangeAsync(day.AddDays(-1),day.AddDays(-1),ct);Assert.Equal(7,historical.Days[0].DefectInventoryCount!.Quantity);Assert.Equal(7,historical.Days[0].DefectInventory);
        await Assert.ThrowsAsync<ArgumentException>(()=>operations.SaveDefectInventoryCountAsync(day,new(10,null),admin,ct));
        await Assert.ThrowsAsync<ArgumentException>(()=>operations.SaveMetricsAsync(day,[new(G2MetricCodes.Defect,3,null)],admin,ct));
        await Assert.ThrowsAsync<ArgumentException>(()=>store.SaveActionAsync(first.Id,new(day.AddDays(-1),"Invalid date",Photos,first.Version),quality,ct));
        var action=(await store.SaveActionAsync(first.Id,new(day.AddDays(1),"Fixed",Photos,first.Version),quality,ct))!;
        await Assert.ThrowsAsync<ArgumentException>(()=>store.DeleteAsync(first.Id,action.Version,false,quality,ct));
        clock.Now=clock.Now.AddDays(1);
        await Assert.ThrowsAsync<ArgumentException>(()=>store.SaveFirstAsync(first.Id,request with {OccurrenceDate=day.AddDays(2),ExpectedVersion=action.Version},quality,ct));
        clock.Now=clock.Now.AddDays(-1);
        await Assert.ThrowsAsync<DBConcurrencyException>(()=>store.SaveActionAsync(first.Id,new(day,"Changed",Photos,first.Version),quality,ct));
        range=await operations.GetRangeAsync(day,day.AddDays(1),ct);Assert.Equal(1,range.Days[1].RepairTotal);Assert.Equal(1,range.Days[1].DefectInventory);
        var reopened=(await store.DeleteAsync(first.Id,action.Version,true,quality,ct))!; Assert.Null(reopened.Action);
        range=await operations.GetRangeAsync(day,day.AddDays(1),ct);Assert.Null(range.Days[1].RepairTotal);Assert.Equal(2,range.Days[1].DefectInventory);
        var edits=await Task.WhenAll(Enumerable.Range(0,2).Select(async i=>{try{await store.SaveFirstAsync(first.Id,request with {ExpectedVersion=reopened.Version,Content=$"Edit{i}",Photos=reopened.Photos},quality,ct);return true;}catch(DBConcurrencyException){return false;}}));Assert.Single(edits,x=>x);
        var list=JsonSerializer.SerializeToElement(await store.ReadAsync(staff,ct));Assert.False(list.GetProperty("canEdit").GetBoolean());
        await store.DeleteAsync(first.Id,reopened.Version+1,false,admin,ct);
        Assert.Null(await store.PhotoAsync(first.Id,first.Photos[0].Id!.Value,ct));
        Assert.True(await db.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"select count(*) from g2_nonconformance_history",ct)>0);
        clock.Now=clock.Now.AddMonths(3).AddSeconds(1);await store.PruneAsync(ct);
        Assert.Equal(0,await db.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"select count(*) from g2_nonconformance_history",ct));
        Assert.NotNull(await store.PhotoAsync(initial.Id,initial.Photos[0].Id!.Value,ct));
        using var factory=QmsWebApplicationFactory.Create("Testing",new Dictionary<string,string?>{
            ["DevAuthentication:Enabled"]="true",["Database:ApplyMigrationsOnStartup"]="false",["DevelopmentData:SeedEnabled"]="false",
            ["ConnectionStrings:QmsDatabase"]=provider.GetConnectionString(db.BusinessUnits.GetBusiness(BusinessUnitCodes.Cheongju))});
        using var client=factory.CreateClient();client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader,"dev-manufacturing");
        using var denied=await client.PostAsJsonAsync("/api/g2/nonconformances",request,ct);Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        client.DefaultRequestHeaders.Remove(DevelopmentAuthenticationDefaults.UserHeader);client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader,"dev-quality");
        using var created=await client.PostAsJsonAsync("/api/g2/nonconformances",request with {Serial="HTTP"},ct);Assert.True(created.IsSuccessStatusCode,await created.Content.ReadAsStringAsync(ct));
        var httpItem=(await created.Content.ReadFromJsonAsync<G2QualityItem>(ct))!;
        using var duplicate=await client.PostAsJsonAsync("/api/g2/nonconformances",request with {Serial=" http "},ct);Assert.Equal(HttpStatusCode.Conflict,duplicate.StatusCode);
        var duplicateSaves=await Task.WhenAll(Enumerable.Range(0,2).Select(async _=>{try {await store.SaveFirstAsync(null,request with {Serial="CONCURRENT"},quality,ct);return true;}catch(G2QualityDuplicateSerialException){return false;}}));Assert.Single(duplicateSaves,x=>x);
        var context=new DefaultHttpContext();
        BusinessUnitRequestContextFeature.Set(context,new(BusinessUnitAccessStatuses.Selected,Guid.NewGuid(),db.BusinessUnits.GetBusiness(BusinessUnitCodes.Osan),[BusinessUnitCodes.Osan],false,"test"));
        using var wrongProvider=new DatabaseConnectionStringProvider(db.Configuration,new HttpContextAccessor {HttpContext=context});
        var wrongStore=new G2NonconformanceStore(new CheongjuDatabase(wrongProvider),clock);
        var wrongCampus=await Assert.ThrowsAsync<BusinessUnitContextUnavailableException>(()=>wrongStore.PhotoAsync(httpItem.Id,httpItem.Photos[0].Id!.Value,ct));Assert.Equal("business_unit_module_mismatch",wrongCampus.Reason);
        await Assert.ThrowsAsync<BusinessUnitContextUnavailableException>(()=>wrongStore.ReadAsync(quality,ct));
        await Assert.ThrowsAsync<BusinessUnitContextUnavailableException>(()=>wrongStore.SaveFirstAsync(null,request with {Serial="WRONG-CAMPUS"},quality,ct));
        using(var anonymous=factory.CreateClient()) {using var unauthorized=await anonymous.GetAsync($"/api/g2/nonconformances/{httpItem.Id}/photos/{httpItem.Photos[0].Id}",ct);Assert.Equal(HttpStatusCode.Unauthorized,unauthorized.StatusCode);}
        using var photo=await client.GetAsync($"/api/g2/nonconformances/{httpItem.Id}/photos/{httpItem.Photos[0].Id}",ct);Assert.Equal(HttpStatusCode.OK,photo.StatusCode);
        using var repaired=await client.PutAsJsonAsync($"/api/g2/nonconformances/{httpItem.Id}/action",new SaveG2QualityAction(day,"Fixed",Photos,httpItem.Version),ct);Assert.Equal(HttpStatusCode.OK,repaired.StatusCode);
        using var blocked=await client.DeleteAsync($"/api/g2/nonconformances/{httpItem.Id}?expectedVersion=2",ct);Assert.Equal(HttpStatusCode.BadRequest,blocked.StatusCode);
        using var undo=await client.DeleteAsync($"/api/g2/nonconformances/{httpItem.Id}/action?expectedVersion=2",ct);Assert.Equal(HttpStatusCode.OK,undo.StatusCode);
        using var deleted=await client.DeleteAsync($"/api/g2/nonconformances/{httpItem.Id}?expectedVersion=3",ct);Assert.Equal(HttpStatusCode.OK,deleted.StatusCode);
    }
}
