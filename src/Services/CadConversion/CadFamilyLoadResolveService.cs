using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadFamilyLoadResolveService : ICadFamilyLoadResolveService
    {
        private readonly IFamilyLoadOptionsFactory _familyLoadOptionsFactory;
        private readonly ICadFamilySymbolService _familySymbolService;

        public CadFamilyLoadResolveService(IFamilyLoadOptionsFactory familyLoadOptionsFactory, ICadFamilySymbolService familySymbolService)
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
