using System.Net;
using System.Net.Http.Json;
using System.Text;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.InteriorBusbar;
using Npgsql;
using Xunit;
using Fixture = Emi.Qms.Api.Tests.InteriorBusbarStoreTests.Fixture;

namespace Emi.Qms.Api.Tests;

public sealed class InteriorBusbarInspectionTests
{
    public static bool HasDatabase => InteriorBusbarStoreTests.HasDatabase;

    private static async Task<(Guid Product, Guid Family, Guid Worker, Guid Project)> Arrange(Fixture f, bool complete = true)
    {
        var family = await f.Store.Master("product-families", new(null,"F","Family"),f.Actor);
        var worker = await f.Store.Master("workers",new(null,"W","Worker"),f.Actor);
        var material = await f.Store.Master("materials",new(null,"M","Material","개","도급"),f.Actor);
        await f.Store.Bom(new(family,[new(material,1)]),f.Actor);
        await f.Store.Settings(new("COMMON"),f.Actor);
        var project = await f.Store.Project(new(null,"Project","TASK",family,2,"Destination",new(2026,10,1)),f.Actor);
        var product = await f.Store.Product(new(Guid.NewGuid(),family,worker),f.Actor);
        await f.Store.Photo(product,"front",[1],null,f.Actor);
        if (complete) await f.Store.Photo(product,"back",[2],null,f.Actor);
        return (product,family,worker,project);
    }

    [Fact(SkipUnless=nameof(HasDatabase),Skip="Requires disposable busbar database.")]
    public async Task InspectionRequiresProduction_ShipmentAndScanRequireInspection_NoQuantityFallback()
    {
        await using var f = await Fixture.Create();
        var (product,family,_,project) = await Arrange(f,false);
        await Assert.ThrowsAsync<BusbarException>(()=>f.Store.InspectProduct(product,f.Actor,"Inspector"));
        await f.Store.Photo(product,"back",[2],null,f.Actor);
        Assert.Equal("Complete",(await f.Store.GetProduct(product))["status"]);
        Assert.Equal(1m,await f.Balance("Finished",family));
        var request = new BusbarShipmentRequest(Guid.NewGuid(),project,1,[product]);
        Assert.Equal("inspection_required",(await Assert.ThrowsAsync<BusbarException>(()=>f.Store.Shipment(request,f.Actor))).Code);
        var number = (string)(await f.Store.GetProduct(product))["number"]!;
        Assert.Equal("inspection_required",(await Assert.ThrowsAsync<BusbarException>(()=>f.Store.ResolveShipmentPanel(project,number))).Code);
        await Assert.ThrowsAsync<BusbarException>(()=>f.Store.Shipment(request with { ProductIds=null },f.Actor));
        Assert.Equal(0L,await f.Scalar("select count(*) from busbar_shipments"));
        Assert.Equal(1m,await f.Balance("Finished",family));
        await f.Store.InspectProduct(product,f.Actor,"Inspector");
        await f.Store.ResolveShipmentPanel(project,number);
        var shipped = await f.Store.Shipment(request,f.Actor);
        Assert.Equal(shipped,await f.Store.Shipment(request,f.Actor));
        Assert.True((bool)(await f.Store.GetProduct(product))["isShipped"]!);
        Assert.Equal(0m,await f.Balance("Finished",family));
    }

    [Fact(SkipUnless=nameof(HasDatabase),Skip="Requires disposable busbar database.")]
    public async Task ConcurrentInspectionIsSingleRecord_RetryKeepsActorAndTime_CancelClearsActiveApproval()
    {
        await using var f = await Fixture.Create();
        var (product,_,worker,_) = await Arrange(f);
        var originalTime = f.Clock.Now;
        await Task.WhenAll(Enumerable.Range(0,8).Select(_=>f.Store.InspectProduct(product,f.Actor,"First inspector")));
        f.Clock.Now = f.Clock.Now.AddDays(1);
        await f.Store.InspectProduct(product,Guid.NewGuid(),"Retry must not replace inspector");
        var inspected = await f.Store.GetProduct(product);
        Assert.Equal(f.Actor,inspected["inspectedBy"]);
        Assert.Equal("First inspector",inspected["inspectedByDisplayName"]);
        Assert.Equal(originalTime.UtcDateTime,inspected["inspectedAtUtc"]);
        Assert.Equal(1L,await f.Scalar("select count(*) from busbar_audit where entity_kind='ProductInspection'"));
        await Assert.ThrowsAsync<BusbarException>(()=>f.Store.Photo(product,"front",[3],"Replace",f.Actor));
        await f.Store.CorrectProduct(product,new(worker,"Worker metadata confirmation"),f.Actor);
        Assert.Equal(inspected["inspectedAtUtc"],(await f.Store.GetProduct(product))["inspectedAtUtc"]);
        await f.Store.CancelProduct(product,new(Guid.NewGuid(),"Synthetic cancellation"),f.Actor);
        Assert.Null((await f.Store.GetProduct(product))["inspectedAtUtc"]);
        await Assert.ThrowsAsync<BusbarException>(()=>f.Store.InspectProduct(product,f.Actor,"Inspector"));
        Assert.Equal(1L,await f.Scalar("select count(*) from busbar_audit where entity_kind='ProductInspection'"));
    }

