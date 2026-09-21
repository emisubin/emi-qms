using System.Text;
using ImageMagick;
using Emi.Qms.Api.BusinessUnits;
using Npgsql;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging.Abstractions;
using Emi.Qms.Api.InteriorBusbar;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class InteriorBusbarPublicationTests
{
    private const string Token = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly byte[] Pixel = CreatePixel();
    private static byte[] CreatePixel()
    {
        using var image = new MagickImage(MagickColors.LightSteelBlue, 320, 200);
        return image.ToByteArray(MagickFormat.Png);
    }
    private static InteriorBusbarPublicSnapshot Product(bool cancelled = false) => new(Guid.NewGuid(), "IB-001", "<script>alert(1)</script>",
        new DateTimeOffset(2026, 9, 9, 0, 12, 34, TimeSpan.Zero), Token, 1, cancelled,
        [new("front", "image/png", Pixel), new("back", "image/png", Pixel)]);

    [Fact]
    public void PageIsStandaloneEscapesNamesAndShowsServerTimeInKorea()
    {
        var html = Encoding.UTF8.GetString(InteriorBusbarPublicPage.Render(Product()));
        if (Environment.GetEnvironmentVariable("BUSBAR_PUBLIC_PAGE_EVIDENCE_PATH") is { } path)
            File.WriteAllText(path, html);
        Assert.DoesNotContain("IB-001", html);
        Assert.Contains("2026-09-09", html);
        Assert.Contains("09:12:34", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("data:image/png;base64,", html);
        Assert.DoesNotContain("https://", html);
        Assert.DoesNotContain("/api/", html);
        Assert.DoesNotContain("PMS", html);
        Assert.Contains("default-src 'none'", html);
        Assert.Equal(2, html.Split("<img ").Length - 1);
    }

    [Fact]
    public void CancelledPagePublishesNoIdentityOrPhotoData()
    {
        var html = Encoding.UTF8.GetString(InteriorBusbarPublicPage.Render(Product(true)));
        Assert.Contains("정보 제공이 중지", html);
        Assert.DoesNotContain("IB-001", html);
        Assert.DoesNotContain("alert(1)", html);
        Assert.DoesNotContain("data:image", html);
        Assert.DoesNotContain("2026-09-09", html);
    }

    [Fact]
    public void MissingRequiredPhotoCannotBePublished()
    {
        var product = Product() with { Photos = [new("front", "image/png", Pixel)] };
        Assert.Throws<InvalidOperationException>(() => InteriorBusbarPublicPage.Render(product));
    }

    [Fact]
    public void ImageContentCannotInjectHtml()
    {
        var product = Product() with { Photos = [new("front", "image/svg+xml", Pixel), new("back", "image/png", Pixel)] };
        Assert.Throws<InvalidOperationException>(() => InteriorBusbarPublicPage.Render(product));
    }

    [Theory]
    [InlineData("../private")]
    [InlineData("1234")]
    [InlineData("https://example.test")]
    public void TokensCannotBecomePaths(string token)
    {
        Assert.Throws<ArgumentException>(() => InteriorBusbarPublicationOptions.ValidateToken(token));
    }

    [Fact]
    public void DisabledByDefaultAndReviewSafeSuppressesConfiguredPublishing()
    {
        Assert.False(InteriorBusbarPublicationOptions.Load(new ConfigurationBuilder().Build()).Enabled);
        var config = Config(new() { ["InteriorBusbar:Publication:Enabled"] = "true", ["ReviewSafe:Enabled"] = "true" });
        Assert.False(InteriorBusbarPublicationOptions.Load(config).Enabled);
    }

    [Fact]
    public void MalformedActivationFailsClosed()
    {
        Assert.Throws<InvalidOperationException>(() => InteriorBusbarPublicationOptions.Load(Config(new() { ["InteriorBusbar:Publication:Enabled"] = "yes" })));
        Assert.Throws<InvalidOperationException>(() => InteriorBusbarPublicationOptions.Load(Config(new() { ["InteriorBusbar:Publication:Enabled"] = "true" })));
    }

    [Theory]
    [InlineData("http://example.test")]
    [InlineData("https://user:password@example.test")]
    [InlineData("https://example.test/?token=private")]
    public void PublicUrlsCannotExposeSecretsOrUsePlainHttp(string url)
    {
        Assert.Throws<InvalidOperationException>(() => InteriorBusbarPublicationOptions.Load(Config(new() { ["InteriorBusbar:Publication:PublicBaseUrl"] = url })));
    }

    [Fact]
    public void PublicUrlUsesDedicatedStaticPath()
    {
        var options = InteriorBusbarPublicationOptions.Load(Config(new() { ["InteriorBusbar:Publication:PublicBaseUrl"] = "https://product.example.test" }));
        Assert.Equal($"https://product.example.test/p/{Token}.html", options.GetPublicUrl(Token));
    }

    public static bool HasDatabase => InteriorBusbarStoreTests.HasDatabase;

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task FailurePreservesProduction_RetryUsesLatestCorrection_WithdrawalKeepsAddress()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        var family = await f.Store.Master("product-families", new(null, "F", "Family"), f.Actor);
        var material = await f.Store.Master("materials", new(null, "M", "Material", "개", "도급"), f.Actor);
        var workerId = await f.Store.Master("workers", new(null, "W", "Original worker"), f.Actor);
        await f.Store.Bom(new(family, [new(material, 3)]), f.Actor);
        var productId = await f.Store.Product(new(Guid.NewGuid(), family, workerId), f.Actor);
        await f.Store.Photo(productId, "front", Pixel, null, f.Actor);
        await f.Store.Photo(productId, "back", Pixel, null, f.Actor);
        var config = Config(new() { ["ConnectionStrings:QmsDatabase"] = f.Connection });
        var sink = new RecordingSink();
        using var worker = new InteriorBusbarPublicationWorker(new(config), Options(), sink, NullLogger<InteriorBusbarPublicationWorker>.Instance);
        Assert.True(await worker.PublishNextAsync(TestContext.Current.CancellationToken));
        var failed = await f.Store.GetProduct(productId);
        Assert.Equal("Failed", failed["publicationState"]);
        Assert.Equal("Complete", failed["status"]);
        Assert.DoesNotContain("secret", (string)failed["publicationError"]!);
        Assert.Equal(1m, await f.Balance("Finished", family));
        Assert.Equal(-3m, await f.Balance("Material", material));
        await f.Store.Master("workers", new(workerId, "W", "Correct worker"), f.Actor);
        await f.Store.CorrectProduct(productId, new(workerId, "작업자 정정"), f.Actor);
        await f.Store.RetryPublication(productId);
        sink.Fail = false;
        Assert.True(await worker.PublishNextAsync(TestContext.Current.CancellationToken));
        Assert.Contains("/interior-busbar/production?productId=" + productId, sink.Html);
        Assert.DoesNotContain("data:image", sink.Html);
        Assert.DoesNotContain("Original worker", sink.Html);
        var published = await f.Store.GetProduct(productId);
        Assert.Equal("Published", published["publicationState"]);
        Assert.Equal(published["revision"], published["publishedRevision"]);
        Assert.Equal(failed["manufacturedAtUtc"], published["manufacturedAtUtc"]);
        var address = sink.Token;
        await f.Store.CancelProduct(productId, new(Guid.NewGuid(), "생산 취소"), f.Actor);
        Assert.True(await worker.PublishNextAsync(TestContext.Current.CancellationToken));
        Assert.Equal(address, sink.Token);
        Assert.Contains("정보 제공이 중지", sink.Html);
        Assert.DoesNotContain("Correct worker", sink.Html);
        Assert.DoesNotContain("data:image", sink.Html);
        Assert.Equal(0m, await f.Balance("Finished", family));
        Assert.Equal(0m, await f.Balance("Material", material));
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task HeicOriginalPublishesDerivedJpegWithoutChangingStoredEvidence()
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        var family = await f.Store.Master("product-families", new(null, "F", "Family"), f.Actor);
        var material = await f.Store.Master("materials", new(null, "M", "Material", "개", "도급"), f.Actor);
        var workerId = await f.Store.Master("workers", new(null, "W", "Worker"), f.Actor);
        await f.Store.Bom(new(family, [new(material, 1)]), f.Actor);
        var productId = await f.Store.Product(new(Guid.NewGuid(), family, workerId), f.Actor);
        var validation = await OsanProjects.OsanProgressPhotoValidator.ValidateAsync(
            "original.heic", "image/heic", OsanHeicPhotoTests.CreateHeic(), TestContext.Current.CancellationToken);
        Assert.NotNull(validation.Photo);
        var validated = validation.Photo!;
        await f.Store.Photo(productId, "front", validated.Content, "image/heic", null, f.Actor);
        await f.Store.Photo(productId, "back", Pixel, "image/png", null, f.Actor);

        await f.Store.Settings(new("SYN"), f.Actor);
        var project = await f.Store.Project(new(null, "Shipment", "TEST", family, 1, "Destination", new DateOnly(2026,9,30)), f.Actor);
        await f.Store.InspectProduct(productId,f.Actor,"Synthetic quality inspector");
        await f.Store.Shipment(new(Guid.NewGuid(), project, 1, [productId]), f.Actor);
        // Queue a republish of the frozen shipment snapshot.
        await f.Store.CorrectProduct(productId, new(workerId, "독립 출하 사진 보존 확인"), f.Actor);
        var sink = new RecordingSink { Fail = false };
        using var publication = new InteriorBusbarPublicationWorker(
            new(Config(new() { ["ConnectionStrings:QmsDatabase"] = f.Connection })), Options(), sink,
            NullLogger<InteriorBusbarPublicationWorker>.Instance);
        Assert.True(await publication.PublishNextAsync(TestContext.Current.CancellationToken));

        Assert.Contains("data:image/jpeg;base64,", sink.Html);
        Assert.Contains("data:image/png;base64,", sink.Html);
        var stored = await f.Store.GetPhotoFile(productId, "front");
        Assert.Equal("image/heic", stored.ContentType);
        Assert.Equal(validated.Content, stored.Content);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AzurePublishUsesConditionalVersionToRejectStaleWrite(bool exists)
    {
        var handler = new ConditionalHandler(exists);
        using var sink = new AzureInteriorBusbarPublicationSink(Options(), handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sink.PublishAsync(Token, Encoding.UTF8.GetBytes("latest"), TestContext.Current.CancellationToken));
        Assert.True(handler.SawConditionalPut);
    }

    [Theory(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    [InlineData("business", "OSAN")]
    [InlineData("directory", "DIRECTORY")]
    public async Task WorkerRejectsWrongDatabaseIdentityBeforeReadingOrPublishing(string kind, string code)
    {
        await using var f = await InteriorBusbarStoreTests.Fixture.Create();
        await using (var c = new NpgsqlConnection(f.Connection))
        {
            await c.OpenAsync(TestContext.Current.CancellationToken);
            await using var cmd = new NpgsqlCommand("create table qms_database_identity(singleton bool, database_kind text, business_unit_code text, schema_contract text);insert into qms_database_identity values(true,@kind,@code,@schema)", c);
            cmd.Parameters.AddWithValue("kind", kind); cmd.Parameters.AddWithValue("code", code);
            cmd.Parameters.AddWithValue("schema", BusinessUnitConfiguration.BusinessSchemaVersion);
            await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
        var values = new Dictionary<string, string?> { ["BusinessUnits:Enabled"] = "true" };
        foreach (var (section, unit) in new[] { ("Directory", "DIRECTORY"), ("Units:Cheongju", "CHEONGJU"), ("Units:Osan", "OSAN") })
        {
            var prefix = "BusinessUnits:" + section + ":";
            values[prefix + "Code"] = unit;
            values[prefix + "RuntimeConnection"] = unit + "Runtime";
            values[prefix + "MigrationConnection"] = unit + "Migration";
            values[prefix + "AdministratorConnection"] = unit + "Administrator";
            values[prefix + "ExpectedDatabaseName"] = unit.ToLowerInvariant();
            values[prefix + "RuntimeRoleName"] = unit.ToLowerInvariant() + "_runtime";
            values[prefix + "MigrationRoleName"] = unit.ToLowerInvariant() + "_migration";
            values[prefix + "ExpectedSchemaVersion"] = unit == "DIRECTORY" ? BusinessUnitConfiguration.DirectorySchemaVersion : BusinessUnitConfiguration.BusinessSchemaVersion;
        }
        values["ConnectionStrings:CHEONGJURuntime"] = f.Connection;
        var sink = new RecordingSink { Fail = false };
        using var worker = new InteriorBusbarPublicationWorker(new(Config(values)), Options(), sink, NullLogger<InteriorBusbarPublicationWorker>.Instance);
        var error = await Assert.ThrowsAsync<BusinessUnitContextUnavailableException>(() => worker.PublishNextAsync(TestContext.Current.CancellationToken));
        Assert.Equal("busbar_publication_database_identity_mismatch", error.Reason);
        Assert.Equal("", sink.Html);
    }

    [Fact]
    public async Task DelayedOlderAzurePutCannotOverwriteNewerPage()
    {
        var handler = new DelayedPutHandler();
        using var sink = new AzureInteriorBusbarPublicationSink(Options(), handler);
        var old = sink.PublishAsync(Token, Encoding.UTF8.GetBytes("old"), TestContext.Current.CancellationToken);
        await handler.OldArrived.Task.WaitAsync(TestContext.Current.CancellationToken);
        await sink.PublishAsync(Token, Encoding.UTF8.GetBytes("new"), TestContext.Current.CancellationToken);
        handler.ReleaseOld.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => old);
        Assert.Equal("new", handler.Stored);
    }

    private sealed class DelayedPutHandler : HttpMessageHandler
    {
        public readonly TaskCompletionSource OldArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource ReleaseOld = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? Stored;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Head) return new(HttpStatusCode.NotFound);
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (body == "old")
            {
                OldArrived.SetResult();
                await ReleaseOld.Task.WaitAsync(cancellationToken);
            }
            // Model Azure's atomic If-None-Match conditional commit, including delayed requests.
            Assert.Equal("*", Assert.Single(request.Headers.IfNoneMatch).Tag);
            if (Stored is not null) return new(HttpStatusCode.PreconditionFailed);
            Stored = body;
            return new(HttpStatusCode.Created);
        }
    }

    [Fact]
    public void EnabledPublicAddressMustBelongToSameSeparateStorageAccount()
    {
        var values = new Dictionary<string, string?> {
            ["InteriorBusbar:Publication:Enabled"] = "true",
            ["InteriorBusbar:Publication:PublicBaseUrl"] = "https://products.z1.web.core.windows.net",
            ["InteriorBusbar:Publication:BlobEndpoint"] = "https://products.blob.core.windows.net",
            ["InteriorBusbar:Publication:SasToken"] = "synthetic",
            ["Frontend:Origin"] = "https://pms.example.test" };
        Assert.True(InteriorBusbarPublicationOptions.Load(Config(values)).Enabled);
        values["InteriorBusbar:Publication:PublicBaseUrl"] = "https://wrong.z1.web.core.windows.net";
        Assert.Throws<InvalidOperationException>(() => InteriorBusbarPublicationOptions.Load(Config(values)));
    }

    private static InteriorBusbarPublicationOptions Options() => new(true,
        new("https://products.z1.web.core.windows.net/"), new("https://products.blob.core.windows.net/"), "synthetic", PmsBaseUrl: new("https://pms.example.test/"));

    private sealed class RecordingSink : IInteriorBusbarPublicationSink
    {
        public bool Fail = true;
        public string Html = "";
        public string Token = "";
        public Task PublishAsync(string token, byte[] html, CancellationToken cancellationToken)
        {
            if (Fail) throw new InvalidOperationException("secret storage details");
            Token = token;
            Html = Encoding.UTF8.GetString(html);
            return Task.CompletedTask;
        }
    }
    private sealed class ConditionalHandler(bool exists) : HttpMessageHandler
    {
        public bool SawConditionalPut;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Head)
            {
                var response = new HttpResponseMessage(exists ? HttpStatusCode.OK : HttpStatusCode.NotFound);
                if (exists) response.Headers.ETag = new EntityTagHeaderValue("\"v1\"");
                return Task.FromResult(response);
            }
            Assert.Equal(HttpMethod.Put, request.Method);
            if (exists) Assert.Equal("\"v1\"", Assert.Single(request.Headers.IfMatch).Tag);
            else Assert.Equal("*", Assert.Single(request.Headers.IfNoneMatch).Tag);
            Assert.Equal("no-cache, no-store, must-revalidate", Assert.Single(request.Headers.GetValues("x-ms-blob-cache-control")));
            SawConditionalPut = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PreconditionFailed));
        }
    }

    private static IConfiguration Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
