using Emi.Qms.Api.InteriorBusbar;
using Npgsql;
using Xunit;
using Fixture = Emi.Qms.Api.Tests.InteriorBusbarStoreTests.Fixture;
namespace Emi.Qms.Api.Tests;

public sealed class InteriorBusbarLabelTests
{
    public static bool HasDatabase => InteriorBusbarStoreTests.HasDatabase;
    [Theory]
    [InlineData("1","1")]
    [InlineData("0001","1")]
    [InlineData("ib-00000001","1")]
    [InlineData("10","10")]
    [InlineData("one",null)]
    [InlineData("https://example.test/1",null)]
    public void NumericLookupIsExact(string input,string? expected) => Assert.Equal(expected,InteriorBusbarStore.NormalizePanelNumber(input));

    [Fact(SkipUnless=nameof(HasDatabase),Skip="Requires disposable database.")]
    public async Task MigrationMarksExistingLabelsUnknownWithoutChangingIdentifiers()
    {
        await using var f=await Fixture.Create(applyLabelMigration:false);
        var family=await f.Store.Master("product-families",new(null,"F","Family"),f.Actor);
        await f.Store.Plan(new(null,family,new(2026,9,21),1),f.Actor);
        await using var c=new NpgsqlConnection(f.Connection);await c.OpenAsync(TestContext.Current.CancellationToken);
        var before=(string)(await new NpgsqlCommand("select number from busbar_products",c).ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
        var root=AppContext.BaseDirectory;
        while(!Directory.Exists(Path.Combine(root,"database","migrations")))root=Directory.GetParent(root)!.FullName;
        await new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(root,"database/migrations/0121_interior_busbar_label_tracking.sql"),TestContext.Current.CancellationToken),c).ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        Assert.Equal("LegacyUnknown",await new NpgsqlCommand("select label_state from busbar_products",c).ExecuteScalarAsync(TestContext.Current.CancellationToken));
        Assert.Equal(before,await new NpgsqlCommand("select number from busbar_products",c).ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact(SkipUnless=nameof(HasDatabase),Skip="Requires disposable database.")]
    public async Task ExplicitConfirmationIsAtomicAccountScopedIdempotentAndReprintPreservesAttachment()
    {
        await using var f=await Fixture.Create(new(false,new Uri("https://synthetic.example/"),null,""));
        var family=await f.Store.Master("product-families",new(null,"F","Family"),f.Actor);
        var plan=await f.Store.Plan(new(null,family,new(2026,9,21),2),f.Actor);
        await using var c=new NpgsqlConnection(f.Connection); await c.OpenAsync(TestContext.Current.CancellationToken);
        var ids=new List<Guid>();
        await using(var cmd=new NpgsqlCommand("select id from busbar_products order by number",c))
        await using(var r=await cmd.ExecuteReaderAsync(TestContext.Current.CancellationToken)) while(await r.ReadAsync(TestContext.Current.CancellationToken))ids.Add(r.GetGuid(0));
        var other=Guid.NewGuid(); await using(var cmd=new NpgsqlCommand("insert into qms_users(id) values(@id)",c)){cmd.Parameters.AddWithValue("id",other);await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);}
        Assert.Equal("Unprinted",(await f.Store.GetProduct(ids[0]))["labelState"]);
        Assert.Equal(new DateOnly(2026,9,21),(await f.Store.GetProduct(ids[0]))["planDate"]);
        var printed=new BusbarLabelRequest(Guid.NewGuid(),ids);
        await Assert.ThrowsAsync<BusbarException>(()=>f.Store.ConfirmLabels(printed,f.Actor,false));
        await new NpgsqlCommand("update busbar_products set publication_state='Published',published_revision=revision",c).ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        await Task.WhenAll(f.Store.ConfirmLabels(printed,f.Actor,false),f.Store.ConfirmLabels(printed,f.Actor,false));
        Assert.Equal(2L,await f.Scalar("select count(*) from busbar_label_events"));
        Assert.Equal(2,Assert.IsType<List<Dictionary<string,object?>>>(await f.Store.PendingLabels(f.Actor)).Count);
        Assert.Empty(Assert.IsType<List<Dictionary<string,object?>>>(await f.Store.PendingLabels(other)));
        await Assert.ThrowsAsync<BusbarException>(()=>f.Store.ConfirmLabels(printed,other,false));
        await f.Store.ConfirmLabels(new(Guid.NewGuid(),[ids[0]]),other,true);
        await f.Store.ConfirmLabels(new(Guid.NewGuid(),[ids[0]]),f.Actor,false);
        Assert.Equal("Attached",(await f.Store.GetProduct(ids[0]))["labelState"]);
        Assert.Single(Assert.IsType<List<Dictionary<string,object?>>>(await f.Store.PendingLabels(f.Actor)));
        var number=(string)(await f.Store.GetProduct(ids[0]))["number"]!;
        var numeric=InteriorBusbarStore.NormalizePanelNumber(number)!;
        Assert.Equal(ids[0],Assert.IsType<Dictionary<string,object?>>(await f.Store.ResolveLabel(numeric))["id"]);
        await using(var cmd=new NpgsqlCommand("update busbar_products set status='Cancelled' where id=@id",c)){cmd.Parameters.AddWithValue("id",ids[1]);await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);}
        var before=await f.Scalar("select count(*) from busbar_label_events");
        await Assert.ThrowsAsync<BusbarException>(()=>f.Store.ConfirmLabels(new(Guid.NewGuid(),ids),f.Actor,false));
        Assert.Equal(before,await f.Scalar("select count(*) from busbar_label_events"));
        Assert.Empty(Assert.IsType<List<Dictionary<string,object?>>>(await f.Store.PendingLabels(f.Actor)));
    }
}
