// Wave 0 skip-gated RED tests for Phase 4 — Plan 04-03 will flip these GREEN.
// Covers IFormulaUpdateService wiring (REQ-02).
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace LECG.Tests.Services;

/// <summary>
/// Wave 0 RED fixture for IFormulaUpdateService injection and delegation (REQ-02).
/// All behavioural tests are skip-gated; plan 04-03 un-skips them.
/// </summary>
public class FormulaUpdateServiceTests
{
    // -----------------------------------------------------------------------
    // Anchor — keeps --filter discovery working when all other tests are Skipped
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "anchor — delete when plan 04-03 fills this fixture")]
    [Trait("Category", "Renaming")]
    public void Fixture_Anchor_Exists() => Assert.True(true);

    // -----------------------------------------------------------------------
    // FormulaUpdateService delegation — REQ-02
    // -----------------------------------------------------------------------

    [Fact(Skip = "Implement in plan 04-03")]
    [Trait("Category", "Renaming")]
    public void UpdateFormula_DelegatesTo_FormulaNameUpdater_WithExactArgs()
    {
        // RED — fills in plan 04-03
    }

    [Fact(Skip = "Implement in plan 04-03")]
    [Trait("Category", "Renaming")]
    public void UpdateFormula_EmptyFormulaInput_ReturnsEmpty_WithoutThrowing()
    {
        // RED — fills in plan 04-03
    }

    [Fact(Skip = "Implement in plan 04-03")]
    [Trait("Category", "Renaming")]
    public void IFormulaUpdateService_IsInjectableIntoBatchRenameExecutionService_ViaConstructor()
    {
        // RED — fills in plan 04-03
    }
}
