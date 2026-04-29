using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ICadFamilyLoadResolveService
    {
        FamilySymbol? LoadAndResolvePrimarySymbol(Document doc, string path);
    }
}
