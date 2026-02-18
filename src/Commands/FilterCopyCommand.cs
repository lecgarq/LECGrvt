using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.ViewModels;
using LECG.Views;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class FilterCopyCommand : RevitCommand
    {
        protected override string? TransactionName => null;

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            var viewModel = ServiceLocator.CreateWith<FilterCopyViewModel>(doc);
            var view = ServiceLocator.CreateWith<FilterCopyView>(viewModel);
            view.ShowDialog();
        }
    }
}
