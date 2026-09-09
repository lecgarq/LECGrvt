using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadFamilyInstancePlacementService
    {
        private readonly CadPlacementViewService _placementViewService;

        public CadFamilyInstancePlacementService(CadPlacementViewService placementViewService)
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
