// Phase 03 Plan 01 — GREEN. Targets `LECG.Services.ElementLabelService` (Plan 03-01).
// VALIDATION rows: 3-W0-01, 3-01-T1.
using FluentAssertions;
using LECG.Services;

namespace LECG.Tests.Services;

public class ElementLabelServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void GetLabelsFromRaw_returns_synthetic_name_when_raw_name_is_whitespace_or_empty()
    {
        var (name, _) = ElementLabelService.GetLabelsFromRaw("", "Walls", "WallType", 12345);
        name.Should().Be("<WallType 12345>");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetLabelsFromRaw_returns_synthetic_name_when_raw_name_is_null()
    {
        var (name, _) = ElementLabelService.GetLabelsFromRaw(null!, "Floors", "FloorType", 99);
        name.Should().Be("<FloorType 99>");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetLabelsFromRaw_preserves_raw_name_when_present()
    {
        var (name, _) = ElementLabelService.GetLabelsFromRaw("Generic - 200mm", "Walls", "WallType", 1);
        name.Should().Be("Generic - 200mm");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetLabelsFromRaw_falls_back_to_clr_type_name_when_category_is_null()
    {
        var (_, category) = ElementLabelService.GetLabelsFromRaw("Name", null!, "GraphicsStyle", 1);
        category.Should().Be("GraphicsStyle");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetLabelsFromRaw_falls_back_to_clr_type_name_when_category_is_whitespace()
    {
        var (_, category) = ElementLabelService.GetLabelsFromRaw("Name", "   ", "View", 1);
        category.Should().Be("View");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetLabelsFromRaw_preserves_raw_category_when_present()
    {
        var (_, category) = ElementLabelService.GetLabelsFromRaw("Name", "Walls", "WallType", 1);
        category.Should().Be("Walls");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    [InlineData(null, "")]
    [InlineData("", null)]
    [Trait("Category", "Unit")]
    public void GetLabelsFromRaw_never_returns_null_or_whitespace_for_either_field(
        string? rawName, string? rawCategory)
    {
        var (name, category) = ElementLabelService.GetLabelsFromRaw(rawName!, rawCategory!, "Element", 42);
        name.Should().NotBeNullOrWhiteSpace();
        category.Should().NotBeNullOrWhiteSpace();
    }
}
