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

    // -----------------------------------------------------------------------
    // Wave 1 RED rows — plan 05-01 owns implementation
    // -----------------------------------------------------------------------

    [Fact(Skip = "Implemented by plan 05-01: ParamGroup label resolution helper — GetId succeeds, GetLabel returns string")]
    public void TryGetGroupLabel_GetIdSucceeds_GetLabelReturnsString_ReturnsLabel()
    {
        Assert.True(false, "see plan 05-01");
    }

    [Fact(Skip = "Implemented by plan 05-01: ParamGroup label resolution helper — GetId throws, returns empty string")]
    public void TryGetGroupLabel_GetIdThrows_ReturnsEmptyString()
    {
        Assert.True(false, "see plan 05-01");
    }

    [Fact(Skip = "Implemented by plan 05-01: ParamGroup label resolution helper — GetLabel throws, returns empty string")]
    public void TryGetGroupLabel_GetLabelThrows_ReturnsEmptyString()
    {
        Assert.True(false, "see plan 05-01");
    }

    [Fact(Skip = "Implemented by plan 05-01: scope dispatch helper — Types-only returns types mask")]
    public void DispatchScopeFlags_TypesOnly_ReturnsTypesMask()
    {
        Assert.True(false, "see plan 05-01");
    }

    [Fact(Skip = "Implemented by plan 05-01: scope dispatch helper — Families-only returns families mask")]
    public void DispatchScopeFlags_FamiliesOnly_ReturnsFamiliesMask()
    {
        Assert.True(false, "see plan 05-01");
    }

    [Fact(Skip = "Implemented by plan 05-01: scope dispatch helper — Materials-only returns materials mask")]
    public void DispatchScopeFlags_MaterialsOnly_ReturnsMaterialsMask()
    {
        Assert.True(false, "see plan 05-01");
    }

    [Fact(Skip = "Implemented by plan 05-01: scope dispatch helper — Parameters-only returns parameters mask")]
    public void DispatchScopeFlags_ParametersOnly_ReturnsParametersMask()
    {
        Assert.True(false, "see plan 05-01");
    }

    [Fact(Skip = "Implemented by plan 05-01: scope dispatch helper — multiple scopes returns combined mask")]
    public void DispatchScopeFlags_MultipleScopes_ReturnsCombinedMask()
    {
        Assert.True(false, "see plan 05-01");
    }

    [Fact(Skip = "Implemented by plan 05-01: Phase A/B param scan merge helper — deduplicates by family ID and param name")]
    public void MergeParamScanResults_DeduplicatesByFamilyIdAndParamName()
    {
        Assert.True(false, "see plan 05-01");
    }

    [Fact(Skip = "Implemented by plan 05-01: Phase A/B param scan merge helper — empty inputs returns empty")]
    public void MergeParamScanResults_EmptyInputs_ReturnsEmpty()
    {
        Assert.True(false, "see plan 05-01");
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
