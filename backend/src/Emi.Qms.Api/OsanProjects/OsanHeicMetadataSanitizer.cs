using System.Buffers.Binary;
using System.Text;

namespace Emi.Qms.Api.OsanProjects;

internal static class OsanHeicMetadataSanitizer
{
    private const int MaximumBoxes = 256;
    private const int MaximumItems = 256;
    private const int MaximumExtents = 512;
    private static readonly HashSet<string> HeicBrands = new(StringComparer.Ordinal)
    {
        "heic", "heix", "hevc", "hevx"
    };
    private static readonly HashSet<string> VisualItemTypes = new(StringComparer.Ordinal)
    {
        "hvc1", "grid", "iden", "iovl"
    };
    private static readonly HashSet<string> AllowedMetaBoxes = new(StringComparer.Ordinal)
    {
        "hdlr", "dinf", "pitm", "iinf", "iref", "iprp", "iloc", "idat"
    };
    private static readonly HashSet<string> XmpContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/rdf+xml", "application/xml", "text/xml"
    };

    internal static bool LooksLikeHeic(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        try
        {
            var box = ReadBox(content, 0, content.Length);
            if (box.Type != "ftyp" || box.Start != 0 || box.PayloadLength < 8)
            {
                return false;
            }

            var majorBrand = ReadFourCc(content, box.PayloadStart);
            var hasHeicBrand = HeicBrands.Contains(majorBrand);
            for (var offset = box.PayloadStart + 8; offset <= box.End - 4; offset += 4)
            {
                var brand = ReadFourCc(content, offset);
                if (brand is "avif" or "avis")
                {
                    return false;
                }
                hasHeicBrand |= HeicBrands.Contains(brand);
            }

            return hasHeicBrand
                && (HeicBrands.Contains(majorBrand) || majorBrand == "mif1")
                && (box.PayloadLength - 8) % 4 == 0;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    internal static byte[] Sanitize(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!LooksLikeHeic(content))
        {
            throw new InvalidDataException("Unsupported HEIC file type.");
        }

        var topLevelBoxes = ReadBoxes(content, 0, content.Length);
        if (topLevelBoxes.Count is < 2 or > MaximumBoxes
            || topLevelBoxes[0].Type != "ftyp")
        {
            throw new InvalidDataException("Invalid HEIC box layout.");
        }

        Box? meta = null;
        var mediaDataBoxes = new List<Box>();
        foreach (var box in topLevelBoxes)
        {
            switch (box.Type)
            {
                case "ftyp" when box.Start == 0:
                    break;
                case "meta" when meta is null:
                    meta = box;
                    break;
                case "mdat":
                    mediaDataBoxes.Add(box);
                    break;
                default:
                    throw new InvalidDataException("Unsupported HEIC top-level box.");
            }
        }
        if (meta is null)
        {
            throw new InvalidDataException("HEIC metadata is missing.");
        }

        var metaBox = meta.Value;
        var metaPayload = ReadFullBox(content, metaBox, expectedVersion: 0);
        var metaChildren = ReadBoxes(content, metaPayload, metaBox.End);
        Box? itemInfo = null;
        Box? itemLocation = null;
        Box? itemData = null;
        var scrubRanges = new List<(int Start, int Length)>();
        var seenMetaBoxes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var box in metaChildren)
        {
            if (!AllowedMetaBoxes.Contains(box.Type))
            {
                throw new InvalidDataException("Unsupported HEIC metadata box.");
            }
            if (!seenMetaBoxes.Add(box.Type))
            {
                throw new InvalidDataException("Duplicate HEIC metadata box.");
            }
            if (box.Type == "iinf")
            {
                if (itemInfo is not null)
                {
                    throw new InvalidDataException("Duplicate HEIC item information.");
                }
                itemInfo = box;
            }
            else if (box.Type == "iloc")
            {
                if (itemLocation is not null)
                {
                    throw new InvalidDataException("Duplicate HEIC item location.");
                }
                itemLocation = box;
            }
            else if (box.Type == "idat")
            {
                if (itemData is not null)
                {
                    throw new InvalidDataException("Duplicate HEIC item data.");
                }
                itemData = box;
            }
            else if (box.Type == "hdlr")
            {
                scrubRanges.Add(ValidateHandler(content, box));
            }
            else if (box.Type == "dinf")
            {
                ValidateDataInformation(content, box);
            }
            else if (box.Type == "pitm")
            {
                ValidatePrimaryItem(content, box);
            }
            else if (box.Type == "iref")
            {
                ValidateItemReferences(content, box);
            }
            else if (box.Type == "iprp")
            {
                ValidateItemProperties(content, box);
            }
        }
        if (itemInfo is null || itemLocation is null)
        {
            throw new InvalidDataException("HEIC item metadata is missing.");
        }

        var infos = ParseItemInfo(content, itemInfo.Value);
        var locations = ParseItemLocations(content, itemLocation.Value, itemData, mediaDataBoxes);
        if (infos.Count != locations.Count
            || infos.Keys.Any(itemId => !locations.ContainsKey(itemId))
            || locations.Keys.Any(itemId => !infos.ContainsKey(itemId))
            || !infos.Values.Any(info => VisualItemTypes.Contains(info.Type)))
        {
            throw new InvalidDataException("HEIC item declarations do not match their data.");
        }

        var allExtents = locations.Values.SelectMany(location => location.Extents)
            .OrderBy(extent => extent.Start)
            .ToList();
        for (var index = 1; index < allExtents.Count; index += 1)
        {
            var previous = allExtents[index - 1];
            var current = allExtents[index];
            if (previous.End > current.Start)
            {
                throw new InvalidDataException("Overlapping HEIC item extents.");
            }
        }
        EnsureUnreferencedDataIsEmpty(content, mediaDataBoxes, itemData, allExtents);

        byte[]? output = null;
        foreach (var (start, length) in scrubRanges.Where(range => range.Length > 0))
        {
            output ??= (byte[])content.Clone();
            output.AsSpan(start, length).Fill((byte)' ');
        }
        foreach (var info in infos.Values)
        {
            if (info.NameLength > 0)
            {
                output ??= (byte[])content.Clone();
                output.AsSpan(info.NameStart, info.NameLength).Fill((byte)' ');
            }

            byte[]? replacement = info.Type switch
            {
                "Exif" => CreateExifReplacement(ReadItem(content, locations[info.ItemId])),
                "mime" => CreateXmpReplacement(info, locations[info.ItemId]),
                _ when VisualItemTypes.Contains(info.Type) => null,
                _ => throw new InvalidDataException("Unsupported HEIC item type.")
            };
            if (replacement is null)
            {
                continue;
            }

            output ??= (byte[])content.Clone();
            WriteItem(output, locations[info.ItemId], replacement);
        }

        return output ?? content;
    }

    private static Dictionary<uint, ItemInfo> ParseItemInfo(byte[] content, Box box)
    {
        var (version, payloadStart) = ReadFullBoxVersion(content, box);
        if (version is not 0 and not 1)
        {
            throw new InvalidDataException("Unsupported HEIC item information version.");
        }
        var offset = payloadStart;
        var entryCount = version == 0
            ? ReadUInt(content, ref offset, 2, box.End)
            : ReadUInt(content, ref offset, 4, box.End);
        if (entryCount == 0 || entryCount > MaximumItems)
        {
            throw new InvalidDataException("Invalid HEIC item count.");
        }

        var entries = ReadBoxes(content, offset, box.End);
        if (entries.Count != entryCount)
        {
            throw new InvalidDataException("HEIC item count does not match its entries.");
        }

        var result = new Dictionary<uint, ItemInfo>();
        foreach (var entry in entries)
        {
            if (entry.Type != "infe")
            {
                throw new InvalidDataException("Unsupported HEIC item information entry.");
            }
            var (entryVersion, entryFlags, entryOffsetStart) = ReadFullBoxHeader(content, entry);
            if (entryVersion is not 2 and not 3)
            {
                throw new InvalidDataException("Unsupported HEIC item entry version.");
            }
            if ((entryFlags & ~1U) != 0)
            {
                throw new InvalidDataException("Unsupported HEIC item entry flags.");
            }
            var entryOffset = entryOffsetStart;
            var itemId = ReadUInt(content, ref entryOffset, entryVersion == 2 ? 2 : 4, entry.End);
            var protectionIndex = ReadUInt(content, ref entryOffset, 2, entry.End);
            if (itemId == 0 || protectionIndex != 0 || entryOffset > entry.End - 4)
            {
                throw new InvalidDataException("Invalid or protected HEIC item.");
            }
            var itemType = ReadFourCc(content, entryOffset);
            entryOffset += 4;
            var (nameStart, nameLength) = ReadNullTerminated(content, ref entryOffset, entry.End);

            string? contentType = null;
            if (itemType == "mime")
            {
                var contentTypeRange = ReadNullTerminated(content, ref entryOffset, entry.End);
                contentType = Encoding.UTF8.GetString(
                    content,
                    contentTypeRange.Start,
                    contentTypeRange.Length);
                if (!XmpContentTypes.Contains(contentType))
                {
                    throw new InvalidDataException("Unsupported HEIC MIME metadata.");
                }
                if (entryOffset < entry.End)
                {
                    var contentEncoding = ReadNullTerminated(content, ref entryOffset, entry.End);
                    if (contentEncoding.Length != 0)
                    {
                        throw new InvalidDataException("Encoded HEIC MIME metadata is unsupported.");
                    }
                }
            }
            if (entryOffset != entry.End)
            {
                throw new InvalidDataException("Unexpected HEIC item information fields.");
            }
            if (!result.TryAdd(itemId, new ItemInfo(
                    itemId,
                    itemType,
                    contentType,
                    nameStart,
                    nameLength)))
            {
                throw new InvalidDataException("Duplicate HEIC item identifier.");
            }
        }
        return result;
    }

    private static Dictionary<uint, ItemLocation> ParseItemLocations(
        byte[] content,
        Box box,
        Box? itemData,
        IReadOnlyList<Box> mediaDataBoxes)
    {
        var (version, payloadStart) = ReadFullBoxVersion(content, box);
        if (version is < 0 or > 2)
        {
            throw new InvalidDataException("Unsupported HEIC item location version.");
        }
        var offset = payloadStart;
        if (offset > box.End - 2)
        {
            throw new InvalidDataException("Truncated HEIC item location.");
        }
        var offsetSize = content[offset] >> 4;
        var lengthSize = content[offset] & 0x0f;
        var baseOffsetSize = content[offset + 1] >> 4;
        var indexSize = version is 1 or 2 ? content[offset + 1] & 0x0f : 0;
        if (version == 0 && (content[offset + 1] & 0x0f) != 0
            || offsetSize > 8 || lengthSize is 0 or > 8
            || baseOffsetSize > 8 || indexSize > 8)
        {
            throw new InvalidDataException("Unsupported HEIC item location field sizes.");
        }
        offset += 2;
        var itemCount = ReadUInt(content, ref offset, version < 2 ? 2 : 4, box.End);
        if (itemCount == 0 || itemCount > MaximumItems)
        {
            throw new InvalidDataException("Invalid HEIC item location count.");
        }

        var result = new Dictionary<uint, ItemLocation>();
        var totalExtents = 0;
        for (var itemIndex = 0U; itemIndex < itemCount; itemIndex += 1)
        {
            var itemId = ReadUInt(content, ref offset, version < 2 ? 2 : 4, box.End);
            uint constructionMethod = 0;
            if (version is 1 or 2)
            {
                var methodField = ReadUInt(content, ref offset, 2, box.End);
                if ((methodField & 0xfff0) != 0)
                {
                    throw new InvalidDataException("Invalid HEIC construction method flags.");
                }
                constructionMethod = methodField & 0x0f;
            }
            var dataReferenceIndex = ReadUInt(content, ref offset, 2, box.End);
            var baseOffset = ReadVariableUInt(content, ref offset, baseOffsetSize, box.End);
            var extentCount = ReadUInt(content, ref offset, 2, box.End);
            if (itemId == 0 || dataReferenceIndex != 0 || constructionMethod > 1
                || extentCount == 0 || extentCount > MaximumExtents
                || totalExtents + extentCount > MaximumExtents)
            {
                throw new InvalidDataException("Unsupported HEIC item location.");
            }

            var extents = new List<Extent>((int)extentCount);
            for (var extentIndex = 0U; extentIndex < extentCount; extentIndex += 1)
            {
                if (version is 1 or 2 && indexSize > 0
                    && ReadVariableUInt(content, ref offset, indexSize, box.End) != 0)
                {
                    throw new InvalidDataException("Indexed HEIC extents are unsupported.");
                }
                var extentOffset = ReadVariableUInt(content, ref offset, offsetSize, box.End);
                var extentLength = ReadVariableUInt(content, ref offset, lengthSize, box.End);
                if (extentLength == 0)
                {
                    throw new InvalidDataException("Empty HEIC item extent.");
                }
                var origin = constructionMethod == 0
                    ? 0UL
                    : itemData is null
                        ? throw new InvalidDataException("HEIC item data box is missing.")
                        : (ulong)itemData.Value.PayloadStart;
                var absoluteStart = AddChecked(origin, AddChecked(baseOffset, extentOffset));
                var absoluteEnd = AddChecked(absoluteStart, extentLength);
                if (absoluteEnd > (ulong)content.Length
                    || constructionMethod == 0
                       && !mediaDataBoxes.Any(data =>
                           absoluteStart >= (ulong)data.PayloadStart
                           && absoluteEnd <= (ulong)data.End)
                    || constructionMethod == 1
                       && (absoluteStart < (ulong)itemData!.Value.PayloadStart
                           || absoluteEnd > (ulong)itemData.Value.End))
                {
                    throw new InvalidDataException("HEIC item extent is outside its data box.");
                }
                extents.Add(new Extent((int)absoluteStart, (int)absoluteEnd));
            }
            totalExtents += (int)extentCount;
            if (!result.TryAdd(itemId, new ItemLocation(extents)))
            {
                throw new InvalidDataException("Duplicate HEIC item location.");
            }
        }
        if (offset != box.End)
        {
            throw new InvalidDataException("Unexpected HEIC item location fields.");
        }
        return result;
    }

    private static byte[] CreateExifReplacement(byte[] item)
    {
        if (item.Length < 18)
        {
            throw new InvalidDataException("HEIC EXIF item is too short.");
        }
        var tiffOffset = BinaryPrimitives.ReadUInt32BigEndian(item.AsSpan(0, 4));
        var tiffStartLong = 4L + tiffOffset;
        if (tiffStartLong > item.Length - 14)
        {
            throw new InvalidDataException("Invalid HEIC EXIF TIFF offset.");
        }
        var tiffStart = (int)tiffStartLong;
        var orientation = ReadTiffOrientation(item.AsSpan(tiffStart));
        var tiffLength = orientation is null ? 14 : 26;
        if (tiffStart > item.Length - tiffLength)
        {
            throw new InvalidDataException("HEIC EXIF item cannot preserve orientation.");
        }

        var replacement = new byte[item.Length];
        BinaryPrimitives.WriteUInt32BigEndian(replacement.AsSpan(0, 4), tiffOffset);
        var tiff = replacement.AsSpan(tiffStart);
        "MM"u8.CopyTo(tiff);
        BinaryPrimitives.WriteUInt16BigEndian(tiff[2..], 42);
        BinaryPrimitives.WriteUInt32BigEndian(tiff[4..], 8);
        if (orientation is null)
        {
            BinaryPrimitives.WriteUInt16BigEndian(tiff[8..], 0);
            BinaryPrimitives.WriteUInt32BigEndian(tiff[10..], 0);
        }
        else
        {
            BinaryPrimitives.WriteUInt16BigEndian(tiff[8..], 1);
            BinaryPrimitives.WriteUInt16BigEndian(tiff[10..], 0x0112);
            BinaryPrimitives.WriteUInt16BigEndian(tiff[12..], 3);
            BinaryPrimitives.WriteUInt32BigEndian(tiff[14..], 1);
            BinaryPrimitives.WriteUInt16BigEndian(tiff[18..], orientation.Value);
            BinaryPrimitives.WriteUInt32BigEndian(tiff[22..], 0);
        }
        return replacement;
    }

    private static ushort? ReadTiffOrientation(ReadOnlySpan<byte> tiff)
    {
        if (tiff.Length < 14)
        {
            throw new InvalidDataException("Truncated HEIC TIFF metadata.");
        }
        var littleEndian = tiff[..2].SequenceEqual("II"u8);
        if (!littleEndian && !tiff[..2].SequenceEqual("MM"u8))
        {
            throw new InvalidDataException("Invalid HEIC TIFF byte order.");
        }
        ushort Read16(ReadOnlySpan<byte> value) => littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(value)
            : BinaryPrimitives.ReadUInt16BigEndian(value);
        uint Read32(ReadOnlySpan<byte> value) => littleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(value)
            : BinaryPrimitives.ReadUInt32BigEndian(value);
        if (Read16(tiff[2..]) != 42)
        {
            throw new InvalidDataException("Invalid HEIC TIFF signature.");
        }
        var ifdOffset = Read32(tiff[4..]);
        if (ifdOffset > int.MaxValue || ifdOffset > tiff.Length - 2)
        {
            throw new InvalidDataException("Invalid HEIC TIFF directory offset.");
        }
        var directoryStart = (int)ifdOffset;
        var entryCount = Read16(tiff[directoryStart..]);
        if (entryCount > 256
            || (long)directoryStart + 2L + entryCount * 12L + 4L > tiff.Length)
        {
            throw new InvalidDataException("Invalid HEIC TIFF directory.");
        }

        ushort? orientation = null;
        for (var index = 0; index < entryCount; index += 1)
        {
            var entry = tiff.Slice(directoryStart + 2 + index * 12, 12);
            if (Read16(entry) != 0x0112)
            {
                continue;
            }
            if (orientation is not null || Read16(entry[2..]) != 3 || Read32(entry[4..]) != 1)
            {
                throw new InvalidDataException("Invalid HEIC orientation metadata.");
            }
            var value = Read16(entry[8..]);
            if (value is < 1 or > 8)
            {
                throw new InvalidDataException("Invalid HEIC orientation value.");
            }
            orientation = value;
        }
        return orientation;
    }

    private static (int Start, int Length) ValidateHandler(byte[] content, Box box)
    {
        var payloadStart = ReadFullBox(content, box, expectedVersion: 0);
        if (payloadStart > box.End - 20
            || ReadFourCc(content, payloadStart + 4) != "pict")
        {
            throw new InvalidDataException("Unsupported HEIC handler.");
        }
        if (content.AsSpan(payloadStart, 4).IndexOfAnyExcept((byte)0) >= 0
            || content.AsSpan(payloadStart + 8, 12).IndexOfAnyExcept((byte)0) >= 0)
        {
            throw new InvalidDataException("Invalid HEIC handler fields.");
        }
        var nameStart = payloadStart + 20;
        if (nameStart == box.End)
        {
            return (nameStart, 0);
        }
        if (content[box.End - 1] != 0
            || content.AsSpan(nameStart, box.End - nameStart - 1).Contains((byte)0))
        {
            throw new InvalidDataException("Invalid HEIC handler name.");
        }
        return (nameStart, box.End - nameStart - 1);
    }

    private static void ValidateDataInformation(byte[] content, Box box)
    {
        var children = ReadBoxes(content, box.PayloadStart, box.End);
        if (children.Count != 1 || children[0].Type != "dref")
        {
            throw new InvalidDataException("Unsupported HEIC data information.");
        }
        var dref = children[0];
        var payloadStart = ReadFullBox(content, dref, expectedVersion: 0);
        var offset = payloadStart;
        var entryCount = ReadUInt(content, ref offset, 4, dref.End);
        if (entryCount is 0 or > MaximumItems)
        {
            throw new InvalidDataException("Invalid HEIC data reference count.");
        }
        var entries = ReadBoxes(content, offset, dref.End);
        if (entries.Count != entryCount)
        {
            throw new InvalidDataException("HEIC data reference count does not match.");
        }
        foreach (var entry in entries)
        {
            var (version, flags, entryPayloadStart) = ReadFullBoxHeader(content, entry);
            if (entry.Type != "url " || version != 0 || flags != 1
                || entryPayloadStart != entry.End)
            {
                throw new InvalidDataException("External HEIC data references are unsupported.");
            }
        }
    }

    private static void ValidatePrimaryItem(byte[] content, Box box)
    {
        var (version, payloadStart) = ReadFullBoxVersion(content, box);
        if (version > 1)
        {
            throw new InvalidDataException("Unsupported HEIC primary item version.");
        }
        var offset = payloadStart;
        if (ReadUInt(content, ref offset, version == 0 ? 2 : 4, box.End) == 0
            || offset != box.End)
        {
            throw new InvalidDataException("Invalid HEIC primary item.");
        }
    }

    private static void ValidateItemReferences(byte[] content, Box box)
    {
        var (version, payloadStart) = ReadFullBoxVersion(content, box);
        if (version > 1)
        {
            throw new InvalidDataException("Unsupported HEIC item reference version.");
        }
        var references = ReadBoxes(content, payloadStart, box.End);
        if (references.Count > MaximumItems)
        {
            throw new InvalidDataException("Too many HEIC item references.");
        }
        foreach (var reference in references)
        {
            if (reference.Type is not ("thmb" or "auxl" or "cdsc" or "dimg" or "prem"))
            {
                throw new InvalidDataException("Unsupported HEIC item reference type.");
            }
            var offset = reference.PayloadStart;
            if (ReadUInt(content, ref offset, version == 0 ? 2 : 4, reference.End) == 0)
            {
                throw new InvalidDataException("Invalid HEIC item reference.");
            }
            var referenceCount = ReadUInt(content, ref offset, 2, reference.End);
            if (referenceCount is 0 or > MaximumItems)
            {
                throw new InvalidDataException("Invalid HEIC item reference count.");
            }
            for (var index = 0U; index < referenceCount; index += 1)
            {
                if (ReadUInt(content, ref offset, version == 0 ? 2 : 4, reference.End) == 0)
                {
                    throw new InvalidDataException("Invalid HEIC referenced item.");
                }
            }
            if (offset != reference.End)
            {
                throw new InvalidDataException("Unexpected HEIC item reference fields.");
            }
        }
    }

    private static void ValidateItemProperties(byte[] content, Box box)
    {
        var children = ReadBoxes(content, box.PayloadStart, box.End);
        var propertyContainers = children.Where(child => child.Type == "ipco").ToList();
        var associations = children.Where(child => child.Type == "ipma").ToList();
        if (propertyContainers.Count != 1 || associations.Count == 0
            || children.Count != associations.Count + 1)
        {
            throw new InvalidDataException("Unsupported HEIC item property layout.");
        }
        var propertyContainer = propertyContainers[0];
        var properties = ReadBoxes(content, propertyContainer.PayloadStart, propertyContainer.End);
        if (properties.Count is 0 or > 127)
        {
            throw new InvalidDataException("Invalid HEIC item property count.");
        }
        foreach (var property in properties)
        {
            ValidateItemProperty(content, property);
        }
        foreach (var association in associations)
        {
            var (version, flags, payloadStart) = ReadFullBoxHeader(content, association);
            if (version > 1 || (flags & ~1U) != 0)
            {
                throw new InvalidDataException("Unsupported HEIC property association.");
            }
            var offset = payloadStart;
            var entryCount = ReadUInt(content, ref offset, 4, association.End);
            if (entryCount is 0 or > MaximumItems)
            {
                throw new InvalidDataException("Invalid HEIC property association count.");
            }
            for (var entryIndex = 0U; entryIndex < entryCount; entryIndex += 1)
            {
                if (ReadUInt(content, ref offset, version == 0 ? 2 : 4, association.End) == 0)
                {
                    throw new InvalidDataException("Invalid HEIC property item.");
                }
                var associationCount = ReadUInt(content, ref offset, 1, association.End);
                if (associationCount > properties.Count)
                {
                    throw new InvalidDataException("Invalid HEIC property association count.");
                }
                for (var index = 0U; index < associationCount; index += 1)
                {
                    var encoded = ReadUInt(content, ref offset, (flags & 1) == 0 ? 1 : 2, association.End);
                    var propertyIndex = (flags & 1) == 0 ? encoded & 0x7f : encoded & 0x7fff;
                    if (propertyIndex == 0 || propertyIndex > properties.Count)
                    {
                        throw new InvalidDataException("Invalid HEIC property index.");
                    }
                }
            }
            if (offset != association.End)
            {
                throw new InvalidDataException("Unexpected HEIC property association fields.");
            }
        }
    }

    private static void ValidateItemProperty(byte[] content, Box property)
    {
        switch (property.Type)
        {
            case "hvcC" when property.PayloadLength >= 23:
            case "clap" when property.PayloadLength == 32:
            case "pasp" when property.PayloadLength == 8:
            case "clli" when property.PayloadLength == 4:
            case "mdcv" when property.PayloadLength == 24:
                return;
            case "irot" when property.PayloadLength == 1 && (content[property.PayloadStart] & ~3) == 0:
            case "imir" when property.PayloadLength == 1 && (content[property.PayloadStart] & ~1) == 0:
                return;
            case "ispe":
                {
                    var payloadStart = ReadFullBox(content, property, expectedVersion: 0);
                    if (payloadStart != property.End - 8
                        || BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(payloadStart, 4)) == 0
                        || BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(payloadStart + 4, 4)) == 0)
                    {
                        throw new InvalidDataException("Invalid HEIC spatial property.");
                    }
                    return;
                }
            case "pixi":
                {
                    var payloadStart = ReadFullBox(content, property, expectedVersion: 0);
                    if (payloadStart >= property.End)
                    {
                        throw new InvalidDataException("Invalid HEIC pixel property.");
                    }
                    var channelCount = content[payloadStart];
                    if (channelCount is 0 or > 4 || payloadStart + 1 + channelCount != property.End)
                    {
                        throw new InvalidDataException("Invalid HEIC pixel channels.");
                    }
                    return;
                }
            case "colr":
                {
                    if (property.PayloadLength < 4)
                    {
                        throw new InvalidDataException("Invalid HEIC color property.");
                    }
                    var colorType = ReadFourCc(content, property.PayloadStart);
                    if (colorType == "nclx")
                    {
                        if (property.PayloadLength != 11)
                        {
                            throw new InvalidDataException("Invalid HEIC NCLX color property.");
                        }
                        return;
                    }
                    if (colorType is "prof" or "rICC")
                    {
                        ValidateIccProfile(content.AsSpan(property.PayloadStart + 4, property.PayloadLength - 4));
                        return;
                    }
                    throw new InvalidDataException("Unsupported HEIC color property.");
                }
            case "auxC":
                {
                    var payloadStart = ReadFullBox(content, property, expectedVersion: 0);
                    var offset = payloadStart;
                    var auxiliaryType = ReadNullTerminated(content, ref offset, property.End);
                    var value = Encoding.ASCII.GetString(content, auxiliaryType.Start, auxiliaryType.Length);
                    if (value is not ("urn:mpeg:hevc:2015:auxid:1"
                        or "urn:mpeg:hevc:2015:auxid:2"
                        or "urn:com:apple:photo:2020:aux:hdrgainmap"))
                    {
                        throw new InvalidDataException("Unsupported HEIC auxiliary image type.");
                    }
                    if (offset != property.End)
                    {
                        throw new InvalidDataException("HEIC auxiliary subtype data is unsupported.");
                    }
                    return;
                }
            default:
                throw new InvalidDataException("Unsupported HEIC image property.");
        }
    }

    private static void ValidateIccProfile(ReadOnlySpan<byte> profile)
    {
        if (profile.Length < 132
            || BinaryPrimitives.ReadUInt32BigEndian(profile) != profile.Length
            || !profile.Slice(36, 4).SequenceEqual("acsp"u8))
        {
            throw new InvalidDataException("Invalid HEIC ICC color profile.");
        }
        var tagCount = BinaryPrimitives.ReadUInt32BigEndian(profile[128..]);
        if (tagCount > 128 || 132L + tagCount * 12L > profile.Length)
        {
            throw new InvalidDataException("Invalid HEIC ICC tag table.");
        }
        for (var index = 0U; index < tagCount; index += 1)
        {
            var entry = profile.Slice(132 + (int)index * 12, 12);
            var offset = BinaryPrimitives.ReadUInt32BigEndian(entry[4..]);
            var length = BinaryPrimitives.ReadUInt32BigEndian(entry[8..]);
            if (offset > profile.Length || length > profile.Length - offset)
            {
                throw new InvalidDataException("HEIC ICC tag is outside its profile.");
            }
        }
    }

    private static byte[] CreateXmpReplacement(ItemInfo info, ItemLocation location)
    {
        if (!XmpContentTypes.Contains(info.ContentType!))
        {
            throw new InvalidDataException("Unsupported HEIC XMP content type.");
        }
        var length = location.Extents.Sum(extent => extent.End - extent.Start);
        if (length < 4)
        {
            throw new InvalidDataException("HEIC XMP item is too short.");
        }
        var replacement = Enumerable.Repeat((byte)' ', length).ToArray();
        "<x/>"u8.CopyTo(replacement);
        return replacement;
    }

    private static byte[] ReadItem(byte[] content, ItemLocation location)
    {
        var length = location.Extents.Sum(extent => extent.End - extent.Start);
        var result = new byte[length];
        var outputOffset = 0;
        foreach (var extent in location.Extents)
        {
            var extentLength = extent.End - extent.Start;
            content.AsSpan(extent.Start, extentLength).CopyTo(result.AsSpan(outputOffset));
            outputOffset += extentLength;
        }
        return result;
    }

    private static void WriteItem(byte[] output, ItemLocation location, byte[] replacement)
    {
        var inputOffset = 0;
        foreach (var extent in location.Extents)
        {
            var extentLength = extent.End - extent.Start;
            replacement.AsSpan(inputOffset, extentLength).CopyTo(output.AsSpan(extent.Start));
            inputOffset += extentLength;
        }
    }

    private static void EnsureUnreferencedDataIsEmpty(
        byte[] content,
        IReadOnlyList<Box> mediaDataBoxes,
        Box? itemData,
        IReadOnlyList<Extent> extents)
    {
        var dataBoxes = itemData is null
            ? mediaDataBoxes
            : mediaDataBoxes.Concat([itemData.Value]).ToList();
        foreach (var box in dataBoxes)
        {
            var cursor = box.PayloadStart;
            foreach (var extent in extents
                         .Where(extent => extent.Start >= box.PayloadStart && extent.End <= box.End)
                         .OrderBy(extent => extent.Start))
            {
                if (content.AsSpan(cursor, extent.Start - cursor).IndexOfAnyExcept((byte)0) >= 0)
                {
                    throw new InvalidDataException("Unreferenced HEIC data is not empty.");
                }
                cursor = extent.End;
            }
            if (content.AsSpan(cursor, box.End - cursor).IndexOfAnyExcept((byte)0) >= 0)
            {
                throw new InvalidDataException("Unreferenced HEIC data is not empty.");
            }
        }
    }

    private static List<Box> ReadBoxes(byte[] content, int start, int end)
    {
        var result = new List<Box>();
        var offset = start;
        while (offset < end)
        {
            if (result.Count >= MaximumBoxes)
            {
                throw new InvalidDataException("Too many HEIC boxes.");
            }
            var box = ReadBox(content, offset, end);
            result.Add(box);
            offset = box.End;
        }
        if (offset != end)
        {
            throw new InvalidDataException("Invalid HEIC box boundary.");
        }
        return result;
    }

    private static Box ReadBox(byte[] content, int start, int parentEnd)
    {
        if (start < 0 || parentEnd > content.Length || start > parentEnd - 8)
        {
            throw new InvalidDataException("Truncated HEIC box.");
        }
        var size32 = BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(start, 4));
        var type = ReadFourCc(content, start + 4);
        var headerLength = 8;
        ulong size = size32;
        if (size32 == 1)
        {
            if (start > parentEnd - 16)
            {
                throw new InvalidDataException("Truncated extended HEIC box.");
            }
            size = BinaryPrimitives.ReadUInt64BigEndian(content.AsSpan(start + 8, 8));
            headerLength = 16;
        }
        if (size32 == 0 || size < (ulong)headerLength || size > int.MaxValue
            || (ulong)start + size > (ulong)parentEnd)
        {
            throw new InvalidDataException("Invalid HEIC box size.");
        }
        return new Box(start, start + (int)size, type, start + headerLength);
    }

    private static int ReadFullBox(byte[] content, Box box, byte expectedVersion)
    {
        var (version, payloadStart) = ReadFullBoxVersion(content, box);
        if (version != expectedVersion)
        {
            throw new InvalidDataException("Unsupported HEIC full box version.");
        }
        return payloadStart;
    }

    private static (byte Version, int PayloadStart) ReadFullBoxVersion(byte[] content, Box box)
    {
        var (version, flags, payloadStart) = ReadFullBoxHeader(content, box);
        if (flags != 0)
        {
            throw new InvalidDataException("Unsupported HEIC full box flags.");
        }
        return (version, payloadStart);
    }

    private static (byte Version, uint Flags, int PayloadStart) ReadFullBoxHeader(
        byte[] content,
        Box box)
    {
        if (box.PayloadStart > box.End - 4)
        {
            throw new InvalidDataException("Truncated HEIC full box.");
        }
        var flags = (uint)(content[box.PayloadStart + 1] << 16
            | content[box.PayloadStart + 2] << 8
            | content[box.PayloadStart + 3]);
        return (content[box.PayloadStart], flags, box.PayloadStart + 4);
    }

    private static (int Start, int Length) ReadNullTerminated(
        byte[] content,
        ref int offset,
        int end)
    {
        if (offset >= end)
        {
            throw new InvalidDataException("Missing HEIC item string.");
        }
        var relativeEnd = content.AsSpan(offset, end - offset).IndexOf((byte)0);
        if (relativeEnd < 0)
        {
            throw new InvalidDataException("Unterminated HEIC item string.");
        }
        var start = offset;
        offset += relativeEnd + 1;
        return (start, relativeEnd);
    }

    private static uint ReadUInt(byte[] content, ref int offset, int width, int end)
    {
        var value = ReadVariableUInt(content, ref offset, width, end);
        if (value > uint.MaxValue)
        {
            throw new InvalidDataException("HEIC integer is too large.");
        }
        return (uint)value;
    }

    private static ulong ReadVariableUInt(byte[] content, ref int offset, int width, int end)
    {
        if (width == 0)
        {
            return 0;
        }
        if (width is < 0 or > 8 || offset < 0 || offset > end - width)
        {
            throw new InvalidDataException("Truncated HEIC integer.");
        }
        ulong value = 0;
        for (var index = 0; index < width; index += 1)
        {
            value = (value << 8) | content[offset + index];
        }
        offset += width;
        return value;
    }

    private static ulong AddChecked(ulong left, ulong right)
    {
        if (ulong.MaxValue - left < right)
        {
            throw new InvalidDataException("HEIC offset overflow.");
        }
        return left + right;
    }

    private static string ReadFourCc(byte[] content, int offset)
    {
        if (offset < 0 || offset > content.Length - 4)
        {
            throw new InvalidDataException("Truncated HEIC box type.");
        }
        return Encoding.ASCII.GetString(content, offset, 4);
    }

    private readonly record struct Box(int Start, int End, string Type, int PayloadStart)
    {
        public int PayloadLength => End - PayloadStart;
    }

    private sealed record ItemInfo(
        uint ItemId,
        string Type,
        string? ContentType,
        int NameStart,
        int NameLength);

    private sealed record ItemLocation(IReadOnlyList<Extent> Extents);

    private readonly record struct Extent(int Start, int End);
}
