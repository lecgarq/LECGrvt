using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Views;
using LECG.ViewModels;
using LECG.Services;
using System.Linq;
using CommunityToolkit.Mvvm.Input;

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
            // Initialize Service & ViewModel
            var service = ServiceLocator.GetRequiredService<ISearchReplaceService>();
            var vm = new SearchReplaceViewModel();
            
            // Wire up Service
            vm.Initialize(service, doc);

            var view = new SearchReplaceView(vm);
            
            // Wire Up CloseAction
            vm.CloseAction = () => 
            {
                view.DialogResult = vm.ShouldRun;
                view.Close();
            };

            // Show UI
            bool? result = view.ShowDialog();
            
            // If user confirmed logic
            if (result == true && vm.ShouldRun)
            {
                // Show Log Window
                ShowLogWindow("Batch Rename");
                
                try
                {
                    if (_logWindow != null)
                    {
                        int count = service.ExecuteBatchRename(doc, vm.PreviewItems.ToList(), Services.Logging.Logger.Instance, UpdateProgress);
                    }
                }
                catch (System.Exception ex)
                {
                    Log($"CRITICAL ERROR: {ex.Message}");
                }
            }
        }
    }
}
