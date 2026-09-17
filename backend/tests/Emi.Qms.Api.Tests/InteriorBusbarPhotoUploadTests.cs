using System.Net;
using System.Net.Http.Headers;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.InteriorBusbar;
using Emi.Qms.Api.OsanProjects;
using Emi.Qms.Api.Security;
using ImageMagick;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class InteriorBusbarPhotoUploadTests
{
    public static bool HasDatabase => InteriorBusbarStoreTests.HasDatabase;

    [Fact]
    public void EndpointUsesPostScanSanitizationAndFortyMiBBudget()
    {
        using var factory = new QmsWebApplicationFactory();
        var endpoint = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(candidate => candidate.RoutePattern.RawText == "/api/interior-busbar/products/{id:guid}/photos/{side}"
                && candidate.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains("PUT") == true);

        Assert.NotNull(endpoint.Metadata.GetMetadata<SanitizeImageMetadataAfterScanAttribute>());
        Assert.Equal(40 * 1024 * 1024,
            endpoint.Metadata.GetRequiredMetadata<UploadTotalSizeLimitAttribute>().MaximumBytes);
        Assert.Equal(42 * 1024 * 1024,
            endpoint.Metadata.GetRequiredMetadata<IRequestSizeLimitMetadata>().MaxRequestBodySize);
    }

    [Fact]
    public async Task DirectWebpValidationRejectsOversizeBeforeDecode()
    {
        var oversized = new byte[InteriorBusbarPhotoValidator.MaximumTotalBytes + 1];
        "RIFF"u8.CopyTo(oversized);
        "WEBP"u8.CopyTo(oversized.AsSpan(8));

        var result = await InteriorBusbarPhotoValidator.ValidateAsync(
            "large.webp", "image/webp", oversized, TestContext.Current.CancellationToken);

        Assert.Null(result.Photo);
        Assert.Contains("40MiB", result.Error);
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task MigrationAddsHistoryMimeWithoutChangingLegacyPhotoBytes()
    {
        await using var fixture = await InteriorBusbarStoreTests.Fixture.Create(applyPhotoMigration: false);
        var family = await fixture.Store.Master("product-families", new(null, "F", "Family"), fixture.Actor);
        var worker = await fixture.Store.Master("workers", new(null, "W", "Worker"), fixture.Actor);
        var product = await fixture.Store.Product(new(Guid.NewGuid(), family, worker), fixture.Actor);
        byte[] legacy = [0xff, 0xd8, 0xff, 0xd9];
        await using var connection = new NpgsqlConnection(fixture.Connection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using (var seed = new NpgsqlCommand("""
            insert into busbar_photos(product_id,side,content,content_type,registered_at_utc,registered_by)
            values(@product,'front',@content,'image/jpeg',now(),@actor);
            insert into busbar_photo_history(id,product_id,side,content,registered_at_utc,registered_by,reason)
            values(gen_random_uuid(),@product,'front',@content,now(),@actor,'legacy');
            """, connection))
        {
            seed.Parameters.AddWithValue("product", product);
            seed.Parameters.AddWithValue("content", legacy);
            seed.Parameters.AddWithValue("actor", fixture.Actor);
            await seed.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
        var root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "database", "migrations"))) root = Directory.GetParent(root)!.FullName;
        await new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(root, "database/migrations/0118_interior_busbar_photo_originals.sql"), TestContext.Current.CancellationToken), connection)
            .ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        await using var read = new NpgsqlCommand("select content,content_type from busbar_photo_history where product_id=@product", connection);
        read.Parameters.AddWithValue("product", product);
        await using var reader = await read.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(legacy, reader.GetFieldValue<byte[]>(0));
        Assert.Equal("image/jpeg", reader.GetString(1));
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task MultipartUploadScansOriginalThenPreservesSanitizedJpegPngHeicAndWebpCompatibility()
    {
        await using var fixture = await InteriorBusbarStoreTests.Fixture.Create();
        var family = await fixture.Store.Master("product-families", new(null, "F", "Family"), fixture.Actor);
        var material = await fixture.Store.Master("materials", new(null, "M", "Material", "개", "도급"), fixture.Actor);
        var worker = await fixture.Store.Master("workers", new(null, "W", "Worker"), fixture.Actor);
        await fixture.Store.Bom(new(family, [new(material, 1)]), fixture.Actor);
        var scanner = new CapturingScanner(UploadMalwareScanStatus.Clean);
        using var factory = Factory(fixture, scanner);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "busbar-fixture");

        var jpeg = OsanHdrPhotoTests.CreateHdr();
        using var pngImage = new MagickImage(MagickColors.CornflowerBlue, 23, 17);
        var png = pngImage.ToByteArray(MagickFormat.Png);
        var heic = OsanHeicPhotoTests.CreateHeic();
        using var webpImage = new MagickImage(MagickColors.MediumSeaGreen, 19, 13);
        webpImage.SetProfile(ColorProfiles.AdobeRGB1998);
        webpImage.Orientation = OrientationType.RightTop;
        var webpExif = new ExifProfile();
        webpExif.SetValue(ExifTag.Orientation, (ushort)OrientationType.RightTop);
        webpImage.SetProfile(webpExif);
        var webp = webpImage.ToByteArray(MagickFormat.WebP);
        using var expectedWebp = new MagickImage(webp);
        Assert.Equal(OrientationType.RightTop, expectedWebp.Orientation);
        expectedWebp.AutoOrient();
        expectedWebp.TransformColorSpace(ColorProfiles.SRGB);
        expectedWebp.Depth = 8;
        var expectedSrgbPixels = expectedWebp.ToByteArray(MagickFormat.Rgba);

        await AssertStored(client, fixture, family, worker, scanner, jpeg, "photo.jpg", "image/jpeg", "image/jpeg");
        await AssertStored(client, fixture, family, worker, scanner, png, "photo.png", "image/png", "image/png");
        var heicProduct = await AssertStored(client, fixture, family, worker, scanner, heic, "photo.heic", "image/heic", "image/heic");
        var webpProduct = await AssertStored(client, fixture, family, worker, scanner, webp, "photo.webp", "image/webp", "image/png");
        using (var normalizedWebp = new MagickImage((await fixture.Store.GetPhotoFile(webpProduct, "front")).Content))
        {
            Assert.Equal(expectedWebp.Width, normalizedWebp.Width);
            Assert.Equal(expectedWebp.Height, normalizedWebp.Height);
            Assert.Null(normalizedWebp.GetColorProfile());
            Assert.Equal(OrientationType.TopLeft, normalizedWebp.Orientation);
            normalizedWebp.Depth = 8;
            var actualPixels = normalizedWebp.ToByteArray(MagickFormat.Rgba);
            Assert.Equal(expectedSrgbPixels.Length, actualPixels.Length);
            Assert.All(actualPixels.Zip(expectedSrgbPixels), pair =>
                Assert.InRange(Math.Abs(pair.First - pair.Second), 0, 1));
        }

        using var originalResponse = await client.GetAsync($"/api/interior-busbar/products/{heicProduct}/photos/front", TestContext.Current.CancellationToken);
        Assert.Equal("image/heic", originalResponse.Content.Headers.ContentType?.MediaType);
        using var previewResponse = await client.GetAsync($"/api/interior-busbar/products/{heicProduct}/photos/front?preview=true", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        Assert.Equal("image/jpeg", previewResponse.Content.Headers.ContentType?.MediaType);
        var preview = await previewResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.True(preview.AsSpan(0, 2).SequenceEqual([(byte)0xff, (byte)0xd8]));

        var mismatchProduct = await fixture.Store.Product(new(Guid.NewGuid(), family, worker), fixture.Actor);
        using var mismatch = await Upload(client, mismatchProduct, heic, "wrong.jpg", "image/jpeg", worker);
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        using var unknown = await Upload(client, mismatchProduct, [1, 2, 3, 4], "unknown.bin", "application/octet-stream", worker);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(6, scanner.Files.Count);
        Assert.Equal(0L, await fixture.Scalar($"select count(*) from busbar_photos where product_id='{mismatchProduct}'"));
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task CombinedStoredOriginalsCannotExceedFortyMiB()
    {
        await using var fixture = await InteriorBusbarStoreTests.Fixture.Create();
        var family = await fixture.Store.Master("product-families", new(null, "F", "Family"), fixture.Actor);
        var material = await fixture.Store.Master("materials", new(null, "M", "Material", "개", "도급"), fixture.Actor);
        var worker = await fixture.Store.Master("workers", new(null, "W", "Worker"), fixture.Actor);
        await fixture.Store.Bom(new(family, [new(material, 1)]), fixture.Actor);
        var product = await fixture.Store.Product(new(Guid.NewGuid(), family, worker), fixture.Actor);
        using var factory = Factory(fixture, new CapturingScanner(UploadMalwareScanStatus.Clean));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "busbar-fixture");

        using var first = await Upload(client, product, OsanPhotoSizeTests.PngOfSize(21 * 1024 * 1024), "front.png", "image/png", worker, "front");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var overflow = await Upload(client, product, OsanPhotoSizeTests.PngOfSize(20 * 1024 * 1024), "back.png", "image/png", worker, "back");
        Assert.Equal(HttpStatusCode.BadRequest, overflow.StatusCode);
        Assert.Contains("40MiB", await overflow.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1L, await fixture.Scalar($"select count(*) from busbar_photos where product_id='{product}'"));
        Assert.Equal("Draft", (await fixture.Store.GetProduct(product))["status"]);
    }

    [Fact(SkipUnless = nameof(HasDatabase), Skip = "Requires disposable busbar database.")]
    public async Task InfectedOriginalIsRejectedBeforeSanitizationAndNothingIsStored()
    {
        await using var fixture = await InteriorBusbarStoreTests.Fixture.Create();
        var family = await fixture.Store.Master("product-families", new(null, "F", "Family"), fixture.Actor);
        var worker = await fixture.Store.Master("workers", new(null, "W", "Worker"), fixture.Actor);
        var product = await fixture.Store.Product(new(Guid.NewGuid(), family, worker), fixture.Actor);
        var original = OsanHdrPhotoTests.CreateHdr();
        var scanner = new CapturingScanner(UploadMalwareScanStatus.Infected);
        using var factory = Factory(fixture, scanner);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "busbar-fixture");

        using var response = await Upload(client, product, original, "photo.jpg", "image/jpeg", worker);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(original, Assert.Single(scanner.Files));
        Assert.Equal(0L, await fixture.Scalar("select count(*) from busbar_photos"));
        Assert.Equal(0L, await fixture.Scalar("select count(*) from busbar_photo_history"));
    }

    private static async Task<Guid> AssertStored(HttpClient client, InteriorBusbarStoreTests.Fixture fixture,
        Guid family, Guid worker, CapturingScanner scanner, byte[] original, string fileName,
        string declaredMime, string storedMime)
    {
        var product = await fixture.Store.Product(new(Guid.NewGuid(), family, worker), fixture.Actor);
        var scannedBefore = scanner.Files.Count;
        using var response = await Upload(client, product, original, fileName, declaredMime, worker);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(original, scanner.Files[scannedBefore]);
        var validation = await InteriorBusbarPhotoValidator.ValidateAsync(
            fileName, declaredMime, original, TestContext.Current.CancellationToken);
        Assert.NotNull(validation.Photo);
        var expected = validation.Photo!;
        using var download = await client.GetAsync($"/api/interior-busbar/products/{product}/photos/front", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(storedMime, download.Content.Headers.ContentType?.MediaType);
        var stored = await download.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expected.Content, stored);
        if (declaredMime == "image/jpeg")
            Assert.DoesNotContain("SYNTHETIC-PRIVATE", System.Text.Encoding.UTF8.GetString(stored));
        if (declaredMime == "image/heic")
            Assert.DoesNotContain("SECRET-MAKE", System.Text.Encoding.ASCII.GetString(stored));
        Assert.Equal(1L, await fixture.Scalar($"select count(*) from busbar_photo_history where product_id='{product}' and content_type='{storedMime}'"));
        return product;
    }

    private static async Task<HttpResponseMessage> Upload(HttpClient client, Guid product, byte[] bytes,
        string fileName, string mime, Guid worker, string side = "front")
    {
        using var body = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(mime);
        body.Add(file, "file", fileName);
        body.Add(new StringContent(worker.ToString()), "workerId");
        return await client.PutAsync($"/api/interior-busbar/products/{product}/photos/{side}", body, TestContext.Current.CancellationToken);
    }

    private static QmsWebApplicationFactory Factory(InteriorBusbarStoreTests.Fixture fixture, IUploadMalwareScanner scanner) =>
        QmsWebApplicationFactory.Create("Testing", new Dictionary<string, string?>
        {
            ["DevAuthentication:Enabled"] = "true",
            ["Database:ApplyMigrationsOnStartup"] = "false",
            ["DevelopmentData:SeedEnabled"] = "false",
            ["ConnectionStrings:QmsDatabase"] = fixture.Connection,
            ["InteriorBusbar:Publication:Enabled"] = "false",
            ["UploadSecurity:Enabled"] = "true",
            ["UploadSecurity:FailClosed"] = "true",
            ["UploadSecurity:RejectImageMetadata"] = "true"
        }, identityStore: new InteriorBusbarAuthorizationTests.MutableIdentity(fixture.Actor), configureTestServices: services =>
        {
            var existing = services.Single(service => service.ServiceType == typeof(IUploadMalwareScanner));
            services.Remove(existing);
            services.AddSingleton(scanner);
        });

    private sealed class CapturingScanner(UploadMalwareScanStatus status) : IUploadMalwareScanner
    {
        public List<byte[]> Files { get; } = [];

        public async Task<UploadMalwareScanResult> ScanAsync(Stream content, CancellationToken cancellationToken)
        {
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            Files.Add(copy.ToArray());
            return new(status, status.ToString());
        }
    }
}
