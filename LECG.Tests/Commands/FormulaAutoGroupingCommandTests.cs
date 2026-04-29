using FluentAssertions;
using LECG.Core.Rename;
using Xunit;

namespace LECG.Tests.Commands;

/// <summary>
/// Unit tests for pure-logic paths in FormulaAutoGroupingCommand that do not require a live Revit instance.
///
/// Integration-level tests — transaction commit, ReplaceParameter, post-reload verification — require a live
/// Revit instance and are manual-only. They cannot be automated in this test suite.
/// </summary>
[Trait("Category", "FormulaGrouping")]
public class FormulaAutoGroupingCommandTests
{
    [Fact]
    public void ContainsReference_WhenOtherParamReferencesA_ReturnsTrue()
    {
        var result = FormulaNameUpdater.ContainsReference("A * 2", "A");

        result.Should().BeTrue();
    }

    [Fact]
    public void ContainsReference_WhenNoReferenceInFormula_ReturnsFalse()
    {
        var result = FormulaNameUpdater.ContainsReference("Width + Height", "A");

        result.Should().BeFalse();
    }

    [Fact]
    public void ContainsReference_WhenNameIsSubstring_ReturnsFalse()
    {
        var result = FormulaNameUpdater.ContainsReference("A1 + A_B", "A");

        result.Should().BeFalse();
    }

    [Fact]
    public void ContainsReference_WithBracketNotation_ReturnsTrue()
    {
        var result = FormulaNameUpdater.ContainsReference("Wall Width * 2", "Wall Width");

        result.Should().BeTrue();
    }
}
