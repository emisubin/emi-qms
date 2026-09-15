using System.Text.Json;
using Emi.Qms.Api.InteriorBusbar;
using Npgsql;
using Xunit;
namespace Emi.Qms.Api.Tests;

public sealed class InteriorBusbarTraceTests
{
    public static bool HasDatabase => InteriorBusbarStoreTests.HasDatabase;
    private static async Task<(Guid Family, Guid Worker, Guid Project, Guid[] Panels)> Arrange(InteriorBusbarStoreTests.Fixture f, int count = 3)
    {
        var family = await f.Store.Master("product-families", new(null,"F","Family"),f.Actor);
        var worker = await f.Store.Master("workers",new(null,"W","Worker"),f.Actor);
        var material = await f.Store.Master("materials",new(null,"M","Material","개","도급"),f.Actor);
        await f.Store.Bom(new(family,[new(material,2)]),f.Actor);
        await f.Store.Settings(new("COMMON"),f.Actor);
        var project = await f.Store.Project(new(null,"Original project","TASK",family,10,"Original destination",new(2026,10,1)),f.Actor);
        var panels = new List<Guid>();
        for (var i=0;i<count;i++) {
            var id=await f.Store.Product(new(Guid.NewGuid(),family,worker),f.Actor);
            await f.Store.Photo(id,"front",[1],null,f.Actor);
            await f.Store.Photo(id,"back",[2],null,f.Actor);
            panels.Add(id);
        }
        return (family,worker,project,panels.ToArray());
    }

