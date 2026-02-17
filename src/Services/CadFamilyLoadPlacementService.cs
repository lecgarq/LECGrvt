using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadFamilyLoadPlacementService : ICadFamilyLoadPlacementService
    {
        private readonly ICadPlacementViewService _placementViewService;
        private readonly ICadFamilyLoadResolveService _cadFamilyLoadResolveService;
        private readonly ICadTempFileCleanupService _cadTempFileCleanupService;

        public CadFamilyLoadPlacementService(
            ICadPlacementViewService placementViewService,
            ICadFamilyLoadResolveService cadFamilyLoadResolveService,
            ICadTempFileCleanupService cadTempFileCleanupService)
        {
            _placementViewService = placementViewService;
            _cadFamilyLoadResolveService = cadFamilyLoadResolveService;
            _cadTempFileCleanupService = cadTempFileCleanupService;
        }

        public ElementId LoadOnly(Document doc, string path)
        {
            ElementId createdId = ElementId.InvalidElementId;
            using (Transaction t = new Transaction(doc, "Load Family"))
            {
                t.Start();
                FamilySymbol? symbol = _cadFamilyLoadResolveService.LoadAndResolvePrimarySymbol(doc, path);
                if (symbol != null)
                {
                    createdId = symbol.Id;
                }
                t.Commit();
            }
            _cadTempFileCleanupService.Cleanup(path);
            return createdId;
        }

        public ElementId LoadAndPlace(Document doc, string path, XYZ location, ElementId deleteId)
        {
            ElementId createdId = ElementId.InvalidElementId;
            using (Transaction t = new Transaction(doc, "Load and Place Detail Item"))
            {
                t.Start();
                FamilySymbol? symbol = _cadFamilyLoadResolveService.LoadAndResolvePrimarySymbol(doc, path);
                if (symbol != null)
                {
                    createdId = symbol.Id;
                    View? placementView = _placementViewService.ResolvePlacementView(doc, doc.ActiveView);

                    if (placementView != null)
                    {
                        doc.Create.NewFamilyInstance(location, symbol, placementView);

                        if (deleteId != null && deleteId != ElementId.InvalidElementId)
                        {
                            Element e = doc.GetElement(deleteId);
                            if (e != null)
                            {
                                if (e.Pinned) e.Pinned = false;
                                doc.Delete(deleteId);
                            }
                        }
                    }
                    else
                    {
                        doc.Create.NewFamilyInstance(location, symbol, StructuralType.NonStructural);
                    }
                }
                t.Commit();
            }
            _cadTempFileCleanupService.Cleanup(path);
            return createdId;
        }

    }
}
