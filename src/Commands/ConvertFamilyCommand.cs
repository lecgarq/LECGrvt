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
        /// <summary>
        /// Auto-dismiss known-safe Revit dialogs during conversion via explicit whitelist.
        /// Unknown dialogs reach the user (reach-user default per CROSS-03).
        /// Whitelist entries are LOW-confidence until 06-DIALOG-DISCOVERY.md is updated
        /// with runtime-confirmed DialogId values.
        /// </summary>
        private static void OnDialogShowing(object? sender, DialogBoxShowingEventArgs e)
        {
            DialogWhitelist.Global.Apply(e, ServiceLocator.GetRequiredService<ILogger>());
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

                var service = ServiceLocator.GetRequiredService<FamilyConversionService>();

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

                var reporter = new RevitCommandProgressReporter(_logger, UpdateProgress);

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
