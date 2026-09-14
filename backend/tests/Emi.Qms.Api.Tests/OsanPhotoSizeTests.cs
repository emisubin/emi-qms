using System.Buffers.Binary;
using Emi.Qms.Api.OsanProjects;
using Emi.Qms.Api.Security;
using ImageMagick;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class OsanPhotoSizeTests
{
    [Theory]
    [InlineData(6 * 1024 * 1024)]
    [InlineData(40 * 1024 * 1024)]
    public async Task CompletionAcceptsLargeOriginalUpToTotalBoundary(int size)
    {
        var bytes = PngOfSize(size);
        var request = Request([File(bytes)]);
        var result = await OsanProgressEndpointExtensions.ReadCompletionAsync(request, TestContext.Current.CancellationToken);
        Assert.Empty(result.Errors);
        Assert.Equal(bytes, Assert.Single(result.Input!.Photos).Content);
    }

    [Fact]
    public async Task CompletionRejectsCombinedOverflowBeforeImageDecode()
    {
        var result = await OsanProgressEndpointExtensions.ReadCompletionAsync(
            Request([SizedFile(20 * 1024 * 1024), SizedFile(20 * 1024 * 1024 + 1)]), TestContext.Current.CancellationToken);
        Assert.Null(result.Input);
        Assert.Contains("40MiB", Assert.Single(result.Errors["photos"]));
    }

    [Theory]
    [InlineData(true, 40 * 1024 * 1024, 0, UploadMalwareScanStatus.Clean, 200, 1)]
    [InlineData(true, 20 * 1024 * 1024, 20 * 1024 * 1024 + 1, UploadMalwareScanStatus.Clean, 413, 0)]
    [InlineData(false, 32 * 1024 * 1024 + 1, 0, UploadMalwareScanStatus.Clean, 413, 0)]
    [InlineData(true, 33 * 1024 * 1024, 0, UploadMalwareScanStatus.Infected, 422, 1)]
    [InlineData(true, 33 * 1024 * 1024, 0, UploadMalwareScanStatus.Unavailable, 503, 1)]
    public async Task ScopedTotalBudgetPreservesScanningAndOtherUploadLimits(bool scoped, int first, int second,
        UploadMalwareScanStatus scanStatus, int expectedStatus, int expectedScans)
    {
        var files = new FormFileCollection { SizedFile(first) };
        if (second > 0) files.Add(SizedFile(second));
        var context = Request(files).HttpContext;
        context.Response.Body = new MemoryStream();
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        context.RequestServices = services;
        if (scoped) context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new UploadTotalSizeLimitAttribute(40 * 1024 * 1024)), "test"));
        var scanner = new Scanner(scanStatus);
        var reached = false;
        var middleware = new UploadSecurityMiddleware(_ => { reached = true; return Task.CompletedTask; },
            Options.Create(new UploadSecurityOptions { Enabled = true, RejectImageMetadata = false }),
            scanner, NullLogger<UploadSecurityMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal(expectedScans, scanner.Calls);
        Assert.Equal(expectedStatus == 200, reached);
    }

    private static HttpRequest Request(IEnumerable<IFormFile> files)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "multipart/form-data; boundary=synthetic";
        var collection = new FormFileCollection(); foreach (var file in files) collection.Add(file);
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["operationId"] = Guid.NewGuid().ToString(), ["completionMode"] = "individual", ["stageSequence"] = "1",
            ["targets"] = $"[{{\"targetId\":\"{Guid.NewGuid()}\",\"expectedVersion\":1}}]"
        }, collection);
        return context.Request;
    }
    private static IFormFile File(byte[] bytes) => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "photos", "original.png")
        { Headers = new HeaderDictionary(), ContentType = "image/png" };
    private static IFormFile SizedFile(int size) => new FormFile(new MemoryStream([1]), 0, size, "photos", "photo.png")
        { Headers = new HeaderDictionary(), ContentType = "image/png" };
    private sealed class Scanner(UploadMalwareScanStatus status) : IUploadMalwareScanner
    {
        public int Calls { get; private set; }
        public Task<UploadMalwareScanResult> ScanAsync(Stream content, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(new UploadMalwareScanResult(status, "TEST")); }
    }
    internal static byte[] PngOfSize(int size)
    {
        using var image = new MagickImage(MagickColors.Blue, 1, 1);
        var png = OsanProgressPhotoMetadataSanitizer.Sanitize(image.ToByteArray(MagickFormat.Png), "image/png");
        // A legal ancillary chunk exercises byte limits without large decoded pixel allocations.
        var extra = new byte[size - png.Length];
        BinaryPrimitives.WriteUInt32BigEndian(extra, (uint)(extra.Length - 12));
        "vpAg"u8.CopyTo(extra.AsSpan(4));
        uint crc = uint.MaxValue;
        foreach (var value in extra.AsSpan(4, extra.Length - 8))
        { crc ^= value; for (var bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? 0xedb88320U ^ (crc >> 1) : crc >> 1; }
        BinaryPrimitives.WriteUInt32BigEndian(extra.AsSpan(extra.Length - 4), crc ^ uint.MaxValue);
        return [.. png.AsSpan(0, png.Length - 12), .. extra, .. png.AsSpan(png.Length - 12)];
    }
}
