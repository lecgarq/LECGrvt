using FluentAssertions;
using LECG.Core.Naming;

namespace LECG.Tests.Services;

public class LinePatternNamingPolicyTests
{
    [Fact]
    public void CreateFamilyName_WhenDashAndSpace_ReturnsDash()
    {
        var familyName = LinePatternNamingPolicy.CreateFamilyName(new[] { "Dash", "Space" });

        familyName.Should().Be("DASH");
    }

    [Fact]
    public void CreateFamilyName_WhenDashSpaceDashSpace_ReturnsCenter()
    {
        var familyName = LinePatternNamingPolicy.CreateFamilyName(new[] { "Dash", "Space", "Dash", "Space" });

        familyName.Should().Be("CENTER");
    }

    [Fact]
    public void CreateFamilyName_WhenDotSpace_ReturnsDot()
    {
        var familyName = LinePatternNamingPolicy.CreateFamilyName(new[] { "Dot", "Space" });

        familyName.Should().Be("DOT");
    }

    [Fact]
    public void CreateFamilyName_WhenDotSpaceDotSpace_ReturnsDotCenter()
    {
        var familyName = LinePatternNamingPolicy.CreateFamilyName(new[] { "Dot", "Space", "Dot", "Space" });

        familyName.Should().Be("DOT-CENTER");
    }

    [Fact]
    public void CreateFamilyName_WhenDashSpaceDotSpace_ReturnsGrid()
    {
        var familyName = LinePatternNamingPolicy.CreateFamilyName(new[] { "Dash", "Space", "Dot", "Space" });

        familyName.Should().Be("GRID");
    }

    [Fact]
    public void CreateFamilyName_WhenDashDotDotPattern_ReturnsDash2Dots()
    {
        var familyName = LinePatternNamingPolicy.CreateFamilyName(
            new[] { "Dash", "Space", "Dot", "Space", "Dot", "Space" });

        familyName.Should().Be("DASH-2DOTS");
    }

    [Fact]
    public void CreateFamilyName_WhenDashDotDotDotPattern_ReturnsDash3Dots()
    {
        var familyName = LinePatternNamingPolicy.CreateFamilyName(
            new[] { "Dash", "Space", "Dot", "Space", "Dot", "Space", "Dot", "Space" });

        familyName.Should().Be("DASH-3DOTS");
    }

    [Fact]
    public void CreateFamilyName_WhenTripleDash_ReturnsTripleDash()
    {
        var familyName = LinePatternNamingPolicy.CreateFamilyName(
            new[] { "Dash", "Space", "Dash", "Space", "Dash", "Space" });

        familyName.Should().Be("TRIPLE-DASH");
    }

    [Fact]
    public void CreateFamilyName_WhenDoubleDashDot_ReturnsDoubleDashDot()
    {
        var familyName = LinePatternNamingPolicy.CreateFamilyName(
            new[] { "Dash", "Space", "Dash", "Space", "Dot", "Space" });

        familyName.Should().Be("DOUBLE-DASH-DOT");
    }

    [Fact]
    public void CreateFamilyName_WhenDoubleDash2Dots_ReturnsDoubleDash2Dots()
    {
        var familyName = LinePatternNamingPolicy.CreateFamilyName(
            new[] { "Dash", "Space", "Dash", "Space", "Dot", "Space", "Dot", "Space" });

        familyName.Should().Be("DOUBLE-DASH-2DOTS");
    }

    [Fact]
    public void CreateFamilyName_WhenTripleDot_ReturnsTripleDot()
    {
        var familyName = LinePatternNamingPolicy.CreateFamilyName(
            new[] { "Dot", "Space", "Dot", "Space", "Dot", "Space" });

        familyName.Should().Be("3DOTS");
    }

    [Fact]
    public void CreateFamilyName_WhenFourDashes_Returns4Dash()
    {
        var familyName = LinePatternNamingPolicy.CreateFamilyName(
            new[] { "Dash", "Space", "Dash", "Space", "Dash", "Space", "Dash", "Space" });

        familyName.Should().Be("4-DASH");
    }

    [Fact]
    public void CreateFamilyName_WhenOddSegmentCount_ReturnsNull()
    {
        var familyName = LinePatternNamingPolicy.CreateFamilyName(new[] { "Dash", "Dot", "Space" });

        familyName.Should().BeNull();
    }

    [Fact]
    public void CreateFamilyName_WhenEmpty_ReturnsNull()
    {
        var familyName = LinePatternNamingPolicy.CreateFamilyName(Array.Empty<string>());

        familyName.Should().BeNull();
    }

    [Fact]
    public void CreateFamilyName_WhenSpaceNotAlternating_ReturnsNull()
    {
        // Dash Dash Space Space — Space not in odd positions
        var familyName = LinePatternNamingPolicy.CreateFamilyName(new[] { "Dash", "Dash", "Space", "Space" });

        familyName.Should().BeNull();
    }

    [Fact]
    public void IsWithinTolerance_WhenEachValueIsAtBoundary_ReturnsTrue()
    {
        bool withinTolerance = LinePatternNamingPolicy.IsWithinTolerance(
            new[] { 10.0, 5.0 },
            new[] { 11.0, 4.0 },
            toleranceMm: 1.0);

        withinTolerance.Should().BeTrue();
    }

    [Fact]
    public void IsWithinTolerance_WhenAnyValueExceedsBoundary_ReturnsFalse()
    {
        bool withinTolerance = LinePatternNamingPolicy.IsWithinTolerance(
            new[] { 10.0, 5.0 },
            new[] { 11.01, 5.0 },
            toleranceMm: 1.0);

        withinTolerance.Should().BeFalse();
    }

    [Fact]
    public void AverageLengthsMm_WhenGroupedVariantsExist_ReturnsMeanValues()
    {
        IReadOnlyList<double> average = LinePatternNamingPolicy.AverageLengthsMm(
            new[]
            {
                (IReadOnlyList<double>)new[] { 10.0, 5.0 },
                new[] { 10.6, 5.4 },
            });

        average.Should().Equal(10.3, 5.2);
    }

    [Fact]
    public void CreateSemanticName_WhenDashFamily_UsesMillimeterSuffixes()
    {
        string name = LinePatternNamingPolicy.CreateSemanticName(
            "DASH",
            new[] { 1.0, 2.25 });

        name.Should().Be("LECG-LP-DASH (1mm-2.25mm)");
    }

    [Fact]
    public void CreateSemanticName_WhenGridFamily_KeepsFourValuesIncludingZeroDotLength()
    {
        string name = LinePatternNamingPolicy.CreateSemanticName(
            "GRID",
            new[] { 12.0, 6.0, 0.0, 6.0 });

        name.Should().Be("LECG-LP-GRID (12mm-6mm-0mm-6mm)");
    }

    [Fact]
    public void CreateSemanticName_WhenDynamicFamily_FormatsCorrectly()
    {
        string name = LinePatternNamingPolicy.CreateSemanticName(
            "DASH-2DOTS",
            new[] { 3.0, 1.5, 0.0, 1.5, 0.0, 1.5 });

        name.Should().Be("LECG-LP-DASH-2DOTS (3mm-1.5mm-0mm-1.5mm-0mm-1.5mm)");
    }
}
