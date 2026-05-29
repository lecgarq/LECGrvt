using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Models;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class TextStyleCompactionService : ITextStyleCompactionService
    {
        private const string CanonicalPrefix = "LECG-TT-";

        public TextStyleCompactionResult Compact(Document doc, CompactingStylesContext? context, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(reporter);

            Action<string> logCallback = reporter.Log;
            Action<double, string> progressCallback = (percent, message) => reporter.Report(message, percent);
            context ??= CompactingStylesContext.Create(doc, reporter);

            var result = new TextStyleCompactionResult();
            ElementId defaultTypeId = doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
            List<TextStyleCandidate> candidates = CollectCandidates(doc, defaultTypeId);
            List<IGrouping<string, TextStyleCandidate>> duplicateGroups = candidates
                .GroupBy(candidate => candidate.Signature)
                .Where(group => group.Count() > 1)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToList();

            result.DuplicateGroups = duplicateGroups.Count;

            logCallback?.Invoke("");
            logCallback?.Invoke("Scope: Text Styles");
            logCallback?.Invoke($"Scanned text types: {candidates.Count}");
            logCallback?.Invoke($"Duplicate groups found: {duplicateGroups.Count}");

            if (duplicateGroups.Count == 0)
            {
                progressCallback?.Invoke(100, "No duplicate text styles found");
                logCallback?.Invoke("No graphically identical duplicate text styles were found.");
                return result;
            }

            IReadOnlyList<TextNote> textNotes = context.TextNotes;
            var allSourceIds = new HashSet<ElementId>(duplicateGroups.SelectMany(g => g).Select(c => c.Id));
            Dictionary<ElementId, List<TextNote>> textNoteIndex = BuildTextNoteIndex(textNotes, allSourceIds);
            Dictionary<ElementId, HashSet<ElementId>> paramIndex = context.ParameterIndex;

            // Step 2: Build HashSet of existing text style names
            HashSet<string> existingNames = new HashSet<string>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(TextNoteType))
                    .Cast<TextNoteType>()
                    .Select(t => t.Name),
                StringComparer.OrdinalIgnoreCase);

            // Step 3: Two-phase approach — rewire all groups, then single Regenerate + delete
            int nextCanonicalIndex = 1;
            var groupData = new List<(string CanonicalName, ElementId CanonicalId, IGrouping<string, TextStyleCandidate> Group)>();

            for (int groupIndex = 0; groupIndex < duplicateGroups.Count; groupIndex++)
            {
                IGrouping<string, TextStyleCandidate> group = duplicateGroups[groupIndex];
                TextStyleCandidate seed = group.First();
                string canonicalName = CompactionSharedHelper.CreateCanonicalName(CanonicalPrefix, existingNames, ref nextCanonicalIndex);

                ElementId canonicalId;
                try
                {
                    canonicalId = CreateCanonicalType(doc, seed.TypeElement, canonicalName);
                }
                catch (Exception ex) when (IsExpectedTextStyleCompactionException(ex))
                {
                    logCallback?.Invoke("");
                    logCallback?.Invoke($"Group {groupIndex + 1}: {string.Join(", ", group.Select(item => item.Name).OrderBy(name => name, StringComparer.Ordinal))}");
                    logCallback?.Invoke($"  Skipped — could not create canonical: {ex.Message}");
                    continue;
                }

                result.CanonicalTypesCreated++;
                result.CreatedCanonicalNames.Add(canonicalName);

                logCallback?.Invoke("");
                logCallback?.Invoke($"Group {groupIndex + 1}: {string.Join(", ", group.Select(item => item.Name).OrderBy(name => name, StringComparer.Ordinal))}");
                logCallback?.Invoke($"  Created canonical: {canonicalName}");

                int originalIndex = 0;
                foreach (TextStyleCandidate original in group)
                {
                    int rewired = RewireReferences(
                        doc,
                        original.Id,
                        canonicalId,
                        textNoteIndex,
                        paramIndex);
                    result.ReferencesRewired += rewired;
                    logCallback?.Invoke($"  Rewired from '{original.Name}': {rewired} reachable references");

                    // Step 5: Progress after each original within a group
                    originalIndex++;
                    double groupProgress = (groupIndex + (originalIndex / (double)group.Count())) / duplicateGroups.Count * 100d;
                    progressCallback?.Invoke(Math.Round(groupProgress, 0),
                        $"Compacting group {groupIndex + 1} of {duplicateGroups.Count} ({originalIndex}/{group.Count()})");
                }

                groupData.Add((canonicalName, canonicalId, group));
            }

            for (int groupIndex = 0; groupIndex < groupData.Count; groupIndex++)
            {
                var (canonicalName, canonicalId, group) = groupData[groupIndex];

                foreach (TextStyleCandidate original in group)
                {
                    if (CompactionSharedHelper.TryDeleteElement(doc, original.Id))
                    {
                        result.OriginalTypesDeleted++;
                        logCallback?.Invoke($"  Deleted original: {original.Name}");
                        continue;
                    }

                    result.BlockedDeletions.Add(original.Name);
                    logCallback?.Invoke($"  Could not delete original: {original.Name}");
                }
            }

            progressCallback?.Invoke(100, "Text style compaction complete");
            logCallback?.Invoke("");
            logCallback?.Invoke("Text Style Summary");
            logCallback?.Invoke("==================");
            logCallback?.Invoke($"Canonical types created: {result.CanonicalTypesCreated}");
            logCallback?.Invoke($"References rewired: {result.ReferencesRewired}");
            logCallback?.Invoke($"Original types deleted: {result.OriginalTypesDeleted}");
            logCallback?.Invoke($"Blocked deletions: {result.BlockedDeletions.Count}");

            return result;
        }

        private static List<TextStyleCandidate> CollectCandidates(Document doc, ElementId defaultTypeId)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(TextNoteType))
                .Cast<TextNoteType>()
                .Where(t => t.Id != defaultTypeId)
                .Select(t => new TextStyleCandidate(
                    t.Id,
                    t.Name,
                    t,
                    BuildSignature(t)))
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Signature))
                .ToList();
        }

        private static string BuildSignature(TextNoteType type)
        {
            var parts = new List<string>();

            string font = GetStringParam(type, BuiltInParameter.TEXT_FONT) ?? "";
            double size = GetDoubleParam(type, BuiltInParameter.TEXT_SIZE);
            int bold = GetIntParam(type, BuiltInParameter.TEXT_STYLE_BOLD);
            int italic = GetIntParam(type, BuiltInParameter.TEXT_STYLE_ITALIC);
            int underline = GetIntParam(type, BuiltInParameter.TEXT_STYLE_UNDERLINE);
            int color = GetIntParam(type, BuiltInParameter.TEXT_COLOR);
            double widthScale = GetDoubleParam(type, BuiltInParameter.TEXT_WIDTH_SCALE);
            int background = GetIntParam(type, BuiltInParameter.TEXT_BACKGROUND);
            double tabSize = GetDoubleParam(type, BuiltInParameter.TEXT_TAB_SIZE);
            int alignment = GetIntParam(type, BuiltInParameter.TEXT_ALIGNMENT);
            string orientation = GetOrientationSentinel(type);
            string leaderArrow = GetElementIdParam(type, BuiltInParameter.LEADER_ARROWHEAD);

            parts.Add($"Font:{font}");
            parts.Add($"Size:{size.ToString("0.######", CultureInfo.InvariantCulture)}");
            parts.Add($"Bold:{bold}");
            parts.Add($"Italic:{italic}");
            parts.Add($"Underline:{underline}");
            parts.Add($"Color:{color}");
            parts.Add($"WidthScale:{widthScale.ToString("0.######", CultureInfo.InvariantCulture)}");
            parts.Add($"Background:{background}");
            parts.Add($"TabSize:{tabSize.ToString("0.######", CultureInfo.InvariantCulture)}");
            parts.Add($"Alignment:{alignment}");
            parts.Add($"Orientation:{orientation}");
            parts.Add($"LeaderArrow:{leaderArrow}");

            return string.Join("|", parts);
        }

        private static string? GetStringParam(TextNoteType type, BuiltInParameter param)
        {
            Parameter p = type.get_Parameter(param);
            return p?.AsString();
        }

        private static double GetDoubleParam(TextNoteType type, BuiltInParameter param)
        {
            Parameter p = type.get_Parameter(param);
            return p != null ? Math.Round(p.AsDouble(), 6) : 0;
        }

        private static int GetIntParam(TextNoteType type, BuiltInParameter param)
        {
            Parameter p = type.get_Parameter(param);
            return p?.AsInteger() ?? 0;
        }

        private static string GetElementIdParam(TextNoteType type, BuiltInParameter param)
        {
            Parameter p = type.get_Parameter(param);
            ElementId elementId = p?.AsElementId() ?? ElementId.InvalidElementId;
            return elementId.ToString();
        }

        private static string GetOrientationSentinel(TextNoteType type)
        {
            // "Text Orientation" has no BuiltInParameter in Revit 2024.
            // English Revit: LookupParameter resolves; preserve current integer value.
            // Spanish (or unreadable): return per-type sentinel so distinct types never falsely merge.
            Parameter? p = type.LookupParameter("Text Orientation");
            if (p != null) return p.AsInteger().ToString(CultureInfo.InvariantCulture);
            return $"ORIENTATION_UNREADABLE:{type.Id}";
        }

        private static ElementId CreateCanonicalType(Document doc, TextNoteType seed, string canonicalName)
        {
            ElementType duplicated = seed.Duplicate(canonicalName);
            return duplicated.Id;
        }

        private static bool IsExpectedTextStyleCompactionException(Exception ex)
        {
            return ex is ArgumentException
                or InvalidOperationException
                or RevitExceptions.ArgumentException
                or RevitExceptions.InvalidOperationException;
        }

        private static int RewireReferences(
            Document doc,
            ElementId sourceId,
            ElementId targetId,
            Dictionary<ElementId, List<TextNote>> textNoteIndex,
            Dictionary<ElementId, HashSet<ElementId>> paramIndex)
        {
            if (sourceId == targetId)
            {
                return 0;
            }

            int rewired = 0;
            rewired += RewireTextNoteInstancesFromIndex(textNoteIndex, sourceId, targetId);
            rewired += CompactionSharedHelper.RewireParameterReferencesFromIndex(doc, paramIndex, sourceId, targetId);
            return rewired;
        }

        private static Dictionary<ElementId, List<TextNote>> BuildTextNoteIndex(
            IReadOnlyList<TextNote> textNotes,
            HashSet<ElementId> sourceIds)
        {
            var index = new Dictionary<ElementId, List<TextNote>>();
            foreach (TextNote note in textNotes)
            {
                if (note == null || !note.IsValidObject)
                {
                    continue;
                }

                ElementId typeId = note.GetTypeId();
                if (!sourceIds.Contains(typeId))
                {
                    continue;
                }

                if (!index.TryGetValue(typeId, out List<TextNote>? entries))
                {
                    entries = new List<TextNote>();
                    index[typeId] = entries;
                }

                entries.Add(note);
            }

            return index;
        }

        private static int RewireTextNoteInstancesFromIndex(
            Dictionary<ElementId, List<TextNote>> textNoteIndex,
            ElementId sourceId,
            ElementId targetId)
        {
            if (!textNoteIndex.TryGetValue(sourceId, out List<TextNote>? notes))
            {
                return 0;
            }

            int rewired = 0;
            var movedToTarget = new List<TextNote>();

            foreach (TextNote note in notes)
            {
                if (note == null || !note.IsValidObject)
                {
                    continue;
                }

                if (note.GetTypeId() != sourceId)
                {
                    continue;
                }

                note.ChangeTypeId(targetId);
                rewired++;
                movedToTarget.Add(note);
            }

            textNoteIndex.Remove(sourceId);

            if (movedToTarget.Count > 0)
            {
                if (!textNoteIndex.TryGetValue(targetId, out List<TextNote>? targetNotes))
                {
                    targetNotes = new List<TextNote>();
                    textNoteIndex[targetId] = targetNotes;
                }

                targetNotes.AddRange(movedToTarget);
            }

            return rewired;
        }

        private sealed class TextStyleCandidate
        {
            public TextStyleCandidate(ElementId id, string name, TextNoteType typeElement, string signature)
            {
                Id = id;
                Name = name;
                TypeElement = typeElement;
                Signature = signature;
            }

            public ElementId Id { get; }
            public string Name { get; }
            public TextNoteType TypeElement { get; }
            public string Signature { get; }
        }
    }
}
