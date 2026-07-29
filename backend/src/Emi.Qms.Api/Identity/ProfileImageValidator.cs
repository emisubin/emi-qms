using System.Buffers.Binary;

namespace Emi.Qms.Api.Identity;

public static class ProfileImageValidator
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxDimension = 8192;

    public static ProfileImageValidationResult Validate(byte[] content)
    {
        if (content.Length is < 1 or > MaxBytes)
        {
            return ProfileImageValidationResult.Invalid("프로필 사진은 5MB 이하 JPEG 또는 PNG 파일이어야 합니다.");
        }

        if (TryReadPng(content, out var pngWidth, out var pngHeight))
        {
            return ValidDimensions(pngWidth, pngHeight)
                ? ProfileImageValidationResult.Valid("image/png", pngWidth, pngHeight)
                : ProfileImageValidationResult.Invalid("프로필 사진의 가로·세로 크기는 각각 1~8192px여야 합니다.");
        }

        if (TryReadJpeg(content, out var jpegWidth, out var jpegHeight))
        {
            return ValidDimensions(jpegWidth, jpegHeight)
                ? ProfileImageValidationResult.Valid("image/jpeg", jpegWidth, jpegHeight)
                : ProfileImageValidationResult.Invalid("프로필 사진의 가로·세로 크기는 각각 1~8192px여야 합니다.");
        }

        return ProfileImageValidationResult.Invalid("파일 내용이 올바른 JPEG 또는 PNG 프로필 사진이 아닙니다.");
    }

    private static bool TryReadPng(byte[] content, out int width, out int height)
    {
        width = 0;
        height = 0;
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (content.Length < 45 || !content.AsSpan(0, 8).SequenceEqual(signature)) return false;
        if (BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(8, 4)) != 13) return false;
        if (!content.AsSpan(12, 4).SequenceEqual("IHDR"u8)) return false;
        if (!content.AsSpan(content.Length - 8, 4).SequenceEqual("IEND"u8)) return false;
        width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(16, 4)));
        height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(20, 4)));
        return true;
    }

    private static bool TryReadJpeg(byte[] content, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (content.Length < 12 || content[0] != 0xFF || content[1] != 0xD8
            || content[^2] != 0xFF || content[^1] != 0xD9)
        {
            return false;
        }

        var offset = 2;
        while (offset + 4 <= content.Length - 2)
        {
            if (content[offset] != 0xFF) return false;
            while (offset < content.Length && content[offset] == 0xFF) offset += 1;
            if (offset >= content.Length) return false;
            var marker = content[offset++];
            if (marker is 0xD8 or 0xD9 || marker is >= 0xD0 and <= 0xD7 || marker == 0x01) continue;
            if (offset + 2 > content.Length) return false;
            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(content.AsSpan(offset, 2));
            if (segmentLength < 2 || offset + segmentLength > content.Length) return false;

            if (IsStartOfFrame(marker))
            {
                if (segmentLength < 7) return false;
                height = BinaryPrimitives.ReadUInt16BigEndian(content.AsSpan(offset + 3, 2));
                width = BinaryPrimitives.ReadUInt16BigEndian(content.AsSpan(offset + 5, 2));
                return true;
            }

            if (marker == 0xDA) return false;
            offset += segmentLength;
        }

        return false;
    }

    private static bool IsStartOfFrame(byte marker)
        => marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7
            or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF;

    private static bool ValidDimensions(int width, int height)
        => width is >= 1 and <= MaxDimension && height is >= 1 and <= MaxDimension;
}

public sealed record ProfileImageValidationResult(
    bool Succeeded,
    string? NormalizedMime,
    int Width,
    int Height,
    string? ErrorMessage)
{
    public static ProfileImageValidationResult Valid(string normalizedMime, int width, int height)
        => new(true, normalizedMime, width, height, null);

    public static ProfileImageValidationResult Invalid(string message)
        => new(false, null, 0, 0, message);
}
