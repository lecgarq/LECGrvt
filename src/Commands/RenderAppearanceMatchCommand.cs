#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using LECG.Core;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using LECG.ViewModels;

namespace LECG.Commands
{
    /// <summary>
    /// Command to match graphic properties and identity data with Render Appearance.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class RenderAppearanceMatchCommand : RevitCommand
    {
        // Manual transaction to allow selection before transaction start
        protected override string? TransactionName => null;

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            // 2. Initial Setup
            var matService = ServiceLocator.GetRequiredService<IMaterialService>();

            // VM & View
            var vm = ServiceLocator.GetRequiredService<RenderAppearanceViewModel>();
            var view = ServiceLocator.CreateWith<RenderAppearanceView>(vm, uiDoc);
             
             // Set owner to Revit window
            WindowInteropHelper helper = new WindowInteropHelper(view);
            helper.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

            bool? result = view.ShowDialog();

            if (result == true && vm.ShouldRun)
            {
                // 3. Collect Materials
                ShowLogWindow("Syncing Render Appearance...");
                Log("ANALYZING SELECTION");
                HashSet<ElementId> materialsToProcessIds = new HashSet<ElementId>();

                if (vm.ShouldRun)
                {
                    Log("Scope: ALL Project Materials");
                    FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(Material));
                    foreach (Element e in collector) materialsToProcessIds.Add(e.Id);
                }

                Log($"Found {materialsToProcessIds.Count} unique materials.");
                Log("");

                List<Material> materialsList = new List<Material>();
                foreach (ElementId id in materialsToProcessIds)
                {
                    if (doc.GetElement(id) is Material m) materialsList.Add(m);
                }

                // 4. Process Materials (Batch)
                matService.BatchSyncWithRenderAppearance(doc, materialsList, Log, UpdateProgress);

                Log("");
                Log("COMPLETE");
            }
        }
    }
}
