using FluentAssertions;
using LECG.Services;
using LECG.Tests.Helpers;
using LECG.ViewModels;
using Xunit;

namespace LECG.Tests.Services;

public class SexyRevitServiceTests
{
    [Fact]
    public void ApplyGraphicsAndLighting_WhenUseConsistentColorsTrue_SetsDisplayStyleToRealistic()
    {
        var facade = new FakeViewGraphicsFacade();
        var settings = new SexyRevitViewModel { UseConsistentColors = true, UseDetailFine = true };

        SexyRevitService.ApplyGraphicsAndLighting(facade, settings, _ => { }, (_, _) => { });

        facade.DisplayStyle.Should().Be(LECG.Services.Interfaces.ViewDisplayStyle.Realistic);
        facade.DetailLevel.Should().Be(LECG.Services.Interfaces.ViewDetailLevelFacade.Fine);
    }

    [Fact]
    public void ApplyGraphicsAndLighting_LogsExpectedHighLevelMessage()
    {
        var facade = new FakeViewGraphicsFacade();
        var settings = new SexyRevitViewModel { UseConsistentColors = true };
        var logs = new List<string>();

        SexyRevitService.ApplyGraphicsAndLighting(facade, settings, logs.Add, (_, _) => { });

        logs.Should().Contain(m => m.Contains("GRAPHICS & LIGHTING"));
    }
}
