using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Configuration;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgeMaterialService : IPurgeMaterialService
    {
        private readonly IPurgeDeleteElementService _purgeDeleteElementService;
        private readonly IPurgeMaterialUsageCollectorService _purgeMaterialUsageCollectorService;

        public PurgeMaterialService() : this(new PurgeDeleteElementService(), new PurgeMaterialUsageCollectorService(new PurgeReferenceScannerService()))
        {
        }

        public PurgeMaterialService(IPurgeDeleteElementService purgeDeleteElementService, IPurgeMaterialUsageCollectorService purgeMaterialUsageCollectorService)
        {
            _purgeDeleteElementService = purgeDeleteElementService;
            _purgeMaterialUsageCollectorService = purgeMaterialUsageCollectorService;
        }

        public int PurgeUnusedMaterials(Document doc, Action<string>? logCallback = null)
        {
            logCallback?.Invoke("Scanning for unused materials...");

            var allMaterials = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .Where(m => !RevitConstants.IsBuiltInMaterial(m.Name))
                .ToDictionary(m => m.Id, m => m.Name);

            var validMaterialIds = new HashSet<ElementId>(allMaterials.Keys);
            var usedIds = _purgeMaterialUsageCollectorService.CollectUsedMaterialIds(doc, validMaterialIds);

            int deleted = 0;
            foreach (var kvp in allMaterials)
            {
                if (!usedIds.Contains(kvp.Key))
                {
                    if (_purgeDeleteElementService.DeleteElement(doc, kvp.Key, kvp.Value, logCallback)) deleted++;
                }
            }

            logCallback?.Invoke($"  Deleted {deleted} materials.");
            return deleted;
        }
    }
}
