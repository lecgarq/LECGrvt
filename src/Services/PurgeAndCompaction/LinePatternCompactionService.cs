using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Core.Naming;
using LECG.Models;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class LinePatternCompactionService
    {
        private const string CanonicalPrefix = "LECG-LP-";
        private const double GroupingToleranceMm = 1.0;
        private readonly ILogger _logger;

        public LinePatternCompactionService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        private void CompactDuplicateGroups(
            Document doc,
            CompactingStylesContext context,
            List<LinePatternDuplicateGroup> duplicateGroups,
            HashSet<string> existingNames,
            LinePatternCompactionResult result,
            Action<string>? logCallback,
            Action<double, string>? progressCallback)
        {
            LinePatternCompactionIndexes indexes = BuildCompactionIndexes(context, duplicateGroups, logCallback, progressCallback);

            int nextCanonicalIndex = 1;
            var groupData = new List<LinePatternCompactionGroupData>();

            for (int groupIndex = 0; groupIndex < duplicateGroups.Count; groupIndex++)
            {
                LinePatternCompactionGroupData? compactedGroup = CompactDuplicateGroup(
                    doc,
                    duplicateGroups[groupIndex],
                    groupIndex,
                    duplicateGroups.Count,
                    existingNames,
                    indexes,
                    result,
                    ref nextCanonicalIndex,
                    logCallback,
                    progressCallback);
                if (compactedGroup != null)
                {
                    groupData.Add(compactedGroup);
                }
            }

            FinalizeCompactedGroups(doc, existingNames, result, groupData, logCallback);
        }

        private LinePatternCompactionGroupData? CompactDuplicateGroup(
            Document doc,
            LinePatternDuplicateGroup group,
            int groupIndex,
            int totalGroupCount,
            HashSet<string> existingNames,
            LinePatternCompactionIndexes indexes,
            LinePatternCompactionResult result,
            ref int nextCanonicalIndex,
            Action<string>? logCallback,
            Action<double, string>? progressCallback)
        {
            string preferredName = CreatePreferredCanonicalName(group);
            string logHeader = $"Group {groupIndex + 1}: {string.Join(", ", group.Candidates.Select(item => item.Name).OrderBy(name => name, StringComparer.Ordinal))}";

            LinePatternElement? canonical = null;
            Exception? lastException = null;
            foreach (LinePatternCandidate candidateSeed in group.Candidates)
            {
                try
                {
                    string creationName = CompactionSharedHelper.CreateCanonicalName(CanonicalPrefix, existingNames, ref nextCanonicalIndex);
                    canonical = CreateCanonicalPattern(doc, candidateSeed.Pattern, creationName);

                    result.CanonicalPatternsCreated++;
                    int createdNameIndex = result.CreatedCanonicalNames.Count;
                    result.CreatedCanonicalNames.Add(creationName);

                    logCallback?.Invoke("");
                    logCallback?.Invoke(logHeader);
                    logCallback?.Invoke($"  Created canonical: {creationName}");

                    RewireGroupReferences(
                        doc,
                        group,
                        groupIndex,
                        totalGroupCount,
                        canonical.Id,
                        indexes,
                        result,
                        logCallback,
                        progressCallback);

                    return new LinePatternCompactionGroupData(
                        creationName,
                        preferredName,
                        createdNameIndex,
                        canonical.Id,
                        group.Candidates);
                }
                catch (Exception ex) when (IsExpectedLinePatternCompactionException(ex))
                {
                    lastException = ex;
                }
            }

            if (group.Candidates.Count == 0)
            {
                return null;
            }

            LinePatternCandidate fallback = group.Candidates[0];
            IReadOnlyList<LinePatternCandidate> toDelete = group.Candidates.Skip(1).ToList();

            logCallback?.Invoke("");
            logCallback?.Invoke(logHeader);
            logCallback?.Invoke($"  Cannot recreate pattern ({lastException?.Message}), using existing '{fallback.Name}' as canonical");

            foreach (LinePatternCandidate original in toDelete)
            {
                int rewired = RewireReferences(
                    doc,
                    original.Id,
                    fallback.Id,
                    indexes.CategoryIndex,
                    indexes.ParameterIndex,
                    indexes.ViewCategoryIndex,
                    indexes.ViewFilterIndex);
                result.ReferencesRewired += rewired;
                logCallback?.Invoke($"  Rewired from '{original.Name}': {rewired} reachable references");
            }

            return new LinePatternCompactionGroupData(
                fallback.Name,
                preferredName,
                -1,
                fallback.Id,
                toDelete);
        }

        private void RewireGroupReferences(
            Document doc,
            LinePatternDuplicateGroup group,
            int groupIndex,
            int totalGroupCount,
            ElementId canonicalId,
            LinePatternCompactionIndexes indexes,
            LinePatternCompactionResult result,
            Action<string>? logCallback,
            Action<double, string>? progressCallback)
        {
            int originalIndex = 0;
            foreach (LinePatternCandidate original in group.Candidates)
            {
                int rewired = RewireReferences(
                    doc,
                    original.Id,
                    canonicalId,
                    indexes.CategoryIndex,
                    indexes.ParameterIndex,
                    indexes.ViewCategoryIndex,
                    indexes.ViewFilterIndex);
                result.ReferencesRewired += rewired;
                logCallback?.Invoke($"  Rewired from '{original.Name}': {rewired} reachable references");

                originalIndex++;
                double groupProgress = (groupIndex + (originalIndex / (double)group.Candidates.Count)) / totalGroupCount * 100d;
                progressCallback?.Invoke(
                    Math.Round(groupProgress, 0),
                    $"Compacting group {groupIndex + 1} of {totalGroupCount} ({originalIndex}/{group.Candidates.Count})");
            }
        }

        private static void FinalizeCompactedGroups(
            Document doc,
            HashSet<string> existingNames,
            LinePatternCompactionResult result,
            IReadOnlyList<LinePatternCompactionGroupData> groupData,
            Action<string>? logCallback)
        {
            foreach (LinePatternCompactionGroupData group in groupData)
            {
                foreach (LinePatternCandidate original in group.ToDelete)
                {
                    if (CompactionSharedHelper.TryDeleteElement(doc, original.Id))
                    {
                        result.OriginalPatternsDeleted++;
                        existingNames.Remove(original.Name);
                        logCallback?.Invoke($"  Deleted original: {original.Name}");
                        continue;
                    }

                    result.BlockedDeletions.Add(original.Name);
                    logCallback?.Invoke($"  Could not delete original: {original.Name}");
                }

                if (group.CreatedNameIndex < 0)
                {
                    continue;
                }

                string finalizedName = FinalizeCanonicalName(doc, group.CanonicalId, group.CreatedName, group.PreferredName, existingNames);
                if (!string.Equals(finalizedName, group.CreatedName, StringComparison.Ordinal))
                {
                    result.CreatedCanonicalNames[group.CreatedNameIndex] = finalizedName;
                    logCallback?.Invoke($"  Renamed canonical: {finalizedName}");
                }
            }
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
            ArgumentNullException.ThrowIfNull(candidates);
            if (candidates.Count == 0)
            {
                throw new ArgumentException("At least one line pattern candidate is required.", nameof(candidates));
            }

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

        private LinePatternCompactionIndexes BuildCompactionIndexes(
            CompactingStylesContext context,
            List<LinePatternDuplicateGroup> duplicateGroups,
            Action<string>? logCallback,
            Action<double, string>? progressCallback)
        {
            IReadOnlyList<View> views = context.Views;
            IReadOnlyList<Category> categories = context.Categories;
            HashSet<ElementId> allSourceIds = new HashSet<ElementId>(duplicateGroups.SelectMany(g => g.Candidates).Select(c => c.Id));

            Dictionary<ElementId, List<(Category Category, GraphicsStyleType StyleType)>> categoryIndex =
                BuildCategoryPatternIndex(context.AllCategories, allSourceIds);

            logCallback?.Invoke("Building view override index...");
            Dictionary<ElementId, List<(View View, ElementId CategoryId)>> viewCategoryIndex =
                CompactionSharedHelper.BuildViewCategoryOverrideIndex(views, categories, allSourceIds, progressCallback, EnumerateLinePatternIds);

            Dictionary<ElementId, List<(View View, ElementId FilterId)>> viewFilterIndex =
                CompactionSharedHelper.BuildViewFilterOverrideIndex(views, allSourceIds, EnumerateLinePatternIds);

            return new LinePatternCompactionIndexes(
                categoryIndex,
                context.ParameterIndex,
                viewCategoryIndex,
                viewFilterIndex);
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
                    logCallback?.Invoke($"  Renamed: {oldName} -> {preferredName}");
                }
                catch
                {
                    // Revit may reject the rename
                }
            }

            return renamed;
        }

        private int RewireReferences(
            Document doc,
            ElementId sourceId,
            ElementId targetId,
            Dictionary<ElementId, List<(Category Category, GraphicsStyleType StyleType)>> categoryIndex,
            Dictionary<ElementId, HashSet<ElementId>> paramIndex,
            Dictionary<ElementId, List<(View View, ElementId CategoryId)>> viewCatIndex,
            Dictionary<ElementId, List<(View View, ElementId FilterId)>> viewFilterIndex)
        {
            if (sourceId == targetId)
            {
                return 0;
            }

            int rewired = 0;
            rewired += RewireCategoryReferencesFromIndex(categoryIndex, sourceId, targetId);
            rewired += CompactionSharedHelper.RewireParameterReferencesFromIndex(doc, paramIndex, sourceId, targetId);
            rewired += CompactionSharedHelper.RewireViewCategoryOverridesFromIndex(viewCatIndex, sourceId, targetId, RewriteLinePatternOverrides);
            rewired += CompactionSharedHelper.RewireViewFilterOverridesFromIndex(viewFilterIndex, sourceId, targetId, RewriteLinePatternOverrides);
            return rewired;
        }

        private Dictionary<ElementId, List<(Category Category, GraphicsStyleType StyleType)>> BuildCategoryPatternIndex(
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

        private void IndexCategoryPattern(
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
            catch (Exception ex) when (IsExpectedLinePatternCompactionException(ex))
            {
                _logger.LogWarning($"IndexCategoryPattern: {ex.Message}", scope: "LinePatternCompaction");
            }
        }

        private int RewireCategoryReferencesFromIndex(
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
                catch (Exception ex) when (IsExpectedLinePatternCompactionException(ex))
                {
                    _logger.LogWarning($"RewireCategoryReferences: {ex.Message}", scope: "LinePatternCompaction");
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

        private static bool IsExpectedLinePatternCompactionException(Exception ex)
        {
            return ex is ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.ArgumentException
                || ex is RevitExceptions.InvalidOperationException;
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

        private static int RewriteLinePatternOverrides(OverrideGraphicSettings overrides, ElementId sourceId, ElementId targetId)
        {
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

            return changed;
        }

        private sealed class LinePatternCompactionIndexes
        {
            public LinePatternCompactionIndexes(
                Dictionary<ElementId, List<(Category Category, GraphicsStyleType StyleType)>> categoryIndex,
                Dictionary<ElementId, HashSet<ElementId>> parameterIndex,
                Dictionary<ElementId, List<(View View, ElementId CategoryId)>> viewCategoryIndex,
                Dictionary<ElementId, List<(View View, ElementId FilterId)>> viewFilterIndex)
            {
                CategoryIndex = categoryIndex;
                ParameterIndex = parameterIndex;
                ViewCategoryIndex = viewCategoryIndex;
                ViewFilterIndex = viewFilterIndex;
            }

            public Dictionary<ElementId, List<(Category Category, GraphicsStyleType StyleType)>> CategoryIndex { get; }
            public Dictionary<ElementId, HashSet<ElementId>> ParameterIndex { get; }
            public Dictionary<ElementId, List<(View View, ElementId CategoryId)>> ViewCategoryIndex { get; }
            public Dictionary<ElementId, List<(View View, ElementId FilterId)>> ViewFilterIndex { get; }
        }

        private sealed class LinePatternCompactionGroupData
        {
            public LinePatternCompactionGroupData(
                string createdName,
                string preferredName,
                int createdNameIndex,
                ElementId canonicalId,
                IReadOnlyList<LinePatternCandidate> toDelete)
            {
                CreatedName = createdName;
                PreferredName = preferredName;
                CreatedNameIndex = createdNameIndex;
                CanonicalId = canonicalId;
                ToDelete = toDelete;
            }

            public string CreatedName { get; }
            public string PreferredName { get; }
            public int CreatedNameIndex { get; }
            public ElementId CanonicalId { get; }
            public IReadOnlyList<LinePatternCandidate> ToDelete { get; }
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
