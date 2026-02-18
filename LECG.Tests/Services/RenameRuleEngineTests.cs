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

    [Fact]
    public void ApplyReplace_WhenRegexIsInvalid_ReturnsOriginalText()
    {
        var options = new ReplaceRuleOptions(true, "[", "x", MatchCase: false, UseRegex: true);

        var result = RenameRuleEngine.ApplyReplace("Wall-01", options);

        result.Should().Be("Wall-01");
    }

    [Fact]
    public void ApplyNumbering_WhenInactive_ReturnsOriginalText()
    {
        var options = new NumberingRuleOptions(false, CoreNumberingMode.Suffix, StartAt: 1, Increment: 1, Separator: "-", Padding: 2);

        var result = RenameRuleEngine.ApplyNumbering("Item", options, index: 5);

        result.Should().Be("Item");
    }

    [Fact]
    public void ApplyCase_WhenTitle_ConvertsToTitleCase()
    {
        var options = new CaseRuleOptions(true, CoreCaseMode.Title);

        var result = RenameRuleEngine.ApplyCase("mIXed nAME", options);

        result.Should().Be("Mixed Name");
    }

    [Fact]
    public void ApplyCase_WhenCapitalize_UppercasesOnlyFirstCharacter()
    {
        var options = new CaseRuleOptions(true, CoreCaseMode.Capitalize);

        var result = RenameRuleEngine.ApplyCase("mixed NAME", options);

        result.Should().Be("Mixed NAME");
    }

    [Fact]
    public void ApplyRemove_WhenFirstNExceedsLength_ReturnsEmpty()
    {
        var options = new RemoveRuleOptions(true, FirstN: 10, LastN: 0, FromPos: 0, Count: 0);

        var result = RenameRuleEngine.ApplyRemove("ABC", options);

        result.Should().BeEmpty();
    }
}
