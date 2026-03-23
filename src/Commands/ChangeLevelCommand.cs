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
            var preselectedElements = SelectionSeedHelper.GetSelectedElements(uidoc, vm.Selection.Filter);
            if (preselectedElements.Count > 0)
            {
                vm.SetSelectedElements(preselectedElements);
            }

            // 3. View — pass VM explicitly so command and view share the same instance
            var view = ServiceLocator.CreateWith<ChangeLevelView>(vm);
            view.Initialize(uidoc);

            view.ShowDialog();
        }
    }
}
