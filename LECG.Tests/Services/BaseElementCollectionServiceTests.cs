// Phase 03 Plan 03 — GREEN. Targets `LECG.Services.BaseElementCollectionService`
// null-Category fallback path (REQ-01, VALIDATION row 3-W0-02).
//
// Direct unit coverage of CollectBaseElements requires a Revit Document, so the
// no-blanks invariant for collection-layer rows is exercised through the SSoT
// helper `ElementLabelService.GetLabelsFromRaw`, which BaseElementCollectionService
// now delegates to (via `ElementLabelService.GetLabels(Element)`) at every former
// null-skip site (typeCollector + GraphicsStyle path).
//
// Wave 1 (05-01) deepening tests target three extracted pure-data helpers:
//   TryGetGroupLabel, DispatchScopeFlags, MergeParamScanResults.
// Revit-bound paths remain skip-gated with manual verification pointer.
//
// The Type_is_referenceable_anchor test stays as the file's compile anchor.
using FluentAssertions;
using LECG.Services;

namespace LECG.Tests.Services;

/// <summary>
/// 3-W0-02 — Plan 03-03 GREEN. Asserts that the SSoT helper used by
/// BaseElementCollectionService produces non-blank Name and Category for the
/// degenerate inputs the collector previously skipped silently.
///
/// Wave 1 deepening tests (plan 05-01): TryGetGroupLabel, DispatchScopeFlags,
/// MergeParamScanResults pure-data helpers.
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

    // -----------------------------------------------------------------------
    // Wave 1 GREEN tests — TryGetGroupLabel (plan 05-01)
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public void TryGetGroupLabel_GetIdSucceeds_GetLabelReturnsString_ReturnsLabel()
    {
        // resolve() succeeds — returns the label string directly.
        // TryGetGroupLabel should return that label unchanged.
        var result = BaseElementCollectionService.TryGetGroupLabel(
            () => "Identity Data");

        result.Should().Be("Identity Data");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TryGetGroupLabel_GetIdThrows_ReturnsEmptyString()
    {
        // resolve() throws (simulates GetGroupTypeId() failure).
        // TryGetGroupLabel must catch and return "".
        var result = BaseElementCollectionService.TryGetGroupLabel(
            () => throw new InvalidOperationException("no group type"));

        result.Should().Be("");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TryGetGroupLabel_GetLabelThrows_ReturnsEmptyString()
    {
        // resolve() throws (simulates GetLabelForGroup() failure).
        // TryGetGroupLabel must catch and return "".
        var result = BaseElementCollectionService.TryGetGroupLabel(
            () => throw new InvalidOperationException("label resolution failed"));

        result.Should().Be("");
    }

    // -----------------------------------------------------------------------
    // Wave 1 GREEN tests — DispatchScopeFlags (plan 05-01)
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public void DispatchScopeFlags_TypesOnly_ReturnsTypesMask()
    {
        var mask = BaseElementCollectionService.DispatchScopeFlags(
            types: true, families: false, views: false, sheets: false,
            materials: false, objectStyles: false, lineStyles: false,
            fillPatterns: false, familyParameters: false);

        mask.Should().HaveFlag(ScopeMask.Types);
        mask.Should().NotHaveFlag(ScopeMask.Families);
        mask.Should().NotHaveFlag(ScopeMask.Materials);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DispatchScopeFlags_FamiliesOnly_ReturnsFamiliesMask()
    {
        var mask = BaseElementCollectionService.DispatchScopeFlags(
            types: false, families: true, views: false, sheets: false,
            materials: false, objectStyles: false, lineStyles: false,
            fillPatterns: false, familyParameters: false);

        mask.Should().HaveFlag(ScopeMask.Families);
        mask.Should().NotHaveFlag(ScopeMask.Types);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DispatchScopeFlags_MaterialsOnly_ReturnsMaterialsMask()
    {
        var mask = BaseElementCollectionService.DispatchScopeFlags(
            types: false, families: false, views: false, sheets: false,
            materials: true, objectStyles: false, lineStyles: false,
            fillPatterns: false, familyParameters: false);

        mask.Should().HaveFlag(ScopeMask.Materials);
        mask.Should().NotHaveFlag(ScopeMask.Types);
        mask.Should().NotHaveFlag(ScopeMask.Families);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DispatchScopeFlags_ParametersOnly_ReturnsParametersMask()
    {
        var mask = BaseElementCollectionService.DispatchScopeFlags(
            types: false, families: false, views: false, sheets: false,
            materials: false, objectStyles: false, lineStyles: false,
            fillPatterns: false, familyParameters: true);

        mask.Should().HaveFlag(ScopeMask.FamilyParameters);
        mask.Should().NotHaveFlag(ScopeMask.Types);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DispatchScopeFlags_MultipleScopes_ReturnsCombinedMask()
    {
        var mask = BaseElementCollectionService.DispatchScopeFlags(
            types: true, families: true, views: false, sheets: false,
            materials: true, objectStyles: false, lineStyles: false,
            fillPatterns: false, familyParameters: false);

        mask.Should().HaveFlag(ScopeMask.Types);
        mask.Should().HaveFlag(ScopeMask.Families);
        mask.Should().HaveFlag(ScopeMask.Materials);
        mask.Should().NotHaveFlag(ScopeMask.Views);
        mask.Should().NotHaveFlag(ScopeMask.FamilyParameters);
    }

    // -----------------------------------------------------------------------
    // Wave 1 GREEN tests — MergeParamScanResults (plan 05-01)
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public void MergeParamScanResults_DeduplicatesByFamilyIdAndParamName()
    {
        // scanA and scanB both have a row for family 1 / param "Height".
        // The merged result should contain only ONE row for that pair.
        var scanA = new List<ElementData>
        {
            new ElementData { Id = 1, Name = "Height", Category = "DoorFamily" },
            new ElementData { Id = 1, Name = "Width",  Category = "DoorFamily" },
        };
        var scanB = new List<ElementData>
        {
            new ElementData { Id = 1, Name = "Height", Category = "DoorFamily" }, // duplicate
            new ElementData { Id = 2, Name = "Depth",  Category = "WindowFamily" },
        };

        var result = BaseElementCollectionService.MergeParamScanResults(scanA, scanB);

        result.Should().HaveCount(3);
        result.Where(r => r.Id == 1 && r.Name == "Height").Should().HaveCount(1);
        result.Where(r => r.Id == 1 && r.Name == "Width").Should().HaveCount(1);
        result.Where(r => r.Id == 2 && r.Name == "Depth").Should().HaveCount(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MergeParamScanResults_EmptyInputs_ReturnsEmpty()
    {
        var result = BaseElementCollectionService.MergeParamScanResults(
            new List<ElementData>(),
            new List<ElementData>());

        result.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Revit-path RED rows — skip-gated; manual verification in 05-VERIFICATION.md
    // -----------------------------------------------------------------------

    [Fact(Skip = "Revit FilteredElementCollector path — covered by manual Revit verification in 05-VERIFICATION.md")]
    public void CollectByScope_TypesPath_ProducesRowPerType()
    {
        Assert.True(false, "Revit-bound path; see 05-VERIFICATION.md manual checklist");
    }

    [Fact(Skip = "Revit GraphicsStyle path — covered by manual Revit verification in 05-VERIFICATION.md")]
    public void CollectByScope_GraphicsStyleNullCategory_ProducesFallbackRow()
    {
        Assert.True(false, "Revit-bound path; see 05-VERIFICATION.md manual checklist");
    }
}
