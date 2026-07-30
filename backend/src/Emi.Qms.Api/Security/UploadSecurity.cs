using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;

namespace Emi.Qms.Api.Security;

public sealed class UploadSecurityOptions
{
    public const string SectionName = "UploadSecurity";

    public bool Enabled { get; set; }
    public bool FailClosed { get; set; } = true;
    public string ScannerHost { get; set; } = "";
    public int ScannerPort { get; set; } = 3310;
    public int TimeoutSeconds { get; set; } = 20;
    public long MaximumFileBytes { get; set; } = 32 * 1024 * 1024;
    public bool RejectImageMetadata { get; set; } = true;
}

public enum UploadMalwareScanStatus
{
    Clean,
    Infected,
    Unavailable
}

public sealed record UploadMalwareScanResult(
    UploadMalwareScanStatus Status,
    string Code);

public interface IUploadMalwareScanner
{
    Task<UploadMalwareScanResult> ScanAsync(
        Stream content,
        CancellationToken cancellationToken);
}

public sealed class ClamAvUploadMalwareScanner(
    IOptions<UploadSecurityOptions> options,
    ILogger<ClamAvUploadMalwareScanner> logger) : IUploadMalwareScanner
{
    public async Task<UploadMalwareScanResult> ScanAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        var configuration = options.Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(configuration.TimeoutSeconds, 1, 120)));

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(
                configuration.ScannerHost,
                configuration.ScannerPort,
                timeout.Token);
            await using var network = client.GetStream();

            await network.WriteAsync("zINSTREAM\0"u8.ToArray(), timeout.Token);

            var buffer = new byte[8192];
            var lengthPrefix = new byte[sizeof(int)];
            while (true)
            {
                var read = await content.ReadAsync(buffer, timeout.Token);
                if (read == 0)
                {
                    break;
                }

                BinaryPrimitives.WriteInt32BigEndian(lengthPrefix, read);
                await network.WriteAsync(lengthPrefix, timeout.Token);
                await network.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
            }

            Array.Clear(lengthPrefix);
            await network.WriteAsync(lengthPrefix, timeout.Token);
            await network.FlushAsync(timeout.Token);

            var responseBytes = new byte[4096];
            var responseLength = await network.ReadAsync(responseBytes, timeout.Token);
            var response = Encoding.UTF8
                .GetString(responseBytes, 0, responseLength)
                .TrimEnd('\0', '\r', '\n');

            if (response.EndsWith(" OK", StringComparison.Ordinal))
            {
                return new UploadMalwareScanResult(UploadMalwareScanStatus.Clean, "CLEAN");
            }

            if (response.Contains(" FOUND", StringComparison.Ordinal))
            {
                return new UploadMalwareScanResult(UploadMalwareScanStatus.Infected, "MALWARE_FOUND");
            }

            logger.LogError("Upload malware scanner returned an unrecognized response.");
            return new UploadMalwareScanResult(UploadMalwareScanStatus.Unavailable, "SCANNER_RESPONSE_INVALID");
        }
        catch (Exception exception) when (
            exception is SocketException
                or IOException
                or OperationCanceledException)
        {
            logger.LogError(exception, "Upload malware scanner is unavailable.");
            return new UploadMalwareScanResult(UploadMalwareScanStatus.Unavailable, "SCANNER_UNAVAILABLE");
        }
    }
}

public sealed class UploadSecurityMiddleware(
    RequestDelegate next,
    IOptions<UploadSecurityOptions> options,
    IUploadMalwareScanner scanner,
    ILogger<UploadSecurityMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var configuration = options.Value;
        if (!configuration.Enabled
            || !context.Request.HasFormContentType
            || context.Request.ContentType?.StartsWith(
                "multipart/form-data",
                StringComparison.OrdinalIgnoreCase) != true)
        {
            await next(context);
            return;
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(context.RequestAborted);
        }
        catch (BadHttpRequestException exception)
        {
            await RejectAsync(
                context,
                exception.StatusCode,
                "업로드 요청을 읽을 수 없습니다.",
                "파일 형식과 크기를 확인해 주세요.");
            return;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            await RejectAsync(
                context,
                StatusCodes.Status400BadRequest,
                "업로드 요청을 읽을 수 없습니다.",
                "파일 형식과 크기를 확인해 주세요.");
            return;
        }

        foreach (var file in form.Files)
        {
            if (file.Length <= 0)
            {
                continue;
            }

            if (file.Length > configuration.MaximumFileBytes)
            {
                await RejectAsync(
                    context,
                    StatusCodes.Status413PayloadTooLarge,
                    "업로드 파일이 너무 큽니다.",
                    "허용된 파일 크기 안에서 다시 시도해 주세요.");
                return;
            }

            if (configuration.RejectImageMetadata
                && await ImageMetadataInspector.ContainsMetadataAsync(
                    file,
                    context.RequestAborted))
            {
                logger.LogWarning(
                    "Upload rejected because image metadata was detected for {Method} {Path}.",
                    context.Request.Method,
                    context.Request.Path);
                await RejectAsync(
                    context,
                    StatusCodes.Status422UnprocessableEntity,
                    "이미지에 제거되지 않은 정보가 있습니다.",
                    "위치·촬영기기 정보가 제거된 이미지로 다시 업로드해 주세요.");
                return;
            }

            await using var content = file.OpenReadStream();
            var result = await scanner.ScanAsync(content, context.RequestAborted);
            if (result.Status == UploadMalwareScanStatus.Infected)
            {
                logger.LogWarning(
                    "Malware upload blocked for {Method} {Path}.",
                    context.Request.Method,
                    context.Request.Path);
                await RejectAsync(
                    context,
                    StatusCodes.Status422UnprocessableEntity,
                    "안전하지 않은 파일이 차단되었습니다.",
                    "파일을 확인한 후 다시 업로드해 주세요.");
                return;
            }

            if (result.Status == UploadMalwareScanStatus.Unavailable
                && configuration.FailClosed)
            {
                await RejectAsync(
                    context,
                    StatusCodes.Status503ServiceUnavailable,
                    "파일 안전 검사를 완료할 수 없습니다.",
                    "잠시 후 다시 시도해 주세요.");
                return;
            }
        }

        await next(context);
    }

    private static Task RejectAsync(
        HttpContext context,
        int status,
        string title,
        string detail)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        return Results.Problem(
                title: title,
                detail: detail,
                statusCode: status)
            .ExecuteAsync(context);
    }
}

