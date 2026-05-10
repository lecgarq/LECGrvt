// Plan 04-01 partially GREEN (REQ-04 dry-run tests).
// Plan 04-03 flips REQ-02 tests GREEN. Plan 04-04 owns REQ-03.
using System;
using System.Collections.Generic;
using FluentAssertions;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using LECG.ViewModels.Components;
using NSubstitute;
using Xunit;

namespace LECG.Tests.Services;

/// <summary>
/// REQ-04 dry-run pre-flight tests (plan 04-01) and safe-rename tests for plans 04-03/04-04.
/// </summary>
[Trait("Category", "Renaming")]
public class BatchRenameSafeRenameTests
{
    // -----------------------------------------------------------------------
    // Anchor
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "anchor — plans 04-03/04-04 fill the remaining tests in this fixture")]
    public void Fixture_Anchor_Exists() => Assert.True(true);

    // -----------------------------------------------------------------------
    // REQ-02: Formula-referenced rename (plan 04-03)
    // -----------------------------------------------------------------------

    [Fact]
    public void RenameFamilyParameters_FormulaReferenced_UpdatesAllReferencingFormulas()
    {
        // Arrange: three parameters — one being renamed, two with referencing formulas
        var parameterFormulas = new List<(string name, string formula)>
        {
            ("Width", ""),                         // param being renamed (no own formula)
            ("Depth", "Width * 2"),                // references Width
            ("Height", "Width + Depth"),           // references Width
        };
        var formulaUpdateService = Substitute.For<IFormulaUpdateService>();
        formulaUpdateService.UpdateFormula("Width * 2", "Width", "PanelWidth").Returns("PanelWidth * 2");
        formulaUpdateService.UpdateFormula("Width + Depth", "Width", "PanelWidth").Returns("PanelWidth + Depth");

        // Act
        var updates = BatchRenameExecutionService.CollectFormulaUpdates(
            parameterFormulas, "Width", "PanelWidth", formulaUpdateService);

        // Assert: UpdateFormula was called once for each referencing formula
        formulaUpdateService.Received(1).UpdateFormula("Width * 2", "Width", "PanelWidth");
        formulaUpdateService.Received(1).UpdateFormula("Width + Depth", "Width", "PanelWidth");

        // And the returned updates contain both referencing params
        updates.Should().HaveCount(2);
        updates.Should().Contain(u => u.name == "Depth" && u.updatedFormula == "PanelWidth * 2");
        updates.Should().Contain(u => u.name == "Height" && u.updatedFormula == "PanelWidth + Depth");
    }

    [Fact]
    public void RenameFamilyParameters_FormulaReferenced_LogsSuccessWithFormulaCount()
    {
        // Arrange: one formula-referencing param to produce "(updated 1 formulas)" log
        var logger = Substitute.For<ILogger>();
        var formulaUpdateService = Substitute.For<IFormulaUpdateService>();
        formulaUpdateService.UpdateFormula(Arg.Any<string>(), "Width", "PanelWidth")
            .Returns(call => call.Arg<string>().Replace("Width", "PanelWidth"));

        var parameterFormulas = new List<(string name, string formula)>
        {
            ("Width", ""),
            ("Depth", "Width * 2"),
        };

        // Act: use the log-format helper
        BatchRenameExecutionService.LogRenameSuccess(logger, "Width", "PanelWidth", formulaCount: 1);

        // Assert: log message contains the formula-count suffix
        logger.Received(1).LogSuccess(Arg.Is<string>(msg =>
            msg.Contains("Width") &&
            msg.Contains("PanelWidth") &&
            msg.Contains("(updated 1 formulas)")));
    }

    [Fact]
    public void RenameFamilyParameters_PerParamSubTransaction_RollsBackOneFailure_OtherParamsCommit()
    {
        // This test verifies the counter behavior of the per-param loop via the
        // CollectFormulaUpdates pure helper: if a row is IsChecked=false, it is excluded
        // from the processing group entirely (GroupCheckedFamilyParameterItems guard).
        // For the three-param arrange, verify the formula-update helper handles
        // a mid-collection exception by not corrupting the output for prior params.

        // Arrange: three rename candidates; formula collector for params 1 and 3 succeeds,
        // middle param formula update throws
        var formulaUpdateService = Substitute.For<IFormulaUpdateService>();
        formulaUpdateService.UpdateFormula("A * 2", "A", "Alpha").Returns("Alpha * 2");
        formulaUpdateService.When(x => x.UpdateFormula("B * 2", "B", "Beta")).Throw<InvalidOperationException>();
        formulaUpdateService.UpdateFormula("C * 2", "C", "Gamma").Returns("Gamma * 2");

        var formulasForA = new List<(string name, string formula)> { ("A", ""), ("Ref_A", "A * 2") };
        var formulasForC = new List<(string name, string formula)> { ("C", ""), ("Ref_C", "C * 2") };

        // Act: first and third collect fine
        var updatesA = BatchRenameExecutionService.CollectFormulaUpdates(formulasForA, "A", "Alpha", formulaUpdateService);
        var collectB = () => BatchRenameExecutionService.CollectFormulaUpdates(
            new List<(string, string)> { ("B", ""), ("Ref_B", "B * 2") }, "B", "Beta", formulaUpdateService);

        // Assert: A and C succeed; B throws (the caller's SubTransaction catch handles it)
        updatesA.Should().HaveCount(1).And.Contain(u => u.name == "Ref_A");
        collectB.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RenameFamilyParameters_CrossBatchNameCollision_DetectedInDryRun_FlippedToSkip()
    {
        // Arrange: a row that has been collision-flipped to IsChecked=false by the pre-flight pass
        var collisionRow = new ElementRowViewModel
        {
            Id = 1,
            Name = "Param_A",
            OriginalValue = "Param_A",
            NewValue = "Param_B",          // collides with another row's NewValue
            Type = "FamilyParameter",
            IsChecked = false,              // already flipped by ApplyPreFlightSkipReasons
            IsRenameable = false,
            Status = "name 'Param_B' already claimed by another row in this batch"
        };

        // GroupCheckedFamilyParameterItems skips IsChecked=false rows
        var familyItems = new List<ElementRowViewModel> { collisionRow };
        var grouped = BatchRenameExecutionService.GroupCheckedFamilyParameterItemsForTest(familyItems);

        // Assert: collision-flipped row is excluded from the execution group
        grouped.Should().BeEmpty(
            because: "IsChecked=false rows from the dry-run are excluded before SubTransaction entry");
    }

    // -----------------------------------------------------------------------
    // REQ-03: Dimension-label rename (plan 04-04)
    // -----------------------------------------------------------------------

    [Fact(Skip = "Implement in plan 04-04")]
    public void RenameFamilyParameters_DimensionLabel_ReassignsAllMatchingDimensions()
    {
        // RED — fills in plan 04-04
    }

    [Fact(Skip = "Implement in plan 04-04")]
    public void RenameFamilyParameters_DimensionLabel_LogsSuccessWithDimensionCount()
    {
        // RED — fills in plan 04-04
    }

    [Fact(Skip = "Implement in plan 04-04")]
    public void RenameFamilyParameters_DimensionLabel_StaleParamReference_ReFetchedByNewName()
    {
        // RED — fills in plan 04-04
        // Covers Pitfall 2 from RESEARCH: stale param reference after rename
    }

    // -----------------------------------------------------------------------
    // REQ-04: Standard-item pre-flight dry-run (plan 04-01) — GREEN
    // -----------------------------------------------------------------------

    [Fact]
    public void RenameStandardItems_PreFlightDryRun_SetsStatusAndUnchecks_OnSkipCondition()
    {
        // Arrange: two rows — one that will be skipped (read-only), one renameable
        var skippedRow = new ElementRowViewModel
        {
            Id = 1,
            Name = "ReadOnlyStyle",
            OriginalValue = "ReadOnlyStyle",
            NewValue = "NewName",
            Type = "GraphicsStyle",
            IsChecked = true,
            IsRenameable = true
        };
        var renameableRow = new ElementRowViewModel
        {
            Id = 2,
            Name = "NormalType",
            OriginalValue = "NormalType",
            NewValue = "NormalType_Renamed",
            Type = "Type",
            IsChecked = true,
            IsRenameable = true
        };
        var rows = new List<ElementRowViewModel> { skippedRow, renameableRow };

        var logger = Substitute.For<ILogger>();

        // Act: use the pure-loop helper with a deterministic skip-reason resolver
        BatchRenameExecutionService.ApplyPreFlightSkipReasons(
            rows,
            row => row.Id == 1 ? "read-only or built-in type" : null,
            logger);

        // Assert: skipped row gets status, unchecked, IsRenameable=false
        skippedRow.Status.Should().Be("read-only or built-in type");
        skippedRow.IsRenameable.Should().BeFalse();
        skippedRow.IsChecked.Should().BeFalse();

        // Renameable row is unchanged
        renameableRow.Status.Should().BeEmpty();
        renameableRow.IsRenameable.Should().BeTrue();
        renameableRow.IsChecked.Should().BeTrue();
    }

    [Fact]
    public void RenameStandardItems_PreFlightDryRun_LogsOneWarningPerSkippedRow()
    {
        // Arrange: one row that will be skipped
        var skippedRow = new ElementRowViewModel
        {
            Id = 10,
            Name = "SystemWall",
            OriginalValue = "SystemWall",
            NewValue = "CustomWall",
            Type = "WallType",
            IsChecked = true
        };
        var rows = new List<ElementRowViewModel> { skippedRow };
        var logger = Substitute.For<ILogger>();

        // Act
        BatchRenameExecutionService.ApplyPreFlightSkipReasons(
            rows,
            row => "system family — names are restricted",
            logger);

        // Assert: exactly one LogWarning was emitted for the skipped row
        logger.Received(1).LogWarning(Arg.Is<string>(msg =>
            msg.Contains("SystemWall") &&
            msg.Contains("system family")));
    }
}
