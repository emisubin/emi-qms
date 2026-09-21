using System.Text;
using Emi.Qms.Api.InteriorBusbar;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;
using Fixture = Emi.Qms.Api.Tests.InteriorBusbarStoreTests.Fixture;

namespace Emi.Qms.Api.Tests;

public sealed class InteriorBusbarLifecycleTests
{
    public static bool HasDatabase => InteriorBusbarStoreTests.HasDatabase;
    private static readonly InteriorBusbarPublicationOptions Options = new(true,
        new("https://products.z1.web.core.windows.net/"), new("https://products.blob.core.windows.net/"), "synthetic",
        PmsBaseUrl: new("https://pms.example.test/"));
    private static IConfiguration Config(Fixture f) => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string,string?> { ["ConnectionStrings:QmsDatabase"] = f.Connection }).Build();
    private sealed class Sink : IInteriorBusbarPublicationSink
    {
        public string Html = "";
        public bool FailAfterWrite;
        public Task PublishAsync(string token, byte[] html, CancellationToken cancellationToken)
        {
            Html = Encoding.UTF8.GetString(html);
            if (FailAfterWrite) throw new IOException("synthetic lost acknowledgement");
            return Task.CompletedTask;
        }
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task WaitingPanelHasPrintableStableQrAndAuthenticatedPmsRoute()
    {
        await using var f = await Fixture.Create(Options);
        var family = await f.Store.Master("product-families", new(null,"F","Family"), f.Actor);
        var plan = await f.Store.Plan(new(null,family,new DateOnly(2026,9,18),1), f.Actor);
        await using var c = new NpgsqlConnection(f.Connection);
        await c.OpenAsync(TestContext.Current.CancellationToken);
        var id = (Guid)(await new NpgsqlCommand("select id from busbar_products", c).ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
        var before = await f.Store.GetProduct(id);
        Assert.Equal("Draft", before["status"]);
        Assert.StartsWith("IB-", (string)before["number"]!);
        var sink = new Sink();
        using var publisher = new InteriorBusbarPublicationWorker(new(Config(f)), Options, sink, NullLogger<InteriorBusbarPublicationWorker>.Instance);
        Assert.True(await publisher.PublishNextAsync(TestContext.Current.CancellationToken));
        Assert.Contains("https://pms.example.test/interior-busbar/production?productId="+id, sink.Html);
        Assert.DoesNotContain("data:image", sink.Html);
        Assert.NotEmpty(await f.Store.GetPrintableQr(id));
        Assert.Equal(before["number"], (await f.Store.GetProduct(id))["number"]);
        var worker = await f.Store.Master("workers",new(null,"W","Worker"),f.Actor);
        await f.Store.CorrectProduct(id,new(worker,"Waiting worker assignment"),f.Actor);
        Assert.True(await publisher.PublishNextAsync(TestContext.Current.CancellationToken));
        Assert.NotEmpty(await f.Store.GetPrintableQr(id));
        // Restore untouched state solely in this synthetic fixture to exercise plan withdrawal.
        await new NpgsqlCommand("update busbar_products set worker_id=null,worker_name=null",c).ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        await f.Store.Plan(new(plan,family,new DateOnly(2026,9,18),0),f.Actor);
        Assert.True(await publisher.PublishNextAsync(TestContext.Current.CancellationToken));
        Assert.Contains("정보 제공이 중지",sink.Html);
        Assert.DoesNotContain("pms.example.test",sink.Html);
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task LostPublicationResponseRollsBackShipmentAndDurablyRestoresPmsRoute_ThenDetachedSnapshotSurvivesCorrections()
    {
        await using var f = await Fixture.Create(Options);
        var family = await f.Store.Master("product-families",new(null,"F","Family"),f.Actor);
        await f.Store.Settings(new("SYN"), f.Actor);
        var project = await f.Store.Project(new(null,"Project","WO",family,1,"Destination",new DateOnly(2026,9,30)),f.Actor);
        await f.Store.Adjustment(new(Guid.NewGuid(),"Finished",family,1,"Synthetic opening",true),f.Actor);
        var request = await f.ShipmentRequest(project,1);
        var product = request.ProductIds![0];
        var sink = new Sink { FailAfterWrite = true };
        var store = new InteriorBusbarStore(new(Config(f)),f.Clock,Options,publicationSink:sink);
        var failure = await Assert.ThrowsAsync<BusbarException>(()=>store.Shipment(request,f.Actor));
        Assert.Equal("publication_failed",failure.Code);
        Assert.Equal(0L,await f.Scalar("select count(*) from busbar_shipments"));
        Assert.Equal(1L,await f.Scalar("select count(*) from busbar_publication_recovery"));
        sink.FailAfterWrite=false;
        using var publisher = new InteriorBusbarPublicationWorker(new(Config(f)),Options,sink,NullLogger<InteriorBusbarPublicationWorker>.Instance);
        Assert.True(await publisher.PublishNextAsync(TestContext.Current.CancellationToken));
        Assert.Contains("pms.example.test",sink.Html);
        Assert.Equal(0L,await f.Scalar("select count(*) from busbar_publication_recovery"));
        var shipment = await store.Shipment(request,f.Actor);
        Assert.Equal(shipment,await store.Shipment(request,f.Actor));
        var frozen = sink.Html;
        Assert.Contains("data:image/jpeg;base64,",frozen);
        Assert.Contains("Synthetic quality inspector",frozen);
        Assert.Contains("2026-09-09 10:00:00",frozen);
        Assert.DoesNotContain("https://",frozen);
        Assert.DoesNotContain("/api/",frozen);
        Assert.DoesNotContain("PMS",frozen);
        Assert.Equal(1L,await f.Scalar("select count(*) from busbar_shipments"));
        var worker = await store.Master("workers",new(null,"OTHER","Changed after shipping"),f.Actor);
        await store.CorrectProduct(product,new(worker,"Worker correction"),f.Actor);
        Assert.True(await publisher.PublishNextAsync(TestContext.Current.CancellationToken));
        Assert.Equal(frozen,sink.Html);
        Assert.Equal(0L,await f.Scalar("select count(*) from busbar_publication_recovery"));
    }
}
