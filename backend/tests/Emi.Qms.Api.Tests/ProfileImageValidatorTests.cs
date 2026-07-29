using Emi.Qms.Api.Identity;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class ProfileImageValidatorTests
{
    [Fact]
    public void Validate_AcceptsBoundedPngAndReadsDimensions()
    {
        var content = new byte[45];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(content, 0);
        content[11] = 13;
        "IHDR"u8.CopyTo(content.AsSpan(12, 4));
        content[19] = 64;
        content[23] = 32;
        "IEND"u8.CopyTo(content.AsSpan(content.Length - 8, 4));

        var result = ProfileImageValidator.Validate(content);

        Assert.True(result.Succeeded);
        Assert.Equal("image/png", result.NormalizedMime);
        Assert.Equal(64, result.Width);
        Assert.Equal(32, result.Height);
    }

    [Fact]
    public void Validate_AcceptsBoundedJpegAndReadsDimensions()
    {
        byte[] content =
        [
            0xFF, 0xD8,
            0xFF, 0xC0, 0x00, 0x11, 0x08, 0x00, 0x20, 0x00, 0x40,
            0x03, 0x01, 0x11, 0x00, 0x02, 0x11, 0x00, 0x03, 0x11, 0x00,
            0xFF, 0xD9
        ];

        var result = ProfileImageValidator.Validate(content);

        Assert.True(result.Succeeded);
        Assert.Equal("image/jpeg", result.NormalizedMime);
        Assert.Equal(64, result.Width);
        Assert.Equal(32, result.Height);
    }

    [Fact]
    public void Validate_RejectsSpoofedOrOversizedContent()
    {
        var spoofed = ProfileImageValidator.Validate("not an image"u8.ToArray());
        var oversized = ProfileImageValidator.Validate(new byte[ProfileImageValidator.MaxBytes + 1]);

        Assert.False(spoofed.Succeeded);
        Assert.False(oversized.Succeeded);
    }
}
