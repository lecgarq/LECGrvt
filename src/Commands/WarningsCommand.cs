using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.ViewModels;
using LECG.Views;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class WarningsCommand : RevitCommand
    {
        public override void Execute(UIDocument uidoc, Document doc)
        {
            var vm = ServiceLocator.GetRequiredService<WarningsViewModel>();

            // Modal on purpose: select/show/isolate run inside this call stack,
            // in valid API context, with no ExternalEvent plumbing.
            var view = ServiceLocator.CreateWith<WarningsView>(vm);
            view.Initialize(uidoc);
            view.ShowDialog();
        }
    }
}
