using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ICadLineStyleService
    {
        GraphicsStyle CreateOrUpdateDetailLineStyle(Document familyDoc, string styleName, Color color, int weight);
    }
}
