namespace LECG.Services.Interfaces
{
    public enum ViewDisplayStyle
    {
        Wireframe,
        Realistic
    }

    public enum ViewDetailLevelFacade
    {
        Coarse,
        Fine
    }

    public interface IViewGraphicsFacade
    {
        ViewDisplayStyle DisplayStyle { get; set; }
        ViewDetailLevelFacade DetailLevel { get; set; }
    }
}
