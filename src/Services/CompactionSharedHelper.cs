using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    internal static class CompactionSharedHelper
    {
        public static int RewireParameterReferencesFromIndex(
            Document doc,
            Dictionary<ElementId, HashSet<ElementId>> paramIndex,
            ElementId sourceId,
            ElementId targetId)
        {
            if (!paramIndex.TryGetValue(sourceId, out HashSet<ElementId>? elementIds))
            {
                return 0;
            }

            int rewired = 0;
            var movedToTarget = new HashSet<ElementId>();

            foreach (ElementId elementId in elementIds)
            {
                try
                {
                    Element? element = doc.GetElement(elementId);
                    if (element == null || !element.IsValidObject) continue;

                    foreach (Parameter param in element.Parameters)
                    {
                        if (param.IsReadOnly || param.StorageType != StorageType.ElementId) continue;
                        try
                        {
                            if (!param.HasValue) continue;
                            if (param.AsElementId() == sourceId)
                            {
                                param.Set(targetId);
                                rewired++;
                                movedToTarget.Add(elementId);
                            }
                        }
                        catch (ArgumentException ex) { Logging.Logger.Instance.LogWarning($"[CompactionSharedHelper] Parameter rewire for element {elementId}: {ex.Message}"); }
                        catch (InvalidOperationException ex) { Logging.Logger.Instance.LogWarning($"[CompactionSharedHelper] Parameter rewire for element {elementId}: {ex.Message}"); }
                        catch (RevitExceptions.InvalidOperationException ex) { Logging.Logger.Instance.LogWarning($"[CompactionSharedHelper] Parameter rewire for element {elementId}: {ex.Message}"); }
                    }
                }
                catch (ArgumentException ex) { Logging.Logger.Instance.LogWarning($"[CompactionSharedHelper] Element access during rewire {elementId}: {ex.Message}"); }
                catch (InvalidOperationException ex) { Logging.Logger.Instance.LogWarning($"[CompactionSharedHelper] Element access during rewire {elementId}: {ex.Message}"); }
                catch (RevitExceptions.InvalidOperationException ex) { Logging.Logger.Instance.LogWarning($"[CompactionSharedHelper] Element access during rewire {elementId}: {ex.Message}"); }
            }

            paramIndex.Remove(sourceId);

            if (movedToTarget.Count > 0)
            {
                if (!paramIndex.TryGetValue(targetId, out HashSet<ElementId>? targetSet))
                {
                    targetSet = new HashSet<ElementId>();
                    paramIndex[targetId] = targetSet;
                }

                targetSet.UnionWith(movedToTarget);
            }

            return rewired;
        }

        public static bool TryDeleteElement(Document doc, ElementId elementId)
        {
            try
            {
                ICollection<ElementId> deletedIds = doc.Delete(elementId);
                return deletedIds.Count > 0;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (RevitExceptions.InvalidOperationException)
            {
                return false;
            }
        }

        public static string CreateCanonicalName(string prefix, HashSet<string> existingNames, ref int nextCanonicalIndex)
        {
            while (true)
            {
                string candidate = $"{prefix}{nextCanonicalIndex:000}";
                nextCanonicalIndex++;

                if (existingNames.Add(candidate))
                {
                    return candidate;
                }
            }
        }

        public static Dictionary<ElementId, List<(View View, ElementId CategoryId)>> BuildViewCategoryOverrideIndex(
            IReadOnlyList<View> views,
            IReadOnlyList<Category> categories,
            HashSet<ElementId> sourceIds,
            Action<double, string>? progressCallback,
            Func<OverrideGraphicSettings, IEnumerable<ElementId>> enumerateIds)
        {
            var index = new Dictionary<ElementId, List<(View, ElementId)>>();
            int viewCount = 0;

            foreach (View view in views)
            {
                if (view == null || !view.IsValidObject)
                {
                    continue;
                }

                viewCount++;
                if (viewCount % 50 == 0)
                {
                    progressCallback?.Invoke(0, $"Indexing view overrides... {viewCount}/{views.Count}");
                }

                try
                {
                    IndexCategoryHierarchyOverrides(view, categories, sourceIds, enumerateIds, index);
                }
                catch (ArgumentException ex)
                {
                    LogExpectedOperationWarning($"View category override indexing for view {view.Name}", ex);
                }
                catch (InvalidOperationException ex)
                {
                    LogExpectedOperationWarning($"View category override indexing for view {view.Name}", ex);
                }
                catch (RevitExceptions.InvalidOperationException ex)
                {
                    LogExpectedOperationWarning($"View category override indexing for view {view.Name}", ex);
                }
            }

            return index;
        }

        public static Dictionary<ElementId, List<(View View, ElementId FilterId)>> BuildViewFilterOverrideIndex(
            IReadOnlyList<View> views,
            HashSet<ElementId> sourceIds,
            Func<OverrideGraphicSettings, IEnumerable<ElementId>> enumerateIds)
        {
            var index = new Dictionary<ElementId, List<(View, ElementId)>>();

            foreach (View view in views)
            {
                if (view == null || !view.IsValidObject)
                {
                    continue;
                }

                if (!TryGetFilterIds(view, out ICollection<ElementId> filterIds))
                {
                    continue;
                }

                foreach (ElementId filterId in filterIds)
                {
                    TryIndexViewFilterOverride(view, filterId, sourceIds, enumerateIds, index);
                }
            }

            return index;
        }

        public static int RewireViewCategoryOverridesFromIndex(
            Dictionary<ElementId, List<(View View, ElementId CategoryId)>> viewCatIndex,
            ElementId sourceId,
            ElementId targetId,
            Func<OverrideGraphicSettings, ElementId, ElementId, int> rewriteOverrides)
        {
            if (!viewCatIndex.TryGetValue(sourceId, out List<(View View, ElementId CategoryId)>? entries))
            {
                return 0;
            }

            int rewired = 0;

            rewired += RewireViewOverrides(
                entries,
                sourceId,
                targetId,
                rewriteOverrides,
                (view, categoryId) => view.GetCategoryOverrides(categoryId),
                (view, categoryId, overrides) => view.SetCategoryOverrides(categoryId, overrides),
                "View category override rewire");

            viewCatIndex.Remove(sourceId);
            return rewired;
        }

        public static int RewireViewFilterOverridesFromIndex(
            Dictionary<ElementId, List<(View View, ElementId FilterId)>> viewFilterIndex,
            ElementId sourceId,
            ElementId targetId,
            Func<OverrideGraphicSettings, ElementId, ElementId, int> rewriteOverrides)
        {
            if (!viewFilterIndex.TryGetValue(sourceId, out List<(View View, ElementId FilterId)>? entries))
            {
                return 0;
            }

            int rewired = 0;

            rewired += RewireViewOverrides(
                entries,
                sourceId,
                targetId,
                rewriteOverrides,
                (view, filterId) => view.GetFilterOverrides(filterId),
                (view, filterId, overrides) => view.SetFilterOverrides(filterId, overrides),
                "View filter override rewire");

            viewFilterIndex.Remove(sourceId);
            return rewired;
        }

        private static bool TryGetFilterIds(View view, out ICollection<ElementId> filterIds)
        {
            filterIds = Array.Empty<ElementId>();
            try
            {
                filterIds = view.GetFilters();
                return true;
            }
            catch (ArgumentException ex)
            {
                LogExpectedOperationWarning($"GetFilters for view {view.Name}", ex);
                return false;
            }
            catch (InvalidOperationException ex)
            {
                LogExpectedOperationWarning($"GetFilters for view {view.Name}", ex);
                return false;
            }
            catch (RevitExceptions.InvalidOperationException ex)
            {
                LogExpectedOperationWarning($"GetFilters for view {view.Name}", ex);
                return false;
            }
        }

        private static void IndexCategoryHierarchyOverrides(
            View view,
            IReadOnlyList<Category> categories,
            HashSet<ElementId> sourceIds,
            Func<OverrideGraphicSettings, IEnumerable<ElementId>> enumerateIds,
            Dictionary<ElementId, List<(View, ElementId)>> index)
        {
            foreach (Category category in categories)
            {
                IndexSingleCategoryOverride(view, category.Id, sourceIds, enumerateIds, index);

                foreach (Category subCategory in category.SubCategories)
                {
                    IndexSingleCategoryOverride(view, subCategory.Id, sourceIds, enumerateIds, index);
                }
            }
        }

        private static void TryIndexViewFilterOverride(
            View view,
            ElementId filterId,
            HashSet<ElementId> sourceIds,
            Func<OverrideGraphicSettings, IEnumerable<ElementId>> enumerateIds,
            Dictionary<ElementId, List<(View, ElementId)>> index)
        {
            try
            {
                OverrideGraphicSettings overrides = view.GetFilterOverrides(filterId);

                foreach (ElementId sourceId in enumerateIds(overrides))
                {
                    if (!sourceIds.Contains(sourceId))
                    {
                        continue;
                    }

                    AddViewOverrideIndexEntry(index, sourceId, view, filterId);
                    break;
                }
            }
            catch (ArgumentException ex)
            {
                LogExpectedOperationWarning($"Filter override indexing for filter {filterId}", ex);
            }
            catch (InvalidOperationException ex)
            {
                LogExpectedOperationWarning($"Filter override indexing for filter {filterId}", ex);
            }
            catch (RevitExceptions.InvalidOperationException ex)
            {
                LogExpectedOperationWarning($"Filter override indexing for filter {filterId}", ex);
            }
        }

        private static int RewireViewOverrides(
            IEnumerable<(View View, ElementId EntryId)> entries,
            ElementId sourceId,
            ElementId targetId,
            Func<OverrideGraphicSettings, ElementId, ElementId, int> rewriteOverrides,
            Func<View, ElementId, OverrideGraphicSettings> getOverrides,
            Action<View, ElementId, OverrideGraphicSettings> setOverrides,
            string warningContext)
        {
            int rewired = 0;

            foreach (var (view, entryId) in entries)
            {
                if (view == null || !view.IsValidObject)
                {
                    continue;
                }

                try
                {
                    OverrideGraphicSettings overrides = getOverrides(view, entryId);
                    int changed = rewriteOverrides(overrides, sourceId, targetId);

                    if (changed <= 0)
                    {
                        continue;
                    }

                    setOverrides(view, entryId, overrides);
                    rewired += changed;
                }
                catch (ArgumentException ex)
                {
                    LogExpectedOperationWarning(warningContext, ex);
                }
                catch (InvalidOperationException ex)
                {
                    LogExpectedOperationWarning(warningContext, ex);
                }
                catch (RevitExceptions.InvalidOperationException ex)
                {
                    LogExpectedOperationWarning(warningContext, ex);
                }
            }

            return rewired;
        }

        private static void IndexSingleCategoryOverride(
            View view,
            ElementId categoryId,
            HashSet<ElementId> sourceIds,
            Func<OverrideGraphicSettings, IEnumerable<ElementId>> enumerateIds,
            Dictionary<ElementId, List<(View, ElementId)>> index)
        {
            try
            {
                OverrideGraphicSettings overrides = view.GetCategoryOverrides(categoryId);

                foreach (ElementId sourceId in enumerateIds(overrides))
                {
                    if (sourceIds.Contains(sourceId))
                    {
                        AddViewOverrideIndexEntry(index, sourceId, view, categoryId);
                        break;
                    }
                }
            }
            catch (ArgumentException ex)
            {
                LogExpectedOperationWarning($"Category override indexing for category {categoryId}", ex);
            }
            catch (InvalidOperationException ex)
            {
                LogExpectedOperationWarning($"Category override indexing for category {categoryId}", ex);
            }
            catch (RevitExceptions.InvalidOperationException ex)
            {
                LogExpectedOperationWarning($"Category override indexing for category {categoryId}", ex);
            }
        }

        private static void AddViewOverrideIndexEntry(
            Dictionary<ElementId, List<(View, ElementId)>> index,
            ElementId sourceId,
            View view,
            ElementId entryId)
        {
            if (!index.TryGetValue(sourceId, out List<(View, ElementId)>? list))
            {
                list = new List<(View, ElementId)>();
                index[sourceId] = list;
            }

            list.Add((view, entryId));
        }

        private static void LogExpectedOperationWarning(string context, Exception ex)
        {
            Logging.Logger.Instance.LogWarning($"[CompactionSharedHelper] {context}: {ex.Message}");
        }
    }
}
