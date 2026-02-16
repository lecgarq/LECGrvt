using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using LECG.Core;
using LECG.ViewModels;
using LECG.Views;
using LECG.Interfaces;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AlignEdgesCommand : RevitCommand
    {
        protected override string? TransactionName => null;

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            // 1. Service
            var service = ServiceLocator.GetRequiredService<IAlignEdgesService>();
            
            // 2. VM
            var vm = new AlignEdgesViewModel(); // Service is stateless, but we need refs from VM.
            
            // 3. View
            var view = new AlignEdgesView(vm, uiDoc);
            
            // 4. Show
            bool? result = view.ShowDialog();
            
            // 5. Run if confirmed
            if (result == true && vm.ShouldRun)
            {
                try 
                {
                    service.AlignEdges(doc, vm.TargetRefs, vm.ReferenceRefs);
                    TaskDialog.Show("Align Edges", "Alignment completed successfully.");
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Error", $"Alignment failed: {ex.Message}");
                }
            }
        }
    }
}
