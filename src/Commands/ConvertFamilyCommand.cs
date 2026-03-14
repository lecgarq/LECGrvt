#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using LECG.Core;
using LECG.Services;
using LECG.Services.Interfaces;
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
        /// Auto-dismiss any Revit dialog during conversion (e.g. "Parameter 'Thickness' cannot be added")
        /// </summary>
        private static void OnDialogShowing(object? sender, DialogBoxShowingEventArgs e)
        {
            // Always accept/OK — never cancel/close (which would roll back transactions)
            if (e is TaskDialogShowingEventArgs taskArgs)
                taskArgs.OverrideResult(1); // 1 = IDOK
            else
                e.OverrideResult(1);
        }

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            UIApplication uiApp = uiDoc.Application;

            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            try
            {
                // Subscribe to auto-dismiss ALL Revit dialogs during conversion
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
                        var filter = new LECG.Utils.FamilyInstanceFilter();
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

                var reporter = new RevitCommandProgressReporter(Log, UpdateProgress);

                // 3. Execute Batch
                service.ConvertFamilyBatch(doc, instances, customName: "", templatePath: "", isTemporary: false, replaceInPlace: true, reporter);

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
