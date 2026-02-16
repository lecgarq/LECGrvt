using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ICadFamilyLoadPlacementService
    {
        ElementId LoadOnly(Document doc, string path);
        ElementId LoadAndPlace(Document doc, string path, XYZ location, ElementId deleteId);
    }
}
