using ImageMagick;
using ZXing;

namespace Emi.Qms.Api.Tests;

internal static class TestImageCodec
{
    internal static byte[] CreateTransparentImage(int width, int height, MagickFormat format)
    {
        using var image = new MagickImage(MagickColors.Transparent, (uint)width, (uint)height);
        image.Depth = 8;
        image.Strip();
        return image.ToByteArray(format == MagickFormat.Png ? MagickFormat.Png32 : format);
    }

    internal static byte[] CreateRedBlueImage(MagickFormat format)
    {
        using var images = new MagickImageCollection
        {
            new MagickImage(MagickColors.Red, 1, 1),
            new MagickImage(new MagickColor(0, 128, 255), 1, 1)
        };
        using var image = images.AppendHorizontally();
        image.Depth = 8;
        image.Strip();
        return image.ToByteArray(format == MagickFormat.Png ? MagickFormat.Png32 : format);
    }

    internal static Result? DecodeQr(byte[] encodedImage, bool pureBarcode = false)
    {
        using var image = new MagickImage(encodedImage);
        image.Depth = 8;
        var rgb = image.ToByteArray(MagickFormat.Rgb);
        if (rgb.Length != checked((int)(image.Width * image.Height * 3)))
            throw new InvalidDataException("Decoded test image did not produce RGB24 pixels.");
        var reader = new BarcodeReaderGeneric();
        reader.Options.PossibleFormats = [BarcodeFormat.QR_CODE];
        reader.Options.TryHarder = true;
        reader.Options.PureBarcode = pureBarcode;
        return reader.Decode(
            rgb,
            checked((int)image.Width),
            checked((int)image.Height),
            RGBLuminanceSource.BitmapFormat.RGB24);
    }
}
