using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Views;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.ViewModels;
using System.Linq;
using System.Collections.Generic;

namespace LECG.Commands
{
    /// <summary>
    /// Command to assign materials to selected Toposolids/Floors based on their Type Name.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class AssignMaterialCommand : RevitCommand
    {
        protected override string? TransactionName => null; // Handled internally

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            // 1. Resolve Service
            var service = ServiceLocator.GetRequiredService<IMaterialService>();

            // 2. Initialize VM & View
            var vm = ServiceLocator.GetRequiredService<AssignMaterialViewModel>();
            var preselectedRefs = SelectionSeedHelper.GetSelectedReferences(uiDoc, new SelectionFilters.MaterialHostFilter());
            if (preselectedRefs.Count > 0)
            {
                vm.SetSelection(preselectedRefs, doc);
            }

            // Pass VM explicitly so command and view share the same instance
            var view = ServiceLocator.CreateWith<AssignMaterialView>(vm);
            view.Initialize(uiDoc);

            // 3. Show Dialog
            bool? result = view.ShowDialog();

            // 4. Run Logic if Confirmed
            if (result == true && vm.ShouldRun && vm.SelectedRefs.Any())
            {
                ShowLogWindow("Assigning Materials...");

                // Convert References to Elements
                List<Element> elements = vm.SelectedRefs
                    .Select(r => doc.GetElement(r))
                    .Where(e => e != null)
                    .ToList();

                // Call Logic
                var reporter = new RevitCommandProgressReporter(Log, UpdateProgress);
                service.AssignMaterialsToElements(doc, elements, reporter);
                UpdateProgress(100, "Complete");
            }
        }
    }
}
