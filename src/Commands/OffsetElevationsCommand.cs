using System;
using System.Collections.Generic;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using LECG.Core;
using LECG.Views;
using LECG.ViewModels;
using LECG.Services;

namespace LECG.Commands
{
    /// <summary>
    /// Command to offset elevations of Toposolids and Floors.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class OffsetElevationsCommand : RevitCommand
    {
        protected override string? TransactionName => null; // Manual handling for selection loop

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            var offsetService = ServiceLocator.GetRequiredService<IOffsetService>();

            // 1. Settings & Dialog
            var settings = SettingsManager.Load<OffsetElevationsVM>("OffsetSettings.json");
            // Ensure Selection is clean if loaded
            if (settings.Selection == null)
            {
                var freshVm = new OffsetElevationsVM();
                freshVm.OffsetValue = settings.OffsetValue;
                freshVm.IsAddition = settings.IsAddition;
                settings = freshVm;
            }
            
            OffsetElevationsView view = new OffsetElevationsView(settings, uiDoc);
             
             // Set owner to Revit window
            WindowInteropHelper helper = new WindowInteropHelper(view);
            helper.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

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

            using (Transaction t = new Transaction(doc, "Offset Elevations"))
            {
                t.Start();
                foreach (Reference r in refs)
                {
                    Element elem = doc.GetElement(r);
                    if (elem == null) continue;

                    if (offsetService.TryOffsetElement(doc, elem, offsetValue, Log)) successCount++;
                    else failCount++;
                }
                t.Commit();
            }

            UpdateProgress(100, "Complete!");
            Log("");
            Log($"=== SUMMARY ===");
            Log($"✓ Success: {successCount}");
            if (failCount > 0) Log($"✗ Failed: {failCount}");
        }
    }
}
