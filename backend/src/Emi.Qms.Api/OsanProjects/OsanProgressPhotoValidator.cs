using System.Security.Cryptography;
using System.Buffers.Binary;
using System.IO.Compression;
using ImageMagick;

namespace Emi.Qms.Api.OsanProjects;

public static class OsanProgressPhotoValidator
{
    public const int MaximumPhotoCount = 5;
    public const int MaximumPhotoBytes = 5 * 1024 * 1024;
    public const int MaximumTotalBytes = 15 * 1024 * 1024;
    public const long MaximumMultipartBytes = MaximumTotalBytes + (2 * 1024 * 1024);
    private static readonly Lazy<bool> DecoderLimitsConfigured = new(ConfigureDecoderResourceLimitsCore);

    public static void ConfigureDecoderResourceLimits() => _ = DecoderLimitsConfigured.Value;

    public static async Task<(OsanProgressPhotoInput? Photo, string? Error)> ValidateAsync(
        string? fileName,
        string? declaredContentType,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        if (content.Length is < 1 or > MaximumPhotoBytes)
        {
            return (null, "사진은 장당 5MiB 이하여야 합니다.");
        }

        string? normalizedMime;
        try
        {
            ConfigureDecoderResourceLimits();
            normalizedMime = await OsanImageContentValidator.DetectValidFormatAsync(
                content,
                cancellationToken);
        }
        catch (OsanImageResourceLimitException)
        {
            return (null, "서버에서 이미지를 안전하게 처리할 수 없습니다. 다른 이미지 파일을 선택해 주세요.");
        }
        if (normalizedMime is null)
        {
            return (null, "파일 내용이 올바른 JPEG 또는 PNG 이미지가 아닙니다.");
        }

        var normalizedDeclaredType = declaredContentType?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(normalizedDeclaredType)
            && normalizedDeclaredType != "application/octet-stream"
            && !string.Equals(normalizedDeclaredType, normalizedMime, StringComparison.Ordinal))
        {
            return (null, "파일의 형식 정보와 실제 이미지 형식이 일치하지 않습니다.");
        }

        var normalizedPath = (fileName ?? string.Empty).Replace('\\', '/');
        var safeFileName = Path.GetFileName(normalizedPath).Trim();
        safeFileName = new string(safeFileName.Where(character => !char.IsControl(character)).ToArray());
        if (safeFileName.Length == 0)
        {
            safeFileName = normalizedMime == "image/png" ? "photo.png" : "photo.jpg";
        }
        if (safeFileName.Length > 255)
        {
            safeFileName = safeFileName[..255];
        }
        var extension = Path.GetExtension(safeFileName);
        var extensionMatches = normalizedMime == "image/png"
            ? string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
            : string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
              || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase);
        if (!extensionMatches)
        {
            return (null, "파일 확장자와 실제 이미지 형식이 일치하지 않습니다.");
        }

        var sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        return (new OsanProgressPhotoInput(safeFileName, normalizedMime, content, sha256), null);
    }

    private static bool ConfigureDecoderResourceLimitsCore()
    {
        ResourceLimits.Memory = 128UL * 1024 * 1024;
        ResourceLimits.MaxMemoryRequest = 64UL * 1024 * 1024;
        ResourceLimits.Disk = 256UL * 1024 * 1024;
        ResourceLimits.Thread = 1;
        ResourceLimits.ListLength = 2;
        return true;
    }
}

