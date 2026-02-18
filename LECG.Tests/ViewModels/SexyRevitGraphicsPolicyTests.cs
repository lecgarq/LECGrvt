using FluentAssertions;
using LECG.Core.Graphics;
using Xunit;

namespace LECG.Tests.ViewModels;

public class SexyRevitGraphicsPolicyTests
{
    [Fact]
    public void Evaluate_WhenConsistentColorsDisabled_ReturnsNoChanges()
    {
        var decision = SexyRevitGraphicsPolicy.Evaluate(new SexyRevitGraphicsSettings(false, false));

        decision.ShouldApply.Should().BeFalse();
        decision.DisplayStyle.Should().BeNull();
        decision.DetailLevel.Should().BeNull();
        decision.Messages.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_WhenConsistentColorsEnabled_ReturnsRealisticDisplay()
    {
        var decision = SexyRevitGraphicsPolicy.Evaluate(new SexyRevitGraphicsSettings(true, false));

        decision.ShouldApply.Should().BeTrue();
        decision.DisplayStyle.Should().Be(CoreDisplayStyle.Realistic);
        decision.Messages.Should().Contain(m => m.Contains("GRAPHICS & LIGHTING"));
    }

    [Fact]
    public void Evaluate_WhenUseDetailFineEnabled_ReturnsFineDetailLevel()
    {
        var decision = SexyRevitGraphicsPolicy.Evaluate(new SexyRevitGraphicsSettings(true, true));

        decision.DetailLevel.Should().Be(CoreDetailLevel.Fine);
        decision.Messages.Should().Contain(m => m.Contains("Detail Level: Fine"));
    }

    [Fact]
    public void Evaluate_WhenSettingsIsNull_ThrowsArgumentNullException()
    {
        Action act = () => SexyRevitGraphicsPolicy.Evaluate(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Evaluate_WhenConsistentColorsEnabledWithoutFineDetail_UsesDefaultMessageSet()
    {
        var decision = SexyRevitGraphicsPolicy.Evaluate(new SexyRevitGraphicsSettings(true, false));

        decision.DetailLevel.Should().BeNull();
        decision.Messages.Should().HaveCount(3);
        decision.Messages.Should().Contain(m => m.Contains("Display Style: Realistic"));
    }
}
