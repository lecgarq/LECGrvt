using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgeLevelService
    {
        private readonly PurgeDeleteElementService _purgeDeleteElementService;

        public PurgeLevelService(PurgeDeleteElementService purgeDeleteElementService)
        {
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
            var referencedLevelIds = CollectReferencedLevelIds(context, validLevelIds);
            CollectPlanViewLevelIds(doc, referencedLevelIds);

            logCallback?.Invoke($"  Found {referencedLevelIds.Count} levels referenced by parameters or plan views.");

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

        /// <summary>
        /// A level that generates plan views is in use: deleting it would cascade-delete those
        /// views (and empty any sheets they are placed on). ElementLevelFilter does not catch
        /// views, so they are collected explicitly.
        /// </summary>
        private static void CollectPlanViewLevelIds(Document doc, HashSet<ElementId> referencedLevelIds)
        {
            foreach (ViewPlan viewPlan in new FilteredElementCollector(doc).OfClass(typeof(ViewPlan)).Cast<ViewPlan>())
            {
                try
                {
                    Level? genLevel = viewPlan.GenLevel;
                    if (genLevel != null)
                    {
                        referencedLevelIds.Add(genLevel.Id);
                    }
                }
                catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                {
                    // Some plan views (e.g. area plans without a level) do not expose GenLevel.
                }
            }
        }

        private static HashSet<ElementId> CollectReferencedLevelIds(
            PurgeContext context,
            HashSet<ElementId> validLevelIds)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(validLevelIds);

            var referencedLevelIds = new HashSet<ElementId>();
            foreach (ElementId referencedId in context.ParameterReferencedIds)
            {
                if (validLevelIds.Contains(referencedId))
                {
                    referencedLevelIds.Add(referencedId);
                }
            }

            return referencedLevelIds;
        }
    }
}
