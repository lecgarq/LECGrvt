using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgeLevelService : IPurgeLevelService
    {
        private readonly IPurgeReferenceScannerService _referenceScanner;

        public PurgeLevelService() : this(new PurgeReferenceScannerService())
        {
        }

        public PurgeLevelService(IPurgeReferenceScannerService referenceScanner)
        {
            _referenceScanner = referenceScanner;
        }

        public int PurgeUnusedLevels(Document doc, Action<string>? logCallback = null)
        {
            logCallback?.Invoke("Scanning for unused levels...");

            var allLevels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToList();

            if (allLevels.Count <= 1)
            {
                logCallback?.Invoke("  Skipping level purge (project has 1 or fewer levels).");
                return 0;
            }

            var levelIdsToRemove = new HashSet<ElementId>();
            var validLevelIds = new HashSet<ElementId>(allLevels.Select(l => l.Id));
            var referencedLevelIds = new HashSet<ElementId>();

            foreach (Element inst in new FilteredElementCollector(doc).WhereElementIsNotElementType())
            {
                _referenceScanner.CollectUsedIds(inst, validLevelIds, referencedLevelIds);
            }

            logCallback?.Invoke($"  Found {referencedLevelIds.Count} levels referenced by parameters.");

            foreach (var level in allLevels)
            {
                if (referencedLevelIds.Contains(level.Id)) continue;

                ElementLevelFilter levelFilter = new ElementLevelFilter(level.Id);
                var dependentElements = new FilteredElementCollector(doc)
                    .WherePasses(levelFilter)
                    .ToElementIds();

                if (dependentElements.Count == 0)
                {
                    levelIdsToRemove.Add(level.Id);
                }
            }

            int deleted = 0;
            foreach (var level in allLevels)
            {
                if (levelIdsToRemove.Contains(level.Id))
                {
                    if (DeleteElement(doc, level.Id, level.Name, logCallback)) deleted++;
                }
            }

            logCallback?.Invoke($"  Deleted {deleted} levels.");
            return deleted;
        }

        private bool DeleteElement(Document doc, ElementId id, string name, Action<string>? logCallback)
        {
            try
            {
                doc.Delete(id);
                logCallback?.Invoke($"  Deleted: {name}");
                return true;
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"  Could not delete '{name}': {ex.Message}");
                return false;
            }
        }
    }
}
