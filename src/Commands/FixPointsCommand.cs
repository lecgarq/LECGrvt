using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using LECG.ViewModels;
using LECG.Views;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class FixPointsCommand : RevitCommand
    {
        protected override string? TransactionName => null;

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            // 1. Service
            var service = ServiceLocator.GetRequiredService<IFixPointsService>();

            // 2. VM
            var vm = ServiceLocator.GetRequiredService<FixPointsViewModel>();
            var preselectedElements = uiDoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .Where(element => element is Floor || element is Toposolid)
                .ToList();
            if (preselectedElements.Count > 0)
            {
                vm.SetSelectedElements(preselectedElements!);
            }

            // 3. View
            var view = ServiceLocator.CreateWith<FixPointsView>(vm);
            view.Initialize(uiDoc);

            // 4. Show
            view.ShowDialog();

            // 5. Run if confirmed
            if (!vm.ShouldRun) return;

            List<Element> selectedElements = vm.GetSelectedElements(doc);
            ShowLogWindow("Fix Points");

            if (!selectedElements.Any())
            {
                Log("No valid Floor or Toposolid elements found in selection.");
                return;
            }

            var reporter = new RevitCommandProgressReporter(_logger, UpdateProgress);
            Log("Starting Fix Points...");
            Log($"Selected {selectedElements.Count} elements. Sensitivity: {vm.Sensitivity}");

            service.FixPoints(doc, selectedElements, vm.Sensitivity, reporter);

            UpdateProgress(100, "Done");
        }
    }
}