    [Fact(SkipUnless=nameof(HasDatabase),Skip="Requires disposable busbar database.")]
    public async Task MigrationPreservesHistoricalQuantityOnlyShipmentAndExistingPhotos()
    {
        await using var f=await InteriorBusbarStoreTests.Fixture.Create(applyTraceMigration:false);
        var (family,_,project,_)=await Arrange(f);
        await using var c=new NpgsqlConnection(f.Connection);
        await c.OpenAsync(TestContext.Current.CancellationToken);
        var shipment=Guid.NewGuid();
        await using(var cmd=new NpgsqlCommand("insert into busbar_operations(id,request_id,kind,reference_id,reason,created_by) values(@id,@id,'Shipment',@project,'Legacy',@actor); insert into busbar_shipments(id,project_id,quantity) values(@id,@project,2); insert into busbar_ledger(id,operation_id,stock_kind,item_id,quantity) values(@id,@id,'Finished',@family,-2); update busbar_stock set balance=balance-2 where stock_kind='Finished' and item_id=@family",c)) {
            cmd.Parameters.AddWithValue("id",shipment); cmd.Parameters.AddWithValue("project",project);
            cmd.Parameters.AddWithValue("actor",f.Actor); cmd.Parameters.AddWithValue("family",family);
            await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
        var root=AppContext.BaseDirectory;
        while(!Directory.Exists(Path.Combine(root,"database","migrations"))) root=Directory.GetParent(root)!.FullName;
        await using(var migration=new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(root,"database/migrations/0098_interior_busbar_panel_trace.sql"),TestContext.Current.CancellationToken),c))
            await migration.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        var detail=JsonSerializer.SerializeToElement(await f.Store.ProjectDetail(project));
        Assert.Empty(detail.GetProperty("panels").EnumerateArray());
        Assert.Equal(2,detail.GetProperty("shipments")[0].GetProperty("quantity").GetInt32());
        Assert.Equal(JsonValueKind.Null,detail.GetProperty("shipments")[0].GetProperty("destinationSnapshot").ValueKind);
        Assert.Equal(6L,await f.Scalar("select count(*) from busbar_photos"));
        Assert.Equal(1m,await f.Balance("Finished",family));
    }

    [Fact(SkipUnless=nameof(HasDatabase),Skip="Requires disposable busbar database.")]
    public async Task ScannedPanelsCaptureDestination_ReplayAndReversePreserveHistory()
    {
        await using var f=await InteriorBusbarStoreTests.Fixture.Create(new(false,new Uri("https://synthetic.example/"),null,""));
        var (family,_,project,panels)=await Arrange(f);
        var product=await f.Store.GetProduct(panels[0]);
        var number=(string)product["number"]!;
        var resolved=JsonSerializer.SerializeToElement(await f.Store.ResolveShipmentPanel(project,number));
        Assert.Equal(panels[0],resolved.GetProperty("id").GetGuid());
        await using(var c=new NpgsqlConnection(f.Connection)) {
            await c.OpenAsync(TestContext.Current.CancellationToken);
            var url=(string)(await new NpgsqlCommand($"select url from busbar_product_qr where product_id='{panels[0]}'",c).ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
            Assert.Equal(panels[0],JsonSerializer.SerializeToElement(await f.Store.ResolveShipmentPanel(project,url)).GetProperty("id").GetGuid());
        }
        await Assert.ThrowsAsync<BusbarException>(()=>f.Store.ResolveShipmentPanel(project,"https://untrusted.example/p/anything.html"));
        var request=new BusbarShipmentRequest(Guid.NewGuid(),project,2,panels[..2]);
        var shipment=await f.Store.Shipment(request,f.Actor);
        Assert.Equal(shipment,await f.Store.Shipment(request,f.Actor));
        Assert.Equal(1m,await f.Balance("Finished",family));
        await Assert.ThrowsAsync<BusbarException>(()=>f.Store.ResolveShipmentPanel(project,number));
        await Assert.ThrowsAsync<BusbarException>(()=>f.Store.CancelProduct(panels[0],new(Guid.NewGuid(),"Wrong"),f.Actor));
        await f.Store.Project(new(project,"Renamed","OTHER",family,10,"Changed destination",new(2026,10,2),"Correction"),f.Actor);
        var detail=JsonSerializer.SerializeToElement(await f.Store.ProjectDetail(project));
        Assert.Equal(2,detail.GetProperty("panels").GetArrayLength());
        Assert.Equal("Original destination",detail.GetProperty("shipments")[0].GetProperty("destinationSnapshot").GetString());
        Assert.Equal("Original project",detail.GetProperty("shipments")[0].GetProperty("projectNameSnapshot").GetString());
        await f.Store.Reverse(shipment,new(Guid.NewGuid(),"Wrong shipment"),f.Actor);
        Assert.Equal(3m,await f.Balance("Finished",family));
        var second=await f.Store.Shipment(new(Guid.NewGuid(),project,1,[panels[0]]),f.Actor);
        Assert.NotEqual(shipment,second);
        Assert.Equal(2L,await f.Scalar($"select count(*) from busbar_shipment_products where product_id='{panels[0]}'"));
        Assert.Equal(1L,await f.Scalar($"select count(*) from busbar_shipment_products where product_id='{panels[0]}' and released_at_utc is null"));
        Assert.Equal(2,JsonSerializer.SerializeToElement(await f.Store.GetProduct(panels[0])).GetProperty("shipmentHistory").GetArrayLength());
    }

    [Fact(SkipUnless=nameof(HasDatabase),Skip="Requires disposable busbar database.")]
    public async Task InvalidSelectionRollsBack_ConcurrentProjectsCannotShipSamePanel()
    {
        await using var f=await InteriorBusbarStoreTests.Fixture.Create();
        var (family,worker,project,panels)=await Arrange(f);
        var draft=await f.Store.Product(new(Guid.NewGuid(),family,worker),f.Actor);
        var otherFamily=await f.Store.Master("product-families",new(null,"OTHER","Other"),f.Actor);
        var other=await f.Store.Project(new(null,"Other","",otherFamily,10,"Elsewhere",new(2026,10,1)),f.Actor);
        foreach(var request in new BusbarShipmentRequest[] {
            new(Guid.NewGuid(),project,1),new(Guid.NewGuid(),project,2,[panels[0],panels[0]]),
            new(Guid.NewGuid(),project,2,[panels[0]]),new(Guid.NewGuid(),project,2,[panels[0],draft]),
            new(Guid.NewGuid(),other,1,[panels[0]])})
            await Assert.ThrowsAsync<BusbarException>(()=>f.Store.Shipment(request,f.Actor));
        Assert.Equal(0L,await f.Scalar("select count(*) from busbar_shipments"));
        Assert.Equal(3m,await f.Balance("Finished",family));
        var competing=await f.Store.Project(new(null,"Competing","",family,10,"Other destination",new(2026,10,1)),f.Actor);
        async Task<bool> Ship(Guid target) { try { await f.Store.Shipment(new(Guid.NewGuid(),target,1,[panels[0]]),f.Actor); return true; } catch(BusbarException) { return false; } }
        Assert.Single(await Task.WhenAll(Ship(project),Ship(competing)),x=>x);
        Assert.Equal(1L,await f.Scalar("select count(*) from busbar_shipments"));
        Assert.Equal(2m,await f.Balance("Finished",family));
    }

    [Fact(SkipUnless=nameof(HasDatabase),Skip="Requires disposable busbar database.")]
    public async Task FinalPhotosCannotChangeThroughStoreOrSql_WorkerCorrectionKeepsOriginalEvidence()
    {
        await using var f=await InteriorBusbarStoreTests.Fixture.Create();
        var (family,worker,_,panels)=await Arrange(f,1);
        var original=await f.Store.GetProduct(panels[0]);
        foreach(var side in new[]{"front","back"})
            await Assert.ThrowsAsync<BusbarException>(()=>f.Store.Photo(panels[0],side,[9],"Admin reason",f.Actor));
        Assert.Equal(new byte[]{1},await f.Store.GetPhoto(panels[0],"front"));
        Assert.Equal(2L,await f.Scalar("select count(*) from busbar_photo_history"));
        await using(var c=new NpgsqlConnection(f.Connection)) {
            await c.OpenAsync(TestContext.Current.CancellationToken);
            foreach(var sql in new[]{"update busbar_photos set content=decode('99','hex')", "delete from busbar_photos"}) {
                var error=await Assert.ThrowsAsync<PostgresException>(async()=>await new NpgsqlCommand(sql,c).ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
                Assert.Equal("23514",error.SqlState);
            }
        }
        await f.Store.CorrectProduct(panels[0],new(worker,"Worker confirmed"),f.Actor);
        Assert.Equal(original["manufacturedAtUtc"],(await f.Store.GetProduct(panels[0]))["manufacturedAtUtc"]);
        Assert.Equal(new byte[]{2},await f.Store.GetPhoto(panels[0],"back"));
        Assert.Equal(1m,await f.Balance("Finished",family));
    }
}
