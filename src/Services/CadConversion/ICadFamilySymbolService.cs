using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ICadFamilySymbolService
    {
        FamilySymbol? GetPrimarySymbol(Document doc, Family family);
    }
}
