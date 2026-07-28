using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using LECG.Core;
using LECG.Views;
using LECG.ViewModels;
using LECG.Services;
using LECG.Services.Interfaces;

namespace LECG.Commands
{
    /// <summary>
    /// Command to offset elevations of Toposolids and Floors.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class OffsetElevationsCommand : RevitCommand
    {
        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            var offsetService = ServiceLocator.GetRequiredService<OffsetService>();
            var transactionService = ServiceLocator.GetRequiredService<ITransactionService>();

            // 1. Settings & Dialog
            var loadedSettings = SettingsManager.Load<OffsetElevationsViewModel>("OffsetSettings.json");
            var settings = ServiceLocator.GetRequiredService<OffsetElevationsViewModel>();
            settings.OffsetValue = loadedSettings.OffsetValue;
            settings.IsAddition = loadedSettings.IsAddition;
            var preselectedRefs = SelectionSeedHelper.GetSelectedReferences(uiDoc, settings.Selection.Filter);
            if (preselectedRefs.Count > 0)
            {
                settings.SetSelection(preselectedRefs, doc);
            }

            // Pass VM explicitly so command and view share the same instance
            OffsetElevationsView view = ServiceLocator.CreateWith<OffsetElevationsView>(settings);
            view.Initialize(uiDoc);

            if (view.ShowDialog() != true || !settings.ShouldRun) return;

            // Save Settings
            SettingsManager.Save(settings, "OffsetSettings.json");

            double offsetValue = settings.IsAddition ? settings.OffsetValue : -settings.OffsetValue;

            Log($"Offset Elevations");
            Log($"=================");
            Log($"Offset: {offsetValue}");
            Log("");

            // 2. Select Elements - Already selected in VM
            IList<Reference> refs = settings.SelectedRefs;

            if (refs == null || refs.Count == 0) return;

            Log($"Selected {refs.Count} elements.");
            ShowLogWindow("Offset Elevations");

            // 3. Process
            int successCount = 0;
            int failCount = 0;

            transactionService.Run(doc, "Offset Elevations", currentDoc =>
            {
                foreach (Reference r in refs)
                {
                    Element elem = currentDoc.GetElement(r);
                    if (elem == null) continue;

                    if (offsetService.TryOffsetElement(currentDoc, elem, offsetValue, Log)) successCount++;
                    else failCount++;
                }
            });

            UpdateProgress(100, "Complete!");
            Log("");
            Log($"=== SUMMARY ===");
            Log($"✓ Success: {successCount}");
            if (failCount > 0) Log($"✗ Failed: {failCount}");
        }
    }
}
