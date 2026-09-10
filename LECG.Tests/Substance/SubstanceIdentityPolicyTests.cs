using FluentAssertions;
using LECG.Core.Substance;
using Xunit;

namespace LECG.Tests.Substance;

public class SubstanceIdentityPolicyTests
{
    private static SubstanceMaterialEntry Entry(int res = 4096) => new(
        "Asphalt", "asphalt_rough", "Asphalt Rough", @"C:\lib\Asphalt\asphalt_rough",
        "b", "n", "r", "m", null, null, null, res, "DirectX");

    [Fact]
    public void Description_IncludesCategorySlugAndResolution()
    {
        SubstanceIdentityPolicy.Description(Entry()).Should().Be("Asphalt / asphalt_rough / Substance 4K bake");
        SubstanceIdentityPolicy.Description(Entry(2048)).Should().Be("Asphalt / asphalt_rough / Substance 2K bake");
    }

    [Fact]
    public void Keywords_LowercaseCategory()
    {
        SubstanceIdentityPolicy.Keywords(Entry()).Should().Be("substance, pbr, asphalt");
    }

    [Fact]
    public void Keywords_MultiWordCategoryKept()
    {
        var e = Entry() with { Category = "Car Paint" };
        SubstanceIdentityPolicy.Keywords(e).Should().Be("substance, pbr, car paint");
    }
}
