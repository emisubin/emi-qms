using System.Text.Json;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.DeploymentMaintenance;
using Emi.Qms.Api.G2;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace Emi.Qms.Api.Tests;
public sealed class G2MaterialDatabaseTests
{
    [Theory]
    [InlineData("q", "1")]
    [InlineData("P", "1")]
    [InlineData("Q", "-0.0001")]
    public void CalculationOverrideValidationRejectsInvalidKeysAndValues(string key,string raw)
    {
        var value=decimal.Parse(raw,System.Globalization.CultureInfo.InvariantCulture);
        var items=Enumerable.Range(1,92).Select(i=>new G2MaterialValues(i,null,null,null,null,null,null,new(){[key]=value})).ToArray();
        Assert.Throws<ArgumentException>(()=>G2MaterialRules.Validate(items));
    }
    [Fact]
    public void LegacyPayloadOmitsOverridesAndAllSupportedLiteralsValidate()
    {
        var legacy=JsonSerializer.Deserialize<G2MaterialValues>("{\"id\":1,\"materials\":5}",new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(legacy);Assert.Null(legacy.CalculationOverrides);
        var values=new Dictionary<string,decimal>{["Q"]=0,["R"]=999999999999.9999m,["S"]=3.125m,["W"]=1,["X"]=1.25m,["Y"]=2,["Z"]=3,["AA"]=4};
        G2MaterialRules.Validate(Enumerable.Range(1,92).Select(i=>legacy with {Id=i,CalculationOverrides=values}).ToArray());
    }
    private sealed class Clock:TimeProvider{public override DateTimeOffset GetUtcNow()=>new(2026,10,9,3,0,0,TimeSpan.Zero);}
    [Fact]public async Task SnapshotUpsertPermissionsConcurrentSaveAndRetentionUseCheongjuOnly()
    {
        var ct=TestContext.Current.CancellationToken;
        await using var db=await BusinessUnitIsolationTests.IsolationDatabaseSet.CreateAsync(ct);
        var provider=new DatabaseConnectionStringProvider(db.Configuration);
        await BusinessUnitIsolationTests.BootstrapTargetsAsync(new DatabaseRoleBootstrapper(db.Configuration,new DatabaseRuntimePrivilegeManager(),NullLogger<DatabaseRoleBootstrapper>.Instance),ct);
        using var priorCatalog=new PriorBusinessCatalog(Path.Combine(db.RepositoryRoot,"database","business-migrations"));
        await new DatabaseMigrationRunner(provider,DatabaseMigrationCatalog.FromPaths(Path.Combine(db.RepositoryRoot,"database","migrations"),priorCatalog.Path),new DatabaseRuntimePrivilegeManager(),db.Configuration,NullLogger<DatabaseMigrationRunner>.Instance).ApplyAndVerifyAsync(BusinessUnitCodes.Cheongju,ct);
        const string actorSql="'60000000-0000-0000-0000-000000000001'";var actor=Guid.Parse(actorSql.Trim('\''));
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"""
            insert into qms_users(id,development_user_key,display_name,department_id,is_active) values
            ('60000000-0000-0000-0000-000000000001','material-test','Synthetic material',(select id from departments where code='materials'),true),
            ('60000000-0000-0000-0000-000000000002','admin-test','Synthetic administration staff',(select id from departments where code='administration'),true),
            ('60000000-0000-0000-0000-000000000003','role-admin-test','Synthetic role administrator',(select id from departments where code='sales'),true);
            insert into user_roles(user_id,role_id)
            select '60000000-0000-0000-0000-000000000003',id from roles where code='system-administrator';
            insert into g2_material_items(id,source_row,metadata) select i,i+3,jsonb_build_object('category','사급','vendor','Vendor','name','Part','code',i::text,'spec','Spec','unit','EA','setQuantity',2) from generate_series(1,92) i;
            """,ct);
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"insert into g2_material_counts(count_date,item_id,materials,updated_by_user_id) values ('2026-10-06',1,12.3456,'60000000-0000-0000-0000-000000000001')",ct);
        await new DatabaseMigrationRunner(provider,DatabaseMigrationCatalog.FromPaths(Path.Combine(db.RepositoryRoot,"database","migrations"),Path.Combine(db.RepositoryRoot,"database","business-migrations")),new DatabaseRuntimePrivilegeManager(),db.Configuration,NullLogger<DatabaseMigrationRunner>.Instance).ApplyAndVerifyAsync(BusinessUnitCodes.Cheongju,ct);
        var clock=new Clock();var store=new G2MaterialStore(new CheongjuDatabase(provider),clock);var date=new DateOnly(2026,10,7);
        var legacyDay=await store.ReadAsync(new DateOnly(2026,10,6),actor,ct);
        Assert.Equal(12.3456m,legacyDay.Items[0].Values.Materials);Assert.Null(legacyDay.Items[0].Values.CalculationOverrides);
        Assert.True(await store.CanEditAsync(actor,ct));var admin=Guid.Parse("60000000-0000-0000-0000-000000000002");Assert.False(await store.CanEditAsync(admin,ct));
        var blank=await store.ReadAsync(date,actor,ct);Assert.Equal(92,blank.Items.Count);Assert.All(blank.Items,x=>Assert.Null(x.Values.Materials));Assert.All(blank.Items,x=>Assert.Null(x.Values.CalculationOverrides));
        SaveG2MaterialsRequest Snapshot(decimal? v)=>new(Enumerable.Range(1,92).Select(i=>new G2MaterialValues(i,null,0,null,v,null,null)).ToArray());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.SaveAsync(date,Snapshot(9),admin,ct));
        var roleAdmin=Guid.Parse("60000000-0000-0000-0000-000000000003");
        Assert.False((await store.ReadAsync(date,admin,ct)).CanEdit);
        Assert.True(await store.CanEditAsync(roleAdmin,ct)); // Also used by the import endpoint.
        Assert.True((await store.ReadAsync(date,roleAdmin,ct)).CanEdit);
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,
            "update departments set is_active=false where code='sales'",ct);
        Assert.True(await store.CanEditAsync(roleAdmin,ct));
        await store.SaveAsync(date,Snapshot(11),roleAdmin,ct);
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,
            "update departments set is_active=true where code='sales'",ct);
        Assert.All((await store.ReadAsync(date,actor,ct)).Items,x=>Assert.Equal(11,x.Values.Materials));
        Assert.Equal(92L,await db.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,
            "select count(*) from g2_material_counts where updated_by_user_id='60000000-0000-0000-0000-000000000003'",ct));
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,
            "update qms_users set is_active=false where id='60000000-0000-0000-0000-000000000003'",ct);
        Assert.False(await store.CanEditAsync(roleAdmin,ct));
        Assert.False((await store.ReadAsync(date,roleAdmin,ct)).CanEdit);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.SaveAsync(date,Snapshot(12),roleAdmin,ct));
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"""
            update qms_users set is_active=true where id='60000000-0000-0000-0000-000000000003';
            delete from user_roles where user_id='60000000-0000-0000-0000-000000000003';
            """,ct);
        Assert.False(await store.CanEditAsync(roleAdmin,ct));
        Assert.False((await store.ReadAsync(date,roleAdmin,ct)).CanEdit);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.SaveAsync(date,Snapshot(13),roleAdmin,ct));
        Assert.All((await store.ReadAsync(date,actor,ct)).Items,x=>Assert.Equal(11,x.Values.Materials));
        await store.SaveAsync(date,Snapshot(3),actor,ct);await store.SaveAsync(date,Snapshot(5),actor,ct);
        var saved=await store.ReadAsync(date,actor,ct);Assert.All(saved.Items,x=>Assert.Equal(5,x.Values.Materials));Assert.All(saved.Items,x=>Assert.Null(x.Values.Working));Assert.All(saved.Items,x=>Assert.Equal(0,x.Values.Kitting));
        SaveG2MaterialsRequest WithOverrides(Dictionary<string,decimal>? overrides) => new(Snapshot(5).Items.Select(x=>x with {CalculationOverrides=overrides}).ToArray());
        await store.SaveAsync(date,WithOverrides(new(){["Q"]=0,["Y"]=5}),actor,ct);
        Assert.All((await store.ReadAsync(date,actor,ct)).Items,x=>Assert.Null(x.Values.CalculationOverrides));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.SaveAsync(date,WithOverrides(new(){["Q"]=99}),admin,ct));
        await Assert.ThrowsAsync<ArgumentException>(()=>store.SaveAsync(date,WithOverrides(new(){["UNKNOWN"]=1}),actor,ct));
        var mismatch=await Assert.ThrowsAsync<ArgumentException>(()=>store.SaveAsync(date,WithOverrides(new(){["Q"]=99}),actor,ct));
        Assert.Contains("합계가 맞지 않습니다",mismatch.Message);
        Assert.All((await store.ReadAsync(date,actor,ct)).Items,x=>{Assert.Equal(5,x.Values.Materials);Assert.Null(x.Values.CalculationOverrides);});
        await store.SaveAsync(date,Snapshot(null),actor,ct);Assert.All((await store.ReadAsync(date,actor,ct)).Items,x=>{Assert.Null(x.Values.Materials);Assert.Null(x.Values.CalculationOverrides);});
        await Task.WhenAll(store.SaveAsync(date,Snapshot(7),actor,ct),store.SaveAsync(date,Snapshot(8),actor,ct));saved=await store.ReadAsync(date,actor,ct);Assert.Single(saved.Items.Select(x=>x.Values.Materials).Distinct());
        Assert.Equal(93L,await db.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"select count(*) from g2_material_counts",ct));
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,$"insert into g2_material_counts(count_date,item_id,updated_by_user_id) values ('2026-04-08',1,{actorSql}),('2026-04-09',1,{actorSql});",ct);
        await using var services=new ServiceCollection().AddSingleton(provider).AddSingleton<CheongjuDatabase>().AddSingleton<TimeProvider>(clock).AddScoped<G2MaterialStore>().AddScoped<G2NonconformanceStore>().BuildServiceProvider();
        using var worker=new G2MaterialRetentionWorker(services.GetRequiredService<IServiceScopeFactory>(),provider,db.Configuration,clock,NullLogger<G2MaterialRetentionWorker>.Instance);
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"""
            update deployment_maintenance set release_id='70000000-0000-0000-0000-000000000001',version=1,
                popup_version=1,state='Failed',title='Synthetic maintenance',body='Synthetic maintenance',
                starts_at_utc=now()-interval '1 hour',expected_ends_at_utc=now()+interval '1 hour' where id=1
            """,ct);
        Assert.False(await worker.RunOnceAsync(ct));
        Assert.Equal(95L,await db.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"select count(*) from g2_material_counts",ct));

        const long barrierKey=9070301;
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Migration,$$"""
            create function test_pause_g2_retention() returns trigger language plpgsql as $$
            begin
                if old.count_date=date '2026-04-08' then perform pg_advisory_xact_lock({{barrierKey}}); end if;
                return old;
            end $$;
            create trigger test_pause_g2_retention before delete on g2_material_counts
            for each row execute function test_pause_g2_retention();
            """,ct);
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"update deployment_maintenance set state='Completed' where id=1",ct);
        await using var barrier=await db.OpenAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Migration,ct);
        await using var barrierCommand=barrier.CreateCommand();
        barrierCommand.CommandText=$"select pg_advisory_lock({barrierKey})";
        await barrierCommand.ExecuteNonQueryAsync(ct);
        await using var drain=await db.OpenAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Migration,ct);
        Task<bool>? pending=null;Task? exclusive=null;var barrierHeld=true;
        try
        {
            pending=worker.RunOnceAsync(ct);
            await WaitForAdvisoryLockAsync(db,barrierKey,granted:false,ct);
            exclusive=DeploymentMaintenanceLease.AcquireExclusiveAsync(drain,ct);
            await WaitForAdvisoryLockAsync(db,9070125,granted:false,ct);
            Assert.False(exclusive.IsCompleted);
            barrierCommand.CommandText=$"select pg_advisory_unlock({barrierKey})";
            await barrierCommand.ExecuteNonQueryAsync(ct);barrierHeld=false;
            Assert.True(await pending);
            await exclusive;
            Assert.Equal(94L,await db.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"select count(*) from g2_material_counts",ct));
        }
        finally
        {
            if(barrierHeld){barrierCommand.CommandText=$"select pg_advisory_unlock({barrierKey})";await barrierCommand.ExecuteNonQueryAsync(ct);}
            if(pending is not null)await pending;
            if(exclusive is not null){await exclusive;await DeploymentMaintenanceLease.ReleaseExclusiveAsync(drain);}
        }
        Assert.Equal(92L,await db.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"select count(*) from g2_material_items",ct));
        Assert.False(await db.ReadScalarAsync<bool>(BusinessUnitCodes.Osan,BusinessUnitConnectionPurpose.Administrator,"select to_regclass('public.g2_material_counts') is not null",ct));
    }

    [Fact]
    public async Task LatestEndpointRequiresG2ReadAndRejectsUnauthenticatedRequests()
    {
        using var factory=new QmsWebApplicationFactory();
        var endpoint=factory.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>()
            .Endpoints.OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Single(x=>x.RoutePattern.RawText=="/api/g2/materials/latest");
        Assert.Contains(endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>(),
            x=>x.Policy==Emi.Qms.Api.Authorization.QmsPolicies.G2Read);
        using var client=factory.CreateClient();
        using var response=await client.GetAsync("/api/g2/materials/latest",TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized,response.StatusCode);
    }

    [Fact]
    public async Task LatestUsesSavedCountDateWithinRetentionAndPreservesEditPermissions()
    {
        var ct=TestContext.Current.CancellationToken;
        await using var db=await BusinessUnitIsolationTests.IsolationDatabaseSet.CreateAsync(ct);
        var provider=new DatabaseConnectionStringProvider(db.Configuration);
        await BusinessUnitIsolationTests.BootstrapTargetsAsync(new DatabaseRoleBootstrapper(db.Configuration,new DatabaseRuntimePrivilegeManager(),NullLogger<DatabaseRoleBootstrapper>.Instance),ct);
        await new DatabaseMigrationRunner(provider,DatabaseMigrationCatalog.FromPaths(Path.Combine(db.RepositoryRoot,"database","migrations"),Path.Combine(db.RepositoryRoot,"database","business-migrations")),new DatabaseRuntimePrivilegeManager(),db.Configuration,NullLogger<DatabaseMigrationRunner>.Instance).ApplyAndVerifyAsync(BusinessUnitCodes.Cheongju,ct);
        var actor=Guid.Parse("60000000-0000-0000-0000-000000000011");
        var viewer=Guid.Parse("60000000-0000-0000-0000-000000000012");
        var store=new G2MaterialStore(new CheongjuDatabase(provider),new Clock());
        Assert.Null(await store.ReadLatestAsync(actor,ct));
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"""
            insert into qms_users(id,development_user_key,display_name,department_id,is_active) values
            ('60000000-0000-0000-0000-000000000011','latest-material-test','Synthetic material',(select id from departments where code='materials'),true),
            ('60000000-0000-0000-0000-000000000012','latest-viewer-test','Synthetic viewer',(select id from departments where code='sales'),true);
            insert into g2_material_items(id,source_row,metadata) select i,i+3,jsonb_build_object('category','사급','vendor','Vendor','name','Part','code',i::text,'spec','Spec','unit','EA','setQuantity',2) from generate_series(1,92) i;
            insert into g2_material_counts(count_date,item_id,materials,updated_by_user_id) values
            ('2026-04-08',1,99,'60000000-0000-0000-0000-000000000011'),
            ('2026-10-10',1,99,'60000000-0000-0000-0000-000000000011');
            """,ct);
        Assert.Null(await store.ReadLatestAsync(actor,ct));
        SaveG2MaterialsRequest Snapshot(decimal? value)=>new(Enumerable.Range(1,92).Select(i=>new G2MaterialValues(i,null,null,null,value,null,null)).ToArray());
        var cutoff=new DateOnly(2026,4,9);
        await store.SaveAsync(cutoff,Snapshot(1),actor,ct);
        Assert.Equal(cutoff,(await store.ReadLatestAsync(actor,ct))!.Date);
        var latestDate=new DateOnly(2026,10,8);
        await store.SaveAsync(latestDate,Snapshot(8),actor,ct);
        await store.SaveAsync(new DateOnly(2026,10,7),Snapshot(7),actor,ct);
        var latest=Assert.IsType<G2MaterialResponse>(await store.ReadLatestAsync(actor,ct));
        Assert.Equal(latestDate,latest.Date);
        Assert.True(latest.CanEdit);
        Assert.All(latest.Items,x=>Assert.Equal(8,x.Values.Materials));
        var readOnly=Assert.IsType<G2MaterialResponse>(await store.ReadLatestAsync(viewer,ct));
        Assert.Equal(latestDate,readOnly.Date);
        Assert.False(readOnly.CanEdit);
        await store.SaveAsync(latestDate,Snapshot(null),actor,ct);
        latest=Assert.IsType<G2MaterialResponse>(await store.ReadLatestAsync(actor,ct));
        Assert.Equal(latestDate,latest.Date);
        Assert.All(latest.Items,x=>Assert.Null(x.Values.Materials));
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,
            "update qms_users set is_active=false where id='60000000-0000-0000-0000-000000000011'",ct);
        Assert.False((await store.ReadLatestAsync(actor,ct))!.CanEdit);
        Assert.False(await db.ReadScalarAsync<bool>(BusinessUnitCodes.Osan,BusinessUnitConnectionPurpose.Administrator,"select to_regclass('public.g2_material_counts') is not null",ct));
    }

    private static async Task WaitForAdvisoryLockAsync(BusinessUnitIsolationTests.IsolationDatabaseSet db,long key,bool granted,CancellationToken ct)
    {
        for(var attempt=0;attempt<200;attempt++)
        {
            if(await db.ReadScalarAsync<bool>(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Migration,
                $"select exists(select 1 from pg_locks where locktype='advisory' and database=(select oid from pg_database where datname=current_database()) and classid=0 and objid={key} and granted={granted.ToString().ToLowerInvariant()})",ct))return;
            await Task.Delay(25,ct);
        }
        throw new TimeoutException("Synthetic G2 retention request did not reach its lock barrier.");
    }
    private sealed class PriorBusinessCatalog : IDisposable
    {
        private readonly DirectoryInfo directory=Directory.CreateTempSubdirectory("g2-material-prior-catalog-");
        public string Path=>directory.FullName;
        public PriorBusinessCatalog(string root)
        {
            foreach(var unit in new[]{"cheongju","osan"})
            {
                var target=Directory.CreateDirectory(System.IO.Path.Combine(Path,unit));
                foreach(var file in Directory.GetFiles(System.IO.Path.Combine(root,unit),"*.sql"))
                    if(string.CompareOrdinal(System.IO.Path.GetFileName(file),"0134_")<0)
                        File.Copy(file,System.IO.Path.Combine(target.FullName,System.IO.Path.GetFileName(file)));
            }
        }
        public void Dispose()=>directory.Delete(recursive:true);
    }

}
