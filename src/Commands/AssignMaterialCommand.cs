using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Views;
using LECG.Services;
using LECG.ViewModels;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Interop;

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
            // 1. Resolve Service
            var service = ServiceLocator.GetRequiredService<IMaterialService>();

            // 2. Initialize VM & View
            var vm = new AssignMaterialViewModel(service);
            var view = new AssignMaterialView(vm, uiDoc);

            WindowInteropHelper helper = new WindowInteropHelper(view);
            helper.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

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
                service.AssignMaterialsToElements(doc, elements, (msg) => Log(msg), (p, s) => UpdateProgress(p, s));
                //Log("DEBUG: Logic temporarily disabled for build test.");
            }
        }
    }
}
