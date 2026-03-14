using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Core.Naming;
using LECG.Models;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class LinePatternCompactionService : ILinePatternCompactionService
    {
        private const string CanonicalPrefix = "LECG-LP-";
        private const double GroupingToleranceMm = 1.0;

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

        public LinePatternCompactionResult Compact(Document doc, CompactingStylesContext? context = null, Action<string>? logCallback = null, Action<double, string>? progressCallback = null)
        {
            return Compact(doc, context, new LegacyProgressReporter(progressCallback, logCallback));
        }

        public LinePatternCompactionResult Compact(Document doc, CompactingStylesContext? context, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(reporter);

            Action<string> logCallback = reporter.Log;
            Action<double, string> progressCallback = (percent, message) => reporter.Report(message, percent);
            context ??= CompactingStylesContext.Create(doc, reporter);

            var result = new LinePatternCompactionResult();
            List<LinePatternCandidate> candidates = CollectCandidates(doc);
            List<LinePatternDuplicateGroup> duplicateGroups = BuildDuplicateGroups(candidates);

            result.DuplicateGroups = duplicateGroups.Count;

            logCallback?.Invoke("");
            logCallback?.Invoke("Scope: Line Patterns");
            logCallback?.Invoke($"Scanned line patterns: {candidates.Count}");
            logCallback?.Invoke($"Duplicate groups found: {duplicateGroups.Count}");

            // Build HashSet of existing line pattern names (needed for both compaction and rename pass)
            HashSet<string> existingNames = new HashSet<string>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(LinePatternElement))
                    .Cast<LinePatternElement>()
                    .Select(p => p.Name),
                StringComparer.OrdinalIgnoreCase);

            if (duplicateGroups.Count > 0)
            {
                CompactDuplicateGroups(doc, context, duplicateGroups, existingNames, result, logCallback, progressCallback);
            }
            else
            {
                logCallback?.Invoke("");
                logCallback?.Invoke("No graphically identical duplicate line patterns were found.");
            }

            // Rename pass: rename ALL remaining line patterns to semantic names
            result.PatternsRenamed = RenameToSemanticNames(doc, existingNames, logCallback);

            progressCallback?.Invoke(100, "Line pattern compaction complete");
            logCallback?.Invoke("");
            logCallback?.Invoke("Line Pattern Summary");
            logCallback?.Invoke("====================");
            logCallback?.Invoke($"Canonical patterns created: {result.CanonicalPatternsCreated}");
            logCallback?.Invoke($"References rewired: {result.ReferencesRewired}");
            logCallback?.Invoke($"Original patterns deleted: {result.OriginalPatternsDeleted}");
            logCallback?.Invoke($"Blocked deletions: {result.BlockedDeletions.Count}");
            logCallback?.Invoke($"Patterns renamed: {result.PatternsRenamed}");

            return result;
        }

        private static void CompactDuplicateGroups(
            Document doc,
            CompactingStylesContext context,
            List<LinePatternDuplicateGroup> duplicateGroups,
            HashSet<string> existingNames,
            LinePatternCompactionResult result,
            Action<string>? logCallback,
            Action<double, string>? progressCallback)
        {
            IReadOnlyList<View> views = context.Views;
            IReadOnlyList<Category> categories = context.Categories;

            // Collect all source IDs that might be referenced
            var allSourceIds = new HashSet<ElementId>(duplicateGroups.SelectMany(g => g.Candidates).Select(c => c.Id));

            Dictionary<ElementId, List<(Category Category, GraphicsStyleType StyleType)>> categoryIndex =
                BuildCategoryPatternIndex(context.AllCategories, allSourceIds);

            Dictionary<ElementId, List<(Element Element, Parameter Parameter)>> paramIndex = context.ParameterIndex;

            // Build reverse index for view category overrides
            logCallback?.Invoke("Building view override index...");
            Dictionary<ElementId, List<(View View, ElementId CategoryId)>> viewCatIndex =
                BuildViewCategoryOverrideIndex(views, categories, allSourceIds, progressCallback,
                    (overrides, id) => overrides.ProjectionLinePatternId == id || overrides.CutLinePatternId == id);

            // Build reverse index for view filter overrides
            Dictionary<ElementId, List<(View View, ElementId FilterId)>> viewFilterIndex =
                BuildViewFilterOverrideIndex(views, allSourceIds,
                    (overrides, id) => overrides.ProjectionLinePatternId == id || overrides.CutLinePatternId == id);

            // Two-phase: rewire all groups, then single Regenerate + delete
            int nextCanonicalIndex = 1;
            var groupData = new List<(string CreatedName, string PreferredName, int CreatedNameIndex, ElementId CanonicalId, IReadOnlyList<LinePatternCandidate> ToDelete)>();

            for (int groupIndex = 0; groupIndex < duplicateGroups.Count; groupIndex++)
            {
                LinePatternDuplicateGroup group = duplicateGroups[groupIndex];
                string preferredName = CreatePreferredCanonicalName(group);

                string logHeader = $"Group {groupIndex + 1}: {string.Join(", ", group.Candidates.Select(item => item.Name).OrderBy(name => name, StringComparer.Ordinal))}";

                // Try to create a new canonical pattern
                LinePatternElement? canonical = null;
                Exception? lastException = null;
                foreach (LinePatternCandidate candidateSeed in group.Candidates)
                {
                    try
                    {
                        string creationName = CreateIndexedCanonicalName(existingNames, ref nextCanonicalIndex);
                        canonical = CreateCanonicalPattern(doc, candidateSeed.Pattern, creationName);

                        result.CanonicalPatternsCreated++;
                        int createdNameIndex = result.CreatedCanonicalNames.Count;
                        result.CreatedCanonicalNames.Add(creationName);

                        logCallback?.Invoke("");
                        logCallback?.Invoke(logHeader);
                        logCallback?.Invoke($"  Created canonical: {creationName}");

                        // Rewire ALL candidates to the new canonical
                        int originalIndex = 0;
                        foreach (LinePatternCandidate original in group.Candidates)
                        {
                            int rewired = RewireReferences(
                                original.Id,
                                canonical.Id,
                                categoryIndex,
                                paramIndex,
                                viewCatIndex,
                                viewFilterIndex);
                            result.ReferencesRewired += rewired;
                            logCallback?.Invoke($"  Rewired from '{original.Name}': {rewired} reachable references");

                            originalIndex++;
                            double groupProgress = (groupIndex + (originalIndex / (double)group.Candidates.Count)) / duplicateGroups.Count * 100d;
                            progressCallback?.Invoke(Math.Round(groupProgress, 0),
                                $"Compacting group {groupIndex + 1} of {duplicateGroups.Count} ({originalIndex}/{group.Candidates.Count})");
                        }

                        groupData.Add((creationName, preferredName, createdNameIndex, canonical.Id, group.Candidates));
                        break;
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                    }
                }

                if (canonical != null)
                {
                    continue;
                }

                // Fallback: use the first existing candidate as canonical instead of creating a new one
                LinePatternCandidate fallback = group.Candidates[0];
                var toDelete = group.Candidates.Skip(1).ToList();

                logCallback?.Invoke("");
                logCallback?.Invoke(logHeader);
                logCallback?.Invoke($"  Cannot recreate pattern ({lastException?.Message}), using existing '{fallback.Name}' as canonical");

                foreach (LinePatternCandidate original in toDelete)
                {
                    int rewired = RewireReferences(
                        original.Id,
                        fallback.Id,
                        categoryIndex,
                        paramIndex,
                        viewCatIndex,
                        viewFilterIndex);
                    result.ReferencesRewired += rewired;
                    logCallback?.Invoke($"  Rewired from '{original.Name}': {rewired} reachable references");
                }

                // CreatedNameIndex = -1 signals fallback (no entry in CreatedCanonicalNames)
                groupData.Add((fallback.Name, preferredName, -1, fallback.Id, toDelete));
            }

            // Single Regenerate before all deletes
            doc.Regenerate();

            foreach (var (createdName, preferredName, createdNameIndex, canonicalId, toDelete) in groupData)
            {
                foreach (LinePatternCandidate original in toDelete)
                {
                    if (TryDeletePattern(doc, original.Id))
                    {
                        result.OriginalPatternsDeleted++;
                        existingNames.Remove(original.Name);
                        logCallback?.Invoke($"  Deleted original: {original.Name}");
                        continue;
                    }

                    result.BlockedDeletions.Add(original.Name);
                    logCallback?.Invoke($"  Could not delete original: {original.Name}");
                }

                if (createdNameIndex >= 0)
                {
                    string finalizedName = FinalizeCanonicalName(doc, canonicalId, createdName, preferredName, existingNames);
                    if (!string.Equals(finalizedName, createdName, StringComparison.Ordinal))
                    {
                        result.CreatedCanonicalNames[createdNameIndex] = finalizedName;
                        logCallback?.Invoke($"  Renamed canonical: {finalizedName}");
                    }
                }
            }
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
                    IndexCategoryOverrides(view, categories, sourceIds, matchesAny, index);
                }
                catch
                {
                }
            }

            return index;
        }

        private static void IndexCategoryOverrides(
            View view,
            IReadOnlyList<Category> categories,
            HashSet<ElementId> sourceIds,
            Func<OverrideGraphicSettings, ElementId, bool> matchesAny,
            Dictionary<ElementId, List<(View, ElementId)>> index)
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
                foreach (ElementId sourceId in EnumerateLinePatternIds(overrides))
                {
                    if (sourceIds.Contains(sourceId) && matchesAny(overrides, sourceId))
                    {
                        if (!index.TryGetValue(sourceId, out List<(View, ElementId)>? list))
                        {
                            list = new List<(View, ElementId)>();
                            index[sourceId] = list;
                        }

                        list.Add((view, categoryId));
                        break; // This (view, category) pair is already indexed
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
                        foreach (ElementId sourceId in EnumerateLinePatternIds(overrides))
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

        private static List<LinePatternCandidate> CollectCandidates(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(LinePatternElement))
                .Cast<LinePatternElement>()
                .Where(element => element.Id != LinePatternElement.GetSolidPatternId())
                .Select(element => new
                {
                    Element = element,
                    Pattern = element.GetLinePattern()
                })
                .Where(item => item.Pattern != null)
                .Select(item => CreateCandidate(item.Element, item.Pattern!))
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Signature))
                .ToList();
        }

        private static List<LinePatternDuplicateGroup> BuildDuplicateGroups(List<LinePatternCandidate> candidates)
        {
            var duplicateGroups = new List<LinePatternDuplicateGroup>();

            foreach (IGrouping<string, LinePatternCandidate> typeGroup in candidates
                .GroupBy(candidate => candidate.TypeSignature)
                .OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                string? familyName = typeGroup.First().FamilyName;
                duplicateGroups.AddRange(BuildToleranceGroups(typeGroup.ToList(), familyName));
            }

            return duplicateGroups
                .OrderBy(group => group.SortKey, StringComparer.Ordinal)
                .ToList();
        }

        private static IEnumerable<LinePatternDuplicateGroup> BuildToleranceGroups(
            List<LinePatternCandidate> candidates,
            string? familyName)
        {
            List<LinePatternCandidate> ordered = candidates
                .OrderBy(candidate => candidate, Comparer<LinePatternCandidate>.Create(CompareCandidatesByLengths))
                .ToList();

            var currentGroup = new List<LinePatternCandidate>();
            IReadOnlyList<double> currentAverage = Array.Empty<double>();

            foreach (LinePatternCandidate candidate in ordered)
            {
                if (currentGroup.Count == 0)
                {
                    currentGroup.Add(candidate);
                    currentAverage = candidate.SegmentLengthsMm;
                    continue;
                }

                if (LinePatternNamingPolicy.IsWithinTolerance(currentAverage, candidate.SegmentLengthsMm, GroupingToleranceMm))
                {
                    currentGroup.Add(candidate);
                    currentAverage = LinePatternNamingPolicy.AverageLengthsMm(currentGroup.Select(item => item.SegmentLengthsMm));
                    continue;
                }

                if (currentGroup.Count > 1)
                {
                    yield return CreateToleranceGroup(familyName, currentGroup);
                }

                currentGroup = new List<LinePatternCandidate> { candidate };
                currentAverage = candidate.SegmentLengthsMm;
            }

            if (currentGroup.Count > 1)
            {
                yield return CreateToleranceGroup(familyName, currentGroup);
            }
        }

        private static LinePatternDuplicateGroup CreateToleranceGroup(
            string? familyName,
            List<LinePatternCandidate> candidates)
        {
            IReadOnlyList<double> averageLengths = LinePatternNamingPolicy.AverageLengthsMm(candidates.Select(item => item.SegmentLengthsMm));
            string sortKey = $"{candidates[0].TypeSignature}:{string.Join("|", averageLengths.Select(value => value.ToString("0.####", CultureInfo.InvariantCulture)))}";
            return new LinePatternDuplicateGroup(familyName, candidates, averageLengths, sortKey);
        }

        private static int CompareCandidatesByLengths(LinePatternCandidate left, LinePatternCandidate right)
        {
            int compareCount = Math.Min(left.SegmentLengthsMm.Count, right.SegmentLengthsMm.Count);
            for (int i = 0; i < compareCount; i++)
            {
                int lengthComparison = left.SegmentLengthsMm[i].CompareTo(right.SegmentLengthsMm[i]);
                if (lengthComparison != 0)
                {
                    return lengthComparison;
                }
            }

            int countComparison = left.SegmentLengthsMm.Count.CompareTo(right.SegmentLengthsMm.Count);
            if (countComparison != 0)
            {
                return countComparison;
            }

            return StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
        }

        private static LinePatternCandidate CreateCandidate(LinePatternElement element, LinePattern pattern)
        {
            IList<LinePatternSegment> segments = pattern.GetSegments();
            if (segments.Count == 0)
            {
                return new LinePatternCandidate(
                    element.Id,
                    element.Name,
                    pattern,
                    string.Empty,
                    string.Empty,
                    null,
                    Array.Empty<double>());
            }

            var signatureParts = new List<string>(segments.Count);
            var segmentTypes = new List<string>(segments.Count);
            var segmentLengthsMm = new List<double>(segments.Count);

            foreach (LinePatternSegment segment in segments)
            {
                double length = Math.Round(segment.Length, 6);
                signatureParts.Add($"{segment.Type}:{length.ToString("0.######", CultureInfo.InvariantCulture)}");
                segmentTypes.Add(segment.Type.ToString());
                segmentLengthsMm.Add(UnitUtils.ConvertFromInternalUnits(segment.Length, UnitTypeId.Millimeters));
            }

            return new LinePatternCandidate(
                element.Id,
                element.Name,
                pattern,
                string.Join("|", signatureParts),
                LinePatternNamingPolicy.CreateTypeSignature(segmentTypes),
                LinePatternNamingPolicy.CreateFamilyName(segmentTypes),
                segmentLengthsMm);
        }

        private static string CreatePreferredCanonicalName(LinePatternDuplicateGroup group)
        {
            if (LinePatternNamingPolicy.IsSemanticFamily(group.FamilyName))
            {
                return LinePatternNamingPolicy.CreateSemanticName(group.FamilyName!, group.AverageLengthsMm);
            }

            return string.Empty;
        }

        private static string FinalizeCanonicalName(
            Document doc,
            ElementId canonicalId,
            string createdName,
            string preferredName,
            HashSet<string> existingNames)
        {
            if (string.IsNullOrWhiteSpace(preferredName) || string.Equals(createdName, preferredName, StringComparison.Ordinal))
            {
                return createdName;
            }

            if (existingNames.Contains(preferredName))
            {
                return createdName;
            }

            if (!TryRenamePattern(doc, canonicalId, preferredName))
            {
                return createdName;
            }

            existingNames.Remove(createdName);
            existingNames.Add(preferredName);
            return preferredName;
        }

        private static bool TryRenamePattern(Document doc, ElementId canonicalId, string preferredName)
        {
            try
            {
                Element? element = doc.GetElement(canonicalId);
                if (element == null || !element.IsValidObject)
                {
                    return false;
                }

                element.Name = preferredName;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static LinePatternElement CreateCanonicalPattern(Document doc, LinePattern sourcePattern, string canonicalName)
        {
            var linePattern = new LinePattern(canonicalName);
            linePattern.SetSegments(sourcePattern.GetSegments().ToList());
            return LinePatternElement.Create(doc, linePattern);
        }

        private static string CreateIndexedCanonicalName(HashSet<string> existingNames, ref int nextCanonicalIndex)
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

        private static int RenameToSemanticNames(
            Document doc,
            HashSet<string> existingNames,
            Action<string>? logCallback)
        {
            var allPatterns = new FilteredElementCollector(doc)
                .OfClass(typeof(LinePatternElement))
                .Cast<LinePatternElement>()
                .Where(e => e.Id != LinePatternElement.GetSolidPatternId())
                .ToList();

            int renamed = 0;

            foreach (LinePatternElement element in allPatterns)
            {
                LinePattern? pattern = element.GetLinePattern();
                if (pattern == null)
                {
                    continue;
                }

                IList<LinePatternSegment> segments = pattern.GetSegments();
                if (segments.Count == 0)
                {
                    continue;
                }

                var segmentTypes = segments.Select(s => s.Type.ToString()).ToList();
                string? familyName = LinePatternNamingPolicy.CreateFamilyName(segmentTypes);
                if (familyName == null)
                {
                    continue;
                }

                var lengthsMm = segments
                    .Select(s => UnitUtils.ConvertFromInternalUnits(s.Length, UnitTypeId.Millimeters))
                    .ToList();

                string preferredName = LinePatternNamingPolicy.CreateSemanticName(familyName, lengthsMm);

                if (string.Equals(element.Name, preferredName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (existingNames.Contains(preferredName))
                {
                    continue;
                }

                try
                {
                    string oldName = element.Name;
                    element.Name = preferredName;
                    existingNames.Remove(oldName);
                    existingNames.Add(preferredName);
                    renamed++;
                    logCallback?.Invoke($"  Renamed: {oldName} → {preferredName}");
                }
                catch
                {
                    // Revit may reject the rename
                }
            }

            return renamed;
        }

        private static int RewireReferences(
            ElementId sourceId,
            ElementId targetId,
            Dictionary<ElementId, List<(Category Category, GraphicsStyleType StyleType)>> categoryIndex,
            Dictionary<ElementId, List<(Element Element, Parameter Parameter)>> paramIndex,
            Dictionary<ElementId, List<(View View, ElementId CategoryId)>> viewCatIndex,
            Dictionary<ElementId, List<(View View, ElementId FilterId)>> viewFilterIndex)
        {
            if (sourceId == targetId)
            {
                return 0;
            }

            int rewired = 0;
            rewired += RewireCategoryReferencesFromIndex(categoryIndex, sourceId, targetId);
            rewired += RewireParameterReferencesFromIndex(paramIndex, sourceId, targetId);
            rewired += RewireViewCategoryOverridesFromIndex(viewCatIndex, sourceId, targetId);
            rewired += RewireViewFilterOverridesFromIndex(viewFilterIndex, sourceId, targetId);
            return rewired;
        }

        private static Dictionary<ElementId, List<(Category Category, GraphicsStyleType StyleType)>> BuildCategoryPatternIndex(
            IReadOnlyList<Category> categories,
            HashSet<ElementId> sourceIds)
        {
            var index = new Dictionary<ElementId, List<(Category, GraphicsStyleType)>>();
            foreach (Category category in categories)
            {
                IndexCategoryPattern(category, GraphicsStyleType.Projection, sourceIds, index);
                IndexCategoryPattern(category, GraphicsStyleType.Cut, sourceIds, index);
            }

            return index;
        }

        private static void IndexCategoryPattern(
            Category category,
            GraphicsStyleType styleType,
            HashSet<ElementId> sourceIds,
            Dictionary<ElementId, List<(Category Category, GraphicsStyleType StyleType)>> index)
        {
            try
            {
                ElementId patternId = category.GetLinePatternId(styleType);
                if (patternId == ElementId.InvalidElementId || !sourceIds.Contains(patternId))
                {
                    return;
                }

                if (!index.TryGetValue(patternId, out List<(Category, GraphicsStyleType)>? entries))
                {
                    entries = new List<(Category, GraphicsStyleType)>();
                    index[patternId] = entries;
                }

                entries.Add((category, styleType));
            }
            catch
            {
            }
        }

        private static int RewireCategoryReferencesFromIndex(
            Dictionary<ElementId, List<(Category Category, GraphicsStyleType StyleType)>> categoryIndex,
            ElementId sourceId,
            ElementId targetId)
        {
            if (!categoryIndex.TryGetValue(sourceId, out List<(Category Category, GraphicsStyleType StyleType)>? entries))
            {
                return 0;
            }

            int rewired = 0;
            var movedToTarget = new List<(Category, GraphicsStyleType)>();

            foreach (var (category, styleType) in entries)
            {
                try
                {
                    if (category.GetLinePatternId(styleType) != sourceId)
                    {
                        continue;
                    }

                    category.SetLinePatternId(targetId, styleType);
                    rewired++;
                    movedToTarget.Add((category, styleType));
                }
                catch
                {
                }
            }

            categoryIndex.Remove(sourceId);

            if (movedToTarget.Count > 0)
            {
                if (!categoryIndex.TryGetValue(targetId, out List<(Category, GraphicsStyleType)>? targetEntries))
                {
                    targetEntries = new List<(Category, GraphicsStyleType)>();
                    categoryIndex[targetId] = targetEntries;
                }

                targetEntries.AddRange(movedToTarget);
            }

            return rewired;
        }

        private static IEnumerable<ElementId> EnumerateLinePatternIds(OverrideGraphicSettings overrides)
        {
            ElementId projectionId = overrides.ProjectionLinePatternId;
            if (projectionId != ElementId.InvalidElementId)
            {
                yield return projectionId;
            }

            ElementId cutId = overrides.CutLinePatternId;
            if (cutId != ElementId.InvalidElementId && cutId != projectionId)
            {
                yield return cutId;
            }
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

                    if (overrides.ProjectionLinePatternId == sourceId)
                    {
                        overrides.SetProjectionLinePatternId(targetId);
                        changed++;
                    }

                    if (overrides.CutLinePatternId == sourceId)
                    {
                        overrides.SetCutLinePatternId(targetId);
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

                    if (overrides.ProjectionLinePatternId == sourceId)
                    {
                        overrides.SetProjectionLinePatternId(targetId);
                        changed++;
                    }

                    if (overrides.CutLinePatternId == sourceId)
                    {
                        overrides.SetCutLinePatternId(targetId);
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

        private sealed class LinePatternCandidate
        {
            public LinePatternCandidate(
                ElementId id,
                string name,
                LinePattern pattern,
                string signature,
                string typeSignature,
                string? familyName,
                IReadOnlyList<double> segmentLengthsMm)
            {
                Id = id;
                Name = name;
                Pattern = pattern;
                Signature = signature;
                TypeSignature = typeSignature;
                FamilyName = familyName;
                SegmentLengthsMm = segmentLengthsMm;
            }

            public ElementId Id { get; }
            public string Name { get; }
            public LinePattern Pattern { get; }
            public string Signature { get; }
            public string TypeSignature { get; }
            public string? FamilyName { get; }
            public IReadOnlyList<double> SegmentLengthsMm { get; }
        }

        private sealed class LinePatternDuplicateGroup
        {
            public LinePatternDuplicateGroup(
                string? familyName,
                IReadOnlyList<LinePatternCandidate> candidates,
                IReadOnlyList<double> averageLengthsMm,
                string sortKey)
            {
                FamilyName = familyName;
                Candidates = candidates;
                AverageLengthsMm = averageLengthsMm;
                SortKey = sortKey;
            }

            public string? FamilyName { get; }
            public IReadOnlyList<LinePatternCandidate> Candidates { get; }
            public IReadOnlyList<double> AverageLengthsMm { get; }
            public string SortKey { get; }
        }
    }
}
