using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadFamilyLoadPlacementService : ICadFamilyLoadPlacementService
    {
        private readonly ICadFamilyLoadResolveService _cadFamilyLoadResolveService;
        private readonly ICadTempFileCleanupService _cadTempFileCleanupService;
        private readonly ICadSourceCleanupService _cadSourceCleanupService;
        private readonly ICadFamilyInstancePlacementService _cadFamilyInstancePlacementService;

        public CadFamilyLoadPlacementService(
            ICadFamilyLoadResolveService cadFamilyLoadResolveService,
            ICadTempFileCleanupService cadTempFileCleanupService,
            ICadSourceCleanupService cadSourceCleanupService,
            ICadFamilyInstancePlacementService cadFamilyInstancePlacementService)
        {
            _cadFamilyLoadResolveService = cadFamilyLoadResolveService;
            _cadTempFileCleanupService = cadTempFileCleanupService;
            _cadSourceCleanupService = cadSourceCleanupService;
            _cadFamilyInstancePlacementService = cadFamilyInstancePlacementService;
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
                    _cadFamilyInstancePlacementService.Place(doc, symbol, location);
                    _cadSourceCleanupService.DeleteOriginalIfPresent(doc, deleteId);
                }
                t.Commit();
            }
            _cadTempFileCleanupService.Cleanup(path);
            return createdId;
        }

    }
}
