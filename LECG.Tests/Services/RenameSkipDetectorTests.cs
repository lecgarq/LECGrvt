// Wave 0 skip-gated RED tests for Phase 4 — Plan 04-01 will flip these GREEN.
// Covers narrowed GetRenameSkipReason and new GetStandardItemSkipReason.
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace LECG.Tests.Services;

/// <summary>
/// Wave 0 RED fixture for Phase 4 renaming skip-detection (REQ-02/03/04).
/// All behavioural tests are skip-gated; plan 04-01 un-skips them.
/// </summary>
public class RenameSkipDetectorTests
{
    // -----------------------------------------------------------------------
    // Anchor — keeps --filter discovery working when all other tests are Skipped
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "anchor — delete when plan 04-01 fills this fixture")]
    [Trait("Category", "Renaming")]
    public void Fixture_Anchor_Exists() => Assert.True(true);

    // -----------------------------------------------------------------------
    // GetRenameSkipReason (narrowed in plan 04-01) — REQ-02/03/04
    // -----------------------------------------------------------------------

    [Fact(Skip = "Implement in plan 04-01")]
    [Trait("Category", "Renaming")]
    public void GetRenameSkipReason_FormulaReferenced_ReturnsNull_AfterPhase4Narrowing()
    {
        // RED — fills in plan 04-01
    }

    [Fact(Skip = "Implement in plan 04-01")]
    [Trait("Category", "Renaming")]
    public void GetRenameSkipReason_DimensionLabel_ReturnsNull_AfterPhase4Narrowing()
    {
        // RED — fills in plan 04-01
    }

    [Fact(Skip = "Implement in plan 04-01")]
    [Trait("Category", "Renaming")]
    public void GetRenameSkipReason_ElementAssociated_ReturnsNull_AfterPhase4Narrowing()
    {
        // RED — fills in plan 04-01
    }

    [Fact(Skip = "Implement in plan 04-01")]
    [Trait("Category", "Renaming")]
    public void GetRenameSkipReason_BuiltIn_StillReturnsReason_NegativeId()
    {
        // RED — fills in plan 04-01
    }

    [Fact(Skip = "Implement in plan 04-01")]
    [Trait("Category", "Renaming")]
    public void GetRenameSkipReason_Reporting_StillReturnsReason()
    {
        // RED — fills in plan 04-01
    }

    [Fact(Skip = "Implement in plan 04-01")]
    [Trait("Category", "Renaming")]
    public void GetRenameSkipReason_NameConflict_StillReturnsReason_NoAutoSuffix()
    {
        // RED — fills in plan 04-01
    }

    // -----------------------------------------------------------------------
    // GetStandardItemSkipReason (new in plan 04-01) — REQ-04
    // -----------------------------------------------------------------------

    [Fact(Skip = "Implement in plan 04-01")]
    [Trait("Category", "Renaming")]
    public void GetStandardItemSkipReason_ReadOnlyType_ReturnsReason()
    {
        // RED — fills in plan 04-01
    }

    [Fact(Skip = "Implement in plan 04-01")]
    [Trait("Category", "Renaming")]
    public void GetStandardItemSkipReason_NameConflictInSameScope_ReturnsReason()
    {
        // RED — fills in plan 04-01
    }

    [Fact(Skip = "Implement in plan 04-01")]
    [Trait("Category", "Renaming")]
    public void GetStandardItemSkipReason_SystemFamily_IsSystemFamilyTrue_ReturnsReason()
    {
        // RED — fills in plan 04-01
    }

    [Fact(Skip = "Implement in plan 04-01")]
    [Trait("Category", "Renaming")]
    public void GetStandardItemSkipReason_SheetNumberLocked_ReturnsReason()
    {
        // RED — fills in plan 04-01
    }

    [Fact(Skip = "Implement in plan 04-01")]
    [Trait("Category", "Renaming")]
    public void GetStandardItemSkipReason_RenameableSheet_ReturnsNull()
    {
        // RED — fills in plan 04-01
    }
}
