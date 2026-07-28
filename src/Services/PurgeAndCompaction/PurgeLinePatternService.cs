using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class PurgeLinePatternService
    {
        private readonly PurgeDeleteElementService _purgeDeleteElementService;
        private readonly ILogger _logger;

        public PurgeLinePatternService(PurgeDeleteElementService purgeDeleteElementService, ILogger logger)
        {
            _purgeDeleteElementService = purgeDeleteElementService;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public int PurgeUnusedLinePatterns(Document doc, Action<string>? logCallback = null)
        {
            return PurgeUnusedLinePatterns(doc, PurgeContext.Create(doc), logCallback);
        }

        public int PurgeUnusedLinePatterns(Document doc, PurgeContext context, Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(context);

            logCallback?.Invoke("Scanning for unused line patterns...");

            var allPatterns = new FilteredElementCollector(doc)
                .OfClass(typeof(LinePatternElement))
                .Cast<LinePatternElement>()
                .Where(pattern => pattern.Id != LinePatternElement.GetSolidPatternId())
                .ToDictionary(pattern => pattern.Id, pattern => pattern.Name);

            logCallback?.Invoke($"  Found {allPatterns.Count} potential candidates.");
            if (allPatterns.Count == 0)
            {
                return 0;
            }

            var validIds = new HashSet<ElementId>(allPatterns.Keys);
            var usedIds = new HashSet<ElementId>();
            foreach (ElementId referencedId in context.ParameterReferencedIds)
            {
                if (validIds.Contains(referencedId))
                {
                    usedIds.Add(referencedId);
                }
            }

            foreach (ElementId referencedId in context.UsedLinePatternIds)
            {
                if (validIds.Contains(referencedId))
                {
                    usedIds.Add(referencedId);
                }
            }

            List<Category> categories = CollectCategories(doc);

            foreach (Category category in categories)
            {
                TryAddCategoryPattern(category, GraphicsStyleType.Projection, validIds, usedIds);
                TryAddCategoryPattern(category, GraphicsStyleType.Cut, validIds, usedIds);
            }

            foreach (View view in new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>())
            {
                if (view == null || !view.IsValidObject)
                {
                    continue;
                }

                // Schedules, sheets, etc. don't support V/G overrides — querying them throws
                // once per category per view, flooding the log with thousands of warnings.
                if (!view.AreGraphicsOverridesAllowed())
                {
                    continue;
                }

                ScanViewCategoryOverrides(view, categories, validIds, usedIds);
                ScanViewFilterOverrides(view, validIds, usedIds);
            }

            int deleted = 0;
            foreach (var kvp in allPatterns)
            {
                if (!usedIds.Contains(kvp.Key) &&
                    _purgeDeleteElementService.DeleteElement(doc, kvp.Key, kvp.Value, logCallback))
                {
                    deleted++;
                }
            }

            logCallback?.Invoke($"  Deleted {deleted} line patterns.");
            return deleted;
        }

        private static List<Category> CollectCategories(Document doc)
        {
            var categories = new List<Category>();
            foreach (Category category in doc.Settings.Categories)
            {
                AddCategoryTree(category, categories);
            }

            return categories;
        }

        private static void AddCategoryTree(Category category, List<Category> categories)
        {
            categories.Add(category);

            foreach (Category subCategory in category.SubCategories)
            {
                AddCategoryTree(subCategory, categories);
            }
        }

        private void TryAddCategoryPattern(
            Category category,
            GraphicsStyleType styleType,
            HashSet<ElementId> validIds,
            HashSet<ElementId> usedIds)
        {
            try
            {
                AddTrackedId(category.GetLinePatternId(styleType), validIds, usedIds);
            }
            catch (Exception ex) when (IsExpectedRevitException(ex))
            {
                // Categories without line patterns for this style type — expected, skip.
            }
        }

        // Revit's ArgumentException/InvalidOperationException derive from
        // Autodesk.Revit.Exceptions.ApplicationException, not their System namesakes,
        // so both families must be listed or Revit exceptions abort the purge pass.
        private static bool IsExpectedRevitException(Exception ex)
        {
            return ex is ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.ArgumentException
                || ex is RevitExceptions.InvalidOperationException;
        }

        private void ScanViewCategoryOverrides(
            View view,
            IReadOnlyList<Category> categories,
            HashSet<ElementId> validIds,
            HashSet<ElementId> usedIds)
        {
            foreach (Category category in categories)
            {
                try
                {
                    OverrideGraphicSettings overrides = view.GetCategoryOverrides(category.Id);
                    AddTrackedId(overrides.ProjectionLinePatternId, validIds, usedIds);
                    AddTrackedId(overrides.CutLinePatternId, validIds, usedIds);
                }
                catch (Exception ex) when (IsExpectedRevitException(ex))
                {
                    // Non-overridable categories in this view — expected, skip silently
                    // (logging here produces one warning per category per view).
                }
            }
        }

        private void ScanViewFilterOverrides(
            View view,
            HashSet<ElementId> validIds,
            HashSet<ElementId> usedIds)
        {
            ICollection<ElementId> filterIds;
            try
            {
                filterIds = view.GetFilters();
            }
            catch (Exception ex) when (IsExpectedRevitException(ex))
            {
                // Views that don't support filters — expected, skip.
                return;
            }

            foreach (ElementId filterId in filterIds)
            {
                try
                {
                    OverrideGraphicSettings overrides = view.GetFilterOverrides(filterId);
                    AddTrackedId(overrides.ProjectionLinePatternId, validIds, usedIds);
                    AddTrackedId(overrides.CutLinePatternId, validIds, usedIds);
                }
                catch (Exception ex) when (IsExpectedRevitException(ex))
                {
                    _logger.LogWarning($"ScanViewFilterOverrides: {ex.Message}", scope: "PurgeLinePattern");
                }
            }
        }

        private static void AddTrackedId(ElementId id, HashSet<ElementId> validIds, HashSet<ElementId> usedIds)
        {
            if (validIds.Contains(id))
            {
                usedIds.Add(id);
            }
        }
    }
}