    [Theory(SkipUnless=nameof(HasDatabase),Skip="Requires disposable busbar database.")]
    [InlineData("quality",false,true)]
    [InlineData("manufacturing",false,false)]
    [InlineData("sales",false,false)]
    [InlineData("production-planning",false,false)]
    [InlineData("manufacturing",true,false)]
    public async Task EndpointUsesCurrentQualityDepartmentAndAuthenticatedName(string department,bool administrator,bool allowed)
    {
        await using var f = await Fixture.Create();
        var (product,_,_,_) = await Arrange(f);
        var identity = new InteriorBusbarAuthorizationTests.MutableIdentity(f.Actor) { Manager=administrator,DepartmentCode=department };
        using var factory = QmsWebApplicationFactory.Create("Testing",new Dictionary<string,string?> {
            ["DevAuthentication:Enabled"]="true", ["Database:ApplyMigrationsOnStartup"]="false",
            ["ConnectionStrings:QmsDatabase"]=f.Connection,["InteriorBusbar:Publication:Enabled"]="false"
        },identityStore:identity);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader,"busbar-fixture");
        // Extra JSON cannot substitute a caller-controlled inspector/time.
        using var response = await client.PostAsJsonAsync($"/api/interior-busbar/products/{product}/inspection",
            new { inspectedBy=Guid.NewGuid(),inspectedByDisplayName="Forged",inspectedAtUtc="2000-01-01" },TestContext.Current.CancellationToken);
        Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden,response.StatusCode);
        var stored = await f.Store.GetProduct(product);
        Assert.Equal(allowed ? "Synthetic Manager" : null,stored["inspectedByDisplayName"]);
        if (allowed)
        {
            identity.DepartmentCode="manufacturing";
            using var denied = await client.PostAsync($"/api/interior-busbar/products/{product}/inspection",null,TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        }
    }

    [Fact(SkipUnless=nameof(HasDatabase),Skip="Requires disposable busbar database.")]
    public async Task AdditiveMigrationPreservesShippedSnapshotAndLeavesExistingUnshippedUninspected()
    {
        await using var f = await Fixture.Create(applyInspectionMigration:false);
        var (product,family,worker,project) = await Arrange(f);
        var unshipped = await f.Store.Product(new(Guid.NewGuid(),family,worker),f.Actor);
        await f.Store.Photo(unshipped,"front",[1],null,f.Actor);
        await f.Store.Photo(unshipped,"back",[2],null,f.Actor);
        var before = await f.Store.GetProduct(unshipped);
        await using var c = new NpgsqlConnection(f.Connection);
        await c.OpenAsync(TestContext.Current.CancellationToken);
        await using var seed = new NpgsqlCommand("insert into busbar_operations(id,request_id,kind,reference_id,reason,created_by) values(@id,@id,'Shipment',@project,'Legacy',@actor); insert into busbar_shipments(id,project_id,quantity) values(@id,@project,1); insert into busbar_shipment_products(shipment_id,product_id) values(@id,@product); insert into busbar_detached_pages(product_id,html) values(@product,@html)",c);
        seed.Parameters.AddWithValue("id",Guid.NewGuid()); seed.Parameters.AddWithValue("project",project);
        seed.Parameters.AddWithValue("actor",f.Actor); seed.Parameters.AddWithValue("product",product);
        var frozen = Encoding.UTF8.GetBytes("<html>Legacy standalone snapshot</html>");
        seed.Parameters.AddWithValue("html",frozen);
        await seed.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        var root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root,"database","migrations"))) root=Directory.GetParent(root)!.FullName;
        await new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(root,"database/migrations/0122_interior_busbar_quality_inspection.sql"),TestContext.Current.CancellationToken),c).ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        Assert.Null((await f.Store.GetProduct(product))["inspectedAtUtc"]);
        Assert.Equal(frozen,await new NpgsqlCommand("select html from busbar_detached_pages",c).ExecuteScalarAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<BusbarException>(()=>f.Store.InspectProduct(product,f.Actor,"Cannot backfill"));
        Assert.Equal(1L,await f.Scalar("select count(*) from busbar_shipments"));
        Assert.Equal(4L,await f.Scalar("select count(*) from busbar_photos"));
        var after = await f.Store.GetProduct(unshipped);
        Assert.Null(after["inspectedAtUtc"]);
        Assert.Equal(before["manufacturedAtUtc"],after["manufacturedAtUtc"]);
        Assert.Equal(before["number"],after["number"]);
        Assert.Equal("inspection_required",(await Assert.ThrowsAsync<BusbarException>(()=>f.Store.Shipment(new(Guid.NewGuid(),project,1,[unshipped]),f.Actor))).Code);
    }

    [Fact]
    public void DetachedPageEmbedsEscapedInspectorAndKoreanTimeWithoutConnections()
    {
        var page = Encoding.UTF8.GetString(InteriorBusbarPublicPage.Render(new(Guid.NewGuid(),"IB-1","Worker",
            DateTimeOffset.Parse("2026-09-21T00:00:00Z"),new string('a',64),1,false,
            [new("front","image/jpeg",[1]),new("back","image/jpeg",[2])],"Inspector <script>",DateTimeOffset.Parse("2026-09-21T01:02:03Z"))));
        Assert.Contains("Inspector &lt;script&gt;",page);
        Assert.Contains("2026-09-21 10:02:03",page);
        Assert.Contains("data:image/jpeg;base64,",page);
        foreach (var remote in new[] { "https://", "http://", "/api/", "<script", "PMS", "http-equiv=\"refresh\"" }) Assert.DoesNotContain(remote,page);
    }
}
