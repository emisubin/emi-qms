using System.Text.Json;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.G2;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace Emi.Qms.Api.Tests;
public sealed class G2MaterialDatabaseTests
{
    [Theory]
    [InlineData("q", "1")]
    [InlineData("P", "1")]
    [InlineData("Q", "-0.0001")]
    [InlineData("Q", "1000000000000")]
    [InlineData("S", "1.00001")]
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
            ('60000000-0000-0000-0000-000000000002','admin-test','Synthetic admin',(select id from departments where code='administration'),true);
            insert into g2_material_items(id,source_row,metadata) select i,i+3,jsonb_build_object('category','사급','vendor','Vendor','name','Part','code',i::text,'spec','Spec','unit','EA','setQuantity',2) from generate_series(1,92) i;
            """,ct);
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"insert into g2_material_counts(count_date,item_id,materials,updated_by_user_id) values ('2026-10-06',1,12.3456,'60000000-0000-0000-0000-000000000001')",ct);
        await new DatabaseMigrationRunner(provider,DatabaseMigrationCatalog.FromPaths(Path.Combine(db.RepositoryRoot,"database","migrations"),Path.Combine(db.RepositoryRoot,"database","business-migrations")),new DatabaseRuntimePrivilegeManager(),db.Configuration,NullLogger<DatabaseMigrationRunner>.Instance).ApplyAndVerifyAsync(BusinessUnitCodes.Cheongju,ct);
        var store=new G2MaterialStore(new CheongjuDatabase(provider),new Clock());var date=new DateOnly(2026,10,7);
        var legacyDay=await store.ReadAsync(new DateOnly(2026,10,6),actor,ct);
        Assert.Equal(12.3456m,legacyDay.Items[0].Values.Materials);Assert.Null(legacyDay.Items[0].Values.CalculationOverrides);
        Assert.True(await store.CanEditAsync(actor,ct));var admin=Guid.Parse("60000000-0000-0000-0000-000000000002");Assert.False(await store.CanEditAsync(admin,ct));
        var blank=await store.ReadAsync(date,actor,ct);Assert.Equal(92,blank.Items.Count);Assert.All(blank.Items,x=>Assert.Null(x.Values.Materials));Assert.All(blank.Items,x=>Assert.Null(x.Values.CalculationOverrides));
        SaveG2MaterialsRequest Snapshot(decimal? v)=>new(Enumerable.Range(1,92).Select(i=>new G2MaterialValues(i,null,0,null,v,null,null)).ToArray());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.SaveAsync(date,Snapshot(9),admin,ct));
        await store.SaveAsync(date,Snapshot(3),actor,ct);await store.SaveAsync(date,Snapshot(5),actor,ct);
        var saved=await store.ReadAsync(date,actor,ct);Assert.All(saved.Items,x=>Assert.Equal(5,x.Values.Materials));Assert.All(saved.Items,x=>Assert.Null(x.Values.Working));Assert.All(saved.Items,x=>Assert.Equal(0,x.Values.Kitting));
        var exactOverrides = new Dictionary<string, decimal> { ["Q"] = 0, ["R"] = 123.4567m, ["S"] = 3.125m, ["W"] = 4, ["X"] = 8.75m, ["Y"] = 6, ["Z"] = 7, ["AA"] = 999999999999.9999m };
        SaveG2MaterialsRequest WithOverrides(Dictionary<string,decimal>? overrides) => new(Snapshot(5).Items.Select(x=>x with {CalculationOverrides=overrides}).ToArray());
        await store.SaveAsync(date,WithOverrides(exactOverrides),actor,ct);
        saved=await store.ReadAsync(date,actor,ct);
        Assert.All(saved.Items,x=>Assert.Equivalent(exactOverrides,x.Values.CalculationOverrides));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.SaveAsync(date,WithOverrides(new(){["Q"]=99}),admin,ct));
        await Assert.ThrowsAsync<ArgumentException>(()=>store.SaveAsync(date,WithOverrides(new(){["UNKNOWN"]=1}),actor,ct));
        Assert.All((await store.ReadAsync(date,actor,ct)).Items,x=>Assert.Equivalent(exactOverrides,x.Values.CalculationOverrides));
        await store.SaveAsync(date,WithOverrides(new(){["R"]=17.0001m}),actor,ct);
        Assert.All((await store.ReadAsync(date,actor,ct)).Items,x=>Assert.Equivalent(new Dictionary<string,decimal>{["R"]=17.0001m},x.Values.CalculationOverrides));
        await Task.WhenAll(store.SaveAsync(date,WithOverrides(new(){["S"]=1.25m}),actor,ct),store.SaveAsync(date,WithOverrides(new(){["X"]=2.5m}),actor,ct));
        saved=await store.ReadAsync(date,actor,ct);
        Assert.Single(saved.Items.Select(x=>JsonSerializer.Serialize(x.Values.CalculationOverrides)).Distinct());
        await store.SaveAsync(date,Snapshot(null),actor,ct);Assert.All((await store.ReadAsync(date,actor,ct)).Items,x=>{Assert.Null(x.Values.Materials);Assert.Null(x.Values.CalculationOverrides);});
        await Task.WhenAll(store.SaveAsync(date,Snapshot(7),actor,ct),store.SaveAsync(date,Snapshot(8),actor,ct));saved=await store.ReadAsync(date,actor,ct);Assert.Single(saved.Items.Select(x=>x.Values.Materials).Distinct());
        Assert.Equal(93L,await db.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"select count(*) from g2_material_counts",ct));
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,$"insert into g2_material_counts(count_date,item_id,updated_by_user_id) values ('2026-04-08',1,{actorSql}),('2026-04-09',1,{actorSql});",ct);
        Assert.Equal(1,await store.PruneAsync(ct));Assert.Equal(94L,await db.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"select count(*) from g2_material_counts",ct));Assert.Equal(92L,await db.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"select count(*) from g2_material_items",ct));
        Assert.False(await db.ReadScalarAsync<bool>(BusinessUnitCodes.Osan,BusinessUnitConnectionPurpose.Administrator,"select to_regclass('public.g2_material_counts') is not null",ct));
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
