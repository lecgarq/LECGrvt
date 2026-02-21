#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Services.Interfaces;
using LECG.ViewModels;
using LECG.Views;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ConvertFamilyCommand : RevitCommand
    {
        protected override string? TransactionName => "Convert Family (Batch)";

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            // 1. Resolve Service & ViewModel
            var service = ServiceLocator.GetRequiredService<IFamilyConversionService>();
            var viewModel = ServiceLocator.GetRequiredService<ConvertFamilyViewModel>();
            
            // 2. Show UI
            var view = ServiceLocator.CreateWith<ConvertFamilyView>(viewModel, uiDoc);
            
             // Set owner to Revit window
            System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(view);
            helper.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

            bool? result = view.ShowDialog();

            if (result == true && viewModel.ShouldRun && viewModel.SelectedRefs.Any())
            {
                // 3. Execute Conversion via Service
                ShowLogWindow("Converting Families...");
                
                var instances = viewModel.SelectedRefs
                    .Select(r => doc.GetElement(r) as FamilyInstance)
                    .Where(i => i != null)
                    .Cast<FamilyInstance>()
                    .ToList();

                if (instances.Any())
                {
                    // Execute within a single command transaction if service doesn't manage its own for batching
                    // Current service handles family document transactions, but project document edits (placement) 
                    // need to be wrapped. However, RevitCommand provides a transaction if TransactionName is not null.
                    
                    using (Transaction t = new Transaction(doc, "Convert Families (Replace)"))
                    {
                        t.Start();
                        service.ConvertFamilyBatch(
                            doc, 
                            instances, 
                            viewModel.NewFamilyName, 
                            viewModel.TemplatePath, 
                            viewModel.IsTemporary,
                            viewModel.ReplaceInPlace
                        );
                        t.Commit();
                    }
                }
            }
        }
    }
}
