using ImageMagick;

namespace Emi.Qms.Api.OsanProjects;

internal static class OsanHeicImageCodec
{
    private static readonly SemaphoreSlim DecodeGate = new(1, 1);
    internal static async Task<bool> ValidateAsync(byte[] content, CancellationToken ct)
    {
        await DecodeGate.WaitAsync(ct);
        try
        {
            OsanProgressPhotoValidator.ConfigureDecoderResourceLimits();
            using var image = new MagickImage();
            using var stream = new MemoryStream(content, writable: false);
            await image.ReadAsync(stream, MagickFormat.Heic, ct);
            return image.Width > 0 && image.Height > 0;
        }
        catch (MagickResourceLimitErrorException ex) { throw new OsanImageResourceLimitException(ex); }
        catch (MagickException) { return false; }
        finally { DecodeGate.Release(); }
    }
    internal static async Task<byte[]> PreviewAsync(byte[] content, CancellationToken ct)
    {
        await DecodeGate.WaitAsync(ct);
        try
        {
            OsanProgressPhotoValidator.ConfigureDecoderResourceLimits();
            using var image = new MagickImage(); using var stream = new MemoryStream(content, writable: false);
            await image.ReadAsync(stream, MagickFormat.Heic, ct);
            image.AutoOrient();
            // Convert embedded camera color profiles before removing metadata from the display copy.
            image.TransformColorSpace(ColorProfiles.SRGB);
            // Only the display copy is resized. Stored HEVC image payloads are never re-encoded.
            if (image.Width > 2048 || image.Height > 2048) image.Resize(2048, 2048);
            image.Strip(); image.Quality = 90;
            return image.ToByteArray(MagickFormat.Jpeg);
        }
        catch (MagickException ex) { throw new InvalidDataException("HEIC 미리보기를 만들 수 없습니다.", ex); }
        finally { DecodeGate.Release(); }
    }
}
