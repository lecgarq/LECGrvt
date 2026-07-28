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
    /// Read-only warnings access: lists document warnings and acts on their failing
    /// elements (select/show/isolate). No document writes anywhere in this flow.
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

        // Temporary view mode — reversible via Revit's own control, no document write.
        public void Isolate(UIDocument uidoc, IEnumerable<long> elementIds)
        {
            ArgumentNullException.ThrowIfNull(uidoc);
            uidoc.ActiveGraphicalView.IsolateElementsTemporary(ToElementIds(elementIds));
        }

        private static ICollection<ElementId> ToElementIds(IEnumerable<long> elementIds)
        {
            ArgumentNullException.ThrowIfNull(elementIds);
            return elementIds.Select(id => new ElementId(id)).ToList();
        }
    }
}
