using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Views;
using LECG.ViewModels;
using LECG.Services;
using LECG.Services.Interfaces;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using LECG.Services.Logging;

namespace LECG.Commands
{
    /// <summary>
    /// Command to search and replace text in element names across the project.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SearchReplaceCommand : RevitCommand
    {
        // Internal transaction handling within service per batch or user action
        protected override string? TransactionName => null;

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            var service = ServiceLocator.GetRequiredService<ISearchReplaceService>();
            var vm = ServiceLocator.GetRequiredService<SearchReplaceViewModel>();
            
            vm.Initialize(service, doc);

            var view = ServiceLocator.GetRequiredService<SearchReplaceView>();
            view.DataContext = vm;
            bool? result = view.ShowDialog();

            if (result == true && vm.ShouldRun)
            {
                ShowLogWindow("Batch Rename");
                var reporter = new RevitCommandProgressReporter(Log, UpdateProgress);
                service.ExecuteBatchRename(doc, vm.PreviewItems.Where(i => i.IsChecked).ToList(), Logger.Instance, reporter);
                Log("Rename complete.");
            }
        }
    }
}
