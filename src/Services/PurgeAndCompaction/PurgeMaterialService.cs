using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Configuration;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgeMaterialService
    {
        private readonly PurgeDeleteElementService _purgeDeleteElementService;

        public PurgeMaterialService(PurgeDeleteElementService purgeDeleteElementService)
        {
            _purgeDeleteElementService = purgeDeleteElementService;
        }

        public int PurgeUnusedMaterials(Document doc, Action<string>? logCallback = null)
        {
            return PurgeUnusedMaterials(doc, PurgeContext.Create(doc), logCallback);
        }

        public int PurgeUnusedMaterials(Document doc, PurgeContext context, Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(context);

            logCallback?.Invoke("Scanning for unused materials...");

            var allMaterials = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .Where(m => !RevitConstants.IsBuiltInMaterial(m.Name))
                .ToDictionary(m => m.Id, m => m.Name);

            var validMaterialIds = new HashSet<ElementId>(allMaterials.Keys);
            var usedIds = CollectUsedMaterialIds(context, validMaterialIds);

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

        private static HashSet<ElementId> CollectUsedMaterialIds(
            PurgeContext context,
            HashSet<ElementId> validMaterialIds)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(validMaterialIds);

            var usedIds = new HashSet<ElementId>();

            foreach (ElementId usedMaterialId in context.UsedMaterialIds)
            {
                if (validMaterialIds.Contains(usedMaterialId))
                {
                    usedIds.Add(usedMaterialId);
                }
            }

            foreach (ElementId referencedId in context.ParameterReferencedIds)
            {
                if (validMaterialIds.Contains(referencedId))
                {
                    usedIds.Add(referencedId);
                }
            }

            return usedIds;
        }
    }
}