internal static class ImageMetadataInspector
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly HashSet<string> MetadataChunks =
        new(StringComparer.Ordinal) { "eXIf", "iTXt", "tEXt", "zTXt" };

    public static async Task<bool> ContainsMetadataAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length < 4)
        {
            return false;
        }

        await using var stream = file.OpenReadStream();
        var signature = new byte[PngSignature.Length];
        if (!await TryReadExactlyAsync(stream, signature, cancellationToken))
        {
            return false;
        }

        if (signature[0] == 0xff && signature[1] == 0xd8)
        {
            stream.Position = 2;
            return await JpegContainsExifAsync(stream, cancellationToken);
        }

        if (signature.AsSpan().SequenceEqual(PngSignature))
        {
            return await PngContainsMetadataAsync(stream, cancellationToken);
        }

        return false;
    }

    private static async Task<bool> JpegContainsExifAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var markerBytes = new byte[2];
        var lengthBytes = new byte[2];
        var exifHeader = new byte[6];
        while (await TryReadExactlyAsync(stream, markerBytes, cancellationToken))
        {
            if (markerBytes[0] != 0xff)
            {
                return false;
            }

            var marker = markerBytes[1];
            while (marker == 0xff)
            {
                if (!await TryReadExactlyAsync(stream, markerBytes.AsMemory(1, 1), cancellationToken))
                {
                    return false;
                }
                marker = markerBytes[1];
            }

            if (marker is 0xda or 0xd9)
            {
                return false;
            }

            if (marker is 0x01 or >= 0xd0 and <= 0xd7)
            {
                continue;
            }

            if (!await TryReadExactlyAsync(stream, lengthBytes, cancellationToken))
            {
                return false;
            }

            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(lengthBytes);
            if (segmentLength < 2)
            {
                return false;
            }
            var dataLength = segmentLength - 2;

            if (marker == 0xe1
                && dataLength >= exifHeader.Length)
            {
                if (!await TryReadExactlyAsync(stream, exifHeader, cancellationToken))
                {
                    return false;
                }
                if (exifHeader.AsSpan().SequenceEqual("Exif\0\0"u8))
                {
                    return true;
                }
                dataLength -= exifHeader.Length;
            }

            if (!await SkipAsync(stream, dataLength, cancellationToken))
            {
                return false;
            }
        }

        return false;
    }

    private static async Task<bool> PngContainsMetadataAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var chunkHeader = new byte[8];
        while (await TryReadExactlyAsync(stream, chunkHeader, cancellationToken))
        {
            var chunkLength = BinaryPrimitives.ReadUInt32BigEndian(chunkHeader);
            if (chunkLength > int.MaxValue)
            {
                return false;
            }

            var chunkType = Encoding.ASCII.GetString(chunkHeader, 4, 4);
            if (MetadataChunks.Contains(chunkType))
            {
                return true;
            }

            if (string.Equals(chunkType, "IEND", StringComparison.Ordinal))
            {
                return false;
            }

            if (!await SkipAsync(stream, chunkLength + 4L, cancellationToken))
            {
                return false;
            }
        }

        return false;
    }

    private static async Task<bool> TryReadExactlyAsync(
        Stream stream,
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < destination.Length)
        {
            var read = await stream.ReadAsync(destination[offset..], cancellationToken);
            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }

    private static async Task<bool> SkipAsync(
        Stream stream,
        long count,
        CancellationToken cancellationToken)
    {
        if (count < 0)
        {
            return false;
        }

        if (stream.CanSeek)
        {
            if (stream.Position + count > stream.Length)
            {
                return false;
            }

            stream.Seek(count, SeekOrigin.Current);
            return true;
        }

        var buffer = new byte[8192];
        var remaining = count;
        while (remaining > 0)
        {
            var read = await stream.ReadAsync(
                buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)),
                cancellationToken);
            if (read == 0)
            {
                return false;
            }

            remaining -= read;
        }

        return true;
    }
}
