using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ICadRenderContextService
    {
        (GraphicsStyle LineStyle, View PlanView, Transform ToOrigin) Create(
            Document familyDoc,
            XYZ offset,
            string styleName,
            Color color,
            int weight);
    }
}
