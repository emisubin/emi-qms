using Emi.Qms.Api.InteriorBusbar;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using ZXing;
using Fixture = Emi.Qms.Api.Tests.InteriorBusbarStoreTests.Fixture;

namespace Emi.Qms.Api.Tests;

public sealed class InteriorBusbarQrTests
{
    public static bool HasDatabase => InteriorBusbarStoreTests.HasDatabase;
    private static readonly InteriorBusbarPublicationOptions Options = new(false,
        new Uri("https://busbar.example.test/"), null, "");

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires explicitly configured disposable busbar database.")]
    public async Task SecondPhotoStoresDecodableQrBeforePublication_AndKeepsManufacturingAtomic()
    {
        await using var fixture = await Fixture.Create(Options);
        var (product, family, material) = await Prepare(fixture);
        await fixture.Store.Photo(product, "front", [1], null, fixture.Actor);
        Assert.Equal(1L, await fixture.Scalar("select count(*) from busbar_product_qr"));
        await fixture.Store.Photo(product, "back", [2], null, fixture.Actor);
        var completed = await fixture.Store.GetProduct(product);
        Assert.Equal("Complete", completed["status"]);
        Assert.NotNull(completed["number"]);
        Assert.Equal("Pending", completed["publicationState"]);
        Assert.Equal("Ready", completed["qrState"]);
        Assert.Equal(true, completed["qrReady"]);
        Assert.Equal(1m, await fixture.Balance("Finished", family));
        Assert.Equal(-2m, await fixture.Balance("Material", material));
        var bytes = await StoredPng(fixture, product);
        using var image = Image.Load<Rgba32>(bytes);
        var reader = new ZXing.ImageSharp.BarcodeReader<Rgba32>
        {
            Options = { PossibleFormats = [BarcodeFormat.QR_CODE], TryHarder = true }
        };
        Assert.Equal(Options.GetPublicUrl((string)completed["publicToken"]!), reader.Decode(image)?.Text);
        Assert.Equal("publication_not_ready", (await Assert.ThrowsAsync<BusbarException>(() => fixture.Store.GetPrintableQr(product))).Code);
        await Sql(fixture, "update busbar_products set publication_state='Published',published_revision=revision");
        Assert.Equal(bytes, await fixture.Store.GetPrintableQr(product));
        await Assert.ThrowsAsync<BusbarException>(() => fixture.Store.Photo(product, "front", [3], "사진 정정", fixture.Actor));
        Assert.Equal(completed["number"], (await fixture.Store.GetProduct(product))["number"]);
        Assert.Equal(bytes, await StoredPng(fixture, product));
        Assert.Equal(1L, await fixture.Scalar("select count(*) from busbar_product_qr"));
        Assert.Equal(1m, await fixture.Balance("Finished", family));
        await fixture.Store.CorrectProduct(product, new((Guid)completed["workerId"]!, "작업자 확인"), fixture.Actor);
        await Sql(fixture, "update busbar_products set publication_state='Published'");
        Assert.Equal("publication_not_ready", (await Assert.ThrowsAsync<BusbarException>(() => fixture.Store.GetPrintableQr(product))).Code);
        await Sql(fixture, "update busbar_products set published_revision=revision");
        await fixture.Store.CancelProduct(product, new(Guid.NewGuid(), "생산 취소"), fixture.Actor);
        Assert.Equal("publication_not_ready", (await Assert.ThrowsAsync<BusbarException>(() => fixture.Store.GetPrintableQr(product))).Code);
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires explicitly configured disposable busbar database.")]
    public async Task MissingPublicUrlDoesNotBlockProduction_LaterPrintBackfillsExactlyOneArtifact()
    {
        await using var fixture = await Fixture.Create();
        var (product, family, material) = await Prepare(fixture);
        await fixture.Store.Photo(product, "front", [1], null, fixture.Actor);
        await fixture.Store.Photo(product, "back", [2], null, fixture.Actor);
        var completed = await fixture.Store.GetProduct(product);
        Assert.Equal("ConfigurationPending", completed["qrState"]);
        Assert.Equal(false, completed["qrReady"]);
        Assert.Equal(0L, await fixture.Scalar("select count(*) from busbar_product_qr"));
        Assert.Equal(1m, await fixture.Balance("Finished", family));
        Assert.Equal(-2m, await fixture.Balance("Material", material));
        await Sql(fixture, "update busbar_products set publication_state='Published',published_revision=revision");
        Assert.Equal("qr_configuration_pending", (await Assert.ThrowsAsync<BusbarException>(() => fixture.Store.GetPrintableQr(product))).Code);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:QmsDatabase"] = fixture.Connection
        }).Build();
        var configured = new InteriorBusbarStore(new(configuration), fixture.Clock, Options);
        var results = await Task.WhenAll(configured.GetPrintableQr(product), configured.GetPrintableQr(product));
        Assert.Equal(results[0], results[1]);
        Assert.Equal(1L, await fixture.Scalar("select count(*) from busbar_product_qr"));
        Assert.Equal(completed["number"], (await configured.GetProduct(product))["number"]);
        Assert.Equal("Ready", (await configured.GetProduct(product))["qrState"]);
        var movedSite = new InteriorBusbarStore(new(configuration), fixture.Clock,
            Options with { PublicBaseUrl = new Uri("https://changed.example.test/") });
        Assert.Equal("qr_public_url_changed", (await Assert.ThrowsAsync<BusbarException>(() => movedSite.GetPrintableQr(product))).Code);
        Assert.Equal(results[0], await StoredPng(fixture, product));
    }

    private static async Task<(Guid Product, Guid Family, Guid Material)> Prepare(Fixture fixture)
    {
        var family = await fixture.Store.Master("product-families", new(null, "F", "Family"), fixture.Actor);
        var material = await fixture.Store.Master("materials", new(null, "M", "Material", "개", "도급"), fixture.Actor);
        var worker = await fixture.Store.Master("workers", new(null, "W", "Worker"), fixture.Actor);
        await fixture.Store.Bom(new(family, [new(material, 2)]), fixture.Actor);
        var product = await fixture.Store.Product(new(Guid.NewGuid(), family, worker), fixture.Actor);
        return (product, family, material);
    }

    private static async Task Sql(Fixture fixture, string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.Connection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<byte[]> StoredPng(Fixture fixture, Guid product)
    {
        await using var connection = new NpgsqlConnection(fixture.Connection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("select png from busbar_product_qr where product_id=@id", connection);
        command.Parameters.AddWithValue("id", product);
        return (byte[])(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
