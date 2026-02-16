using FluentAssertions;
using LECG.Core.Graphics;
using Xunit;

namespace LECG.Tests.Services;

public class SexyRevitServiceTests
{
    [Fact]
    public void Evaluate_WhenUseConsistentColorsTrue_SetsRealisticAndFine()
    {
        var settings = new SexyRevitGraphicsSettings(UseConsistentColors: true, UseDetailFine: true);

        var decision = SexyRevitGraphicsPolicy.Evaluate(settings);

        decision.ShouldApply.Should().BeTrue();
        decision.DisplayStyle.Should().Be(CoreDisplayStyle.Realistic);
        decision.DetailLevel.Should().Be(CoreDetailLevel.Fine);
    }

    [Fact]
    public void Evaluate_ContainsExpectedHighLevelMessage()
    {
        var settings = new SexyRevitGraphicsSettings(UseConsistentColors: true, UseDetailFine: false);

        var decision = SexyRevitGraphicsPolicy.Evaluate(settings);

        decision.Messages.Should().Contain(m => m.Contains("GRAPHICS & LIGHTING"));
    }
}
