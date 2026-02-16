using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IRenderSolidFillPatternService
    {
        ElementId GetSolidFillPatternId(Document doc);
    }
}
