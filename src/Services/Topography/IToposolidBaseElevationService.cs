using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace LECG.Services.Interfaces
{
    public interface IToposolidBaseElevationService
    {
        (double BaseElevation, string DebugMessage) Resolve(Document doc, Toposolid toposolid);
    }
}
