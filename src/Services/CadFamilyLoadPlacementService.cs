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
        private readonly ITransactionService _transactionService;

        public CadFamilyLoadPlacementService(
            ICadFamilyLoadResolveService cadFamilyLoadResolveService,
            ICadTempFileCleanupService cadTempFileCleanupService,
            ICadSourceCleanupService cadSourceCleanupService,
            ICadFamilyInstancePlacementService cadFamilyInstancePlacementService,
            ITransactionService transactionService)
        {
            _cadFamilyLoadResolveService = cadFamilyLoadResolveService;
            _cadTempFileCleanupService = cadTempFileCleanupService;
            _cadSourceCleanupService = cadSourceCleanupService;
            _cadFamilyInstancePlacementService = cadFamilyInstancePlacementService;
            _transactionService = transactionService;
        }

        public ElementId LoadOnly(Document doc, string path)
        {
            ElementId createdId = _transactionService.Run(doc, "Load Family", _ =>
            {
                FamilySymbol? symbol = _cadFamilyLoadResolveService.LoadAndResolvePrimarySymbol(doc, path);
                return symbol?.Id ?? ElementId.InvalidElementId;
            });
            _cadTempFileCleanupService.Cleanup(path);
            return createdId;
        }

        public ElementId LoadAndPlace(Document doc, string path, XYZ location, ElementId deleteId)
        {
            ElementId createdId = _transactionService.Run(doc, "Load and Place Detail Item", _ =>
            {
                FamilySymbol? symbol = _cadFamilyLoadResolveService.LoadAndResolvePrimarySymbol(doc, path);
                if (symbol != null)
                {
                    ElementId symbolId = symbol.Id;
                    _cadFamilyInstancePlacementService.Place(doc, symbol, location);
                    _cadSourceCleanupService.DeleteOriginalIfPresent(doc, deleteId);
                    return symbolId;
                }

                return ElementId.InvalidElementId;
            });
            _cadTempFileCleanupService.Cleanup(path);
            return createdId;
        }

    }
}
