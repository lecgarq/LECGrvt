using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Views;
using LECG.ViewModels;
using LECG.Services;
using LECG.Services.Interfaces;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class DivideToposolidCommand : RevitCommand
    {
        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            var service = ServiceLocator.GetRequiredService<IDivideToposolidService>();

            Log($"[{DateTime.Now}] Starting Divide Toposolid Command...");

            // 1. UI
            var vm = ServiceLocator.GetRequiredService<DivideToposolidViewModel>();
            var preselectedElements = uiDoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .Where(element => element is Toposolid)
                .ToList();
            if (preselectedElements.Count > 0)
            {
                vm.SetSelectedElements(preselectedElements!);
            }

            var view = ServiceLocator.CreateWith<DivideToposolidView>(vm);
            view.Initialize(uiDoc);

            view.ShowDialog();
            if (!vm.ShouldRun) return;

            // 2. Resolve selected elements
            var elements = vm.GetSelectedElements(doc);
            if (elements.Count == 0)
            {
                ShowLogWindow("Divide Toposolid");
                Log("No valid Toposolid elements were available to divide.");
                return;
            }

            Log($"Selected {elements.Count} elements.");

            // 3. Process
            ShowLogWindow("Divide Toposolid");

            var reporter = new RevitCommandProgressReporter(_logger, UpdateProgress);

            service.DivideToposolids(doc, elements, reporter);

            Log("Divide Toposolid complete.");
            UpdateProgress(100, "Complete");
        }
    }
}
