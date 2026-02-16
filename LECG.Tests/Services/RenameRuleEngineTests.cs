using FluentAssertions;
using LECG.Core.Rename;
using Xunit;

namespace LECG.Tests.Services;

public class RenameRuleEngineTests
{
    [Fact]
    public void ApplyReplace_WhenCaseInsensitive_ReplacesMatches()
    {
        var options = new ReplaceRuleOptions(true, "wall", "beam", MatchCase: false, UseRegex: false);

        var result = RenameRuleEngine.ApplyReplace("Wall Type", options);

        result.Should().Be("beam Type");
    }

    [Fact]
    public void ApplyRemove_WhenConfigured_RemovesFirstLastAndRange()
    {
        var options = new RemoveRuleOptions(true, FirstN: 1, LastN: 1, FromPos: 1, Count: 2);

        var result = RenameRuleEngine.ApplyRemove("ABCDEFG", options);

        result.Should().Be("BEF");
    }

    [Fact]
    public void ApplyAdd_WhenConfigured_AppliesInsertPrefixAndSuffix()
    {
        var options = new AddRuleOptions(true, Prefix: "P_", Suffix: "_S", Insert: "-", AtPos: 2);

        var result = RenameRuleEngine.ApplyAdd("NAME", options);

        result.Should().Be("P_NA-ME_S");
    }

    [Fact]
    public void ApplyCase_WhenUpper_UppercasesText()
    {
        var options = new CaseRuleOptions(true, CoreCaseMode.Upper);

        var result = RenameRuleEngine.ApplyCase("Mixed Name", options);

        result.Should().Be("MIXED NAME");
    }

    [Fact]
    public void ApplyNumbering_WhenPrefix_AddsNumberWithPadding()
    {
        var options = new NumberingRuleOptions(true, CoreNumberingMode.Prefix, StartAt: 1, Increment: 1, Separator: "-", Padding: 3);

        var result = RenameRuleEngine.ApplyNumbering("Item", options, index: 2);

        result.Should().Be("003-Item");
    }
}
