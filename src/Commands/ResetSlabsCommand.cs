using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
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
    /// Command to reset slab shapes for Floor and Toposolid elements.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ResetSlabsCommand : RevitCommand
    {
        protected override string? TransactionName => null; // Manual handling due to selection loop

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            var slabService = ServiceLocator.GetRequiredService<ISlabService>();

            Log($"[{DateTime.Now}] Starting Reset Slabs Command...");

            // 1. Load Settings & UI
            var loadedSettings = SettingsManager.Load<ResetSlabsVM>("ResetSlabsSettings.json");
            var settings = ServiceLocator.GetRequiredService<ResetSlabsVM>();
            settings.DuplicateElements = loadedSettings.DuplicateElements;
            
            ResetSlabsView view = ServiceLocator.CreateWith<ResetSlabsView>(settings, uiDoc);
             
             // Set owner to Revit window
            WindowInteropHelper helper = new WindowInteropHelper(view);
            helper.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

            if (view.ShowDialog() != true || !settings.ShouldRun) return;

            SettingsManager.Save(settings, "ResetSlabsSettings.json");
            
            // 2. Select Elements - Already selected
            IList<Reference> refs = settings.SelectedRefs;
            if (refs == null || refs.Count == 0) return;

            Log($"Selected {refs.Count} elements.");

            bool duplicate = settings.DuplicateElements;

            // 3. Process
            int successCount = 0;
            List<ElementId> processedIds = new List<ElementId>();

            using (Transaction t = new Transaction(doc, "Reset Slab Shapes"))
            {
                t.Start();

                foreach (Reference r in refs)
                {
                    Element elem = doc.GetElement(r);
                    if (elem == null) continue;

                    Element targetElement = elem;
                    Log($"Processing ID: {elem.Id} ({elem.Category?.Name})");

                    if (duplicate)
                    {
                        try
                        {
                            var newElem = slabService.DuplicateElement(doc, elem);
                            if (newElem != null)
                            {
                                targetElement = newElem;
                                Log($"  -> Duplicated to new ID: {targetElement.Id}");
                                processedIds.Add(targetElement.Id);
                            }
                        }
                        catch (Exception copyEx)
                        {
                            Log($"  -> Copy Failed: {copyEx.Message}");
                            continue;
                        }
                    }
                    else
                    {
                        processedIds.Add(elem.Id);
                    }

                    // Reset
                    if (slabService.TryResetSlabShape(targetElement, out string msg)) 
                    {
                        successCount++;
                        Log($"  -> {msg}");
                    }
                    else
                    {
                         Log($"  -> {msg}");
                    }
                }

                t.Commit();

                if (duplicate && processedIds.Count > 0)
                    uiDoc.Selection.SetElementIds(processedIds);
            }

            Log($"Finished. Successfully reset {successCount} slabs.");
            
            // Show log for success
            ShowLogWindow("Reset Slabs");
        }


    }
}
