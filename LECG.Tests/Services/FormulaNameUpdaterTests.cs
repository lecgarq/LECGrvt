using FluentAssertions;
using LECG.Core.Rename;
using Xunit;

namespace LECG.Tests.Services;

[Trait("Category", "Renaming")]
public class FormulaNameUpdaterTests
{
    [Fact]
    public void UpdateFormula_WhenNameMatchesToken_ReplacesName()
    {
        var result = FormulaNameUpdater.UpdateFormula("Width + Height", "Width", "PanelWidth");

        result.Should().Be("PanelWidth + Height");
    }

    [Fact]
    public void UpdateFormula_WhenNameAppearsMultipleTimes_ReplacesAllTokenMatches()
    {
        var result = FormulaNameUpdater.UpdateFormula("Width + Width / 2", "Width", "PanelWidth");

        result.Should().Be("PanelWidth + PanelWidth / 2");
    }

    [Fact]
    public void UpdateFormula_WhenNameIsPartialToken_DoesNotReplace()
    {
        var result = FormulaNameUpdater.UpdateFormula("A + A1 + Length_A", "A", "B");

        result.Should().Be("B + A1 + Length_A");
    }

    [Fact]
    public void UpdateFormula_WhenCaseDiffers_DoesNotReplace()
    {
        var result = FormulaNameUpdater.UpdateFormula("width + Width", "Width", "PanelWidth");

        result.Should().Be("width + PanelWidth");
    }

    [Fact]
    public void UpdateFormula_WhenMatchIsQuoted_DoesNotReplaceQuotedText()
    {
        var result = FormulaNameUpdater.UpdateFormula("\"Width\" + Width", "Width", "PanelWidth");

        result.Should().Be("\"Width\" + PanelWidth");
    }

    [Fact]
    public void ContainsReference_WhenNameIsOnlyPartialToken_ReturnsFalse()
    {
        var result = FormulaNameUpdater.ContainsReference("A1 + Length_A", "A");

        result.Should().BeFalse();
    }
}
