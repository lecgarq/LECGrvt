using System;
using System.Collections.Generic;
using LECG.Services.Logging;

namespace LECG.Core
{
    /// <summary>
    /// Adapter abstraction over Revit's DialogBoxShowingEventArgs.OverrideResult.
    /// Enables unit testing without a live Revit instance (CROSS-03).
    /// Wave 3 adds a convenience overload that accepts DialogBoxShowingEventArgs directly.
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
    /// Wave 3 will populate the list with real DialogIds captured in 06-DIALOG-DISCOVERY.md.
    /// </summary>
    public class DialogWhitelist
    {
        private readonly IReadOnlyDictionary<string, int> _entries;

        public DialogWhitelist(IReadOnlyDictionary<string, int> entries)
        {
            _entries = entries ?? throw new ArgumentNullException(nameof(entries));
        }

        /// <summary>
        /// Global whitelist instance (empty until Wave 3 populates it).
        /// </summary>
        public static readonly DialogWhitelist Global = new DialogWhitelist(
            new Dictionary<string, int>(StringComparer.Ordinal)
            // Wave 3: add entries from 06-DIALOG-DISCOVERY.md here.
        );

        /// <summary>
        /// Applies the whitelist to a dialog.
        /// Hit: invokes override + logs Info.
        /// Miss: does NOT invoke override + logs Warning.
        /// </summary>
        public void Apply(string? dialogId, IDialogOverride sink, ILogger logger)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            if (dialogId != null && _entries.TryGetValue(dialogId, out int result))
            {
                sink?.OverrideResult(result);
                logger.Log($"Auto-handled whitelisted dialog '{dialogId}' with result {result}.", "DialogWhitelist");
            }
            else
            {
                string id = dialogId ?? "(null)";
                logger.LogWarning($"Unhandled dialog '{id}' reached user (not in whitelist).", "DialogWhitelist");
            }
        }
    }
}
