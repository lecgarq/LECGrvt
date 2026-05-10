// Wave 0 skip-gated RED tests for Phase 4 — Plans 04-03 and 04-04 will flip these GREEN.
// Covers safe-rename paths for formula-referenced (REQ-02), dimension-label (REQ-03),
// and standard-item pre-flight (REQ-04) in BatchRenameExecutionService.
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace LECG.Tests.Services;

/// <summary>
/// Wave 0 RED fixture for BatchRenameExecutionService safe-rename paths (REQ-02/03/04).
/// All behavioural tests are skip-gated; plans 04-03 and 04-04 un-skip them.
/// </summary>
public class BatchRenameSafeRenameTests
{
    // -----------------------------------------------------------------------
    // Anchor — keeps --filter discovery working when all other tests are Skipped
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "anchor — delete when plans 04-03/04-04 fill this fixture")]
    [Trait("Category", "Renaming")]
    public void Fixture_Anchor_Exists() => Assert.True(true);

    // -----------------------------------------------------------------------
    // REQ-02: Formula-referenced rename (plan 04-03)
    // -----------------------------------------------------------------------

    [Fact(Skip = "Implement in plan 04-03")]
    [Trait("Category", "Renaming")]
    public void RenameFamilyParameters_FormulaReferenced_UpdatesAllReferencingFormulas()
    {
        // RED — fills in plan 04-03
    }

    [Fact(Skip = "Implement in plan 04-03")]
    [Trait("Category", "Renaming")]
    public void RenameFamilyParameters_FormulaReferenced_LogsSuccessWithFormulaCount()
    {
        // RED — fills in plan 04-03
    }

    [Fact(Skip = "Implement in plan 04-03")]
    [Trait("Category", "Renaming")]
    public void RenameFamilyParameters_PerParamSubTransaction_RollsBackOneFailure_OtherParamsCommit()
    {
        // RED — fills in plan 04-03
    }

    [Fact(Skip = "Implement in plan 04-03")]
    [Trait("Category", "Renaming")]
    public void RenameFamilyParameters_CrossBatchNameCollision_DetectedInDryRun_FlippedToSkip()
    {
        // RED — fills in plan 04-03
        // Covers Pitfall 5 from RESEARCH: cross-batch name collisions
    }

    // -----------------------------------------------------------------------
    // REQ-03: Dimension-label rename (plan 04-04)
    // -----------------------------------------------------------------------

    [Fact(Skip = "Implement in plan 04-04")]
    [Trait("Category", "Renaming")]
    public void RenameFamilyParameters_DimensionLabel_ReassignsAllMatchingDimensions()
    {
        // RED — fills in plan 04-04
    }

    [Fact(Skip = "Implement in plan 04-04")]
    [Trait("Category", "Renaming")]
    public void RenameFamilyParameters_DimensionLabel_LogsSuccessWithDimensionCount()
    {
        // RED — fills in plan 04-04
    }

    [Fact(Skip = "Implement in plan 04-04")]
    [Trait("Category", "Renaming")]
    public void RenameFamilyParameters_DimensionLabel_StaleParamReference_ReFetchedByNewName()
    {
        // RED — fills in plan 04-04
        // Covers Pitfall 2 from RESEARCH: stale param reference after rename
    }

    // -----------------------------------------------------------------------
    // REQ-04: Standard-item pre-flight dry-run (plan 04-01)
    // -----------------------------------------------------------------------

    [Fact(Skip = "Implement in plan 04-01")]
    [Trait("Category", "Renaming")]
    public void RenameStandardItems_PreFlightDryRun_SetsStatusAndUnchecks_OnSkipCondition()
    {
        // RED — fills in plan 04-01
    }

    [Fact(Skip = "Implement in plan 04-01")]
    [Trait("Category", "Renaming")]
    public void RenameStandardItems_PreFlightDryRun_LogsOneWarningPerSkippedRow()
    {
        // RED — fills in plan 04-01
    }
}
