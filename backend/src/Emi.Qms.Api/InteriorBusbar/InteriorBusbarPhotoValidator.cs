using Emi.Qms.Api.OsanProjects;
using ImageMagick;

namespace Emi.Qms.Api.InteriorBusbar;

internal sealed record InteriorBusbarPhotoInput(string ContentType, byte[] Content);

internal static class InteriorBusbarPhotoValidator
{
    private static readonly SemaphoreSlim WebPDecodeGate = new(1, 1);
    public const int MaximumTotalBytes = OsanProgressPhotoValidator.MaximumTotalBytes;
    public const long MaximumMultipartBytes = OsanProgressPhotoValidator.MaximumMultipartBytes;

    public static async Task<(InteriorBusbarPhotoInput? Photo, string? Error)> ValidateAsync(
        string? fileName, string? declaredContentType, byte[] content, CancellationToken cancellationToken)
    {
        if (content.Length < 1)
            return (null, "빈 사진 파일은 첨부할 수 없습니다.");
        if (content.Length > MaximumTotalBytes)
            return (null, "사진 전체 크기는 40MiB 이하여야 합니다.");
        if (LooksLikeWebP(content))
        {
            var declared = declaredContentType?.Trim().ToLowerInvariant();
            var extension = Path.GetExtension(Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/')));
            if (declared is not (null or "" or "application/octet-stream" or "image/webp")
                || !extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
                return (null, "파일의 형식 정보와 실제 이미지 형식이 일치하지 않습니다.");
            await WebPDecodeGate.WaitAsync(cancellationToken);
            try
            {
                OsanProgressPhotoValidator.ConfigureDecoderResourceLimits();
                var info = new MagickImageInfo(content);
                if (info.Format != MagickFormat.WebP || info.Width == 0 || info.Height == 0
                    || info.Width > 12000 || info.Height > 12000 || (long)info.Width * info.Height > 40_000_000)
                    return (null, "파일 내용이 올바른 JPEG·PNG·HEIC·WebP 이미지가 아닙니다.");
                using var image = new MagickImage(content);
                if (image.Format != MagickFormat.WebP || image.Width == 0 || image.Height == 0
                    || image.Width > 12000 || image.Height > 12000 || (long)image.Width * image.Height > 40_000_000)
                    return (null, "파일 내용이 올바른 JPEG·PNG·HEIC·WebP 이미지가 아닙니다.");
                image.AutoOrient();
                image.TransformColorSpace(ColorProfiles.SRGB);
                image.Strip();
                var converted = image.ToByteArray(MagickFormat.Png);
                var (validated, error) = await OsanProgressPhotoValidator.ValidateAsync(
                    "photo.png", "image/png", converted, cancellationToken);
                return validated is null ? (null, error) : (new("image/png", validated.Content), null);
            }
            catch (Exception exception) when (exception is MagickException or InvalidDataException or OverflowException)
            {
                return (null, "파일 내용이 올바른 JPEG·PNG·HEIC·WebP 이미지가 아닙니다.");
            }
            finally { WebPDecodeGate.Release(); }
        }

        var (photo, validationError) = await OsanProgressPhotoValidator.ValidateAsync(
            fileName, declaredContentType, content, cancellationToken);
        return photo is null
            ? (null, validationError)
            : (new(photo.NormalizedMime, photo.Content), null);
    }

    private static bool LooksLikeWebP(byte[] content) => content.Length >= 12
        && content.AsSpan(0, 4).SequenceEqual("RIFF"u8)
        && content.AsSpan(8, 4).SequenceEqual("WEBP"u8);
}
