// Wave 0 RED scaffold for Phase 03 (REQ-01).
// VALIDATION row: 3-W0-02. Targets `LECG.Services.BaseElementCollectionService`
// null-Category fallback path (created in Plan 03-03).
//
// Strategy: BaseElementCollectionService.CollectBaseElements takes a Revit Document,
// so direct unit coverage requires a stub helper. Plan 03-03 will introduce a pure-
// string normalization helper `NormalizeForRow(string rawName, Category? cat, Type clrType, long id)`
// that delegates to ElementLabelService. The tests in this fixture target that future
// helper signature and are Skip-gated until Plan 03-03 lands.
//
// One non-skipped sanity test imports the type to anchor the file and prove the
// type reference still compiles after Plan 03-03's refactor.
using FluentAssertions;
using LECG.Services;

namespace LECG.Tests.Services;

/// <summary>
/// 3-W0-02 — RED scaffold. NormalizeForRow signature lands in Plan 03-03.
/// </summary>
public class BaseElementCollectionServiceTests
{
    private const string SkipReason = "Awaiting Plan 03-03 — NormalizeForRow helper";

    [Fact]
    [Trait("Category", "Unit")]
    public void Type_is_referenceable_anchor()
    {
        // Anchor: forces the test project to break if BaseElementCollectionService is renamed
        // or moved without updating Plan 03-03's references. Will be deleted/replaced in 03-03.
        typeof(BaseElementCollectionService).Should().NotBeNull();
        typeof(BaseElementCollectionService).Namespace.Should().Be("LECG.Services");
    }

    [Fact(Skip = SkipReason)]
    [Trait("Category", "Unit")]
    public void CollectBaseElements_no_longer_silently_skips_elements_with_null_Category()
    {
        // Plan 03-03 removes the `if (el.Category == null) continue;` skip and routes
        // null-Category elements through ElementLabelService for fallback labeling.
        // Test is Skip-gated because direct coverage requires a Revit Document stub
        // — covered indirectly via ElementLabelService unit tests.
        true.Should().BeFalse("placeholder until Plan 03-03 surface area is unit-testable");
    }

    [Fact(Skip = SkipReason)]
    [Trait("Category", "Unit")]
    public void NormalizeForRow_returns_non_blank_name_and_category_for_null_inputs()
    {
        // Target signature (Plan 03-03):
        //   internal static (string name, string category) NormalizeForRow(
        //       string rawName, Category? cat, Type clrType, long id);
        //
        // For null/whitespace inputs, both fields must be non-blank (delegates to
        // ElementLabelService.GetLabelsFromRaw).
        true.Should().BeFalse("placeholder until Plan 03-03 lands NormalizeForRow");
    }

    [Fact(Skip = SkipReason)]
    [Trait("Category", "Unit")]
    public void NormalizeForRow_emits_log_warning_when_category_was_null()
    {
        // Plan 03-03 contract: when raw category is null/blank, NormalizeForRow logs
        // a warning via Logger.Instance.LogWarning so silent skips become observable.
        true.Should().BeFalse("placeholder until Plan 03-03 lands logging");
    }
}
