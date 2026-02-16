using LECG.Services.Interfaces;

namespace LECG.Tests.Helpers;

internal sealed class FakeViewGraphicsFacade : IViewGraphicsFacade
{
    public ViewDisplayStyle DisplayStyle { get; set; } = ViewDisplayStyle.Wireframe;
    public ViewDetailLevelFacade DetailLevel { get; set; } = ViewDetailLevelFacade.Coarse;
}
