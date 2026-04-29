using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadFamilySymbolService : ICadFamilySymbolService
    {
        public FamilySymbol? GetPrimarySymbol(Document doc, Family family)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(family);

            var symbolId = family.GetFamilySymbolIds().FirstOrDefault();
            if (symbolId == null) return null;

            var symbol = doc.GetElement(symbolId) as FamilySymbol;
            if (symbol != null && !symbol.IsActive)
            {
                symbol.Activate();
            }

            return symbol;
        }
    }
}
