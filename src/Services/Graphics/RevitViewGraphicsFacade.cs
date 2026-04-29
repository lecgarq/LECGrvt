using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public sealed class RevitViewGraphicsFacade : IViewGraphicsFacade
    {
        private readonly View _view;

        public RevitViewGraphicsFacade(View view)
        {
            _view = view;
        }

        public ViewDisplayStyle DisplayStyle
        {
            get => _view.DisplayStyle == Autodesk.Revit.DB.DisplayStyle.Realistic
                ? LECG.Services.Interfaces.ViewDisplayStyle.Realistic
                : LECG.Services.Interfaces.ViewDisplayStyle.Wireframe;
            set => _view.DisplayStyle = value == LECG.Services.Interfaces.ViewDisplayStyle.Realistic
                ? Autodesk.Revit.DB.DisplayStyle.Realistic
                : Autodesk.Revit.DB.DisplayStyle.Wireframe;
        }

        public ViewDetailLevelFacade DetailLevel
        {
            get => _view.DetailLevel == ViewDetailLevel.Fine ? ViewDetailLevelFacade.Fine : ViewDetailLevelFacade.Coarse;
            set => _view.DetailLevel = value == ViewDetailLevelFacade.Fine ? ViewDetailLevel.Fine : ViewDetailLevel.Coarse;
        }
    }
}
