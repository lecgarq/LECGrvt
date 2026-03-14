using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgeLevelService : IPurgeLevelService
    {
        private readonly IPurgeReferencedLevelService _purgeReferencedLevelService;
        private readonly IPurgeDeleteElementService _purgeDeleteElementService;

        public PurgeLevelService(IPurgeReferencedLevelService purgeReferencedLevelService, IPurgeDeleteElementService purgeDeleteElementService)
        {
            _purgeReferencedLevelService = purgeReferencedLevelService;
            _purgeDeleteElementService = purgeDeleteElementService;
        }

        public int PurgeUnusedLevels(Document doc, Action<string>? logCallback = null)
        {
            return PurgeUnusedLevels(doc, PurgeContext.Create(doc), logCallback);
        }

        public int PurgeUnusedLevels(Document doc, PurgeContext context, Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(context);

            logCallback?.Invoke("Scanning for unused levels...");

            var allLevels = context.Levels
                .Where(level => level != null && level.IsValidObject)
                .ToList();

            if (allLevels.Count <= 1)
            {
                logCallback?.Invoke("  Skipping level purge (project has 1 or fewer levels).");
                return 0;
            }

            var levelIdsToRemove = new HashSet<ElementId>();
            var validLevelIds = new HashSet<ElementId>(allLevels.Select(l => l.Id));
            var referencedLevelIds = _purgeReferencedLevelService.CollectReferencedLevelIds(context, validLevelIds);

            logCallback?.Invoke($"  Found {referencedLevelIds.Count} levels referenced by parameters.");

            foreach (var level in allLevels)
            {
                if (referencedLevelIds.Contains(level.Id) || context.PlacedLevelIds.Contains(level.Id)) continue;

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
                    if (_purgeDeleteElementService.DeleteElement(doc, level.Id, level.Name, logCallback)) deleted++;
                }
            }

            logCallback?.Invoke($"  Deleted {deleted} levels.");
            return deleted;
        }
    }
}
