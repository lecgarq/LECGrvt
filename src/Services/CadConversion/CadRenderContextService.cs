using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadRenderContextService
    {
        private readonly CadLineStyleService _lineStyleService;
        private readonly CadDrawingViewService _drawingViewService;

        public CadRenderContextService(CadLineStyleService lineStyleService, CadDrawingViewService drawingViewService)
        {
            _lineStyleService = lineStyleService;
            _drawingViewService = drawingViewService;
        }

        public (GraphicsStyle LineStyle, View PlanView, Transform ToOrigin) Create(
            Document familyDoc,
            XYZ offset,
            string styleName,
            Color color,
            int weight)
        {
            GraphicsStyle lineStyle = _lineStyleService.CreateOrUpdateDetailLineStyle(familyDoc, styleName, color, weight);
            View planView = _drawingViewService.ResolveFamilyDrawingView(familyDoc);
            Transform toOrigin = Transform.CreateTranslation(-offset);
            return (lineStyle, planView, toOrigin);
        }
    }
}
