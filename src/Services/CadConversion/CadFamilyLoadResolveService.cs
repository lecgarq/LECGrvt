using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadFamilyLoadResolveService
    {
        private readonly FamilyLoadOptionsFactory _familyLoadOptionsFactory;
        private readonly CadFamilySymbolService _familySymbolService;

        public CadFamilyLoadResolveService(FamilyLoadOptionsFactory familyLoadOptionsFactory, CadFamilySymbolService familySymbolService)
        {
            _familyLoadOptionsFactory = familyLoadOptionsFactory;
            _familySymbolService = familySymbolService;
        }

        public FamilySymbol? LoadAndResolvePrimarySymbol(Document doc, string path)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(path);

            Family? family;
            doc.LoadFamily(path, _familyLoadOptionsFactory.Create(), out family);
            if (family == null)
            {
                return null;
            }

            return _familySymbolService.GetPrimarySymbol(doc, family);
        }
    }
}
