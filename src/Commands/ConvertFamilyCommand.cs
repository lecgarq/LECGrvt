using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using LECG.Core;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ConvertFamilyCommand : RevitCommand
    {
        protected override string? TransactionName => null;

        /// <summary>
        /// Selectively dismiss known-safe Revit dialogs during conversion.
        /// Dangerous dialogs (delete type, remove constraints) are CANCELLED to prevent family breakage.
        /// Unknown dialogs are also cancelled (conservative default).
        /// </summary>
        private static void OnDialogShowing(object? sender, DialogBoxShowingEventArgs e)
        {
            if (e is TaskDialogShowingEventArgs taskArgs)
            {
                string message = taskArgs.Message ?? "";
                string dialogId = taskArgs.DialogId ?? "";

                // Known SAFE dialogs — auto-accept (expected during conversion)
                if (message.Contains("cannot be added", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("will be replaced", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("overwrite", StringComparison.OrdinalIgnoreCase) ||
                    dialogId.Contains("Duplicate", StringComparison.OrdinalIgnoreCase))
                {
                    taskArgs.OverrideResult(1); // IDOK — accept
                    Logger.Instance.Log($"[ConvertFamily] Auto-accepted safe dialog: {message}");
                    return;
                }

                // Known DANGEROUS dialogs — auto-CANCEL to prevent family breakage
                if (message.Contains("delete", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("remove", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("constraint", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("discard", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("cannot be undone", StringComparison.OrdinalIgnoreCase))
                {
                    taskArgs.OverrideResult(2); // IDCANCEL — block the destructive action
                    Logger.Instance.LogWarning($"[ConvertFamily] BLOCKED dangerous dialog: {message}");
                    return;
                }

                // Unknown dialogs — cancel to be safe (conservative default)
                taskArgs.OverrideResult(2);
                Logger.Instance.LogWarning($"[ConvertFamily] Blocked unknown dialog '{dialogId}': {message}");
            }
            else
            {
                // Standard Windows dialog (not TaskDialog) — cancel to be safe
                e.OverrideResult(2);
                Logger.Instance.LogWarning($"[ConvertFamily] Blocked non-task dialog: {e.DialogId ?? "unknown"}");
            }
        }

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            UIApplication uiApp = uiDoc.Application;

            try
            {
                // Subscribe to smart dialog handler (accepts safe, cancels dangerous)
                uiApp.DialogBoxShowing += OnDialogShowing;

                var service = ServiceLocator.GetRequiredService<IFamilyConversionService>();

                // 1. Get Selection
                var selectedRefs = new List<Reference>();
                var preSelectionIds = uiDoc.Selection.GetElementIds();

                if (preSelectionIds.Any())
                {
                    selectedRefs = preSelectionIds.Select(id => new Reference(doc.GetElement(id))).ToList();
                }
                else
                {
                    // Unsubscribe temporarily so the pick dialog works
                    uiApp.DialogBoxShowing -= OnDialogShowing;
                    try
                    {
                        var filter = new LECG.Utilities.FamilyInstanceFilter();
                        var refs = uiDoc.Selection.PickObjects(Autodesk.Revit.UI.Selection.ObjectType.Element, filter, "Select hosted family instances to convert.");
                        selectedRefs.AddRange(refs);
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        return;
                    }
                    finally
                    {
                        // Re-subscribe after picking
                        uiApp.DialogBoxShowing += OnDialogShowing;
                    }
                }

                if (!selectedRefs.Any())
                {
                    Log("No valid FamilyInstance elements selected.");
                    return;
                }

                // 2. Prepare instances
                var instances = selectedRefs
                    .Select(r => doc.GetElement(r) as FamilyInstance)
                    .Where(i => i != null)
                    .Cast<FamilyInstance>()
                    .ToList();

                ShowLogWindow("Converting Families...");
                Log("--- 1-Click Seamless Conversion Started ---");
                Log($"Processing {instances.Count} selected instances.");

                var reporter = new RevitCommandProgressReporter(Logger.Instance, UpdateProgress); // TEMPORARY: Wave 2 replaces Logger.Instance with injected _logger

                // 3. Execute Batch
                service.ConvertFamilyBatch(doc, instances, customName: "", templatePath: "", isTemporary: false, replaceInPlace: true, reporter);

                UpdateProgress(100, "Complete");
                Log("--- Conversion Sequence Completed ---");
            }
            finally
            {
                // ALWAYS unsubscribe
                uiApp.DialogBoxShowing -= OnDialogShowing;
            }
        }
    }
}
