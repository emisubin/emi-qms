using System.Buffers.Binary;
using System.Text;

namespace Emi.Qms.Api.OsanProjects;

internal static class OsanProgressPhotoMetadataSanitizer
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] CrcTable = CreateCrcTable();
    private static readonly HashSet<string> PngMetadataChunks = new(StringComparer.Ordinal)
    {
        "eXIf", "iTXt", "tEXt", "zTXt", "tIME"
    };

    public static byte[] Sanitize(byte[] content, string contentType)
    {
        ArgumentNullException.ThrowIfNull(content);
        return contentType switch
        {
            "image/jpeg" => SanitizeJpeg(content),
            "image/png" => SanitizePng(content),
            _ => throw new InvalidDataException("Unsupported image format.")
        };
    }

    internal static ushort? ReadOrientation(byte[] content, string contentType)
    {
        if (contentType == "image/jpeg")
        {
            ushort? orientation = null;
            var sawExif = false;
            foreach (var part in ParseJpeg(content))
            {
                if (part.Marker == 0xe1
                    && content.AsSpan(part.PayloadStart, part.End - part.PayloadStart).StartsWith("Exif\0\0"u8))
                {
                    if (sawExif)
                    {
                        throw new InvalidDataException("Duplicate JPEG EXIF metadata.");
                    }
                    sawExif = true;
                    if (!TryParseExif(
                            content.AsSpan(part.PayloadStart, part.End - part.PayloadStart),
                            out var value))
                    {
                        throw new InvalidDataException("Malformed JPEG EXIF metadata.");
                    }
                    orientation = value;
                }
            }
            return orientation;
        }

        if (contentType == "image/png")
        {
            ushort? orientation = null;
            var sawExif = false;
            foreach (var chunk in ParsePng(content))
            {
                if (chunk.Type == "eXIf")
                {
                    if (sawExif)
                    {
                        throw new InvalidDataException("Duplicate PNG EXIF metadata.");
                    }
                    sawExif = true;
                    if (!TryParseExif(content.AsSpan(chunk.DataStart, chunk.DataLength), out var value))
                    {
                        throw new InvalidDataException("Malformed PNG EXIF metadata.");
                    }
                    orientation = value;
                }
            }
            return orientation;
        }

        return null;
    }

    private static byte[] SanitizeJpeg(byte[] content)
    {
        var parts = ParseJpeg(content);
        var orientation = ReadOrientation(content, "image/jpeg");
        var preserveOrientation = orientation is >= 2 and <= 8;
        var insertedOrientation = false;
        var changed = false;
        using var output = new MemoryStream(content.Length);

        foreach (var part in parts)
        {
            var remove = part.Marker is 0xe1 or 0xed or 0xfe;
            if (!remove)
            {
                output.Write(content, part.Start, part.End - part.Start);
                continue;
            }

            changed = true;
            if (part.Marker == 0xe1 && preserveOrientation && !insertedOrientation)
            {
                WriteJpegOrientation(output, orientation!.Value);
                insertedOrientation = true;
            }
        }

        if (preserveOrientation && !insertedOrientation)
        {
            throw new InvalidDataException("JPEG orientation could not be preserved.");
        }
        return changed ? output.ToArray() : content;
    }

    private static byte[] SanitizePng(byte[] content)
    {
        var chunks = ParsePng(content);
        var orientation = ReadOrientation(content, "image/png");
        var preserveOrientation = orientation is >= 2 and <= 8;
        var insertedOrientation = false;
        var changed = false;
        using var output = new MemoryStream(content.Length);
        output.Write(PngSignature);

        foreach (var chunk in chunks)
        {
            if (!PngMetadataChunks.Contains(chunk.Type))
            {
                output.Write(content, chunk.Start, chunk.End - chunk.Start);
                continue;
            }

            changed = true;
            if (chunk.Type == "eXIf" && preserveOrientation && !insertedOrientation)
            {
                WritePngChunk(output, "eXIf"u8, CreateOrientationTiff(orientation!.Value));
                insertedOrientation = true;
            }
        }

        if (preserveOrientation && !insertedOrientation)
        {
            throw new InvalidDataException("PNG orientation could not be preserved.");
        }
        return changed ? output.ToArray() : content;
    }

    private static List<JpegPart> ParseJpeg(byte[] content)
    {
        if (content.Length < 4 || content[0] != 0xff || content[1] != 0xd8)
        {
            throw new InvalidDataException("Invalid JPEG signature.");
        }

        var parts = new List<JpegPart> { new(0, 2, 0xd8, 2) };
        var offset = 2;
        var sawEnd = false;
        while (offset < content.Length)
        {
            if (content[offset] != 0xff)
            {
                throw new InvalidDataException("Invalid JPEG marker.");
            }
            var markerStart = offset;
            var codeOffset = offset + 1;
            while (codeOffset < content.Length && content[codeOffset] == 0xff)
            {
                codeOffset++;
            }
            if (codeOffset >= content.Length || content[codeOffset] == 0x00)
            {
                throw new InvalidDataException("Invalid JPEG marker.");
            }

            var marker = content[codeOffset];
            if (marker == 0xd9)
            {
                parts.Add(new JpegPart(markerStart, codeOffset + 1, marker, codeOffset + 1));
                offset = codeOffset + 1;
                sawEnd = true;
                break;
            }
            if (marker is 0x01 or >= 0xd0 and <= 0xd7)
            {
                parts.Add(new JpegPart(markerStart, codeOffset + 1, marker, codeOffset + 1));
                offset = codeOffset + 1;
                continue;
            }
            if (codeOffset + 2 >= content.Length)
            {
                throw new InvalidDataException("Truncated JPEG segment.");
            }
            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(content.AsSpan(codeOffset + 1, 2));
            if (segmentLength < 2)
            {
                throw new InvalidDataException("Invalid JPEG segment length.");
            }
            var segmentEndLong = (long)codeOffset + 1 + segmentLength;
            if (segmentEndLong > content.Length)
            {
                throw new InvalidDataException("Truncated JPEG segment.");
            }
            var segmentEnd = (int)segmentEndLong;
            parts.Add(new JpegPart(markerStart, segmentEnd, marker, codeOffset + 3));
            offset = segmentEnd;

            if (marker == 0xda)
            {
                var nextMarker = FindNextJpegMarker(content, offset);
                if (nextMarker > offset)
                {
                    parts.Add(new JpegPart(offset, nextMarker, null, offset));
                }
                offset = nextMarker;
            }
        }

        if (!sawEnd || offset != content.Length)
        {
            throw new InvalidDataException("Invalid JPEG end marker.");
        }
        return parts;
    }

    private static int FindNextJpegMarker(byte[] content, int offset)
    {
        while (offset < content.Length)
        {
            if (content[offset] != 0xff)
            {
                offset++;
                continue;
            }
            var markerStart = offset;
            var codeOffset = offset + 1;
            while (codeOffset < content.Length && content[codeOffset] == 0xff)
            {
                codeOffset++;
            }
            if (codeOffset >= content.Length)
            {
                throw new InvalidDataException("Truncated JPEG scan data.");
            }
            var marker = content[codeOffset];
            if (marker == 0x00 || marker is >= 0xd0 and <= 0xd7)
            {
                offset = codeOffset + 1;
                continue;
            }
            return markerStart;
        }
        throw new InvalidDataException("JPEG scan has no end marker.");
    }

    private static List<PngChunk> ParsePng(byte[] content)
    {
        if (content.Length < PngSignature.Length
            || !content.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature))
        {
            throw new InvalidDataException("Invalid PNG signature.");
        }
        var chunks = new List<PngChunk>();
        var offset = PngSignature.Length;
        var sawEnd = false;
        while (offset <= content.Length - 12)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(offset, 4));
            if (length > int.MaxValue)
            {
                throw new InvalidDataException("PNG chunk is too large.");
            }
            var endLong = (long)offset + 12 + length;
            if (endLong > content.Length)
            {
                throw new InvalidDataException("Truncated PNG chunk.");
            }
            var dataLength = (int)length;
            var type = Encoding.ASCII.GetString(content, offset + 4, 4);
            var end = (int)endLong;
            chunks.Add(new PngChunk(offset, end, type, offset + 8, dataLength));
            offset = end;
            if (type == "IEND")
            {
                sawEnd = true;
                break;
            }
        }
        if (!sawEnd || offset != content.Length)
        {
            throw new InvalidDataException("Invalid PNG end chunk.");
        }
        return chunks;
    }

    private static bool TryParseExif(ReadOnlySpan<byte> data, out ushort? orientation)
    {
        orientation = null;
        if (data.StartsWith("Exif\0\0"u8))
        {
            data = data[6..];
        }
        if (data.Length < 14)
        {
            return false;
        }
        var littleEndian = data[0] == (byte)'I' && data[1] == (byte)'I';
        if (!littleEndian && !(data[0] == (byte)'M' && data[1] == (byte)'M'))
        {
            return false;
        }
        if (ReadUInt16(data, 2, littleEndian) != 42)
        {
            return false;
        }
        var ifdOffset = ReadUInt32(data, 4, littleEndian);
        if (ifdOffset < 8 || ifdOffset > data.Length - 2)
        {
            return false;
        }
        var visited = new HashSet<uint>();
        var remainingEntries = 1024;
        if (!ValidateIfd(
                data,
                ifdOffset,
                littleEndian,
                visited,
                ref remainingEntries,
                out orientation))
        {
            return false;
        }
        return true;
    }

    private static bool ValidateIfd(
        ReadOnlySpan<byte> data,
        uint ifdOffset,
        bool littleEndian,
        HashSet<uint> visited,
        ref int remainingEntries,
        out ushort? orientation)
    {
        orientation = null;
        if (ifdOffset == 0)
        {
            return true;
        }
        if (ifdOffset > data.Length - 2 || !visited.Add(ifdOffset) || visited.Count > 32)
        {
            return false;
        }
        var entryCount = ReadUInt16(data, (int)ifdOffset, littleEndian);
        remainingEntries -= entryCount;
        if (remainingEntries < 0)
        {
            return false;
        }
        var entriesEnd = (long)ifdOffset + 2 + (12L * entryCount);
        if (entriesEnd + 4 > data.Length)
        {
            return false;
        }
        for (var index = 0; index < entryCount; index++)
        {
            var entry = checked((int)ifdOffset + 2 + (12 * index));
            var tag = ReadUInt16(data, entry, littleEndian);
            var type = ReadUInt16(data, entry + 2, littleEndian);
            var count = ReadUInt32(data, entry + 4, littleEndian);
            var typeSize = type switch
            {
                1 or 2 or 6 or 7 => 1,
                3 or 8 => 2,
                4 or 9 or 11 or 13 => 4,
                5 or 10 or 12 => 8,
                _ => 0
            };
            if (typeSize == 0)
            {
                return false;
            }
            var valueSize = (ulong)typeSize * count;
            if (valueSize > 4)
            {
                var valueOffset = ReadUInt32(data, entry + 8, littleEndian);
                if ((ulong)valueOffset + valueSize > (ulong)data.Length)
                {
                    return false;
                }
            }
            if (tag == 0x0112)
            {
                if (type != 3 || count != 1 || orientation is not null)
                {
                    return false;
                }
                var value = ReadUInt16(data, entry + 8, littleEndian);
                if (value is < 1 or > 8)
                {
                    return false;
                }
                orientation = value;
            }

            if (tag is 0x8769 or 0x8825 or 0xa005)
            {
                if (type is not (4 or 13) || count != 1)
                {
                    return false;
                }
                var childIfdOffset = ReadUInt32(data, entry + 8, littleEndian);
                if (childIfdOffset == 0)
                {
                    return false;
                }
                if (!ValidateIfd(
                        data,
                        childIfdOffset,
                        littleEndian,
                        visited,
                        ref remainingEntries,
                        out _))
                {
                    return false;
                }
            }
        }

        var nextIfdOffset = ReadUInt32(data, (int)entriesEnd, littleEndian);
        if (!ValidateIfd(
                data,
                nextIfdOffset,
                littleEndian,
                visited,
                ref remainingEntries,
                out _))
        {
            return false;
        }
        return true;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> data, int offset, bool littleEndian) =>
        littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2))
            : BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset, bool littleEndian) =>
        littleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4))
            : BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));

    private static byte[] CreateOrientationTiff(ushort orientation)
    {
        var tiff = new byte[26];
        tiff[0] = (byte)'I';
        tiff[1] = (byte)'I';
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(2, 2), 42);
        BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(4, 4), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(8, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(10, 2), 0x0112);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(12, 2), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(14, 4), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(18, 2), orientation);
        return tiff;
    }

    private static void WriteJpegOrientation(Stream output, ushort orientation)
    {
        var tiff = CreateOrientationTiff(orientation);
        var payloadLength = 6 + tiff.Length;
        output.WriteByte(0xff);
        output.WriteByte(0xe1);
        Span<byte> length = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(length, checked((ushort)(payloadLength + 2)));
        output.Write(length);
        output.Write("Exif\0\0"u8);
        output.Write(tiff);
    }

    private static void WritePngChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> value = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(value, checked((uint)data.Length));
        output.Write(value);
        output.Write(type);
        output.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(value, ComputeCrc(type, data));
        output.Write(value);
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
        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xedb88320U ^ (value >> 1) : value >> 1;
            }
            table[index] = value;
        }
        return table;
    }

    private sealed record JpegPart(int Start, int End, byte? Marker, int PayloadStart);
    private sealed record PngChunk(int Start, int End, string Type, int DataStart, int DataLength);
}
