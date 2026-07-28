using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core.Warnings;
using LECG.Services.Logging;

namespace LECG.Services
{
    /// <summary>
    /// Warnings access: lists document warnings and acts on their failing elements
    /// (select/show/isolate). Nothing here writes persistent model data — but Revit
    /// classifies temporary isolate as a model modification, so <see cref="Isolate"/>
    /// needs a transaction. See the note on that method.
    /// </summary>
    public class WarningsService
    {
        private readonly ILogger _logger;

        public WarningsService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IReadOnlyList<WarningItem> ReadWarnings(Document doc)
        {
            ArgumentNullException.ThrowIfNull(doc);
            return ReadAll(doc.GetWarnings(), Map);
        }

        /// <summary>
        /// Maps each message, skipping (with a logged warning) any that throws —
        /// one disposed/invalid FailureMessage never aborts the listing.
        /// </summary>
        public IReadOnlyList<WarningItem> ReadAll<T>(IEnumerable<T> messages, Func<T, WarningItem> map)
        {
            ArgumentNullException.ThrowIfNull(messages);
            ArgumentNullException.ThrowIfNull(map);

            var items = new List<WarningItem>();
            foreach (T message in messages)
            {
                try
                {
                    items.Add(map(message));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Skipped unreadable warning: {ex.Message}", scope: nameof(WarningsService), exception: ex);
                }
            }
            return items;
        }

        private static WarningItem Map(FailureMessage message) =>
            new(message.GetDescriptionText(),
                message.GetSeverity().ToString(),
                message.GetFailingElements().Select(id => id.Value).ToList());

        public void Select(UIDocument uidoc, IEnumerable<long> elementIds)
        {
            ArgumentNullException.ThrowIfNull(uidoc);
            uidoc.Selection.SetElementIds(ToElementIds(elementIds));
        }

        public void Show(UIDocument uidoc, IEnumerable<long> elementIds)
        {
            ArgumentNullException.ThrowIfNull(uidoc);
            uidoc.ShowElements(ToElementIds(elementIds));
        }

        // Temporary view mode: not persisted unless the user saves, and reversible via
        // Revit's own Reset Temporary Hide/Isolate. It is still a model modification as
        // far as Revit is concerned — calling it without a transaction throws
        // ModificationOutsideTransactionException (verified live, 2026-07-27).
        public void Isolate(UIDocument uidoc, IEnumerable<long> elementIds)
        {
            ArgumentNullException.ThrowIfNull(uidoc);
            ICollection<ElementId> ids = ToElementIds(elementIds);
            View view = uidoc.ActiveGraphicalView;

            // Raw Transaction rather than ITransactionService on purpose: taking that
            // interface as a constructor dependency forces the Revit type graph to load
            // when the service is constructed, and the test runner has only reference
            // assemblies — it would make this class untestable outside Revit. Referencing
            // Transaction inside the method body keeps type loading lazy.
            using Transaction transaction = new(uidoc.Document, "Isolate Warning Elements");
            transaction.Start();
            view.IsolateElementsTemporary(ids);
            transaction.Commit();
        }

        private static ICollection<ElementId> ToElementIds(IEnumerable<long> elementIds)
        {
            ArgumentNullException.ThrowIfNull(elementIds);
            return elementIds.Select(id => new ElementId(id)).ToList();
        }
    }
}
