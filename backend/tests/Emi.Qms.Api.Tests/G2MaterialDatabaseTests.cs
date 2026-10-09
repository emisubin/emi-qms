using System.Text.Json;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.G2;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace Emi.Qms.Api.Tests;
public sealed class G2MaterialDatabaseTests
{
    private sealed class Clock:TimeProvider{public override DateTimeOffset GetUtcNow()=>new(2026,10,9,3,0,0,TimeSpan.Zero);}
    [Fact]public async Task SnapshotUpsertPermissionsConcurrentSaveAndRetentionUseCheongjuOnly()
    {
        var ct=TestContext.Current.CancellationToken;
        await using var db=await BusinessUnitIsolationTests.IsolationDatabaseSet.CreateAsync(ct);
        var provider=new DatabaseConnectionStringProvider(db.Configuration);
        await new DatabaseRoleBootstrapper(db.Configuration,new DatabaseRuntimePrivilegeManager(),NullLogger<DatabaseRoleBootstrapper>.Instance).BootstrapAsync(BusinessUnitCodes.Cheongju,ct);
        await new DatabaseMigrationRunner(provider,DatabaseMigrationCatalog.FromPaths(Path.Combine(db.RepositoryRoot,"database","migrations"),Path.Combine(db.RepositoryRoot,"database","business-migrations")),new DatabaseRuntimePrivilegeManager(),db.Configuration,NullLogger<DatabaseMigrationRunner>.Instance).ApplyAndVerifyAsync(BusinessUnitCodes.Cheongju,ct);
        const string actorSql="'60000000-0000-0000-0000-000000000001'";var actor=Guid.Parse(actorSql.Trim('\''));
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"""
            insert into qms_users(id,development_user_key,display_name,department_id,is_active) values
            ('60000000-0000-0000-0000-000000000001','material-test','Synthetic material',(select id from departments where code='materials'),true),
            ('60000000-0000-0000-0000-000000000002','admin-test','Synthetic admin',(select id from departments where code='administration'),true);
            insert into g2_material_items(id,source_row,metadata) select i,i+3,jsonb_build_object('category','사급','vendor','Vendor','name','Part','code',i::text,'spec','Spec','unit','EA','setQuantity',2) from generate_series(1,92) i;
            """,ct);
        var store=new G2MaterialStore(new CheongjuDatabase(provider),new Clock());var date=new DateOnly(2026,10,7);
        Assert.True(await store.CanEditAsync(actor,ct));var admin=Guid.Parse("60000000-0000-0000-0000-000000000002");Assert.False(await store.CanEditAsync(admin,ct));
        var blank=await store.ReadAsync(date,actor,ct);Assert.Equal(92,blank.Items.Count);Assert.All(blank.Items,x=>Assert.Null(x.Values.Materials));
        SaveG2MaterialsRequest Snapshot(decimal? v)=>new(Enumerable.Range(1,92).Select(i=>new G2MaterialValues(i,null,0,null,v,null,null)).ToArray());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.SaveAsync(date,Snapshot(9),admin,ct));
        await store.SaveAsync(date,Snapshot(3),actor,ct);await store.SaveAsync(date,Snapshot(5),actor,ct);
        var saved=await store.ReadAsync(date,actor,ct);Assert.All(saved.Items,x=>Assert.Equal(5,x.Values.Materials));Assert.All(saved.Items,x=>Assert.Null(x.Values.Working));Assert.All(saved.Items,x=>Assert.Equal(0,x.Values.Kitting));
        await store.SaveAsync(date,Snapshot(null),actor,ct);Assert.All((await store.ReadAsync(date,actor,ct)).Items,x=>Assert.Null(x.Values.Materials));
        await Task.WhenAll(store.SaveAsync(date,Snapshot(7),actor,ct),store.SaveAsync(date,Snapshot(8),actor,ct));saved=await store.ReadAsync(date,actor,ct);Assert.Single(saved.Items.Select(x=>x.Values.Materials).Distinct());
        Assert.Equal(92L,await db.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"select count(*) from g2_material_counts",ct));
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,$"insert into g2_material_counts(count_date,item_id,updated_by_user_id) values ('2026-04-08',1,{actorSql}),('2026-04-09',1,{actorSql});",ct);
        Assert.Equal(1,await store.PruneAsync(ct));Assert.Equal(93L,await db.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"select count(*) from g2_material_counts",ct));Assert.Equal(92L,await db.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"select count(*) from g2_material_items",ct));
        Assert.False(await db.ReadScalarAsync<bool>(BusinessUnitCodes.Osan,BusinessUnitConnectionPurpose.Administrator,"select to_regclass('public.g2_material_counts') is not null",ct));
    }
}
