using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Configuration;
using LECG.Models;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class LineStyleCompactionService : ILineStyleCompactionService
    {
        public LineStyleCompactionResult Compact(Document doc, CompactingStylesContext? context = null, Action<string>? logCallback = null, Action<double, string>? progressCallback = null)
        {
            return Compact(doc, context, new LegacyProgressReporter(progressCallback, logCallback));
        }

        public LineStyleCompactionResult Compact(Document doc, CompactingStylesContext? context, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(reporter);

            Action<string> logCallback = reporter.Log;
            Action<double, string> progressCallback = (percent, message) => reporter.Report(message, percent);
            context ??= CompactingStylesContext.Create(doc, reporter);

            var result = new LineStyleCompactionResult();
            List<LineStyleCandidate> candidates = CollectCandidates(doc);
            List<LineStyleDuplicateGroup> duplicateGroups = BuildDuplicateGroups(candidates);

            result.DuplicateGroups = duplicateGroups.Count;

            logCallback?.Invoke("");
            logCallback?.Invoke("Scope: Line Styles");
            logCallback?.Invoke($"Scanned line styles: {candidates.Count}");
            logCallback?.Invoke($"Duplicate groups found: {duplicateGroups.Count}");

            if (duplicateGroups.Count == 0)
            {
                progressCallback?.Invoke(100, "No duplicate line styles found");
                logCallback?.Invoke("");
                logCallback?.Invoke("No graphically identical duplicate line styles were found.");
                return result;
            }

            Dictionary<ElementId, HashSet<ElementId>> paramIndex = context.ParameterIndex;

            for (int groupIndex = 0; groupIndex < duplicateGroups.Count; groupIndex++)
            {
                LineStyleDuplicateGroup group = duplicateGroups[groupIndex];
                LineStyleCandidate survivor = group.Survivor;

                logCallback?.Invoke("");
                logCallback?.Invoke($"Group {groupIndex + 1}: {string.Join(", ", group.Candidates.Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal))}");
                logCallback?.Invoke($"  Survivor: {survivor.Name}");

                result.SurvivorNames.Add(survivor.Name);

                // Rewire references for each non-survivor
                int candidateIndex = 0;
                foreach (LineStyleCandidate original in group.Candidates)
                {
                    if (original.SubcategoryId == survivor.SubcategoryId)
                    {
                        candidateIndex++;
                        continue;
                    }

                    int rewired = RewireReferences(doc, original, survivor, paramIndex);
                    result.ReferencesRewired += rewired;
                    logCallback?.Invoke($"  Rewired from '{original.Name}': {rewired} references");

                    candidateIndex++;
                    double groupProgress = (groupIndex + (candidateIndex / (double)group.Candidates.Count)) / duplicateGroups.Count * 100d;
                    progressCallback?.Invoke(Math.Round(groupProgress, 0),
                        $"Compacting group {groupIndex + 1} of {duplicateGroups.Count} ({candidateIndex}/{group.Candidates.Count})");
                }
            }

            // Delete non-survivors
            foreach (LineStyleDuplicateGroup group in duplicateGroups)
            {
                foreach (LineStyleCandidate original in group.Candidates)
                {
                    if (original.SubcategoryId == group.Survivor.SubcategoryId)
                    {
                        continue;
                    }

                    if (CompactionSharedHelper.TryDeleteElement(doc, original.SubcategoryId))
                    {
                        result.OriginalStylesDeleted++;
                        logCallback?.Invoke($"  Deleted original: {original.Name}");
                    }
                    else
                    {
                        result.BlockedDeletions.Add(original.Name);
                        logCallback?.Invoke($"  Could not delete original: {original.Name}");
                    }
                }
            }

            progressCallback?.Invoke(100, "Line style compaction complete");
            logCallback?.Invoke("");
            logCallback?.Invoke("Line Style Summary");
            logCallback?.Invoke("==================");
            logCallback?.Invoke($"Duplicate groups: {result.DuplicateGroups}");
            logCallback?.Invoke($"References rewired: {result.ReferencesRewired}");
            logCallback?.Invoke($"Original styles deleted: {result.OriginalStylesDeleted}");
            logCallback?.Invoke($"Blocked deletions: {result.BlockedDeletions.Count}");

            return result;
        }

        private static List<LineStyleCandidate> CollectCandidates(Document doc)
        {
            var candidates = new List<LineStyleCandidate>();

            Category? linesCategory = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
            if (linesCategory == null)
            {
                return candidates;
            }

            foreach (Category subCat in linesCategory.SubCategories)
            {
                if (RevitConstants.IsBuiltInLineStyle(subCat.Name))
                {
                    continue;
                }

                try
                {
                    ElementId patternId = subCat.GetLinePatternId(GraphicsStyleType.Projection);
                    int weight = subCat.GetLineWeight(GraphicsStyleType.Projection) ?? 1;
                    Color color = subCat.LineColor;
                    string colorKey = color != null && color.IsValid
                        ? $"{color.Red},{color.Green},{color.Blue}"
                        : "0,0,0";

                    string signature = $"{patternId}|{weight}|{colorKey}";

                    GraphicsStyle? graphicsStyle = subCat.GetGraphicsStyle(GraphicsStyleType.Projection);

                    candidates.Add(new LineStyleCandidate(
                        subCat.Id,
                        subCat.Name,
                        signature,
                        graphicsStyle));
                }
                catch
                {
                    // Skip subcategories that fail to read properties
                }
            }

            return candidates;
        }

        private static List<LineStyleDuplicateGroup> BuildDuplicateGroups(List<LineStyleCandidate> candidates)
        {
            return candidates
                .GroupBy(c => c.Signature)
                .Where(g => g.Count() > 1)
                .Select(g =>
                {
                    List<LineStyleCandidate> members = g.ToList();
                    LineStyleCandidate survivor = PickSurvivor(members);
                    return new LineStyleDuplicateGroup(members, survivor);
                })
                .OrderBy(g => g.Survivor.Name, StringComparer.Ordinal)
                .ToList();
        }

        private static LineStyleCandidate PickSurvivor(List<LineStyleCandidate> candidates)
        {
            ArgumentNullException.ThrowIfNull(candidates);
            if (candidates.Count == 0)
            {
                throw new ArgumentException("At least one line style candidate is required.", nameof(candidates));
            }

            return candidates
                .OrderBy(c => c.Name.Length)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .First();
        }

        private static int RewireReferences(
            Document doc,
            LineStyleCandidate source,
            LineStyleCandidate survivor,
            Dictionary<ElementId, HashSet<ElementId>> paramIndex)
        {
            int rewired = 0;

            // Rewire CurveElement.LineStyle references
            if (survivor.GraphicsStyle != null)
            {
                rewired += RewireCurveElements(doc, source, survivor);
            }

            // Rewire parameter references pointing to the subcategory ID
            rewired += CompactionSharedHelper.RewireParameterReferencesFromIndex(doc, paramIndex, source.SubcategoryId, survivor.SubcategoryId);

            // Rewire parameter references pointing to the GraphicsStyle ID
            if (source.GraphicsStyle != null && survivor.GraphicsStyle != null)
            {
                rewired += CompactionSharedHelper.RewireParameterReferencesFromIndex(doc, paramIndex, source.GraphicsStyle.Id, survivor.GraphicsStyle.Id);
            }

            return rewired;
        }

        private static int RewireCurveElements(Document doc, LineStyleCandidate source, LineStyleCandidate survivor)
        {
            int rewired = 0;

            var curves = new FilteredElementCollector(doc).OfClass(typeof(CurveElement));
            foreach (CurveElement curve in curves)
            {
                try
                {
                    if (curve.LineStyle is GraphicsStyle gs &&
                        gs.GraphicsStyleCategory != null &&
                        gs.GraphicsStyleCategory.Id == source.SubcategoryId)
                    {
                        curve.LineStyle = survivor.GraphicsStyle;
                        rewired++;
                    }
                }
                catch
                {
                }
            }

            return rewired;
        }

        private sealed class LineStyleCandidate
        {
            public LineStyleCandidate(ElementId subcategoryId, string name, string signature, GraphicsStyle? graphicsStyle)
            {
                SubcategoryId = subcategoryId;
                Name = name;
                Signature = signature;
                GraphicsStyle = graphicsStyle;
            }

            public ElementId SubcategoryId { get; }
            public string Name { get; }
            public string Signature { get; }
            public GraphicsStyle? GraphicsStyle { get; }
        }

        private sealed class LineStyleDuplicateGroup
        {
            public LineStyleDuplicateGroup(List<LineStyleCandidate> candidates, LineStyleCandidate survivor)
            {
                Candidates = candidates;
                Survivor = survivor;
            }

            public List<LineStyleCandidate> Candidates { get; }
            public LineStyleCandidate Survivor { get; }
        }
    }
}
