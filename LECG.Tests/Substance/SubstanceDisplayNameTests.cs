using FluentAssertions;
using LECG.Core.Substance;
using Xunit;

namespace LECG.Tests.Substance;

public class SubstanceDisplayNameTests
{
    [Theory]
    [InlineData("asphalt_rough", "Asphalt Rough")]
    [InlineData("clean_asphalt_ground_patch_01", "Clean Asphalt Ground Patch 01")]
    [InlineData("ceramic_tiles_night_rider", "Ceramic Tiles Night Rider")]
    [InlineData("porcelain", "Porcelain")]
    public void FromSlug_TitleCasesTokens(string slug, string expected)
    {
        SubstanceDisplayName.FromSlug(slug).Should().Be(expected);
    }

    [Fact]
    public void FromSlug_CollapsesRepeatedUnderscores()
    {
        SubstanceDisplayName.FromSlug("a__b").Should().Be("A B");
    }

    [Fact]
    public void FromSlug_Empty_ReturnsEmpty()
    {
        SubstanceDisplayName.FromSlug("").Should().BeEmpty();
    }
}
