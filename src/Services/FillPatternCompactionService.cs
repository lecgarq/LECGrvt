using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Configuration;
using LECG.Models;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class FillPatternCompactionService : IFillPatternCompactionService
    {
        private const string CanonicalPrefix = "LECG-FP-";

        private static readonly HashSet<ViewType> SupportedViewTypes = new HashSet<ViewType>
        {
            ViewType.FloorPlan,
            ViewType.CeilingPlan,
            ViewType.Elevation,
            ViewType.Section,
            ViewType.Detail,
            ViewType.ThreeD,
            ViewType.DraftingView,
            ViewType.Legend,
            ViewType.AreaPlan,
            ViewType.EngineeringPlan,
        };

        public FillPatternCompactionResult Compact(Document doc, CompactingStylesContext? context = null, Action<string>? logCallback = null, Action<double, string>? progressCallback = null)
        {
            return Compact(doc, context, new LegacyProgressReporter(progressCallback, logCallback));
        }

        public FillPatternCompactionResult Compact(Document doc, CompactingStylesContext? context, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(reporter);

            Action<string> logCallback = reporter.Log;
            Action<double, string> progressCallback = (percent, message) => reporter.Report(message, percent);
            context ??= CompactingStylesContext.Create(doc, reporter);

            var result = new FillPatternCompactionResult();
            List<FillPatternCandidate> candidates = CollectCandidates(doc);
            List<IGrouping<string, FillPatternCandidate>> duplicateGroups = candidates
                .GroupBy(candidate => candidate.Signature)
                .Where(group => group.Count() > 1)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToList();

            result.DuplicateGroups = duplicateGroups.Count;

            logCallback?.Invoke("");
            logCallback?.Invoke("Scope: Fill Patterns");
            logCallback?.Invoke($"Scanned fill patterns: {candidates.Count}");
            logCallback?.Invoke($"Duplicate groups found: {duplicateGroups.Count}");

            if (duplicateGroups.Count == 0)
            {
                progressCallback?.Invoke(100, "No duplicate fill patterns found");
                logCallback?.Invoke("No graphically identical duplicate fill patterns were found.");
                return result;
            }

            IReadOnlyList<Material> materials = context.Materials;
            IReadOnlyList<FilledRegionType> filledRegionTypes = context.FilledRegionTypes;
            IReadOnlyList<View> views = context.Views;
            IReadOnlyList<Category> categories = context.Categories;

            // Collect all source IDs that might be referenced
            var allSourceIds = new HashSet<ElementId>(duplicateGroups.SelectMany(g => g).Select(c => c.Id));

            Dictionary<ElementId, List<(Material Material, FillPatternReferenceSlot Slot)>> materialIndex =
                BuildMaterialPatternIndex(materials, allSourceIds);

            Dictionary<ElementId, List<(FilledRegionType RegionType, FillPatternReferenceSlot Slot)>> filledRegionIndex =
                BuildFilledRegionPatternIndex(filledRegionTypes, allSourceIds);

            Dictionary<ElementId, List<(Element Element, Parameter Parameter)>> paramIndex = context.ParameterIndex;

            // Build reverse index for view category overrides
            logCallback?.Invoke("Building view override index...");
            Dictionary<ElementId, List<(View View, ElementId CategoryId)>> viewCatIndex =
                BuildViewCategoryOverrideIndex(views, categories, allSourceIds, progressCallback,
                    (overrides, id) =>
                        overrides.SurfaceForegroundPatternId == id ||
                        overrides.SurfaceBackgroundPatternId == id ||
                        overrides.CutForegroundPatternId == id ||
                        overrides.CutBackgroundPatternId == id);

            // Build reverse index for view filter overrides
            Dictionary<ElementId, List<(View View, ElementId FilterId)>> viewFilterIndex =
                BuildViewFilterOverrideIndex(views, allSourceIds,
                    (overrides, id) =>
                        overrides.SurfaceForegroundPatternId == id ||
                        overrides.SurfaceBackgroundPatternId == id ||
                        overrides.CutForegroundPatternId == id ||
                        overrides.CutBackgroundPatternId == id);

            // Build HashSet of existing fill pattern names
            HashSet<string> existingNames = new HashSet<string>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement))
                    .Cast<FillPatternElement>()
                    .Select(p => p.Name),
                StringComparer.OrdinalIgnoreCase);

            // Two-phase: rewire all groups, then single Regenerate + delete
            int nextCanonicalIndex = 1;
            var groupData = new List<(string CanonicalName, ElementId CanonicalId, IGrouping<string, FillPatternCandidate> Group)>();

            for (int groupIndex = 0; groupIndex < duplicateGroups.Count; groupIndex++)
            {
                IGrouping<string, FillPatternCandidate> group = duplicateGroups[groupIndex];
                FillPatternCandidate seed = group.First();
                string canonicalName = CreateCanonicalName(existingNames, ref nextCanonicalIndex);

                FillPatternElement? canonical;
                try
                {
                    canonical = CreateCanonicalPattern(doc, seed.Element, canonicalName);
                }
                catch (Exception ex)
                {
                    logCallback?.Invoke("");
                    logCallback?.Invoke($"Group {groupIndex + 1}: {string.Join(", ", group.Select(item => item.Name).OrderBy(name => name, StringComparer.Ordinal))}");
                    logCallback?.Invoke($"  Skipped — could not create canonical: {ex.Message}");
                    continue;
                }

                result.CanonicalPatternsCreated++;
                result.CreatedCanonicalNames.Add(canonicalName);

                logCallback?.Invoke("");
                logCallback?.Invoke($"Group {groupIndex + 1}: {string.Join(", ", group.Select(item => item.Name).OrderBy(name => name, StringComparer.Ordinal))}");
                logCallback?.Invoke($"  Created canonical: {canonicalName}");

                int originalIndex = 0;
                foreach (FillPatternCandidate original in group)
                {
                    int rewired = RewireReferences(
                        original.Id,
                        canonical.Id,
                        materialIndex,
                        filledRegionIndex,
                        paramIndex,
                        viewCatIndex,
                        viewFilterIndex);
                    result.ReferencesRewired += rewired;
                    logCallback?.Invoke($"  Rewired from '{original.Name}': {rewired} reachable references");

                    originalIndex++;
                    double groupProgress = (groupIndex + (originalIndex / (double)group.Count())) / duplicateGroups.Count * 100d;
                    progressCallback?.Invoke(Math.Round(groupProgress, 0),
                        $"Compacting group {groupIndex + 1} of {duplicateGroups.Count} ({originalIndex}/{group.Count()})");
                }

                groupData.Add((canonicalName, canonical.Id, group));
            }

            // Single Regenerate before all deletes
            doc.Regenerate();

            foreach (var (canonicalName, canonicalId, group) in groupData)
            {
                foreach (FillPatternCandidate original in group)
                {
                    if (TryDeletePattern(doc, original.Id))
                    {
                        result.OriginalPatternsDeleted++;
                        logCallback?.Invoke($"  Deleted original: {original.Name}");
                        continue;
                    }

                    result.BlockedDeletions.Add(original.Name);
                    logCallback?.Invoke($"  Could not delete original: {original.Name}");
                }
            }

            progressCallback?.Invoke(100, "Fill pattern compaction complete");
            logCallback?.Invoke("");
            logCallback?.Invoke("Fill Pattern Summary");
            logCallback?.Invoke("====================");
            logCallback?.Invoke($"Canonical patterns created: {result.CanonicalPatternsCreated}");
            logCallback?.Invoke($"References rewired: {result.ReferencesRewired}");
            logCallback?.Invoke($"Original patterns deleted: {result.OriginalPatternsDeleted}");
            logCallback?.Invoke($"Blocked deletions: {result.BlockedDeletions.Count}");

            return result;
        }

        private static Dictionary<ElementId, List<(Element Element, Parameter Parameter)>> BuildParamIndex(
            IReadOnlyList<Element> instanceElements,
            IReadOnlyList<Element> typeElements,
            Action<double, string>? progressCallback)
        {
            var index = new Dictionary<ElementId, List<(Element, Parameter)>>();
            int total = instanceElements.Count + typeElements.Count;
            int processed = 0;

            void IndexElements(IReadOnlyList<Element> elements)
            {
                foreach (Element element in elements)
                {
                    if (element == null || !element.IsValidObject)
                    {
                        processed++;
                        continue;
                    }

                    foreach (Parameter parameter in element.Parameters)
                    {
                        if (parameter.IsReadOnly || parameter.StorageType != StorageType.ElementId)
                        {
                            continue;
                        }

                        ElementId value = parameter.AsElementId();
                        if (value == ElementId.InvalidElementId)
                        {
                            continue;
                        }

                        if (!index.TryGetValue(value, out List<(Element, Parameter)>? list))
                        {
                            list = new List<(Element, Parameter)>();
                            index[value] = list;
                        }

                        list.Add((element, parameter));
                    }

                    processed++;
                    if (processed % 5000 == 0)
                    {
                        progressCallback?.Invoke(0, $"Indexing parameters... {processed}/{total}");
                    }
                }
            }

            IndexElements(instanceElements);
            IndexElements(typeElements);
            return index;
        }

        private static Dictionary<ElementId, List<(View View, ElementId CategoryId)>> BuildViewCategoryOverrideIndex(
            IReadOnlyList<View> views,
            IReadOnlyList<Category> categories,
            HashSet<ElementId> sourceIds,
            Action<double, string>? progressCallback,
            Func<OverrideGraphicSettings, ElementId, bool> matchesAny)
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
                    foreach (Category category in categories)
                    {
                        IndexSingleCategoryOverride(view, category.Id, sourceIds, matchesAny, index);

                        foreach (Category subCategory in category.SubCategories)
                        {
                            IndexSingleCategoryOverride(view, subCategory.Id, sourceIds, matchesAny, index);
                        }
                    }
                }
                catch
                {
                }
            }

            return index;
        }

        private static void IndexSingleCategoryOverride(
            View view,
            ElementId categoryId,
            HashSet<ElementId> sourceIds,
            Func<OverrideGraphicSettings, ElementId, bool> matchesAny,
            Dictionary<ElementId, List<(View, ElementId)>> index)
        {
            try
            {
                OverrideGraphicSettings overrides = view.GetCategoryOverrides(categoryId);

                foreach (ElementId sourceId in EnumerateFillPatternIds(overrides))
                {
                    if (sourceIds.Contains(sourceId) && matchesAny(overrides, sourceId))
                    {
                        if (!index.TryGetValue(sourceId, out List<(View, ElementId)>? list))
                        {
                            list = new List<(View, ElementId)>();
                            index[sourceId] = list;
                        }

                        list.Add((view, categoryId));
                        break;
                    }
                }
            }
            catch
            {
            }
        }

        private static Dictionary<ElementId, List<(View View, ElementId FilterId)>> BuildViewFilterOverrideIndex(
            IReadOnlyList<View> views,
            HashSet<ElementId> sourceIds,
            Func<OverrideGraphicSettings, ElementId, bool> matchesAny)
        {
            var index = new Dictionary<ElementId, List<(View, ElementId)>>();

            foreach (View view in views)
            {
                if (view == null || !view.IsValidObject)
                {
                    continue;
                }

                ICollection<ElementId> filterIds;
                try { filterIds = view.GetFilters(); }
                catch { continue; }

                foreach (ElementId filterId in filterIds)
                {
                    try
                    {
                        OverrideGraphicSettings overrides = view.GetFilterOverrides(filterId);

                        foreach (ElementId sourceId in EnumerateFillPatternIds(overrides))
                        {
                            if (sourceIds.Contains(sourceId) && matchesAny(overrides, sourceId))
                            {
                                if (!index.TryGetValue(sourceId, out List<(View, ElementId)>? list))
                                {
                                    list = new List<(View, ElementId)>();
                                    index[sourceId] = list;
                                }

                                list.Add((view, filterId));
                                break;
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }

            return index;
        }

        private static List<FillPatternCandidate> CollectCandidates(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .Where(element => !RevitConstants.IsBuiltInFillPattern(element.Name))
                .Select(element => new
                {
                    Element = element,
                    Pattern = element.GetFillPattern()
                })
                .Where(item => item.Pattern != null)
                .Select(item => new FillPatternCandidate(
                    item.Element.Id,
                    item.Element.Name,
                    item.Element,
                    BuildSignature(item.Pattern!)))
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Signature))
                .ToList();
        }

        private static string BuildSignature(FillPattern pattern)
        {
            var parts = new List<string>();
            parts.Add($"Target:{pattern.Target}");
            parts.Add($"HostOrientation:{pattern.HostOrientation}");

            IList<FillGrid> grids = pattern.GetFillGrids();
            for (int i = 0; i < grids.Count; i++)
            {
                FillGrid grid = grids[i];
                double angle = Math.Round(grid.Angle, 6);
                double offset = Math.Round(grid.Offset, 6);
                double shift = Math.Round(grid.Shift, 6);

                string segmentsPart = string.Join(",",
                    grid.GetSegments().Select(s => Math.Round(s, 6).ToString("0.######", CultureInfo.InvariantCulture)));

                parts.Add($"Grid{i}:angle:{angle.ToString("0.######", CultureInfo.InvariantCulture)}" +
                          $",offset:{offset.ToString("0.######", CultureInfo.InvariantCulture)}" +
                          $",shift:{shift.ToString("0.######", CultureInfo.InvariantCulture)}" +
                          $",segs:[{segmentsPart}]");
            }

            return string.Join("|", parts);
        }

        private static FillPatternElement CreateCanonicalPattern(Document doc, FillPatternElement seed, string canonicalName)
        {
            FillPattern sourcePattern = seed.GetFillPattern();
            var fillPattern = new FillPattern(canonicalName, sourcePattern.Target, sourcePattern.HostOrientation);
            fillPattern.SetFillGrids(sourcePattern.GetFillGrids().ToList());
            return FillPatternElement.Create(doc, fillPattern);
        }

        private static string CreateCanonicalName(HashSet<string> existingNames, ref int nextCanonicalIndex)
        {
            while (true)
            {
                string candidate = $"{CanonicalPrefix}{nextCanonicalIndex:000}";
                nextCanonicalIndex++;

                if (existingNames.Add(candidate))
                {
                    return candidate;
                }
            }
        }

        private static int RewireReferences(
            ElementId sourceId,
            ElementId targetId,
            Dictionary<ElementId, List<(Material Material, FillPatternReferenceSlot Slot)>> materialIndex,
            Dictionary<ElementId, List<(FilledRegionType RegionType, FillPatternReferenceSlot Slot)>> filledRegionIndex,
            Dictionary<ElementId, List<(Element Element, Parameter Parameter)>> paramIndex,
            Dictionary<ElementId, List<(View View, ElementId CategoryId)>> viewCatIndex,
            Dictionary<ElementId, List<(View View, ElementId FilterId)>> viewFilterIndex)
        {
            if (sourceId == targetId)
            {
                return 0;
            }

            int rewired = 0;
            rewired += RewireMaterialReferencesFromIndex(materialIndex, sourceId, targetId);
            rewired += RewireFilledRegionTypeReferencesFromIndex(filledRegionIndex, sourceId, targetId);
            rewired += RewireParameterReferencesFromIndex(paramIndex, sourceId, targetId);
            rewired += RewireViewCategoryOverridesFromIndex(viewCatIndex, sourceId, targetId);
            rewired += RewireViewFilterOverridesFromIndex(viewFilterIndex, sourceId, targetId);
            return rewired;
        }

        private static Dictionary<ElementId, List<(Material Material, FillPatternReferenceSlot Slot)>> BuildMaterialPatternIndex(
            IReadOnlyList<Material> materials,
            HashSet<ElementId> sourceIds)
        {
            var index = new Dictionary<ElementId, List<(Material, FillPatternReferenceSlot)>>();
            foreach (Material mat in materials)
            {
                if (mat == null || !mat.IsValidObject)
                {
                    continue;
                }

                IndexFillPatternReference(index, mat, mat.SurfaceForegroundPatternId, FillPatternReferenceSlot.SurfaceForeground, sourceIds);
                IndexFillPatternReference(index, mat, mat.SurfaceBackgroundPatternId, FillPatternReferenceSlot.SurfaceBackground, sourceIds);
                IndexFillPatternReference(index, mat, mat.CutForegroundPatternId, FillPatternReferenceSlot.CutForeground, sourceIds);
                IndexFillPatternReference(index, mat, mat.CutBackgroundPatternId, FillPatternReferenceSlot.CutBackground, sourceIds);
            }

            return index;
        }

        private static Dictionary<ElementId, List<(FilledRegionType RegionType, FillPatternReferenceSlot Slot)>> BuildFilledRegionPatternIndex(
            IReadOnlyList<FilledRegionType> filledRegionTypes,
            HashSet<ElementId> sourceIds)
        {
            var index = new Dictionary<ElementId, List<(FilledRegionType, FillPatternReferenceSlot)>>();
            foreach (FilledRegionType frt in filledRegionTypes)
            {
                if (frt == null || !frt.IsValidObject)
                {
                    continue;
                }

                IndexFillPatternReference(index, frt, frt.ForegroundPatternId, FillPatternReferenceSlot.Foreground, sourceIds);
                IndexFillPatternReference(index, frt, frt.BackgroundPatternId, FillPatternReferenceSlot.Background, sourceIds);
            }

            return index;
        }

        private static int RewireMaterialReferencesFromIndex(
            Dictionary<ElementId, List<(Material Material, FillPatternReferenceSlot Slot)>> materialIndex,
            ElementId sourceId,
            ElementId targetId)
        {
            if (!materialIndex.TryGetValue(sourceId, out List<(Material Material, FillPatternReferenceSlot Slot)>? entries))
            {
                return 0;
            }

            int rewired = 0;
            var movedToTarget = new List<(Material, FillPatternReferenceSlot)>();

            foreach (var (material, slot) in entries)
            {
                if (material == null || !material.IsValidObject)
                {
                    continue;
                }

                if (!TrySetMaterialPattern(material, slot, sourceId, targetId))
                {
                    continue;
                }

                rewired++;
                movedToTarget.Add((material, slot));
            }

            materialIndex.Remove(sourceId);

            if (movedToTarget.Count > 0)
            {
                if (!materialIndex.TryGetValue(targetId, out List<(Material, FillPatternReferenceSlot)>? targetEntries))
                {
                    targetEntries = new List<(Material, FillPatternReferenceSlot)>();
                    materialIndex[targetId] = targetEntries;
                }

                targetEntries.AddRange(movedToTarget);
            }

            return rewired;
        }

        private static int RewireFilledRegionTypeReferencesFromIndex(
            Dictionary<ElementId, List<(FilledRegionType RegionType, FillPatternReferenceSlot Slot)>> filledRegionIndex,
            ElementId sourceId,
            ElementId targetId)
        {
            if (!filledRegionIndex.TryGetValue(sourceId, out List<(FilledRegionType RegionType, FillPatternReferenceSlot Slot)>? entries))
            {
                return 0;
            }

            int rewired = 0;
            var movedToTarget = new List<(FilledRegionType, FillPatternReferenceSlot)>();

            foreach (var (regionType, slot) in entries)
            {
                if (regionType == null || !regionType.IsValidObject)
                {
                    continue;
                }

                if (!TrySetFilledRegionPattern(regionType, slot, sourceId, targetId))
                {
                    continue;
                }

                rewired++;
                movedToTarget.Add((regionType, slot));
            }

            filledRegionIndex.Remove(sourceId);

            if (movedToTarget.Count > 0)
            {
                if (!filledRegionIndex.TryGetValue(targetId, out List<(FilledRegionType, FillPatternReferenceSlot)>? targetEntries))
                {
                    targetEntries = new List<(FilledRegionType, FillPatternReferenceSlot)>();
                    filledRegionIndex[targetId] = targetEntries;
                }

                targetEntries.AddRange(movedToTarget);
            }

            return rewired;
        }

        private static int RewireParameterReferencesFromIndex(
            Dictionary<ElementId, List<(Element Element, Parameter Parameter)>> paramIndex,
            ElementId sourceId,
            ElementId targetId)
        {
            if (!paramIndex.TryGetValue(sourceId, out List<(Element Element, Parameter Parameter)>? entries))
            {
                return 0;
            }

            int rewired = 0;
            var movedToTarget = new List<(Element, Parameter)>();

            foreach (var (element, parameter) in entries)
            {
                if (element == null || !element.IsValidObject)
                {
                    continue;
                }

                try
                {
                    if (parameter.AsElementId() == sourceId && parameter.Set(targetId))
                    {
                        rewired++;
                        movedToTarget.Add((element, parameter));
                    }
                }
                catch
                {
                }
            }

            paramIndex.Remove(sourceId);

            if (movedToTarget.Count > 0)
            {
                if (!paramIndex.TryGetValue(targetId, out List<(Element, Parameter)>? targetList))
                {
                    targetList = new List<(Element, Parameter)>();
                    paramIndex[targetId] = targetList;
                }

                targetList.AddRange(movedToTarget);
            }

            return rewired;
        }

        private static int RewireViewCategoryOverridesFromIndex(
            Dictionary<ElementId, List<(View View, ElementId CategoryId)>> viewCatIndex,
            ElementId sourceId,
            ElementId targetId)
        {
            if (!viewCatIndex.TryGetValue(sourceId, out List<(View View, ElementId CategoryId)>? entries))
            {
                return 0;
            }

            int rewired = 0;

            foreach (var (view, categoryId) in entries)
            {
                if (view == null || !view.IsValidObject)
                {
                    continue;
                }

                try
                {
                    OverrideGraphicSettings overrides = view.GetCategoryOverrides(categoryId);
                    int changed = 0;

                    if (overrides.SurfaceForegroundPatternId == sourceId)
                    {
                        overrides.SetSurfaceForegroundPatternId(targetId);
                        changed++;
                    }

                    if (overrides.SurfaceBackgroundPatternId == sourceId)
                    {
                        overrides.SetSurfaceBackgroundPatternId(targetId);
                        changed++;
                    }

                    if (overrides.CutForegroundPatternId == sourceId)
                    {
                        overrides.SetCutForegroundPatternId(targetId);
                        changed++;
                    }

                    if (overrides.CutBackgroundPatternId == sourceId)
                    {
                        overrides.SetCutBackgroundPatternId(targetId);
                        changed++;
                    }

                    if (changed > 0)
                    {
                        view.SetCategoryOverrides(categoryId, overrides);
                        rewired += changed;
                    }
                }
                catch
                {
                }
            }

            viewCatIndex.Remove(sourceId);
            return rewired;
        }

        private static int RewireViewFilterOverridesFromIndex(
            Dictionary<ElementId, List<(View View, ElementId FilterId)>> viewFilterIndex,
            ElementId sourceId,
            ElementId targetId)
        {
            if (!viewFilterIndex.TryGetValue(sourceId, out List<(View View, ElementId FilterId)>? entries))
            {
                return 0;
            }

            int rewired = 0;

            foreach (var (view, filterId) in entries)
            {
                if (view == null || !view.IsValidObject)
                {
                    continue;
                }

                try
                {
                    OverrideGraphicSettings overrides = view.GetFilterOverrides(filterId);
                    int changed = 0;

                    if (overrides.SurfaceForegroundPatternId == sourceId)
                    {
                        overrides.SetSurfaceForegroundPatternId(targetId);
                        changed++;
                    }

                    if (overrides.SurfaceBackgroundPatternId == sourceId)
                    {
                        overrides.SetSurfaceBackgroundPatternId(targetId);
                        changed++;
                    }

                    if (overrides.CutForegroundPatternId == sourceId)
                    {
                        overrides.SetCutForegroundPatternId(targetId);
                        changed++;
                    }

                    if (overrides.CutBackgroundPatternId == sourceId)
                    {
                        overrides.SetCutBackgroundPatternId(targetId);
                        changed++;
                    }

                    if (changed > 0)
                    {
                        view.SetFilterOverrides(filterId, overrides);
                        rewired += changed;
                    }
                }
                catch
                {
                }
            }

            viewFilterIndex.Remove(sourceId);
            return rewired;
        }

        private static bool TryDeletePattern(Document doc, ElementId patternId)
        {
            try
            {
                ICollection<ElementId> deletedIds = doc.Delete(patternId);
                return deletedIds.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static void IndexFillPatternReference<TElement>(
            Dictionary<ElementId, List<(TElement Element, FillPatternReferenceSlot Slot)>> index,
            TElement element,
            ElementId patternId,
            FillPatternReferenceSlot slot,
            HashSet<ElementId> sourceIds)
        {
            if (patternId == ElementId.InvalidElementId || !sourceIds.Contains(patternId))
            {
                return;
            }

            if (!index.TryGetValue(patternId, out List<(TElement, FillPatternReferenceSlot)>? entries))
            {
                entries = new List<(TElement, FillPatternReferenceSlot)>();
                index[patternId] = entries;
            }

            entries.Add((element, slot));
        }

        private static bool TrySetMaterialPattern(Material material, FillPatternReferenceSlot slot, ElementId sourceId, ElementId targetId)
        {
            switch (slot)
            {
                case FillPatternReferenceSlot.SurfaceForeground:
                    if (material.SurfaceForegroundPatternId != sourceId) return false;
                    material.SurfaceForegroundPatternId = targetId;
                    return true;
                case FillPatternReferenceSlot.SurfaceBackground:
                    if (material.SurfaceBackgroundPatternId != sourceId) return false;
                    material.SurfaceBackgroundPatternId = targetId;
                    return true;
                case FillPatternReferenceSlot.CutForeground:
                    if (material.CutForegroundPatternId != sourceId) return false;
                    material.CutForegroundPatternId = targetId;
                    return true;
                case FillPatternReferenceSlot.CutBackground:
                    if (material.CutBackgroundPatternId != sourceId) return false;
                    material.CutBackgroundPatternId = targetId;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TrySetFilledRegionPattern(FilledRegionType regionType, FillPatternReferenceSlot slot, ElementId sourceId, ElementId targetId)
        {
            switch (slot)
            {
                case FillPatternReferenceSlot.Foreground:
                    if (regionType.ForegroundPatternId != sourceId) return false;
                    regionType.ForegroundPatternId = targetId;
                    return true;
                case FillPatternReferenceSlot.Background:
                    if (regionType.BackgroundPatternId != sourceId) return false;
                    regionType.BackgroundPatternId = targetId;
                    return true;
                default:
                    return false;
            }
        }

        private static IEnumerable<ElementId> EnumerateFillPatternIds(OverrideGraphicSettings overrides)
        {
            ElementId surfaceForegroundId = overrides.SurfaceForegroundPatternId;
            if (surfaceForegroundId != ElementId.InvalidElementId)
            {
                yield return surfaceForegroundId;
            }

            ElementId surfaceBackgroundId = overrides.SurfaceBackgroundPatternId;
            if (surfaceBackgroundId != ElementId.InvalidElementId && surfaceBackgroundId != surfaceForegroundId)
            {
                yield return surfaceBackgroundId;
            }

            ElementId cutForegroundId = overrides.CutForegroundPatternId;
            if (cutForegroundId != ElementId.InvalidElementId &&
                cutForegroundId != surfaceForegroundId &&
                cutForegroundId != surfaceBackgroundId)
            {
                yield return cutForegroundId;
            }

            ElementId cutBackgroundId = overrides.CutBackgroundPatternId;
            if (cutBackgroundId != ElementId.InvalidElementId &&
                cutBackgroundId != surfaceForegroundId &&
                cutBackgroundId != surfaceBackgroundId &&
                cutBackgroundId != cutForegroundId)
            {
                yield return cutBackgroundId;
            }
        }

        private sealed class FillPatternCandidate
        {
            public FillPatternCandidate(ElementId id, string name, FillPatternElement element, string signature)
            {
                Id = id;
                Name = name;
                Element = element;
                Signature = signature;
            }

            public ElementId Id { get; }
            public string Name { get; }
            public FillPatternElement Element { get; }
            public string Signature { get; }
        }

        private enum FillPatternReferenceSlot
        {
            SurfaceForeground,
            SurfaceBackground,
            CutForeground,
            CutBackground,
            Foreground,
            Background,
        }
    }
}
