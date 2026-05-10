// Wave 1 GREEN implementation — plan 05-01.
// Covers RenameRulePipelineService composition and guard behaviours (REQ-05).
//
// NOTE: RenameRulePipelineService reads rules from RenameRuleContext (not constructor args).
// Tests use concrete rule objects — no Revit API dependency (pure pipeline).
using FluentAssertions;
using LECG.Models;
using LECG.Services;
using Xunit;

namespace LECG.Tests.Services;

/// <summary>
/// Wave 1 GREEN tests for RenameRulePipelineService coverage (plan 05-01).
/// Covers rule composition order, index propagation, null-throw guards,
/// inactive-rules pass-through, and null-text guard.
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
    // Helper: create a context with all rules inactive (pass-through defaults)
    // -----------------------------------------------------------------------

    private static RenameRuleContext MakeContext(
        RemoveRule? remove = null,
        ReplaceRule? replace = null,
        CaseRule? caseRule = null,
        AddRule? add = null,
        NumberingRule? numbering = null)
    {
        return new RenameRuleContext(
            ReplaceRule: replace ?? new ReplaceRule { IsActive = false },
            RemoveRule: remove ?? new RemoveRule { IsActive = false },
            AddRule: add ?? new AddRule { IsActive = false },
            NumberingRule: numbering ?? new NumberingRule { IsActive = false },
            CaseRule: caseRule ?? new CaseRule { IsActive = false },
            ScopeTypeName: false,
            ScopeFamilyName: false,
            ScopeViewName: false,
            ScopeSheetName: false,
            ScopeMaterialName: false,
            ScopeObjectStyleName: false,
            ScopeLineStyleName: false,
            ScopeFillPatternName: false,
            ScopeFamilyParameterName: false,
            FilterName: "",
            FilterCategory: "",
            SelectedFilterType: SearchFilterType.Contains,
            FilterParamGroup: "",
            FilterIsInstance: null,
            FilterIsReadOnly: null,
            FilterViewType: "");
    }

    // -----------------------------------------------------------------------
    // Wave 1 GREEN tests
    // -----------------------------------------------------------------------

    [Fact]
    public void ApplyRules_ComposesRulesInOrder_RemoveThenReplaceThenCaseThenAddThenNumbering()
    {
        // Arrange: set up a chain where each rule transforms the text in an
        // observable, order-dependent way.
        //
        // Starting text: "hello world"
        // Remove first 6 chars -> "world"
        // Replace "world" -> "earth"
        // Case Upper          -> "EARTH"
        // Add suffix "-x"     -> "EARTH-x"
        // Numbering suffix "-1"-> "EARTH-x-1"   (StartAt=1, index=0, Increment=1 -> 1+0*1=1)

        var remove = new RemoveRule { IsActive = true, FirstN = 6 };
        var replace = new ReplaceRule { IsActive = true, FindText = "world", ReplaceText = "earth" };
        var caseRule = new CaseRule { IsActive = true, Mode = CaseMode.Upper };
        var add = new AddRule { IsActive = true, Suffix = "-x" };
        var numbering = new NumberingRule { IsActive = true, Mode = NumberingMode.Suffix, StartAt = 1, Increment = 1, Separator = "-", Padding = 1 };

        var svc = new RenameRulePipelineService();
        var ctx = MakeContext(remove, replace, caseRule, add, numbering);

        var result = svc.ApplyRules("hello world", ctx, 0);

        // If Remove applied first: "world"; then Replace: "earth"; then Upper: "EARTH";
        // then Add suffix: "EARTH-x"; then Numbering suffix "-1": "EARTH-x-1"
        result.Should().Be("EARTH-x-1");
    }

    [Fact]
    public void ApplyRules_PassesIndexArgumentToEveryRule()
    {
        // Arrange: only NumberingRule active — its output is index-dependent.
        // index=3, StartAt=1, Increment=1 -> number = 1 + 3*1 = 4 (suffix).
        // Without correct index propagation the number would be wrong.

        var numbering = new NumberingRule
        {
            IsActive = true,
            Mode = NumberingMode.Suffix,
            StartAt = 1,
            Increment = 1,
            Separator = "-",
            Padding = 1
        };

        var svc = new RenameRulePipelineService();
        var ctx = MakeContext(numbering: numbering);

        var result = svc.ApplyRules("item", ctx, index: 3);

        // Number = StartAt + index * Increment = 1 + 3 = 4
        result.Should().Be("item-4");
    }

    [Fact]
    public void ApplyRules_NullContext_Throws()
    {
        var svc = new RenameRulePipelineService();

        Action act = () => svc.ApplyRules("x", null!, 0);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyRules_InactiveRules_PassesThroughUnchanged()
    {
        // All rules use default (IsActive=false) — text must be returned verbatim.
        var svc = new RenameRulePipelineService();
        var ctx = MakeContext(); // all rules inactive

        var result = svc.ApplyRules("unchanged text", ctx, 0);

        result.Should().Be("unchanged text");
    }

    [Fact]
    public void ApplyRules_NullText_Throws()
    {
        // Production code guards: ArgumentNullException.ThrowIfNull(text)
        var svc = new RenameRulePipelineService();
        var ctx = MakeContext();

        Action act = () => svc.ApplyRules(null!, ctx, 0);

        act.Should().Throw<ArgumentNullException>();
    }
}
