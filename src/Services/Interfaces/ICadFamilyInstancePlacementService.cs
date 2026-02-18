using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ICadFamilyInstancePlacementService
    {
        void Place(Document doc, FamilySymbol symbol, XYZ location);
    }
}
