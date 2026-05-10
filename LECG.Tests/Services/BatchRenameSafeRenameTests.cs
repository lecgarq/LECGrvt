// Plan 04-01 partially GREEN (REQ-04 dry-run tests).
// Plans 04-03 and 04-04 will flip the remaining REQ-02/REQ-03 tests GREEN.
using System.Collections.Generic;
using FluentAssertions;
using LECG.Services;
using LECG.Services.Logging;
using LECG.ViewModels.Components;
using NSubstitute;
using Xunit;

namespace LECG.Tests.Services;

/// <summary>
/// REQ-04 dry-run pre-flight tests (plan 04-01) and safe-rename scaffold for plans 04-03/04-04.
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

    [Fact(Skip = "Implement in plan 04-03")]
    public void RenameFamilyParameters_FormulaReferenced_UpdatesAllReferencingFormulas()
    {
        // RED — fills in plan 04-03
    }

    [Fact(Skip = "Implement in plan 04-03")]
    public void RenameFamilyParameters_FormulaReferenced_LogsSuccessWithFormulaCount()
    {
        // RED — fills in plan 04-03
    }

    [Fact(Skip = "Implement in plan 04-03")]
    public void RenameFamilyParameters_PerParamSubTransaction_RollsBackOneFailure_OtherParamsCommit()
    {
        // RED — fills in plan 04-03
    }

    [Fact(Skip = "Implement in plan 04-03")]
    public void RenameFamilyParameters_CrossBatchNameCollision_DetectedInDryRun_FlippedToSkip()
    {
        // RED — fills in plan 04-03
        // Covers Pitfall 5 from RESEARCH: cross-batch name collisions
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
