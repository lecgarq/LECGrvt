#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618
using Autodesk.Revit.Attributes;
using LECG.Interfaces;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Utils;
using LECG.ViewModels;
using LECG.Views;
using System;
using System.Collections.Generic;
using System.Windows.Interop;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ChangeLevelCommand : RevitCommand
    {
        public override void Execute(UIDocument uidoc, Document doc)
        {
            // 1. Resolve Service
            var service = ServiceLocator.GetRequiredService<IChangeLevelService>();

            // 2. ViewModel
            var vm = new ChangeLevelViewModel(doc, service);

            // 3. View
            // Pass UIDocument to view for selection handling
            var view = new ChangeLevelView(vm, uidoc);
            
            // Set owner to Revit window
            WindowInteropHelper helper = new WindowInteropHelper(view);
            helper.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            
            view.ShowDialog();
        }
    }
}
