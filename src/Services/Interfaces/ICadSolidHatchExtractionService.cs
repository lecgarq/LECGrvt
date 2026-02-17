using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ICadSolidHatchExtractionService
    {
        List<HatchData> Extract(Document doc, GeometryObject sourceObject, Solid solid, Transform currentTransform);
    }
}
