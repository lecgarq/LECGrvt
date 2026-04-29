using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Configuration;
using LECG.Models;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class FillPatternCompactionService : IFillPatternCompactionService
    {
        private const string CanonicalPrefix = "LECG-FP-";

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
            List<IGrouping<string, FillPatternCandidate>> duplicateGroups = BuildDuplicateGroups(candidates);

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

            FillPatternCompactionIndexes indexes = BuildCompactionIndexes(doc, context, duplicateGroups, logCallback, progressCallback);
            HashSet<string> existingNames = indexes.ExistingNames;

            int nextCanonicalIndex = 1;
            var groupsToDelete = new List<IGrouping<string, FillPatternCandidate>>();

            for (int groupIndex = 0; groupIndex < duplicateGroups.Count; groupIndex++)
            {
                IGrouping<string, FillPatternCandidate> group = duplicateGroups[groupIndex];
                string logHeader = CreateGroupLogHeader(groupIndex, group);
                FillPatternCandidate seed = group.First();
                string canonicalName = CompactionSharedHelper.CreateCanonicalName(CanonicalPrefix, existingNames, ref nextCanonicalIndex);

                FillPatternElement? canonical;
                try
                {
                    canonical = CreateCanonicalPattern(doc, seed.Element, canonicalName);
                }
                catch (Exception ex) when (IsExpectedFillPatternCompactionException(ex))
                {
                    logCallback?.Invoke("");
                    logCallback?.Invoke($"Group {groupIndex + 1}: {string.Join(", ", group.Select(item => item.Name).OrderBy(name => name, StringComparer.Ordinal))}");
                    logCallback?.Invoke($"  Skipped — could not create canonical: {ex.Message}");
                    continue;
                }

                RecordCanonicalPatternCreation(result, canonicalName, logHeader, logCallback);

                RewireGroupReferences(doc, group, groupIndex, duplicateGroups.Count, canonical.Id, indexes, result, logCallback, progressCallback);

                groupsToDelete.Add(group);
            }

            DeleteOriginalPatterns(doc, groupsToDelete, result, logCallback);

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

        private static string CreateGroupLogHeader(int groupIndex, IGrouping<string, FillPatternCandidate> group)
        {
            return $"Group {groupIndex + 1}: {string.Join(", ", group.Select(item => item.Name).OrderBy(name => name, StringComparer.Ordinal))}";
        }

        private static void RecordCanonicalPatternCreation(
            FillPatternCompactionResult result,
            string canonicalName,
            string logHeader,
            Action<string>? logCallback)
        {
            result.CanonicalPatternsCreated++;
            result.CreatedCanonicalNames.Add(canonicalName);

            logCallback?.Invoke("");
            logCallback?.Invoke(logHeader);
            logCallback?.Invoke($"  Created canonical: {canonicalName}");
        }

        private static void RewireGroupReferences(
            Document doc,
            IGrouping<string, FillPatternCandidate> group,
            int groupIndex,
            int totalGroupCount,
            ElementId canonicalId,
            FillPatternCompactionIndexes indexes,
            FillPatternCompactionResult result,
            Action<string>? logCallback,
            Action<double, string>? progressCallback)
        {
            int originalIndex = 0;
            int groupCount = group.Count();
            foreach (FillPatternCandidate original in group)
            {
                int rewired = RewireReferences(
                    doc,
                    original.Id,
                    canonicalId,
                    indexes.MaterialIndex,
                    indexes.FilledRegionIndex,
                    indexes.ParameterIndex,
                    indexes.ViewCategoryIndex,
                    indexes.ViewFilterIndex);
                result.ReferencesRewired += rewired;
                logCallback?.Invoke($"  Rewired from '{original.Name}': {rewired} reachable references");

                originalIndex++;
                double groupProgress = (groupIndex + (originalIndex / (double)groupCount)) / totalGroupCount * 100d;
                progressCallback?.Invoke(
                    Math.Round(groupProgress, 0),
                    $"Compacting group {groupIndex + 1} of {totalGroupCount} ({originalIndex}/{groupCount})");
            }
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

        private static List<IGrouping<string, FillPatternCandidate>> BuildDuplicateGroups(List<FillPatternCandidate> candidates)
        {
            return candidates
                .GroupBy(candidate => candidate.Signature)
                .Where(group => group.Count() > 1)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToList();
        }

        private static FillPatternCompactionIndexes BuildCompactionIndexes(
            Document doc,
            CompactingStylesContext context,
            List<IGrouping<string, FillPatternCandidate>> duplicateGroups,
            Action<string>? logCallback,
            Action<double, string>? progressCallback)
        {
            IReadOnlyList<Material> materials = context.Materials;
            IReadOnlyList<FilledRegionType> filledRegionTypes = context.FilledRegionTypes;
            IReadOnlyList<View> views = context.Views;
            IReadOnlyList<Category> categories = context.Categories;
            HashSet<ElementId> allSourceIds = new HashSet<ElementId>(duplicateGroups.SelectMany(g => g).Select(c => c.Id));

            Dictionary<ElementId, List<(Material Material, FillPatternReferenceSlot Slot)>> materialIndex =
                BuildMaterialPatternIndex(materials, allSourceIds);

            Dictionary<ElementId, List<(FilledRegionType RegionType, FillPatternReferenceSlot Slot)>> filledRegionIndex =
                BuildFilledRegionPatternIndex(filledRegionTypes, allSourceIds);

            logCallback?.Invoke("Building view override index...");
            Dictionary<ElementId, List<(View View, ElementId CategoryId)>> viewCategoryIndex =
                CompactionSharedHelper.BuildViewCategoryOverrideIndex(views, categories, allSourceIds, progressCallback, EnumerateFillPatternIds);

            Dictionary<ElementId, List<(View View, ElementId FilterId)>> viewFilterIndex =
                CompactionSharedHelper.BuildViewFilterOverrideIndex(views, allSourceIds, EnumerateFillPatternIds);

            HashSet<string> existingNames = new HashSet<string>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement))
                    .Cast<FillPatternElement>()
                    .Select(p => p.Name),
                StringComparer.OrdinalIgnoreCase);

            return new FillPatternCompactionIndexes(
                materialIndex,
                filledRegionIndex,
                context.ParameterIndex,
                viewCategoryIndex,
                viewFilterIndex,
                existingNames);
        }

        private static void DeleteOriginalPatterns(
            Document doc,
            IEnumerable<IEnumerable<FillPatternCandidate>> groupsToDelete,
            FillPatternCompactionResult result,
            Action<string>? logCallback)
        {
            foreach (IEnumerable<FillPatternCandidate> group in groupsToDelete)
            {
                foreach (FillPatternCandidate original in group)
                {
                    if (CompactionSharedHelper.TryDeleteElement(doc, original.Id))
                    {
                        result.OriginalPatternsDeleted++;
                        logCallback?.Invoke($"  Deleted original: {original.Name}");
                        continue;
                    }

                    result.BlockedDeletions.Add(original.Name);
                    logCallback?.Invoke($"  Could not delete original: {original.Name}");
                }
            }
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

        private static int RewireReferences(
            Document doc,
            ElementId sourceId,
            ElementId targetId,
            Dictionary<ElementId, List<(Material Material, FillPatternReferenceSlot Slot)>> materialIndex,
            Dictionary<ElementId, List<(FilledRegionType RegionType, FillPatternReferenceSlot Slot)>> filledRegionIndex,
            Dictionary<ElementId, HashSet<ElementId>> paramIndex,
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
            rewired += CompactionSharedHelper.RewireParameterReferencesFromIndex(doc, paramIndex, sourceId, targetId);
            rewired += CompactionSharedHelper.RewireViewCategoryOverridesFromIndex(viewCatIndex, sourceId, targetId, RewriteFillPatternOverrides);
            rewired += CompactionSharedHelper.RewireViewFilterOverridesFromIndex(viewFilterIndex, sourceId, targetId, RewriteFillPatternOverrides);
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

        private static int RewriteFillPatternOverrides(OverrideGraphicSettings overrides, ElementId sourceId, ElementId targetId)
        {
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

            return changed;
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

        private static bool IsExpectedFillPatternCompactionException(Exception ex)
        {
            return ex is ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.ArgumentException
                || ex is RevitExceptions.InvalidOperationException;
        }

        private sealed class FillPatternCompactionIndexes
        {
            public FillPatternCompactionIndexes(
                Dictionary<ElementId, List<(Material Material, FillPatternReferenceSlot Slot)>> materialIndex,
                Dictionary<ElementId, List<(FilledRegionType RegionType, FillPatternReferenceSlot Slot)>> filledRegionIndex,
                Dictionary<ElementId, HashSet<ElementId>> parameterIndex,
                Dictionary<ElementId, List<(View View, ElementId CategoryId)>> viewCategoryIndex,
                Dictionary<ElementId, List<(View View, ElementId FilterId)>> viewFilterIndex,
                HashSet<string> existingNames)
            {
                MaterialIndex = materialIndex;
                FilledRegionIndex = filledRegionIndex;
                ParameterIndex = parameterIndex;
                ViewCategoryIndex = viewCategoryIndex;
                ViewFilterIndex = viewFilterIndex;
                ExistingNames = existingNames;
            }

            public Dictionary<ElementId, List<(Material Material, FillPatternReferenceSlot Slot)>> MaterialIndex { get; }
            public Dictionary<ElementId, List<(FilledRegionType RegionType, FillPatternReferenceSlot Slot)>> FilledRegionIndex { get; }
            public Dictionary<ElementId, HashSet<ElementId>> ParameterIndex { get; }
            public Dictionary<ElementId, List<(View View, ElementId CategoryId)>> ViewCategoryIndex { get; }
            public Dictionary<ElementId, List<(View View, ElementId FilterId)>> ViewFilterIndex { get; }
            public HashSet<string> ExistingNames { get; }
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
