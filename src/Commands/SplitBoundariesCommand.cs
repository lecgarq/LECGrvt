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
    /// <summary>
    /// Command to split multi-boundary Floor and Toposolid elements into independent instances.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SplitBoundariesCommand : RevitCommand
    {
        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            var splitService = ServiceLocator.GetRequiredService<SplitBoundariesService>();

            Log($"[{DateTime.Now}] Starting Split Boundaries Command...");

            // 1. UI
            var vm = ServiceLocator.GetRequiredService<SplitBoundariesViewModel>();
            var preselectedElements = uiDoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .Where(element => element is Floor || element is Toposolid)
                .ToList();
            if (preselectedElements.Count > 0)
            {
                vm.SetSelectedElements(preselectedElements!);
            }

            var view = ServiceLocator.CreateWith<SplitBoundariesView>(vm);
            view.Initialize(uiDoc);

            view.ShowDialog();
            if (!vm.ShouldRun) return;

            // 2. Resolve selected elements
            var elements = vm.GetSelectedElements(doc);
            if (elements.Count == 0)
            {
                ShowLogWindow("Split Boundaries");
                Log("No valid Floor or Toposolid elements were available to split.");
                return;
            }

            Log($"Selected {elements.Count} elements.");

            // 3. Process
            ShowLogWindow("Split Boundaries");

            var reporter = new RevitCommandProgressReporter(_logger, UpdateProgress);

            splitService.SplitBoundaries(doc, elements, reporter);

            Log("Split Boundaries complete.");
            UpdateProgress(100, "Complete");
        }
    }
}
