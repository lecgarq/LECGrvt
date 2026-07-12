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
        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            var service = ServiceLocator.GetRequiredService<ISearchReplaceService>();
            var view = ServiceLocator.GetRequiredService<SearchReplaceView>();
            var vm = (SearchReplaceViewModel)view.DataContext;

            vm.Initialize(service, doc);
            bool? result = view.ShowDialog();

            if (result == true && vm.ShouldRun)
            {
                ShowLogWindow("Batch Rename");
                var reporter = new RevitCommandProgressReporter(_logger, UpdateProgress);
                service.ExecuteBatchRename(doc, vm.PreviewItems.Where(i => i.IsChecked).ToList(), _logger, reporter);
                UpdateProgress(100, "Complete");
                Log("Rename complete.");
            }
        }
    }
}
