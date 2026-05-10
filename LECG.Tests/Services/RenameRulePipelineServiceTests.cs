// Wave 0 skip-gated RED scaffold — plan 05-01 flips these GREEN.
// Covers RenameRulePipelineService composition and guard behaviours (REQ-05).
using LECG.Services;
using Xunit;

namespace LECG.Tests.Services;

/// <summary>
/// Wave 1 RED anchor for RenameRulePipelineService coverage.
/// Behavioural tests are skip-gated naming plan 05-01.
/// Un-skipped by plan 05-01.
/// </summary>
[Trait("Category", "Renaming")]
public class RenameRulePipelineServiceTests
{
    // -----------------------------------------------------------------------
    // Anchor — keeps --filter discovery working even when all behavioural
    // tests are skip-gated
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "anchor — fixture exists; behavioural tests skip-gated by implementing plan")]
    public void Fixture_Anchor_Exists()
    {
        Assert.NotNull(typeof(RenameRulePipelineService));
    }

    // -----------------------------------------------------------------------
    // Wave 1 RED rows — plan 05-01 owns implementation
    // -----------------------------------------------------------------------

    [Fact(Skip = "Implemented by plan 05-01: rule composition order — Remove→Replace→Case→Add→Numbering")]
    public void ApplyRules_ComposesRulesInOrder_RemoveThenReplaceThenCaseThenAddThenNumbering()
    {
        Assert.True(false, "see plan 05-01");
    }

    [Fact(Skip = "Implemented by plan 05-01: index argument propagated to every rule")]
    public void ApplyRules_PassesIndexArgumentToEveryRule()
    {
        Assert.True(false, "see plan 05-01");
    }

    [Fact(Skip = "Implemented by plan 05-01: null RenameRuleContext throws ArgumentNullException")]
    public void ApplyRules_NullContext_Throws()
    {
        Assert.True(false, "see plan 05-01");
    }

    [Fact(Skip = "Implemented by plan 05-01: inactive rules pass text through unchanged")]
    public void ApplyRules_InactiveRules_PassesThroughUnchanged()
    {
        Assert.True(false, "see plan 05-01");
    }

    [Fact(Skip = "Implemented by plan 05-01: null rule list in constructor throws ArgumentNullException")]
    public void Constructor_NullRuleList_Throws()
    {
        Assert.True(false, "see plan 05-01");
    }
}
