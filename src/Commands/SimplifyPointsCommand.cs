using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using LECG.Core;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using LECG.Utilities;
using LECG.ViewModels;
using LECG.Views;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class SimplifyPointsCommand : RevitCommand
    {
        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            // 1. Service
            var service = ServiceLocator.GetRequiredService<SimplifyPointsService>();

            // 2. VM
            var vm = ServiceLocator.GetRequiredService<SimplifyPointsViewModel>();
            var preselectedRefs = SelectionSeedHelper.GetSelectedReferences(uiDoc, vm.Selection.Filter);
            if (preselectedRefs.Count > 0)
            {
                vm.SetSelection(preselectedRefs, doc);
            }

            // 3. View — pass VM explicitly so command and view share the same instance
            var view = ServiceLocator.CreateWith<SimplifyPointsView>(vm);
            view.Initialize(uiDoc);

            // 4. Show
            bool? result = view.ShowDialog();

            // 5. Run if confirmed
            if (result == true && vm.ShouldRun && vm.SelectedRefs.Any())
            {
                var reporter = new RevitCommandProgressReporter(_logger, UpdateProgress);
                // Show Log
                ShowLogWindow("Simplify Points");
                Log("Starting simplification...");
                Log($"Selected {vm.SelectedRefs.Count} elements.");

                // Map references to Elements
                var elements = vm.SelectedRefs.Select(r => doc.GetElement(r)).Where(e => e != null).ToList();

                service.SimplifyPoints(doc, elements, reporter);

                UpdateProgress(100, "Done");
            }
        }
    }
}
