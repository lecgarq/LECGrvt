// Wave 0 skip-gated RED scaffold — plans 05-02 and 05-03 flip these GREEN.
// Covers direct BatchRenameExecutionService pure-data helpers and the 3 polish fixes (REQ-05).
using System;
using System.Collections.Generic;
using FluentAssertions;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.ViewModels.Components;
using NSubstitute;
using Xunit;

namespace LECG.Tests.Services;

/// <summary>
/// Wave 2 + Wave 3 RED anchor for direct BatchRenameExecutionService coverage
/// and the 3 polish fixes.
/// Behavioural tests are skip-gated naming plan 05-02 (direct coverage) or 05-03 (polish).
/// Un-skipped by plans 05-02 and 05-03.
/// </summary>
[Trait("Category", "Renaming")]
public class BatchRenameExecutionServiceTests
{
    // -----------------------------------------------------------------------
    // Anchor — keeps --filter discovery working even when all behavioural
    // tests are skip-gated
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "anchor — fixture exists; behavioural tests skip-gated by implementing plan")]
    public void Fixture_Anchor_Exists()
    {
        Assert.NotNull(typeof(BatchRenameExecutionService));
    }

    // -----------------------------------------------------------------------
    // Wave 2 GREEN rows — plan 05-02 direct coverage
    // -----------------------------------------------------------------------

    // --- EvaluateFamilyParamSkipReason: 4 branches ---

    [Fact]
    public void EvaluateFamilyParamSkipReason_BuiltInParam_ReturnsBuiltInReason()
    {
        // paramIdValue < 0 → built-in branch
        var result = BatchRenameExecutionService.EvaluateFamilyParamSkipReason(
            paramIdValue: -1,
            isReporting: false,
            paramName: "Width",
            newName: "PanelWidth",
            existingParamNames: Array.Empty<string>(),
            formulaReferenced: new HashSet<string>(),
            dimensionLabels: new HashSet<string>(),
            elementAssociated: new HashSet<string>());

        result.Should().NotBeNull();
        result.Should().Contain("built-in");
    }

    [Fact]
    public void EvaluateFamilyParamSkipReason_ReportingParam_ReturnsReportingReason()
    {
        // isReporting = true → reporting branch
        var result = BatchRenameExecutionService.EvaluateFamilyParamSkipReason(
            paramIdValue: 100,
            isReporting: true,
            paramName: "Height",
            newName: "PanelHeight",
            existingParamNames: Array.Empty<string>(),
            formulaReferenced: new HashSet<string>(),
            dimensionLabels: new HashSet<string>(),
            elementAssociated: new HashSet<string>());

        result.Should().NotBeNull();
        result.Should().Contain("reporting");
    }

    [Fact]
    public void EvaluateFamilyParamSkipReason_NameConflict_ReturnsConflictReason()
    {
        // newName collides with an existing param name (case-insensitive)
        var result = BatchRenameExecutionService.EvaluateFamilyParamSkipReason(
            paramIdValue: 200,
            isReporting: false,
            paramName: "Depth",
            newName: "Width",
            existingParamNames: new[] { "Width", "Height" },
            formulaReferenced: new HashSet<string>(),
            dimensionLabels: new HashSet<string>(),
            elementAssociated: new HashSet<string>());

        result.Should().NotBeNull();
        result.Should().Contain("Width");
    }

    [Fact]
    public void EvaluateFamilyParamSkipReason_SafeToRename_ReturnsNull()
    {
        // positive id, not reporting, no name conflict → safe
        var result = BatchRenameExecutionService.EvaluateFamilyParamSkipReason(
            paramIdValue: 300,
            isReporting: false,
            paramName: "Thickness",
            newName: "WallThickness",
            existingParamNames: new[] { "Width", "Height" },
            formulaReferenced: new HashSet<string>(),
            dimensionLabels: new HashSet<string>(),
            elementAssociated: new HashSet<string>());

        result.Should().BeNull();
    }

    // --- EvaluateStandardItemSkipReason: 6 branches ---

    [Fact]
    public void EvaluateStandardItemSkipReason_CrossBatchCollision_ReturnsCollisionReason()
    {
        var claimed = new HashSet<string>(StringComparer.Ordinal) { "NewName" };

        var result = BatchRenameExecutionService.EvaluateStandardItemSkipReason(
            isReadOnly: false,
            isSystemFamily: false,
            nameAlreadyInScope: false,
            isSheetWithLockedNumber: false,
            newValue: "NewName",
            claimedNewNames: claimed);

        result.Should().NotBeNull();
        result.Should().Contain("NewName");
        result.Should().Contain("claimed");
    }

    [Fact]
    public void EvaluateStandardItemSkipReason_SystemFamily_ReturnsSystemFamilyReason()
    {
        var result = BatchRenameExecutionService.EvaluateStandardItemSkipReason(
            isReadOnly: false,
            isSystemFamily: true,
            nameAlreadyInScope: false,
            isSheetWithLockedNumber: false,
            newValue: "NewType",
            claimedNewNames: new HashSet<string>());

        result.Should().NotBeNull();
        result.Should().Contain("system family");
    }

    [Fact]
    public void EvaluateStandardItemSkipReason_NameInScope_ReturnsCollisionReason()
    {
        var result = BatchRenameExecutionService.EvaluateStandardItemSkipReason(
            isReadOnly: false,
            isSystemFamily: false,
            nameAlreadyInScope: true,
            isSheetWithLockedNumber: false,
            newValue: "ExistingName",
            claimedNewNames: new HashSet<string>());

        result.Should().NotBeNull();
        result.Should().Contain("ExistingName");
        result.Should().Contain("already in use");
    }

    [Fact]
    public void EvaluateStandardItemSkipReason_SheetLocked_ReturnsLockedReason()
    {
        var result = BatchRenameExecutionService.EvaluateStandardItemSkipReason(
            isReadOnly: false,
            isSystemFamily: false,
            nameAlreadyInScope: false,
            isSheetWithLockedNumber: true,
            newValue: "A101",
            claimedNewNames: new HashSet<string>());

        result.Should().NotBeNull();
        result.Should().Contain("locked");
    }

    [Fact]
    public void EvaluateStandardItemSkipReason_ReadOnly_ReturnsReadOnlyReason()
    {
        var result = BatchRenameExecutionService.EvaluateStandardItemSkipReason(
            isReadOnly: true,
            isSystemFamily: false,
            nameAlreadyInScope: false,
            isSheetWithLockedNumber: false,
            newValue: "NewStyle",
            claimedNewNames: new HashSet<string>());

        result.Should().NotBeNull();
        result.Should().Contain("read-only");
    }

    [Fact]
    public void EvaluateStandardItemSkipReason_FreelyRenameable_ReturnsNull()
    {
        var result = BatchRenameExecutionService.EvaluateStandardItemSkipReason(
            isReadOnly: false,
            isSystemFamily: false,
            nameAlreadyInScope: false,
            isSheetWithLockedNumber: false,
            newValue: "FreeType",
            claimedNewNames: new HashSet<string>());

        result.Should().BeNull();
    }

    // --- FormatSafeRenameLog: 4 branches ---

    [Fact]
    public void FormatSafeRenameLog_BothCountsPositive_RendersCompositeMessage()
    {
        var msg = BatchRenameExecutionService.FormatSafeRenameLog("old", "new", formulaCount: 2, dimCount: 3);

        msg.Should().Contain("old");
        msg.Should().Contain("new");
        msg.Should().Contain("2");
        msg.Should().Contain("3");
        msg.Should().Contain("formulas");
        msg.Should().Contain("dimension labels");
    }

    [Fact]
    public void FormatSafeRenameLog_FormulasOnly_RendersFormulaSuffix()
    {
        var msg = BatchRenameExecutionService.FormatSafeRenameLog("Width", "PanelWidth", formulaCount: 5, dimCount: 0);

        msg.Should().Contain("Width");
        msg.Should().Contain("PanelWidth");
        msg.Should().Contain("5");
        msg.Should().Contain("formulas");
        msg.Should().NotContain("dimension labels");
    }

    [Fact]
    public void FormatSafeRenameLog_DimsOnly_RendersDimSuffix()
    {
        var msg = BatchRenameExecutionService.FormatSafeRenameLog("Depth", "WallDepth", formulaCount: 0, dimCount: 4);

        msg.Should().Contain("Depth");
        msg.Should().Contain("WallDepth");
        msg.Should().Contain("4");
        msg.Should().Contain("dimension labels");
        msg.Should().NotContain("formulas");
    }

    [Fact]
    public void FormatSafeRenameLog_NeitherSideEffect_RendersBaseMessage()
    {
        var msg = BatchRenameExecutionService.FormatSafeRenameLog("Alpha", "Beta", formulaCount: 0, dimCount: 0);

        msg.Should().Contain("Alpha");
        msg.Should().Contain("Beta");
        msg.Should().NotContain("formulas");
        msg.Should().NotContain("dimension labels");
    }

    // --- GroupCheckedFamilyParameterItemsForTest: 2 tests ---

    [Fact]
    public void GroupCheckedFamilyParameterItems_FiltersUnchecked_PreservesOrder()
    {
        // Two checked items for the same family (Id=10), one unchecked for family Id=20
        var checked1 = new ElementRowViewModel { Id = 10, Name = "Param1", OriginalValue = "Param1", NewValue = "ParamA", Type = "FamilyParameter", IsChecked = true };
        var checked2 = new ElementRowViewModel { Id = 10, Name = "Param2", OriginalValue = "Param2", NewValue = "ParamB", Type = "FamilyParameter", IsChecked = true };
        var uncheckedRow = new ElementRowViewModel { Id = 20, Name = "Param3", OriginalValue = "Param3", NewValue = "ParamC", Type = "FamilyParameter", IsChecked = false };

        var items = new List<ElementRowViewModel> { checked1, checked2, uncheckedRow };

        var result = BatchRenameExecutionService.GroupCheckedFamilyParameterItemsForTest(items);

        result.Should().HaveCount(1, because: "only family Id=10 has checked items");
        result[10].Should().HaveCount(2, because: "two checked items belong to family Id=10");
        result[10][0].Should().BeSameAs(checked1, because: "order preserved");
        result[10][1].Should().BeSameAs(checked2, because: "order preserved");
        result.Should().NotContainKey(20, because: "unchecked row is excluded");
    }

    [Fact]
    public void GroupCheckedFamilyParameterItems_EmptyInput_ReturnsEmpty()
    {
        var result = BatchRenameExecutionService.GroupCheckedFamilyParameterItemsForTest(new List<ElementRowViewModel>());

        result.Should().BeEmpty();
    }

    // --- LegacyProgressReporter: all 5 methods no-throw when callback is null ---

    [Fact]
    public void LegacyProgressReporter_NullCallback_AllFiveMethods_NoThrow()
    {
        var reporter = new LegacyProgressReporter(progressCallback: null, logCallback: null);

        Action report    = () => reporter.Report("msg", 50);
        Action log       = () => reporter.Log("info");
        Action logWarn   = () => reporter.LogWarning("warning");
        Action logError  = () => reporter.LogError("error");

        // LegacyProgressReporter only has 4 methods (Report, Log, LogWarning, LogError)
        report.Should().NotThrow();
        log.Should().NotThrow();
        logWarn.Should().NotThrow();
        logError.Should().NotThrow();
    }

    // --- Constructor null-guard ---

    [Fact]
    public void Constructor_NullFormulaUpdateService_Throws()
    {
        // ITransactionService and IFamilyLoadOptionsFactory use Revit API types — Castle DynamicProxy
        // cannot proxy them without RevitAPI.dll in the test runner (Phase 04-03 decision).
        // Use reflection to invoke the constructor with null for formulaUpdateService and verify
        // the resulting TargetInvocationException wraps ArgumentNullException.
        var ctor = typeof(BatchRenameExecutionService).GetConstructors()[0];

        // Pass null for all Revit-API-bound args; only the null-guard on formulaUpdateService fires
        // because it is the last parameter and the guard executes after the other assignments.
        Action act = () =>
        {
            try
            {
                ctor.Invoke(new object?[] { null, null, null });
            }
            catch (System.Reflection.TargetInvocationException tie)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(tie.InnerException!).Throw();
            }
        };

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("formulaUpdateService");
    }

    // -----------------------------------------------------------------------
    // Wave 3 RED rows — plan 05-03 owns implementation (3 polish fixes)
    // -----------------------------------------------------------------------

    [Fact]
    public void AccumulateCommittedFamilyCount_Committed_AddsRenamedToCurrentCount()
    {
        var result = BatchRenameExecutionService.AccumulateCommittedFamilyCount(
            committed: true, renamedInFamily: 5, currentCount: 10);
        result.Should().Be(15);
    }

    [Fact]
    public void AccumulateCommittedFamilyCount_NotCommitted_ReturnsCurrentCountUnchanged()
    {
        var result = BatchRenameExecutionService.AccumulateCommittedFamilyCount(
            committed: false, renamedInFamily: 5, currentCount: 10);
        result.Should().Be(10);
    }

    [Fact]
    public void ExecuteDimensionReassignments_PreClearsLabel_BeforeReassigning()
    {
        var log = new List<string>();
        var pairs = new (Action clear, Action assign)[]
        {
            ((Action)(() => log.Add("clear-A")), (Action)(() => log.Add("assign-A"))),
            ((Action)(() => log.Add("clear-B")), (Action)(() => log.Add("assign-B"))),
        };

        BatchRenameExecutionService.ExecuteDimensionReassignments(pairs, "old", "new", out int dimCount);

        dimCount.Should().Be(2);
        log.Should().Equal("clear-A", "assign-A", "clear-B", "assign-B");
    }

    [Fact]
    public void BatchRenameProgress_MixedBatch_MaxBeforeDoneEquals100()
    {
        var seq = BatchRenameExecutionService.BuildProgressSequence(
            standardCount: 5,
            familyGroupRowCounts: new[] { 3, 2 });

        seq.Max().Should().Be(100.0);
    }

    [Fact]
    public void BuildProgressSequence_IsMonotonic()
    {
        var seq = BatchRenameExecutionService.BuildProgressSequence(
            standardCount: 5,
            familyGroupRowCounts: new[] { 3, 2 });

        for (int i = 1; i < seq.Count; i++)
            seq[i].Should().BeGreaterThanOrEqualTo(seq[i - 1]);
    }
}
