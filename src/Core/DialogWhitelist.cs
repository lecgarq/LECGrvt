using System;
using System.Collections.Generic;
using Autodesk.Revit.UI.Events;
using LECG.Services.Logging;

namespace LECG.Core
{
    /// <summary>
    /// Adapter abstraction over Revit's DialogBoxShowingEventArgs.OverrideResult.
    /// Enables unit testing without a live Revit instance (CROSS-03).
    /// </summary>
    public interface IDialogOverride
    {
        void OverrideResult(int result);
    }

    /// <summary>
    /// Hardcoded whitelist of known-safe Revit dialog IDs and their auto-dismiss result codes.
    /// Matching is exact string, case-sensitive (CONTEXT §3.2).
    /// Hit  → OverrideResult(n) called + Info logged.
    /// Miss → no override, Warning logged (dialog reaches user).
    ///
    /// SOURCE: .planning/phases/06-cross-cutting-foundation/06-DIALOG-DISCOVERY.md
    /// Reviewer: extend only via deliberate code change after runtime discovery confirms a DialogId.
    /// </summary>
    public class DialogWhitelist
    {
        private const string Scope = "DialogWhitelist";

        private readonly IReadOnlyDictionary<string, int> _entries;

        public DialogWhitelist(IReadOnlyDictionary<string, int> entries)
        {
            _entries = entries ?? throw new ArgumentNullException(nameof(entries));
        }

        // -----------------------------------------------------------------------
        // Global production whitelist
        // SOURCE: .planning/phases/06-cross-cutting-foundation/06-DIALOG-DISCOVERY.md
        // STATUS: 06-DIALOG-DISCOVERY.md status=blocked — all entries below are
        //         LOW-confidence research guesses, NOT runtime-confirmed.
        //         Replace after a real discovery pass updates 06-DIALOG-DISCOVERY.md.
        // -----------------------------------------------------------------------
        public static readonly DialogWhitelist Global = new DialogWhitelist(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                // confidence: low — unverified; replace after real discovery pass populates 06-DIALOG-DISCOVERY.md
                // Purge: "Extrusion is too thin" geometry-validation dialog — cancel to preserve geometry
                { "TaskDialog_ExtrusionTooThin", 2 },

                // confidence: low — unverified; replace after real discovery pass populates 06-DIALOG-DISCOVERY.md
                // Purge: "Base sketch for extrusion is invalid" — cancel to preserve geometry
                { "TaskDialog_BaseSketchInvalid", 2 },

                // confidence: low — unverified; replace after real discovery pass populates 06-DIALOG-DISCOVERY.md
                // Purge (deep): LoadFamily triggers a confirm dialog when family already loaded — accept
                { "TaskDialog_LoadFamily", 1 },

                // confidence: low — unverified; replace after real discovery pass populates 06-DIALOG-DISCOVERY.md
                // ConvertFamily: "already exists / will be replaced" overwrite confirm — accept
                { "TaskDialog_Overwrite", 1 },

                // confidence: low — unverified; replace after real discovery pass populates 06-DIALOG-DISCOVERY.md
                // ConvertFamily: duplicate family check during conversion — accept
                { "TaskDialog_DuplicateFamily", 1 },
            }
        );

        /// <summary>
        /// Applies the whitelist to a dialog identified by its DialogId string.
        /// Hit: invokes OverrideResult on sink + logs Info.
        /// Miss: does NOT invoke OverrideResult + logs Warning (dialog reaches user).
        /// </summary>
        public void Apply(string? dialogId, IDialogOverride sink, ILogger logger)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            if (dialogId != null && _entries.TryGetValue(dialogId, out int result))
            {
                sink?.OverrideResult(result);
                logger.Log($"Auto-handled whitelisted dialog '{dialogId}' with result {result}.", Scope);
            }
            else
            {
                string id = dialogId ?? "(null)";
                logger.LogWarning($"Dialog '{id}' not in whitelist — reaching user.", Scope);
            }
        }

        /// <summary>
        /// Convenience overload: wraps <see cref="DialogBoxShowingEventArgs"/> in an adapter
        /// and delegates to <see cref="Apply(string?, IDialogOverride, ILogger)"/>.
        /// Called from command OnDialogShowing handlers.
        /// </summary>
        public void Apply(DialogBoxShowingEventArgs e, ILogger logger)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            Apply(e.DialogId, new EventArgsAdapter(e), logger);
        }

        // -----------------------------------------------------------------------
        // Private adapter — bridges the sealed Revit type to the test seam
        // -----------------------------------------------------------------------
        private sealed class EventArgsAdapter : IDialogOverride
        {
            private readonly DialogBoxShowingEventArgs _e;
            public EventArgsAdapter(DialogBoxShowingEventArgs e) => _e = e;
            public void OverrideResult(int result) => _e.OverrideResult(result);
        }
    }
}
