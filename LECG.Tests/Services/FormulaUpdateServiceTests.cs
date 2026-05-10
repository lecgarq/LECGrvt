// Wave 0 skip-gated RED tests for Phase 4 — Plan 04-03 flipped these GREEN.
// Covers IFormulaUpdateService wiring (REQ-02).
using System.Linq;
using FluentAssertions;
using LECG.Core.Rename;
using LECG.Services;
using LECG.Services.Interfaces;
using NSubstitute;
using Xunit;

namespace LECG.Tests.Services;

/// <summary>
/// Fixture for IFormulaUpdateService injection and delegation (REQ-02).
/// Un-skipped by plan 04-03.
/// </summary>
public class FormulaUpdateServiceTests
{
    // -----------------------------------------------------------------------
    // Anchor — keeps --filter discovery working even without other tests
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "anchor — FormulaUpdateServiceTests fixture is active")]
    [Trait("Category", "Renaming")]
    public void Fixture_Anchor_Exists() => Assert.True(true);

    // -----------------------------------------------------------------------
    // FormulaUpdateService delegation — REQ-02
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Renaming")]
    public void UpdateFormula_DelegatesTo_FormulaNameUpdater_WithExactArgs()
    {
        // Arrange
        var sut = new FormulaUpdateService();

        // Assert behavioral equivalence with FormulaNameUpdater for 3 representative inputs
        // (same inputs exercised in FormulaNameUpdaterTests)
        sut.UpdateFormula("Width + Height", "Width", "PanelWidth")
            .Should().Be(FormulaNameUpdater.UpdateFormula("Width + Height", "Width", "PanelWidth"));

        sut.UpdateFormula("Width + Width / 2", "Width", "PanelWidth")
            .Should().Be(FormulaNameUpdater.UpdateFormula("Width + Width / 2", "Width", "PanelWidth"));

        sut.UpdateFormula("\"Width\" + Width", "Width", "PanelWidth")
            .Should().Be(FormulaNameUpdater.UpdateFormula("\"Width\" + Width", "Width", "PanelWidth"));
    }

    [Fact]
    [Trait("Category", "Renaming")]
    public void UpdateFormula_EmptyFormulaInput_ReturnsEmpty_WithoutThrowing()
    {
        var sut = new FormulaUpdateService();

        var act = () => sut.UpdateFormula(string.Empty, "Width", "PanelWidth");
        act.Should().NotThrow();
        act().Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Renaming")]
    public void IFormulaUpdateService_IsInjectableIntoBatchRenameExecutionService_ViaConstructor()
    {
        // Verify via reflection that BatchRenameExecutionService has a constructor
        // that accepts IFormulaUpdateService as a parameter. This avoids constructing
        // Revit-API-dependent mocks (ITransactionService/IFamilyLoadOptionsFactory) which
        // Castle DynamicProxy cannot proxy without RevitAPI.dll loaded in the test runner.
        var ctors = typeof(BatchRenameExecutionService).GetConstructors();
        bool hasFormulaUpdateServiceParam = ctors.Any(ctor =>
            ctor.GetParameters().Any(p => p.ParameterType == typeof(IFormulaUpdateService)));

        hasFormulaUpdateServiceParam.Should().BeTrue(
            because: "BatchRenameExecutionService must accept IFormulaUpdateService via constructor injection");
    }
}
