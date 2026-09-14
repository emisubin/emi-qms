using System.Buffers.Binary;

namespace Emi.Qms.Api.OsanProjects;

// MPF offsets are relative to the MP TIFF header, except the primary image's zero offset.
internal static class OsanJpegContainer
{
    internal sealed record Image(byte[] Content, uint Attributes, ushort Dependent1, ushort Dependent2);

    internal static IReadOnlyList<Image> Read(byte[] content)
    {
        var primaryParts = OsanProgressPhotoMetadataSanitizer.ParseJpeg(content, allowTrailing: true);
        var primaryEnd = primaryParts[^1].End;
        var mpf = primaryParts.Where(p => p.Marker == 0xe2 &&
            content.AsSpan(p.PayloadStart, p.End - p.PayloadStart).StartsWith("MPF\0"u8)).ToArray();
        if (mpf.Length == 0)
        {
            if (primaryEnd != content.Length) throw new InvalidDataException("Unindexed JPEG trailer.");
            return [new(content, 0x30000, 0, 0)];
        }
        if (mpf.Length != 1) throw new InvalidDataException("Duplicate MP index.");
        var tiffStart = mpf[0].PayloadStart + 4;
        var tiff = content.AsSpan(tiffStart, mpf[0].End - tiffStart);
        if (tiff.Length < 8) throw new InvalidDataException("Truncated MP index.");
        var little = tiff[..2].SequenceEqual("II"u8);
        if (!little && !tiff[..2].SequenceEqual("MM"u8) || U16(tiff, 2, little) != 42)
            throw new InvalidDataException("Invalid MP TIFF header.");
        var ifd = checked((int)U32(tiff, 4, little));
        var count = U16(tiff, ifd, little);
        if (count != 3 || (long)ifd + 2 + count * 12 + 4 > tiff.Length)
            throw new InvalidDataException("Invalid MP index fields.");
        uint images = 0, entriesOffset = 0, entriesBytes = 0;
        var tags = new HashSet<ushort>();
        for (var i = 0; i < count; i++)
        {
            var at = ifd + 2 + i * 12;
            var tag = U16(tiff, at, little); var type = U16(tiff, at + 2, little);
            var length = U32(tiff, at + 4, little); var value = U32(tiff, at + 8, little);
            if (!tags.Add(tag)) throw new InvalidDataException("Duplicate MP field.");
            switch (tag)
            {
                case 0xb000 when type == 7 && length == 4 && tiff.Slice(at + 8, 4).SequenceEqual("0100"u8): break;
                case 0xb001 when type == 4 && length == 1: images = value; break;
                case 0xb002 when type == 7: entriesOffset = value; entriesBytes = length; break;
                default: throw new InvalidDataException("Unsupported MP index field.");
            }
        }
        if (images is < 2 or > 5 || entriesBytes != images * 16 || entriesOffset < ifd + 2 + count * 12 + 4
            || (ulong)entriesOffset + entriesBytes > (ulong)tiff.Length
            || U32(tiff, ifd + 2 + count * 12, little) != 0)
            throw new InvalidDataException("Invalid MP image table.");
        var result = new List<Image>();
        long next = 0;
        for (var i = 0; i < images; i++)
        {
            var at = checked((int)entriesOffset + i * 16);
            var attributes = U32(tiff, at, little);
            var size = U32(tiff, at + 4, little); var relative = U32(tiff, at + 8, little);
            var start = i == 0 ? 0L : (long)tiffStart + relative;
            var d1 = U16(tiff, at + 12, little); var d2 = U16(tiff, at + 14, little);
            if (start != next || size < 4 || start + size > content.Length || d1 > images || d2 > images
                || (i == 0 && (relative != 0 || size != primaryEnd)) || (attributes & 0x07000000) != 0)
                throw new InvalidDataException("Invalid MP image bounds.");
            var frame = content.AsSpan((int)start, (int)size).ToArray();
            OsanProgressPhotoMetadataSanitizer.ParseJpeg(frame);
            result.Add(new(frame, attributes, d1, d2)); next = start + size;
        }
        if (next != content.Length) throw new InvalidDataException("Unindexed MP trailer.");
        return result;
    }

    internal static byte[] Sanitize(byte[] content)
    {
        var images = Read(content);
        if (images.Count == 1) return OsanProgressPhotoMetadataSanitizer.SanitizeJpegFrame(content);
        var frames = images.Select(i => OsanProgressPhotoMetadataSanitizer.SanitizeJpegFrame(i.Content, preserveHdr: true)).ToArray();
        frames[0] = OsanProgressPhotoMetadataSanitizer.SanitizeJpegFrame(images[0].Content, preserveHdr: true, gainMapLength: frames[1].Length);
        var mpf = CreateIndex(images, frames);
        using var output = new MemoryStream();
        output.Write(frames[0], 0, 2); output.Write(mpf); output.Write(frames[0], 2, frames[0].Length - 2);
        foreach (var frame in frames.Skip(1)) output.Write(frame);
        return output.ToArray();
    }

    private static byte[] CreateIndex(IReadOnlyList<Image> images, byte[][] frames)
    {
        var segment = new byte[4 + 4 + 50 + images.Count * 16];
        segment[0] = 0xff; segment[1] = 0xe2;
        BinaryPrimitives.WriteUInt16BigEndian(segment.AsSpan(2), (ushort)(segment.Length - 2));
        "MPF\0"u8.CopyTo(segment.AsSpan(4)); var tiff = segment.AsSpan(8);
        "MM"u8.CopyTo(tiff); W16(tiff, 2, 42); W32(tiff, 4, 8); W16(tiff, 8, 3);
        W16(tiff, 10, 0xb000); W16(tiff, 12, 7); W32(tiff, 14, 4); "0100"u8.CopyTo(tiff[18..]);
        W16(tiff, 22, 0xb001); W16(tiff, 24, 4); W32(tiff, 26, 1); W32(tiff, 30, (uint)images.Count);
        W16(tiff, 34, 0xb002); W16(tiff, 36, 7); W32(tiff, 38, (uint)images.Count * 16); W32(tiff, 42, 50);
        var position = 0;
        for (var i = 0; i < images.Count; i++)
        {
            var at = 50 + i * 16; var length = frames[i].Length + (i == 0 ? segment.Length : 0);
            W32(tiff, at, images[i].Attributes); W32(tiff, at + 4, (uint)length);
            W32(tiff, at + 8, i == 0 ? 0 : (uint)(position - 10)); // SOI(2) + marker/length(4) + MPF(4)
            W16(tiff, at + 12, images[i].Dependent1); W16(tiff, at + 14, images[i].Dependent2);
            position += length;
        }
        return segment;
    }
    private static ushort U16(ReadOnlySpan<byte> b, int at, bool little)
    { if (at < 0 || at > b.Length - 2) throw new InvalidDataException("MP index bounds."); return little ? BinaryPrimitives.ReadUInt16LittleEndian(b[at..]) : BinaryPrimitives.ReadUInt16BigEndian(b[at..]); }
    private static uint U32(ReadOnlySpan<byte> b, int at, bool little)
    { if (at < 0 || at > b.Length - 4) throw new InvalidDataException("MP index bounds."); return little ? BinaryPrimitives.ReadUInt32LittleEndian(b[at..]) : BinaryPrimitives.ReadUInt32BigEndian(b[at..]); }
    private static void W16(Span<byte> b, int at, ushort value) => BinaryPrimitives.WriteUInt16BigEndian(b[at..], value);
    private static void W32(Span<byte> b, int at, uint value) => BinaryPrimitives.WriteUInt32BigEndian(b[at..], value);
}
