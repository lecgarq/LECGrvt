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
    public class ConvertToposolidToFloorCommand : RevitCommand
    {
        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            var service = ServiceLocator.GetRequiredService<IConversionService>();

            // 1. ViewModel & View
            var vm = ServiceLocator.GetRequiredService<ConvertToposolidToFloorViewModel>();
            vm.Initialize(doc);
            var preselectedToposolids = uiDoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .OfType<Toposolid>()
                .Cast<Element>()
                .ToList();
            if (preselectedToposolids.Count > 0)
            {
                vm.SetSelectedElements(preselectedToposolids);
            }

            var view = ServiceLocator.CreateWith<ConvertToposolidToFloorView>(vm);
            view.Initialize(uiDoc);

            view.ShowDialog();
            if (!vm.ShouldRun) return;

            // 2. Gather selected elements
            ShowLogWindow("Toposolid to Floor");

            List<Element> elements = vm.GetSelectedElements(doc);
            if (elements.Count == 0)
            {
                Log("No valid Toposolid elements were available to convert.");
                return;
            }

            // 3. Execute conversion
            Log("Toposolid to Floor Conversion");
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

            service.ConvertToposolidToFloor(
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
