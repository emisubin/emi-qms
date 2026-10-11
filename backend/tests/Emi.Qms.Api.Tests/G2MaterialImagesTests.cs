using Xunit;
using System.Security.Cryptography;
using Emi.Qms.Api.G2;

namespace Emi.Qms.Api.Tests;

public sealed class G2MaterialImagesTests
{
    [Fact]
    public void Approved_template_images_match_original_catalog_and_exclude_non_item_artwork()
    {
        var images = G2MaterialImages.All;
        Assert.Equal(47, images.Count);
        Assert.Equal("af6fdc65c2bba0c8d960de68bfeeec9390b3a9707e1019cd070f073df4e4f12c", Convert.ToHexStringLower(SHA256.HashData(images["item-01-1.png"])));
        Assert.Equal("6292335ec659d65c9a774e46c2e77ee60f3d2bc1c35374a89770f37ced70e096", Convert.ToHexStringLower(SHA256.HashData(images["item-02-1.png"])));
        Assert.DoesNotContain("../xl/media/image1.png", images.Keys);
        Assert.Same(images, G2MaterialImages.All);
    }
}
