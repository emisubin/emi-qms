using System.Buffers.Binary;
using System.Text;
using Emi.Qms.Api.OsanProjects;
using ImageMagick;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class OsanHdrPhotoTests
{
    private static readonly SefRecord[] CameraSefRecords =
    [
        new(0, 0x0a01, "Image_UTC_Data", "2026:10:13 12:34:56"u8.ToArray()),
        new(0, 0x0cc1, "Color_Display_P3", [1]),
        new(0, 0x0cd2, "Photo_HDR_Info", [1, 2, 3, 4])
    ];

    [Fact]
    public async Task HdrJpeg_PreservesBothEncodedImagesAndSafeGainMapMetadata()
    {
        var original = CreateHdr();
        var result = await OsanProgressPhotoValidator.ValidateAsync("phone.JPG", "image/jpeg", original, TestContext.Current.CancellationToken);
        Assert.Null(result.Error); Assert.NotNull(result.Photo);
        var before = OsanJpegContainer.Read(original); var after = OsanJpegContainer.Read(result.Photo.Content);
        Assert.Equal(2, after.Count);
        for (var i = 0; i < before.Count; i++) Assert.Equal(ImageBytes(before[i].Content), ImageBytes(after[i].Content));
        var text = Encoding.UTF8.GetString(result.Photo.Content);
        Assert.Contains("urn:com:apple:photo:2020:aux:hdrgainmap", text);
        Assert.Contains("HDRGainMapHeadroom", text);
        Assert.Contains("ICC_PROFILE", Encoding.ASCII.GetString(result.Photo.Content));
        Assert.DoesNotContain("SYNTHETIC-PRIVATE", text);
        Assert.Equal((ushort)6, OsanProgressPhotoMetadataSanitizer.ReadOrientation(after[0].Content, "image/jpeg"));
        Assert.Equal(result.Photo.Content, OsanJpegContainer.Sanitize(result.Photo.Content));
    }
    [Fact]
    public async Task SamsungSefTrailer_IsRemovedFromSingleJpegBeforeStorage()
    {
        using var image = new MagickImage(MagickColors.Orange, 48, 32);
        image.SetProfile(ColorProfiles.AdobeRGB1998);
        var jpeg = image.ToByteArray(MagickFormat.Jpeg);
        var withSef = AddSefTrailer(jpeg, CameraSefRecords).Content;

        var result = await OsanProgressPhotoValidator.ValidateAsync(
            "samsung.jpg", "image/jpeg", withSef, TestContext.Current.CancellationToken);

        Assert.Null(result.Error);
        Assert.NotNull(result.Photo);
        Assert.Equal(jpeg, result.Photo.Content);
        Assert.Single(OsanJpegContainer.Read(result.Photo.Content));
        Assert.Contains("ICC_PROFILE", Encoding.ASCII.GetString(result.Photo.Content));
        Assert.DoesNotContain("SEFH", Encoding.ASCII.GetString(result.Photo.Content));
        Assert.DoesNotContain("SEFT", Encoding.ASCII.GetString(result.Photo.Content));
    }
    [Fact]
    public async Task SamsungSefTrailer_IsRemovedFromHdrJpegWithoutChangingFramesOrHdrMetadata()
    {
        var original = CreateHdr();
        var before = OsanJpegContainer.Read(original);
        var withSef = AddSefTrailer(original, CameraSefRecords).Content;

        var result = await OsanProgressPhotoValidator.ValidateAsync(
            "samsung-hdr.jpg", "image/jpeg", withSef, TestContext.Current.CancellationToken);

        Assert.Null(result.Error);
        Assert.NotNull(result.Photo);
        var after = OsanJpegContainer.Read(result.Photo.Content);
        Assert.Equal(2, after.Count);
        for (var i = 0; i < before.Count; i++) Assert.Equal(ImageBytes(before[i].Content), ImageBytes(after[i].Content));
        var text = Encoding.UTF8.GetString(result.Photo.Content);
        Assert.Contains("urn:com:apple:photo:2020:aux:hdrgainmap", text);
        Assert.Contains("HDRGainMapHeadroom", text);
        Assert.Contains("ICC_PROFILE", Encoding.ASCII.GetString(result.Photo.Content));
        Assert.DoesNotContain("SEFH", text);
        Assert.DoesNotContain("SEFT", text);
    }
    [Fact]
    public async Task SamsungSefTrailer_RejectsMalformedOrPartiallyIndexedTrailingData()
    {
        using var image = new MagickImage(MagickColors.Purple, 32, 24);
        var jpeg = image.ToByteArray(MagickFormat.Jpeg);
        var valid = AddSefTrailer(jpeg, CameraSefRecords);
        var corruptFooterLength = valid.Content.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(
            corruptFooterLength.AsSpan(corruptFooterLength.Length - 8, 4),
            BinaryPrimitives.ReadUInt32LittleEndian(corruptFooterLength.AsSpan(corruptFooterLength.Length - 8, 4)) + 1);
        var mismatchedType = valid.Content.ToArray();
        mismatchedType[valid.RecordStarts[0] + 2] ^= 1;
        var outOfBoundsRecord = valid.Content.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(outOfBoundsRecord.AsSpan(valid.HeaderStart + 12 + 4, 4), uint.MaxValue);
        var overlappingRecords = valid.Content.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(
            overlappingRecords.AsSpan(valid.HeaderStart + 24 + 4, 4),
            BinaryPrimitives.ReadUInt32LittleEndian(overlappingRecords.AsSpan(valid.HeaderStart + 12 + 4, 4)));
        var zeroVersion = valid.Content.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(zeroVersion.AsSpan(valid.HeaderStart + 4, 4), 0);
        var countMismatch = valid.Content.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(countMismatch.AsSpan(valid.HeaderStart + 8, 4), 4);
        var gapBeforeRecords = AddSefTrailer(jpeg, CameraSefRecords, leadingGapBytes: 1).Content;
        var malformed = new[]
        {
            valid.Content[..^1],
            corruptFooterLength,
            mismatchedType,
            outOfBoundsRecord,
            overlappingRecords,
            zeroVersion,
            countMismatch,
            gapBeforeRecords,
            jpeg.Concat(new byte[] { 1, 2, 3, 4 }).ToArray()
        };

        foreach (var content in malformed)
        {
            var result = await OsanProgressPhotoValidator.ValidateAsync(
                "bad.jpg", "image/jpeg", content, TestContext.Current.CancellationToken);
            Assert.Null(result.Photo);
            Assert.NotNull(result.Error);
        }
    }
    [Fact]
    public async Task HdrJpeg_RejectsUnindexedOrCorruptAuxiliaryImages()
    {
        var original = CreateHdr(); var frames = OsanJpegContainer.Read(original);
        foreach (var bad in new[] { original[..^1], original.Concat(new byte[] { 1 }).ToArray(), frames[0].Content,
            frames[1].Content.Concat(frames[1].Content).ToArray() })
            Assert.Null((await OsanProgressPhotoValidator.ValidateAsync("bad.jpg", "image/jpeg", bad, TestContext.Current.CancellationToken)).Photo);
        var invalidOffset = original.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(invalidOffset.AsSpan(10 + 50 + 16 + 8), uint.MaxValue);
        Assert.Null((await OsanProgressPhotoValidator.ValidateAsync("bad.jpg", "image/jpeg", invalidOffset, TestContext.Current.CancellationToken)).Photo);
    }
    [Fact]
    public void HdrMetadata_RejectsDtdAndDeepTrees()
    {
        Assert.Throws<InvalidDataException>(() => OsanHdrXmp.Sanitize(Xmp("<!DOCTYPE a [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><a>&x;</a>"), null));
        Assert.Throws<InvalidDataException>(() => OsanHdrXmp.Sanitize(Xmp(string.Concat(Enumerable.Repeat("<a>", 40)) + string.Concat(Enumerable.Repeat("</a>", 40))), null));
    }
    internal static byte[] CreateHdr()
    {
        using var primary = new MagickImage(MagickColors.Blue, 64, 48);
        primary.SetProfile(ColorProfiles.AdobeRGB1998);
        using var gain = new MagickImage(MagickColors.Gray, 16, 12);
        byte[] exif = [.. "Exif\0\0"u8, .. Convert.FromHexString("49492A0008000000010012010300010000000600000000000000")];
        var first = Segment(primary.ToByteArray(MagickFormat.Jpeg), 0xe1, exif);
        first = Segment(first, 0xfe, "SYNTHETIC-PRIVATE-COMMENT"u8.ToArray());
        var second = Segment(gain.ToByteArray(MagickFormat.Jpeg), 0xe1, Xmp("""
            <x:xmpmeta xmlns:x="adobe:ns:meta/"><rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"><rdf:Description rdf:about="SYNTHETIC-PRIVATE-ID" xmlns:p="http://ns.apple.com/pixeldatainfo/1.0/" xmlns:h="http://ns.apple.com/HDRGainMap/1.0/" xmlns:e="http://ns.adobe.com/exif/1.0/" e:GPSLatitude="SYNTHETIC-PRIVATE-GPS"><p:AuxiliaryImageType>urn:com:apple:photo:2020:aux:hdrgainmap</p:AuxiliaryImageType><h:HDRGainMapHeadroom>2.5</h:HDRGainMapHeadroom></rdf:Description></rdf:RDF></x:xmpmeta>
            """));
        var tiff = Convert.FromHexString("4D4D002A000000080003B00000070000000430313030B00100040000000100000002B0020007000000200000003200000000" + new string('0', 64));
        var payload = new byte[4 + tiff.Length]; "MPF\0"u8.CopyTo(payload); tiff.CopyTo(payload, 4);
        var primarySize = first.Length + payload.Length + 4;
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4 + 50), 0x30000);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4 + 54), (uint)primarySize);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4 + 70), (uint)second.Length);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4 + 74), (uint)(primarySize - 10));
        return [.. Segment(first, 0xe2, payload), .. second];
    }
    private static byte[] Xmp(string xml) => [.. "http://ns.adobe.com/xap/1.0/\0"u8, .. Encoding.UTF8.GetBytes(xml)];
    private static byte[] Segment(byte[] source, byte marker, byte[] payload)
    { byte[] length = new byte[2]; BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)(payload.Length + 2)); return [.. source.AsSpan(0, 2), 0xff, marker, .. length, .. payload, .. source.AsSpan(2)]; }
    private static byte[] ImageBytes(byte[] content) => OsanProgressPhotoMetadataSanitizer.ParseJpeg(content)
        .Where(p => p.Marker is null || p.Marker < 0xe0).SelectMany(p => content.AsSpan(p.Start, p.End - p.Start).ToArray()).ToArray();

    private static SefLayout AddSefTrailer(byte[] jpeg, IReadOnlyList<SefRecord> records, int leadingGapBytes = 0)
    {
        using var output = new MemoryStream();
        output.Write(jpeg);
        if (leadingGapBytes > 0) output.Write(new byte[leadingGapBytes]);
        var recordStarts = new int[records.Count];
        var recordSizes = new int[records.Count];
        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];
            var name = Encoding.UTF8.GetBytes(record.Name);
            recordStarts[index] = checked((int)output.Position);
            WriteUInt16LittleEndian(output, record.Flags);
            WriteUInt16LittleEndian(output, record.Type);
            WriteUInt32LittleEndian(output, checked((uint)name.Length));
            output.Write(name);
            output.Write(record.Data);
            recordSizes[index] = checked((int)output.Position - recordStarts[index]);
        }

        var headerStart = checked((int)output.Position);
        output.Write("SEFH"u8);
        WriteUInt32LittleEndian(output, 107);
        WriteUInt32LittleEndian(output, checked((uint)records.Count));
        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];
            WriteUInt16LittleEndian(output, record.Flags);
            WriteUInt16LittleEndian(output, record.Type);
            WriteUInt32LittleEndian(output, checked((uint)(headerStart - recordStarts[index])));
            WriteUInt32LittleEndian(output, checked((uint)recordSizes[index]));
        }
        WriteUInt32LittleEndian(output, checked((uint)(12 + records.Count * 12)));
        output.Write("SEFT"u8);
        return new(output.ToArray(), headerStart, recordStarts);
    }

    private static void WriteUInt16LittleEndian(Stream output, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        output.Write(bytes);
    }
    private static void WriteUInt32LittleEndian(Stream output, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        output.Write(bytes);
    }

    private sealed record SefRecord(ushort Flags, ushort Type, string Name, byte[] Data);
    private sealed record SefLayout(byte[] Content, int HeaderStart, int[] RecordStarts);
}
