using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using LECG.Core;
using LECG.Interfaces;
using LECG.Utils;
using LECG.ViewModels;
using LECG.Views;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class SimplifyPointsCommand : RevitCommand
    {
        // Manual handling to support selection within the command flow
        protected override string? TransactionName => null;

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            // 1. Service
            var service = ServiceLocator.GetRequiredService<ISimplifyPointsService>();
            
            // 2. VM
            var vm = new SimplifyPointsViewModel();
            
            // 3. View
            var view = new SimplifyPointsView(vm, uiDoc);
             
             // Set owner to Revit window
            WindowInteropHelper helper = new WindowInteropHelper(view);
            helper.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            
            // 4. Show
            bool? result = view.ShowDialog();
            
            // 5. Run if confirmed
            if (result == true && vm.ShouldRun && vm.SelectedRefs.Any())
            {
                 // Show Log
                ShowLogWindow("Simplify Points");
                Log("Starting simplification...");
                Log($"Selected {vm.SelectedRefs.Count} elements.");

                // Map references to Elements
                var elements = vm.SelectedRefs.Select(r => doc.GetElement(r)).Where(e => e != null);

                service.SimplifyPoints(doc, elements, UpdateProgress, Log);
                
                UpdateProgress(100, "Done");
            }
        }
    }
}
