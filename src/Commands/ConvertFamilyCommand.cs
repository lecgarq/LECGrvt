#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using LECG.Core;
using LECG.Services.Interfaces;
using LECG.ViewModels;
using LECG.Views;
using System;
using System.IO;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ConvertFamilyCommand : RevitCommand
    {
        protected override string? TransactionName => null;

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

            if (result == true && viewModel.ShouldRun && viewModel.SelectedRef != null)
            {
                // 3. Execute Conversion via Service
                ShowLogWindow("Converting Family...");
                
                FamilyInstance? instance = doc.GetElement(viewModel.SelectedRef!) as FamilyInstance;
                if (instance != null)
                {
                    service.ConvertFamily(
                        doc, 
                        instance, 
                        viewModel.NewFamilyName, 
                        viewModel.TemplatePath, 
                        viewModel.IsTemporary
                    );
                }
            }
        }
    }
}
