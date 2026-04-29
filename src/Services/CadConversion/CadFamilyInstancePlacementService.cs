using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadFamilyInstancePlacementService : ICadFamilyInstancePlacementService
    {
        private readonly ICadPlacementViewService _placementViewService;

        public CadFamilyInstancePlacementService(ICadPlacementViewService placementViewService)
        {
            _placementViewService = placementViewService;
        }

        public void Place(Document doc, FamilySymbol symbol, XYZ location)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(symbol);
            ArgumentNullException.ThrowIfNull(location);

            View? placementView = _placementViewService.ResolvePlacementView(doc, doc.ActiveView);
            if (placementView != null)
            {
                doc.Create.NewFamilyInstance(location, symbol, placementView);
            }
            else
            {
                doc.Create.NewFamilyInstance(location, symbol, StructuralType.NonStructural);
            }
        }
    }
}
