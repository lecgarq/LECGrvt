// Phase 03 Plan 03 — GREEN. Targets `LECG.Services.BaseElementCollectionService`
// null-Category fallback path (REQ-01, VALIDATION row 3-W0-02).
//
// Direct unit coverage of CollectBaseElements requires a Revit Document, so the
// no-blanks invariant for collection-layer rows is exercised through the SSoT
// helper `ElementLabelService.GetLabelsFromRaw`, which BaseElementCollectionService
// now delegates to (via `ElementLabelService.GetLabels(Element)`) at every former
// null-skip site (typeCollector + GraphicsStyle path).
//
// The Type_is_referenceable_anchor test stays as the file's compile anchor.
using FluentAssertions;
using LECG.Services;

namespace LECG.Tests.Services;

/// <summary>
/// 3-W0-02 — Plan 03-03 GREEN. Asserts that the SSoT helper used by
/// BaseElementCollectionService produces non-blank Name and Category for the
/// degenerate inputs the collector previously skipped silently.
/// </summary>
public class BaseElementCollectionServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Type_is_referenceable_anchor()
    {
        // Anchor: forces the test project to break if BaseElementCollectionService is
        // renamed or moved. Verifies plan 03-03's refactor preserves the public surface.
        typeof(BaseElementCollectionService).Should().NotBeNull();
        typeof(BaseElementCollectionService).Namespace.Should().Be("LECG.Services");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Collector_normalization_produces_non_blank_name_for_blank_raw_name()
    {
        // BaseElementCollectionService now routes every row through
        // ElementLabelService.GetLabels(Element), which delegates to
        // GetLabelsFromRaw. For a blank raw name, the helper must yield a synthetic
        // non-blank label so no row in the returned list has a blank Name.
        var (name, category) = ElementLabelService.GetLabelsFromRaw(
            rawName: "  ", rawCategory: "Walls", clrTypeName: "WallType", id: 42);

        name.Should().NotBeNullOrWhiteSpace();
        name.Should().Be("<WallType 42>");
        category.Should().Be("Walls");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Collector_normalization_produces_non_blank_category_when_raw_category_is_null()
    {
        // BaseElementCollectionService used to silently skip elements with null
        // Category. Plan 03-03 removes that skip and relies on
        // ElementLabelService.GetLabelsFromRaw to fall back to the CLR type name
        // when raw category is null/blank.
        var (name, category) = ElementLabelService.GetLabelsFromRaw(
            rawName: "Generic - 200mm", rawCategory: null!, clrTypeName: "WallType", id: 42);

        name.Should().Be("Generic - 200mm");
        category.Should().NotBeNullOrWhiteSpace();
        category.Should().Be("WallType");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Collector_normalization_produces_non_blank_pair_when_both_inputs_blank()
    {
        // Worst-case fallback: blank name AND blank category. The collector's
        // no-blanks invariant requires both fields to come back populated.
        var (name, category) = ElementLabelService.GetLabelsFromRaw(
            rawName: null!, rawCategory: "", clrTypeName: "GraphicsStyle", id: 7);

        name.Should().NotBeNullOrWhiteSpace();
        name.Should().Be("<GraphicsStyle 7>");
        category.Should().NotBeNullOrWhiteSpace();
        category.Should().Be("GraphicsStyle");
    }
}
