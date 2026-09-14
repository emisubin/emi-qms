using System.Buffers.Binary;
using System.Text;
using Emi.Qms.Api.OsanProjects;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class OsanHeicPhotoTests
{
    private const string SyntheticHeicBase64 =
        "AAAAJGZ0eXBoZWljAAAAAG1pZjFNaVBybWlhZk1pSEJoZWljAAABwW1ldGEAAAAAAAAAIWhkbHIAAAAAAAAAAHBpY3QAAAAAAAAAAAAAAAAAAAAAJGRpbmYAAAAcZHJlZgAAAAAAAAABAAAADHVybCAAAAABAAAADnBpdG0AAAAAAAEAAAA4aWluZgAAAAAAAgAAABVpbmZlAgAAAAABAABodmMxAAAAABVpbmZlAgAAAQACAABFeGlmAAAAABppcmVmAAAAAAAAAA5jZHNjAAIAAQABAAAA5GlwcnAAAADDaXBjbwAAABNjb2xybmNseAACAAIABoAAAAAMY2xsaQDLAEAAAAAUaXNwZQAAAAAAAADAAAAAwAAAAAlpcm90AAAAABBwaXhpAAAAAAMICAgAAABvaHZjQwEDcAAAALAAAAAAAB7wAPz9+PgAAAsDoAABABdAAQwB//8DcAAAAwCwAAADAAADAB5wJKEAAQAhQgEBA3AAAAMAsAAAAwAAAwAeoBggMFiHuRZVNwICBgCAogABAAlEAcBhcshAUyQAAAAZaXBtYQAAAAAAAAABAAEGgQIDBYaEAAAALGlsb2MAAAAARAAAAgABAAAAAQAAAmMAAAU3AAIAAAABAAAB9QAAAG4AAAABbWRhdAAAAAAAAAW1AAAABkV4aWYAAE1NACoAAAAIAAUBDwACAAAADAAAAEoBEAACAAAADQAAAFYBEgADAAAAAQABAAABQgAEAAAAAQAAAMABQwAEAAAAAQAAAMAAAAAAU0VDUkVULU1BS0UAU0VDUkVULU1PREVMAAAAAAUzKAGvmFAaBEx0+A9H9C4v8SM0WLcyZPc4oD5nDvXEPKtNQEGzSTeCvXvsm2YNwvL7nF+Zb990aLNqvT/0RtBnGv9Kl88n5JmCNOZunhyW1cWulmRQcRFofhdWnFx19SYYSybGF6lern/9EKP/kEGYhgFzIsl3vIvQx3FbrMCDWgxJBT0rdgOdbmQW8CyuJKwRnJU5HB0eVq2cOzlL+i+MfhLb4q+1Ymi6HUysc+5zNz9ZU7RfP7UjsvR5d9ZAacxX1TCuo/Dgn2M8NCBiiicb09RjCICJJrTStBwwDnAWAoqS9mtAuFgs/xbNElRogx1u71p3tmfpvSxP4LzTYNtMH8HeRuqxgWZjXXsT+c+n+8uRVJUgSmjpI5vh1qs1praOTHPFQTjYvW5/AWd3LLIC2s9LP1U1qMDg1E5t+Yr00lL9+fbDXBVfXHLf6h9Ki10nLQa+euJUcclfJVs/V3gdGodYISeFBrksZLKay4YQgJ+oPfYicNMhioxUeOLY5YkRCKIo732PLJfUqxGgdjklBwGVs4kVz6WIJw1CAUjBtsS97JvzBxs04Uv3CGMYInPkJRBipy8gAjsv6DLitYatL4cJ29H1zX9aJi2jdwBOgFDxfshO+CIxi5shSNxMY4S9RJRP/k5RAGnkqwdyIVYkPxdqjnDMwOuRDkOZWkKumTWbXPldy+jQy7v88wzK2SbrdBNhc0RsCukZPwYiNh42uXHuXdmtV/O/mn74Lrc/hvNyYAdIZm1OrUXS1U4KzCvjrilkRwFiLRjtVAGA1E9AIMmzNQMmjYZ1rPW0qKfUfH/PQNqvNpxfjfXETDBHqov7ilrXMf4tMrr/pJre1LB6B1mLwqNZwD+lvm9xkD6MjvLl+U8DcFMYMqDvT651yMHMM1w5pHFG+1Y4kuI115pfDXNlteShpKgY/2hhfl6tmyzCo4Yjj4wVCzBzJiSLJ2NkcparF+cK8SJiCBn/IOwk+VWpDTFNnBzop/4qyS4iOpir8lMwVSLdS3EaQxE11CsuVA7WcawI5EvaQ1DieWo7lr6I1FVL3nESyYbK8+SXL3ihigJCmYnwoy9k2oNZ2OBXe7HQPLFv40I4bzOm+W3teqgmWyRekOnsRfpW2o18lOux9o3+Fl8yMybAFkXWeE7+rGawl5FLC24gMT2mQ/V/2Mc6uNIWACE7IBaNvblP2Ne6dfILmIrIE9dp30FO3LlDbyyxsvChIGOb3GCu/f3BKtS1jdf9UwFybyHNshktiYLUjsKvQYS2j+hfXWSngVOXisC+FVvIVb3ZrVzXTbsLSeynUXWK6JkEqqgj9fpljHy0gpJiJwf0OudWGQREIzlPNj6y7TkES/AADUjSnTcEN/8PqGNPAtrtWVvPKek0EgIPyGIAxrJh9f1149eIHlZo+M6+8PQK+c0kEcVAEjCBP6sJiV8s+AYQP2VpejWVUDPKr5/k+aE6JavrRGezcZ4hmF58Q5RBqPV9ssJ+c7nCwHjK3xvJUl4S/vf3ma8xHXWJ/oSjfMIwa5wBhyH//pqn2ZrmhJKzRQlrd1ehGmOq7Ft/7nkT2ZvJ07VSncxRiPUf+JgzhEfXZe+fKRgn+a17aLLxQe/pLbWKM9rQFo2gk0aRQWEsH0oIsiMc0RvVbsiksbb+HAY9W5qq9QcYsOryg4q6RFyS+JQcLkGxACQcsnOkCBcIsyvoT6FUDshZyFt4wCOfJbC9nRBu+oCnR7FkBs8iFJ87mk0/ccfehi0oHjLUNF9QQGpmb8Zi3EgQVgAABiQ=";

    public static byte[] CreateHeic() => Convert.FromBase64String(SyntheticHeicBase64);

    [Fact]
    public async Task ValidateAsync_accepts_synthetic_heic_and_removes_device_metadata_without_changing_hevc()
    {
        var original = CreateHeic();
        var originalHevc = original.AsSpan(0x263, 0x537).ToArray();

        var (photo, error) = await OsanProgressPhotoValidator.ValidateAsync(
            "phone.heic",
            "image/heic",
            original,
            TestContext.Current.CancellationToken);

        Assert.Null(error);
        Assert.NotNull(photo);
        Assert.Equal("image/heic", photo.NormalizedMime);
        Assert.Equal(original.Length, photo.Content.Length);
        Assert.DoesNotContain("SECRET-MAKE", Encoding.ASCII.GetString(photo.Content));
        Assert.DoesNotContain("SECRET-MODEL", Encoding.ASCII.GetString(photo.Content));
        Assert.Equal(originalHevc, photo.Content.AsSpan(0x263, 0x537).ToArray());
        Assert.True(await OsanHeicImageCodec.ValidateAsync(
            photo.Content,
            TestContext.Current.CancellationToken));

        var preview = await OsanHeicImageCodec.PreviewAsync(
            photo.Content,
            TestContext.Current.CancellationToken);
        Assert.True(preview.AsSpan(0, 2).SequenceEqual([(byte)0xff, (byte)0xd8]));
        Assert.True(preview.AsSpan(preview.Length - 2).SequenceEqual([(byte)0xff, (byte)0xd9]));
    }

    [Fact]
    public void Sanitize_accepts_hidden_item_flag_and_rejects_external_data_reference()
    {
        var hiddenExif = CreateHeic();
        var exifType = FindBytes(hiddenExif, "Exif"u8);
        hiddenExif[exifType - 5] = 1;
        Assert.DoesNotContain("SECRET-MAKE", Encoding.ASCII.GetString(
            OsanHeicMetadataSanitizer.Sanitize(hiddenExif)));

        var externalReference = CreateHeic();
        var urlBox = FindBytes(externalReference, "url \0\0\0\u0001"u8);
        externalReference[urlBox + 7] = 0;
        Assert.Throws<InvalidDataException>(() =>
            OsanHeicMetadataSanitizer.Sanitize(externalReference));
    }

    [Fact]
    public void LooksLikeHeic_rejects_avif_brand()
    {
        var content = CreateHeic();
        "avif"u8.CopyTo(content.AsSpan(8, 4));

        Assert.False(OsanHeicMetadataSanitizer.LooksLikeHeic(content));
    }

    [Fact]
    public void Sanitize_scrubs_named_mime_and_handler_without_shifting_fields_and_is_idempotent()
    {
        var original = CreateStructuralHeic();
        var pixelOffset = FindBytes(original, [(byte)0xde, 0xad, 0xbe, 0xef]);

        var sanitized = OsanHeicMetadataSanitizer.Sanitize(original);

        Assert.Equal(original.Length, sanitized.Length);
        Assert.Equal(original.AsSpan(pixelOffset, 4).ToArray(), sanitized.AsSpan(pixelOffset, 4).ToArray());
        var text = Encoding.ASCII.GetString(sanitized);
        Assert.DoesNotContain("PRIVATE-HANDLER", text);
        Assert.DoesNotContain("PRIVATE-ITEM", text);
        Assert.DoesNotContain("SECRET-XMP", text);
        Assert.Contains("application/rdf+xml", text);
        Assert.Equal(sanitized, OsanHeicMetadataSanitizer.Sanitize(sanitized));
    }

    [Fact]
    public void Sanitize_supports_idat_construction_method_one()
    {
        var original = CreateStructuralHeic(useIdat: true);

        var sanitized = OsanHeicMetadataSanitizer.Sanitize(original);

        Assert.DoesNotContain("SECRET-XMP", Encoding.ASCII.GetString(sanitized));
        Assert.Contains("application/rdf+xml", Encoding.ASCII.GetString(sanitized));
    }

    [Fact]
    public void Sanitize_rejects_duplicate_or_unknown_property_and_unknown_metadata_box()
    {
        Assert.Throws<InvalidDataException>(() =>
            OsanHeicMetadataSanitizer.Sanitize(CreateStructuralHeic(duplicateIpco: true)));
        Assert.Throws<InvalidDataException>(() =>
            OsanHeicMetadataSanitizer.Sanitize(CreateStructuralHeic(unknownProperty: true)));
        Assert.Throws<InvalidDataException>(() =>
            OsanHeicMetadataSanitizer.Sanitize(CreateStructuralHeic(unknownMetadataBox: true)));
    }

    [Fact]
    public void Sanitize_accepts_valid_icc_profile_and_apple_hdr_gain_map_property()
    {
        var content = CreateStructuralHeic(includeIcc: true, includeHdrAuxiliary: true);

        var sanitized = OsanHeicMetadataSanitizer.Sanitize(content);

        Assert.DoesNotContain("SECRET-XMP", Encoding.ASCII.GetString(sanitized));
        Assert.Contains("prof", Encoding.ASCII.GetString(sanitized));
        Assert.Contains("urn:com:apple:photo:2020:aux:hdrgainmap", Encoding.ASCII.GetString(sanitized));
    }

    private static byte[] CreateStructuralHeic(
        bool useIdat = false,
        bool duplicateIpco = false,
        bool unknownProperty = false,
        bool unknownMetadataBox = false,
        bool includeIcc = false,
        bool includeHdrAuxiliary = false)
    {
        var visual = new byte[] { 0xde, 0xad, 0xbe, 0xef };
        var metadata = Encoding.ASCII.GetBytes("<rdf>SECRET-XMP</rdf>");
        var ftyp = Box("ftyp", Encoding.ASCII.GetBytes("heic"), U32(0), Encoding.ASCII.GetBytes("mif1heic"));

        byte[] CreateItemLocation(uint visualOffset, uint metadataOffset)
        {
            byte[] Entry(ushort id, uint itemOffset, uint itemLength, bool idat) => Bytes(
                U16(id),
                idat ? U16(1) : [],
                U16(0),
                U16(1),
                U32(itemOffset),
                U32(itemLength));
            return Box("iloc", FullBox(
                useIdat ? (byte)1 : (byte)0,
                0,
                [0x44, 0x00],
                U16(2),
                Entry(1, visualOffset, (uint)visual.Length, useIdat),
                Entry(2, metadataOffset, (uint)metadata.Length, useIdat)));
        }

        byte[] CreateMeta(uint visualOffset, uint metadataOffset)
        {
            var handler = Box("hdlr", FullBox(
                0,
                0,
                U32(0),
                Encoding.ASCII.GetBytes("pict"),
                new byte[12],
                Encoding.ASCII.GetBytes("PRIVATE-HANDLER\0")));
            var url = Box("url ", FullBox(0, 1));
            var dataInformation = Box("dinf", Box("dref", FullBox(0, 0, U32(1), url)));
            var primary = Box("pitm", FullBox(0, 0, U16(1)));
            var visualInfo = Box("infe", FullBox(
                2,
                1,
                U16(1),
                U16(0),
                Encoding.ASCII.GetBytes("hvc1\0")));
            var metadataInfo = Box("infe", FullBox(
                2,
                0,
                U16(2),
                U16(0),
                Encoding.ASCII.GetBytes("mimePRIVATE-ITEM\0application/rdf+xml\0\0")));
            var itemInfo = Box("iinf", FullBox(0, 0, U16(2), visualInfo, metadataInfo));
            var reference = Box("iref", FullBox(
                0,
                0,
                Box("cdsc", U16(2), U16(1), U16(1))));

            var properties = new List<byte[]>
            {
                Box("ispe", FullBox(0, 0, U32(1), U32(1))),
                Box(unknownProperty ? "uuid" : "hvcC", new byte[23]),
                Box("pixi", FullBox(0, 0, [3, 8, 8, 8]))
            };
            if (includeIcc)
            {
                var icc = new byte[132];
                BinaryPrimitives.WriteUInt32BigEndian(icc, (uint)icc.Length);
                "acsp"u8.CopyTo(icc.AsSpan(36));
                properties.Add(Box("colr", Encoding.ASCII.GetBytes("prof"), icc));
            }
            if (includeHdrAuxiliary)
            {
                properties.Add(Box("auxC", FullBox(
                    0,
                    0,
                    Encoding.ASCII.GetBytes("urn:com:apple:photo:2020:aux:hdrgainmap\0"))));
            }
            var propertyContainer = Box("ipco", properties.ToArray());
            var associationBytes = Enumerable.Range(1, properties.Count)
                .Select(index => (byte)(0x80 | index))
                .ToArray();
            var propertyAssociation = Box("ipma", FullBox(
                0,
                0,
                U32(1),
                U16(1),
                [(byte)properties.Count],
                associationBytes));
            var itemProperties = duplicateIpco
                ? Box("iprp", propertyContainer, propertyContainer, propertyAssociation)
                : Box("iprp", propertyContainer, propertyAssociation);
            var unknown = unknownMetadataBox ? Box("uuid", new byte[16]) : [];
            var itemLocation = CreateItemLocation(visualOffset, metadataOffset);
            var itemData = useIdat ? Box("idat", visual, metadata) : [];
            return Box(
                "meta",
                FullBox(
                    0,
                    0,
                    handler,
                    dataInformation,
                    primary,
                    itemInfo,
                    reference,
                    itemProperties,
                    unknown,
                    itemLocation,
                    itemData));
        }

        if (useIdat)
        {
            return Bytes(ftyp, CreateMeta(0, (uint)visual.Length));
        }

        var placeholderMeta = CreateMeta(0, 0);
        var mediaDataStart = (uint)(ftyp.Length + placeholderMeta.Length + 8);
        var meta = CreateMeta(mediaDataStart, mediaDataStart + (uint)visual.Length);
        return Bytes(ftyp, meta, Box("mdat", visual, metadata));
    }

    private static byte[] FullBox(byte version, uint flags, params byte[][] payload) => Bytes(
        [version, (byte)(flags >> 16), (byte)(flags >> 8), (byte)flags],
        Bytes(payload));

    private static byte[] Box(string type, params byte[][] payload)
    {
        var body = Bytes(payload);
        return Bytes(U32((uint)(8 + body.Length)), Encoding.ASCII.GetBytes(type), body);
    }

    private static byte[] Bytes(params byte[][] values)
    {
        var length = values.Sum(value => value.Length);
        var result = new byte[length];
        var offset = 0;
        foreach (var value in values)
        {
            value.CopyTo(result, offset);
            offset += value.Length;
        }
        return result;
    }

    private static byte[] U16(ushort value)
    {
        var result = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(result, value);
        return result;
    }

    private static byte[] U32(uint value)
    {
        var result = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(result, value);
        return result;
    }

    private static int FindBytes(byte[] content, ReadOnlySpan<byte> needle)
    {
        var offset = content.AsSpan().IndexOf(needle);
        Assert.True(offset >= 0);
        return offset;
    }
}
