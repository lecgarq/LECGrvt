using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Views;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class HomeCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var view = ServiceLocator.GetRequiredService<HomeView>();
            // owner is main window handled by base class or helper in ShowDialog if needed
            // For simple view interaction:
            System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(view);
            helper.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            
            if (view.ShowDialog() != true)
            {
                return Result.Cancelled;
            }

            // Dispatch to selected command
            string selectedTool = view.Tag as string ?? "";
            
            // Handle Align Master Sub-Menu
            if (selectedTool == "AlignMaster")
            {
                var alignView = ServiceLocator.GetRequiredService<AlignDashboardView>();
                System.Windows.Interop.WindowInteropHelper alignHelper = new System.Windows.Interop.WindowInteropHelper(alignView);
                alignHelper.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

                if (alignView.ShowDialog() != true) return Result.Cancelled;
                selectedTool = alignView.Tag as string ?? "";
            }

            return DispatchCommand(selectedTool, data, ref message, elements);
        }

        private Result DispatchCommand(string toolTag, ExternalCommandData data, ref string message, ElementSet elements)
        {
            IExternalCommand? cmd = null;

            switch (toolTag)
            {
                case "AlignLeft": cmd = new AlignLeftCommand(); break;
                case "AlignCenter": cmd = new AlignCenterCommand(); break;
                case "AlignRight": cmd = new AlignRightCommand(); break;
                case "AlignTop": cmd = new AlignTopCommand(); break;
                case "AlignMiddle": cmd = new AlignMiddleCommand(); break;
                case "AlignBottom": cmd = new AlignBottomCommand(); break;
                case "DistributeH": cmd = new DistributeHorizontallyCommand(); break;
                case "DistributeV": cmd = new DistributeVerticallyCommand(); break;
                
                case "AssignMaterial": cmd = new AssignMaterialCommand(); break;
                case "SexyRevit": cmd = new SexyRevitCommand(); break;
                case "Purge": cmd = new PurgeCommand(); break;
                case "Offsets": cmd = new OffsetElevationsCommand(); break;
                case "ResetSlabs": cmd = new ResetSlabsCommand(); break;
                case "SimplifyPoints": cmd = new SimplifyPointsCommand(); break;
                case "AlignEdges": cmd = new AlignEdgesCommand(); break;
                case "UpdateContours": cmd = new UpdateContoursCommand(); break;
                case "ChangeLevel": cmd = new ChangeLevelCommand(); break;
                case "CleanSchemas": cmd = new CleanSchemasCommand(); break;
                case "RenderMatch": cmd = new RenderAppearanceMatchCommand(); break;
                case "ConvertFamily": cmd = new ConvertFamilyCommand(); break;
                case "ConvertCad": cmd = new ConvertCadCommand(); break;
                case "BatchRename": cmd = new SearchReplaceCommand(); break;

                default: return Result.Cancelled;
            }

            if (cmd != null)
            {
                return cmd.Execute(data, ref message, elements);
            }
            return Result.Succeeded;
        }
    }
}
