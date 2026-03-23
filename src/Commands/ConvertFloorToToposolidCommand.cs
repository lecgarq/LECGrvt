using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Services.Interfaces;
using LECG.ViewModels;
using LECG.Views;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ConvertFloorToToposolidCommand : RevitCommand
    {
        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            var service = ServiceLocator.GetRequiredService<IConversionService>();

            // 1. ViewModel & View
            var vm = ServiceLocator.GetRequiredService<ConvertFloorToToposolidViewModel>();
            vm.Initialize(doc);
            var preselectedFloors = uiDoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .OfType<Floor>()
                .Cast<Element>()
                .ToList();
            if (preselectedFloors.Count > 0)
            {
                vm.SetSelectedElements(preselectedFloors);
            }

            var view = ServiceLocator.CreateWith<ConvertFloorToToposolidView>(vm);
            view.Initialize(uiDoc);

            view.ShowDialog();
            if (!vm.ShouldRun) return;

            // 2. Gather selected elements
            ShowLogWindow("Floor to Toposolid");

            List<Element> elements = vm.GetSelectedElements(doc);
            if (elements.Count == 0)
            {
                Log("No valid Floor elements were available to convert.");
                return;
            }

            // 3. Execute conversion
            Log("Floor to Toposolid Conversion");
            Log("=============================");
            Log($"Elements: {elements.Count}");
            Log($"Target Type: {vm.SelectedType?.Name}");
            Log($"Target Level: {vm.SelectedLevel?.Name}");
            Log($"Delete Source: {vm.DeleteSource}");
            Log("");

            var reporter = new SimpleProgressReporter(report =>
            {
                Log(report.Message);
                UpdateProgress(report.Percentage, report.Message);
            });

            service.ConvertFloorToToposolid(
                doc,
                elements,
                vm.SelectedType!.Id,
                vm.SelectedLevel!.Id,
                vm.DeleteSource,
                reporter);

            UpdateProgress(100, "Complete");
        }
    }
}
