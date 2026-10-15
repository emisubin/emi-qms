using System.Buffers.Binary;
using System.Text;

namespace Emi.Qms.Api.OsanProjects;

internal static class OsanSamsungSefTrailer
{
    private const int HeaderLength = 12;
    private const int DirectoryEntryLength = 12;
    private const int FooterLength = 8;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static void Validate(byte[] content, int imagePayloadEnd)
    {
        if (imagePayloadEnd < 0 || imagePayloadEnd >= content.Length)
            throw new InvalidDataException("Invalid JPEG trailer bounds.");
        if (content.Length - imagePayloadEnd < HeaderLength + DirectoryEntryLength + FooterLength
            || !content.AsSpan(content.Length - 4).SequenceEqual("SEFT"u8))
            throw new InvalidDataException("Unindexed JPEG trailer.");

        var directoryBytes = BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(content.Length - FooterLength, 4));
        if (directoryBytes < HeaderLength + DirectoryEntryLength || directoryBytes > int.MaxValue)
            throw new InvalidDataException("Invalid SEF footer.");
        var headerStartLong = (long)content.Length - FooterLength - directoryBytes;
        if (headerStartLong < imagePayloadEnd || headerStartLong > content.Length - FooterLength - HeaderLength)
            throw new InvalidDataException("Invalid SEF footer bounds.");
        var headerStart = (int)headerStartLong;
        if (!content.AsSpan(headerStart, 4).SequenceEqual("SEFH"u8))
            throw new InvalidDataException("Invalid SEF header.");
        var version = BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(headerStart + 4, 4));
        var count = BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(headerStart + 8, 4));
        if (version == 0 || count == 0 || count > int.MaxValue
            || HeaderLength + (long)count * DirectoryEntryLength != directoryBytes)
            throw new InvalidDataException("Invalid SEF header fields.");
        if ((long)count * 9 > headerStart - imagePayloadEnd)
            throw new InvalidDataException("Invalid SEF record count.");

        var records = new List<(int Start, int End, int EntryStart)>((int)count);
        for (var index = 0; index < (int)count; index++)
        {
            var entryStart = checked(headerStart + HeaderLength + index * DirectoryEntryLength);
            var backwardOffset = BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(entryStart + 4, 4));
            var recordSize = BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(entryStart + 8, 4));
            if (backwardOffset == 0 || recordSize < 9 || backwardOffset > int.MaxValue || recordSize > int.MaxValue)
                throw new InvalidDataException("Invalid SEF record bounds.");
            var recordStartLong = (long)headerStart - backwardOffset;
            var recordEndLong = recordStartLong + recordSize;
            if (recordStartLong < imagePayloadEnd || recordEndLong > headerStart)
                throw new InvalidDataException("Invalid SEF record bounds.");
            var recordStart = (int)recordStartLong;
            var recordEnd = (int)recordEndLong;
            records.Add((recordStart, recordEnd, entryStart));
        }

        records.Sort((left, right) => left.Start.CompareTo(right.Start));
        var next = imagePayloadEnd;
        foreach (var record in records)
        {
            if (record.Start != next) throw new InvalidDataException("SEF records contain a gap or overlap.");
            next = record.End;
        }
        if (next != headerStart) throw new InvalidDataException("SEF records do not cover the trailer payload.");

        foreach (var record in records)
        {
            if (!content.AsSpan(record.Start, 4).SequenceEqual(content.AsSpan(record.EntryStart, 4)))
                throw new InvalidDataException("SEF record type does not match its directory entry.");
            var nameLength = BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(record.Start + 4, 4));
            var recordSize = record.End - record.Start;
            if (nameLength == 0 || nameLength > recordSize - 8 || nameLength > int.MaxValue)
                throw new InvalidDataException("Invalid SEF record name bounds.");
            string name;
            try { name = StrictUtf8.GetString(content, record.Start + 8, (int)nameLength); }
            catch (DecoderFallbackException exception) { throw new InvalidDataException("Invalid SEF record name.", exception); }
            if (name.Any(char.IsControl)) throw new InvalidDataException("Invalid SEF record name.");
        }
    }
}
