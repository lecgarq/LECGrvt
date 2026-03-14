#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618
using Autodesk.Revit.Attributes;
using LECG.Services.Interfaces;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Utils;
using LECG.ViewModels;
using LECG.Views;
using System;
using System.Collections.Generic;

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
            var vm = ServiceLocator.GetRequiredService<ChangeLevelViewModel>();
            vm.Initialize(doc);

            // 3. View
            // Pass UIDocument to view for selection handling
            var view = ServiceLocator.GetRequiredService<ChangeLevelView>();
            view.Initialize(uidoc);

            view.ShowDialog();
        }
    }
}
