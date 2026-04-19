using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using LECG.ViewModels;
using LECG.Models;

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

            // 1. Initial Setup
            var matService = ServiceLocator.GetRequiredService<IMaterialService>();

            // 2. VM & View
            var vm = ServiceLocator.GetRequiredService<RenderAppearanceViewModel>();
            var view = ServiceLocator.CreateWith<RenderAppearanceView>(vm, uiDoc);

            bool? result = view.ShowDialog();

            if (result == true && vm.CanRun)
            {
                var settings = vm.ToSettings();

                // 3. Collect Materials
                ShowLogWindow("Syncing Render Appearance...");
                Log("ANALYZING SELECTION");
                HashSet<ElementId> materialsToProcessIds = new HashSet<ElementId>();

                Log("Scope: ALL Project Materials");
                FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(Material));
                foreach (Element e in collector) materialsToProcessIds.Add(e.Id);

                Log($"Found {materialsToProcessIds.Count} unique materials.");
                Log("");

                List<Material> materialsList = new List<Material>();
                foreach (ElementId id in materialsToProcessIds)
                {
                    if (doc.GetElement(id) is Material m) materialsList.Add(m);
                }

                // 4. Process Materials (Batch)
                var reporter = new RevitCommandProgressReporter(Log, UpdateProgress);
                matService.BatchSyncWithRenderAppearance(doc, materialsList, settings, reporter);

                UpdateProgress(100, "Complete");
                Log("");
                Log("COMPLETE");
            }
        }
    }
}
