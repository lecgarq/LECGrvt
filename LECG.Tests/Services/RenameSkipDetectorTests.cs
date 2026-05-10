// Plan 04-01 GREEN. Tests for narrowed GetRenameSkipReason (via EvaluateFamilyParamSkipReason)
// and new GetStandardItemSkipReason (via EvaluateStandardItemSkipReason).
// Both methods are tested through pure-data helpers extracted from BatchRenameExecutionService
// so they are unit-testable without a live Revit session.
using System.Collections.Generic;
using FluentAssertions;
using LECG.Services;
using Xunit;

namespace LECG.Tests.Services;

/// <summary>
/// Plan 04-01 GREEN. Covers narrowed skip detection for FamilyParameter paths
/// and new standard-item skip detection (REQ-02/03/04).
/// </summary>
[Trait("Category", "Renaming")]
public class RenameSkipDetectorTests
{
    // -----------------------------------------------------------------------
    // Anchor
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "anchor — plan 04-01 filled this fixture")]
    public void Fixture_Anchor_Exists() => Assert.True(true);

    // -----------------------------------------------------------------------
    // EvaluateFamilyParamSkipReason — narrowed (formula/dim/element branches removed)
    // -----------------------------------------------------------------------

    [Fact]
    public void GetRenameSkipReason_FormulaReferenced_ReturnsNull_AfterPhase4Narrowing()
    {
        // After narrowing, formula-referenced names are NO LONGER a skip reason.
        var formulaReferenced = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "Width" };
        var dimensionLabels = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var elementAssociated = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var existingNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "Height" };

        string? reason = BatchRenameExecutionService.EvaluateFamilyParamSkipReason(
            paramIdValue: 100,
            isReporting: false,
            paramName: "Width",
            newName: "Width_New",
            existingParamNames: existingNames,
            formulaReferenced: formulaReferenced,
            dimensionLabels: dimensionLabels,
            elementAssociated: elementAssociated);

        reason.Should().BeNull("formula-referenced is no longer a skip condition after Phase 4 narrowing");
    }

    [Fact]
    public void GetRenameSkipReason_DimensionLabel_ReturnsNull_AfterPhase4Narrowing()
    {
        var dimensionLabels = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "Height" };
        var formulaReferenced = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var elementAssociated = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var existingNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "Width" };

        string? reason = BatchRenameExecutionService.EvaluateFamilyParamSkipReason(
            paramIdValue: 200,
            isReporting: false,
            paramName: "Height",
            newName: "Height_New",
            existingParamNames: existingNames,
            formulaReferenced: formulaReferenced,
            dimensionLabels: dimensionLabels,
            elementAssociated: elementAssociated);

        reason.Should().BeNull("dimension-label is no longer a skip condition after Phase 4 narrowing");
    }

    [Fact]
    public void GetRenameSkipReason_ElementAssociated_ReturnsNull_AfterPhase4Narrowing()
    {
        var elementAssociated = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "Depth" };
        var formulaReferenced = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var dimensionLabels = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var existingNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "Width" };

        string? reason = BatchRenameExecutionService.EvaluateFamilyParamSkipReason(
            paramIdValue: 300,
            isReporting: false,
            paramName: "Depth",
            newName: "Depth_New",
            existingParamNames: existingNames,
            formulaReferenced: formulaReferenced,
            dimensionLabels: dimensionLabels,
            elementAssociated: elementAssociated);

        reason.Should().BeNull("element-associated is no longer a skip condition after Phase 4 narrowing");
    }

    [Fact]
    public void GetRenameSkipReason_BuiltIn_StillReturnsReason_NegativeId()
    {
        var empty = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        string? reason = BatchRenameExecutionService.EvaluateFamilyParamSkipReason(
            paramIdValue: -1,
            isReporting: false,
            paramName: "SomeBuiltIn",
            newName: "NewName",
            existingParamNames: empty,
            formulaReferenced: empty,
            dimensionLabels: empty,
            elementAssociated: empty);

        reason.Should().NotBeNull("built-in parameters (negative Id) must still be skipped");
        reason.Should().Contain("built-in");
    }

    [Fact]
    public void GetRenameSkipReason_Reporting_StillReturnsReason()
    {
        var empty = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        string? reason = BatchRenameExecutionService.EvaluateFamilyParamSkipReason(
            paramIdValue: 400,
            isReporting: true,
            paramName: "ReportingParam",
            newName: "NewName",
            existingParamNames: empty,
            formulaReferenced: empty,
            dimensionLabels: empty,
            elementAssociated: empty);

        reason.Should().NotBeNull("reporting parameters must still be skipped");
        reason.Should().Contain("reporting");
    }

    [Fact]
    public void GetRenameSkipReason_NameConflict_StillReturnsReason_NoAutoSuffix()
    {
        var existingNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "ExistingParam" };
        var empty = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        string? reason = BatchRenameExecutionService.EvaluateFamilyParamSkipReason(
            paramIdValue: 500,
            isReporting: false,
            paramName: "OriginalParam",
            newName: "ExistingParam",
            existingParamNames: existingNames,
            formulaReferenced: empty,
            dimensionLabels: empty,
            elementAssociated: empty);

        reason.Should().NotBeNull("name conflicts must still be skipped with no auto-suffix");
        reason.Should().Contain("ExistingParam");
    }

    // -----------------------------------------------------------------------
    // EvaluateStandardItemSkipReason — new in plan 04-01 (REQ-04)
    // -----------------------------------------------------------------------

    [Fact]
    public void GetStandardItemSkipReason_ReadOnlyType_ReturnsReason()
    {
        var claimed = new HashSet<string>(System.StringComparer.Ordinal);

        string? reason = BatchRenameExecutionService.EvaluateStandardItemSkipReason(
            isReadOnly: true,
            isSystemFamily: false,
            nameAlreadyInScope: false,
            isSheetWithLockedNumber: false,
            newValue: "NewName",
            claimedNewNames: claimed);

        reason.Should().NotBeNull("read-only elements must be skipped");
        reason.Should().Contain("read-only");
    }

    [Fact]
    public void GetStandardItemSkipReason_NameConflictInSameScope_ReturnsReason()
    {
        var claimed = new HashSet<string>(System.StringComparer.Ordinal);

        string? reason = BatchRenameExecutionService.EvaluateStandardItemSkipReason(
            isReadOnly: false,
            isSystemFamily: false,
            nameAlreadyInScope: true,
            isSheetWithLockedNumber: false,
            newValue: "ExistingType",
            claimedNewNames: claimed);

        reason.Should().NotBeNull("name conflict in scope must be skipped");
        reason.Should().Contain("ExistingType");
    }

    [Fact]
    public void GetStandardItemSkipReason_SystemFamily_IsSystemFamilyTrue_ReturnsReason()
    {
        var claimed = new HashSet<string>(System.StringComparer.Ordinal);

        string? reason = BatchRenameExecutionService.EvaluateStandardItemSkipReason(
            isReadOnly: false,
            isSystemFamily: true,
            nameAlreadyInScope: false,
            isSheetWithLockedNumber: false,
            newValue: "NewName",
            claimedNewNames: claimed);

        reason.Should().NotBeNull("system family elements must be skipped");
        reason.Should().Contain("system family");
    }

    [Fact]
    public void GetStandardItemSkipReason_SheetNumberLocked_ReturnsReason()
    {
        var claimed = new HashSet<string>(System.StringComparer.Ordinal);

        string? reason = BatchRenameExecutionService.EvaluateStandardItemSkipReason(
            isReadOnly: false,
            isSystemFamily: false,
            nameAlreadyInScope: false,
            isSheetWithLockedNumber: true,
            newValue: "A-001",
            claimedNewNames: claimed);

        reason.Should().NotBeNull("sheets with locked numbering schemes must be skipped");
        reason.Should().Contain("sheet number");
    }

    [Fact]
    public void GetStandardItemSkipReason_RenameableSheet_ReturnsNull()
    {
        var claimed = new HashSet<string>(System.StringComparer.Ordinal);

        string? reason = BatchRenameExecutionService.EvaluateStandardItemSkipReason(
            isReadOnly: false,
            isSystemFamily: false,
            nameAlreadyInScope: false,
            isSheetWithLockedNumber: false,
            newValue: "A-002",
            claimedNewNames: claimed);

        reason.Should().BeNull("freely-renameable rows must return null");
    }
}
