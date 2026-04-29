using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ICadGeometryExtractionService
    {
        CadData ExtractGeometry(Document doc, ImportInstance imp);
    }
}