internal static class OsanImageContentValidator
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
    private static readonly uint[] CrcTable = CreateCrcTable();
    private static readonly SemaphoreSlim JpegDecodeGate = new(1, 1);
    public static async Task<string?> DetectValidFormatAsync(
        byte[] content,
        CancellationToken cancellationToken)
    {
        if (await IsValidPngAsync(content, cancellationToken))
        {
            return "image/png";
        }
        if (!IsValidJpeg(content, cancellationToken, out var jpegWidth, out var jpegHeight))
        {
            return null;
        }
        return await HasStrictJpegDecodeAsync(
            content,
            jpegWidth,
            jpegHeight,
            cancellationToken)
            ? "image/jpeg"
            : null;
    }

    private static async Task<bool> IsValidPngAsync(
        byte[] content,
        CancellationToken cancellationToken)
    {
        if (content.Length < 57 || !content.AsSpan(0, 8).SequenceEqual(PngSignature))
        {
            return false;
        }

        var offset = 8;
        var firstChunk = true;
        var sawIdat = false;
        var idatEnded = false;
        var sawPalette = false;
        uint width = 0;
        uint height = 0;
        byte bitDepth = 0;
        byte colorType = 0;
        byte interlace = 0;
        using var compressed = new MemoryStream();
        while (offset <= content.Length - 12)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var length = BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(offset, 4));
            if (length > int.MaxValue)
            {
                return false;
            }
            var dataLength = (int)length;
            var chunkEnd = (long)offset + 12L + dataLength;
            if (chunkEnd > content.Length)
            {
                return false;
            }

            var type = content.AsSpan(offset + 4, 4);
            if (!IsValidChunkType(type))
            {
                return false;
            }
            var data = content.AsSpan(offset + 8, dataLength);
            var expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(offset + 8 + dataLength, 4));
            if (ComputeCrc(type, data) != expectedCrc)
            {
                return false;
            }

            if (firstChunk)
            {
                if (!type.SequenceEqual("IHDR"u8) || dataLength != 13)
                {
                    return false;
                }
                width = BinaryPrimitives.ReadUInt32BigEndian(data[..4]);
                height = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4, 4));
                bitDepth = data[8];
                colorType = data[9];
                interlace = data[12];
                if (width == 0 || height == 0
                    || !IsValidPngColorDepth(colorType, bitDepth)
                    || data[10] != 0 || data[11] != 0 || interlace > 1)
                {
                    return false;
                }
                firstChunk = false;
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                if (idatEnded || colorType == 3 && !sawPalette)
                {
                    return false;
                }
                sawIdat = true;
                compressed.Write(data);
            }
            else if (type.SequenceEqual("PLTE"u8))
            {
                if (sawIdat || sawPalette || colorType is 0 or 4
                    || dataLength is < 3 or > 768 || dataLength % 3 != 0
                    || colorType == 3 && dataLength / 3 > 1 << bitDepth)
                {
                    return false;
                }
                sawPalette = true;
            }
            else
            {
                if (sawIdat)
                {
                    idatEnded = true;
                }
                if (type.SequenceEqual("IEND"u8))
                {
                    if (dataLength != 0 || !sawIdat || chunkEnd != content.Length)
                    {
                        return false;
                    }
                    return await HasValidInflatedContentAsync(
                        compressed,
                        width,
                        height,
                        colorType,
                        bitDepth,
                        interlace,
                        cancellationToken);
                }
                if (IsUnknownCriticalChunk(type))
                {
                    return false;
                }
            }

            offset = (int)chunkEnd;
        }
        return false;
    }

    private static async Task<bool> HasValidInflatedContentAsync(
        MemoryStream compressed,
        uint width,
        uint height,
        byte colorType,
        byte bitDepth,
        byte interlace,
        CancellationToken cancellationToken)
    {
        try
        {
            compressed.Position = 0;
            using var zlib = new ZLibStream(compressed, CompressionMode.Decompress, leaveOpen: true);
            var buffer = new byte[8192];
            var layout = new PngInflatedLayout(width, height, colorType, bitDepth, interlace);
            while (true)
            {
                var read = await zlib.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }
                if (!layout.Accept(buffer.AsSpan(0, read)))
                {
                    return false;
                }
            }
            return layout.IsComplete;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static bool IsValidPngColorDepth(byte colorType, byte bitDepth) => colorType switch
    {
        0 => bitDepth is 1 or 2 or 4 or 8 or 16,
        2 => bitDepth is 8 or 16,
        3 => bitDepth is 1 or 2 or 4 or 8,
        4 => bitDepth is 8 or 16,
        6 => bitDepth is 8 or 16,
        _ => false
    };

    private static bool IsUnknownCriticalChunk(ReadOnlySpan<byte> type) =>
        type[0] is >= (byte)'A' and <= (byte)'Z'
        && !type.SequenceEqual("PLTE"u8);

    private static bool IsValidChunkType(ReadOnlySpan<byte> type)
    {
        if (type.Length != 4)
        {
            return false;
        }
        foreach (var value in type)
        {
            if (value is not (>= (byte)'A' and <= (byte)'Z')
                and not (>= (byte)'a' and <= (byte)'z'))
            {
                return false;
            }
        }
        return true;
    }

    private static uint ComputeCrc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var value in type)
        {
            crc = CrcTable[(crc ^ value) & 0xff] ^ (crc >> 8);
        }
        foreach (var value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xff] ^ (crc >> 8);
        }
        return crc ^ uint.MaxValue;
    }

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index += 1)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit += 1)
            {
                value = (value & 1) != 0 ? 0xedb88320U ^ (value >> 1) : value >> 1;
            }
            table[index] = value;
        }
        return table;
    }

    private static bool IsValidJpeg(
        byte[] content,
        CancellationToken cancellationToken,
        out ushort width,
        out ushort height)
    {
        width = 0;
        height = 0;
        if (content.Length < 20 || content[0] != 0xff || content[1] != 0xd8)
        {
            return false;
        }
        var offset = 2;
        var sawFrame = false;
        var sawQuantizationTable = false;
        var sawCodingTable = false;
        var sawScan = false;
        var entropyBytes = 0;
        while (offset < content.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (content[offset] != 0xff)
            {
                return false;
            }
            var markerStart = offset;
            while (offset < content.Length && content[offset] == 0xff)
            {
                offset += 1;
            }
            if (offset >= content.Length)
            {
                return false;
            }
            var marker = content[offset++];
            if (marker == 0xd9)
            {
                return offset == content.Length
                    && sawFrame && sawQuantizationTable && sawCodingTable
                    && sawScan;
            }
            if (marker is 0xd8 or 0x01 or >= 0xd0 and <= 0xd7)
            {
                continue;
            }
            if (offset > content.Length - 2)
            {
                return false;
            }
            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(content.AsSpan(offset, 2));
            if (segmentLength < 2 || (long)offset + segmentLength > content.Length)
            {
                return false;
            }
            if (IsStartOfFrame(marker))
            {
                if (segmentLength < 8)
                {
                    return false;
                }
                height = BinaryPrimitives.ReadUInt16BigEndian(content.AsSpan(offset + 3, 2));
                width = BinaryPrimitives.ReadUInt16BigEndian(content.AsSpan(offset + 5, 2));
                if (width == 0 || height == 0)
                {
                    return false;
                }
                sawFrame = true;
            }
            else if (marker == 0xdb)
            {
                sawQuantizationTable = true;
            }
            else if (marker is 0xc4 or 0xcc)
            {
                sawCodingTable = true;
            }

            offset += segmentLength;
            if (marker != 0xda)
            {
                continue;
            }
            if (!sawFrame)
            {
                return false;
            }
            sawScan = true;
            while (offset < content.Length)
            {
                if ((offset & 0xffff) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
                if (content[offset] != 0xff)
                {
                    entropyBytes += 1;
                    offset += 1;
                    continue;
                }
                var scanMarkerStart = offset;
                while (offset < content.Length && content[offset] == 0xff)
                {
                    offset += 1;
                }
                if (offset >= content.Length)
                {
                    return false;
                }
                var scanMarker = content[offset];
                if (scanMarker == 0x00)
                {
                    entropyBytes += 1;
                    offset += 1;
                    continue;
                }
                if (scanMarker is >= 0xd0 and <= 0xd7)
                {
                    offset += 1;
                    continue;
                }
                offset = scanMarkerStart;
                break;
            }
            if (offset == markerStart)
            {
                return false;
            }
        }
        return false;
    }

    private static async Task<bool> HasStrictJpegDecodeAsync(
        byte[] content,
        ushort expectedWidth,
        ushort expectedHeight,
        CancellationToken cancellationToken)
    {
        await JpegDecodeGate.WaitAsync(cancellationToken);
        try
        {
            var sawWarning = false;
            using var image = new MagickImage();
            image.Warning += (_, _) => sawWarning = true;
            using var stream = new MemoryStream(content, writable: false);
            await image.ReadAsync(stream, MagickFormat.Jpeg, cancellationToken);
            return !sawWarning
                && image.Width == expectedWidth
                && image.Height == expectedHeight;
        }
        catch (MagickResourceLimitErrorException exception)
        {
            throw new OsanImageResourceLimitException(exception);
        }
        catch (MagickException)
        {
            return false;
        }
        finally
        {
            JpegDecodeGate.Release();
        }
    }

    private static bool IsStartOfFrame(byte marker) =>
        marker is 0xc0 or 0xc1 or 0xc2 or 0xc3 or 0xc5 or 0xc6 or 0xc7
            or 0xc9 or 0xca or 0xcb or 0xcd or 0xce or 0xcf;

    private sealed class PngInflatedLayout
    {
        private readonly (ulong RowBytes, ulong Rows)[] passes;
        private int passIndex;
        private ulong rowsRemaining;
        private ulong bytesRemainingInRow;
        private bool expectingFilter = true;

        public PngInflatedLayout(
            uint width,
            uint height,
            byte colorType,
            byte bitDepth,
            byte interlace)
        {
            var channels = colorType switch
            {
                0 => 1UL,
                2 => 3UL,
                3 => 1UL,
                4 => 2UL,
                6 => 4UL,
                _ => 0UL
            };
            var bitsPerPixel = channels * bitDepth;
            if (interlace == 0)
            {
                passes = [(RowBytes(width, bitsPerPixel), height)];
            }
            else
            {
                int[] startX = [0, 4, 0, 2, 0, 1, 0];
                int[] startY = [0, 0, 4, 0, 2, 0, 1];
                int[] stepX = [8, 8, 4, 4, 2, 2, 1];
                int[] stepY = [8, 8, 8, 4, 4, 2, 2];
                passes = Enumerable.Range(0, 7).Select(pass =>
                {
                    var passWidth = PassLength(width, startX[pass], stepX[pass]);
                    var passHeight = PassLength(height, startY[pass], stepY[pass]);
                    return (RowBytes(passWidth, bitsPerPixel), passHeight);
                }).ToArray();
            }
            MoveToNextPass();
        }

        public bool IsComplete => passIndex >= passes.Length;

        public bool Accept(ReadOnlySpan<byte> bytes)
        {
            foreach (var value in bytes)
            {
                if (IsComplete)
                {
                    return false;
                }
                if (expectingFilter)
                {
                    if (value > 4)
                    {
                        return false;
                    }
                    expectingFilter = false;
                    bytesRemainingInRow = passes[passIndex].RowBytes;
                    if (bytesRemainingInRow == 0)
                    {
                        FinishRow();
                    }
                    continue;
                }

                bytesRemainingInRow -= 1;
                if (bytesRemainingInRow == 0)
                {
                    FinishRow();
                }
            }
            return true;
        }

        private void FinishRow()
        {
            rowsRemaining -= 1;
            expectingFilter = true;
            if (rowsRemaining == 0)
            {
                passIndex += 1;
                MoveToNextPass();
            }
        }

        private void MoveToNextPass()
        {
            while (passIndex < passes.Length
                   && (passes[passIndex].Rows == 0 || passes[passIndex].RowBytes == 0))
            {
                passIndex += 1;
            }
            if (passIndex < passes.Length)
            {
                rowsRemaining = passes[passIndex].Rows;
                expectingFilter = true;
            }
        }

        private static ulong RowBytes(ulong pixelWidth, ulong bitsPerPixel) =>
            (pixelWidth * bitsPerPixel + 7) / 8;

        private static ulong PassLength(uint fullLength, int start, int step) =>
            fullLength <= start
                ? 0UL
                : ((ulong)fullLength - (uint)start + (uint)step - 1) / (uint)step;
    }
}

internal sealed class OsanImageResourceLimitException(Exception innerException)
    : Exception("The image decoder resource limit was exceeded.", innerException);
