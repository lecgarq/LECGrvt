using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Views;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class HomeCommand : RevitCommand
    {
        public override void Execute(UIDocument uiDoc, Document doc)
        {
            var view = ServiceLocator.GetRequiredService<HomeView>();

            if (view.ShowDialog() != true)
            {
                return;
            }

            string selectedTool = view.Tag as string ?? "";

            if (selectedTool == "AlignMaster")
            {
                var alignView = ServiceLocator.GetRequiredService<AlignDashboardView>();

                if (alignView.ShowDialog() != true)
                {
                    return;
                }

                selectedTool = alignView.Tag as string ?? "";
            }

            RevitCommand? command = CreateCommand(selectedTool);
            if (command == null)
            {
                return;
            }

            command.PrepareAsSubCommand(CommandData);
            command.Execute(uiDoc, doc);
        }

        private static RevitCommand? CreateCommand(string toolTag) => toolTag switch
        {
            "AlignLeft" => ServiceLocator.CreateWith<AlignLeftCommand>(),
            "AlignCenter" => ServiceLocator.CreateWith<AlignCenterCommand>(),
            "AlignRight" => ServiceLocator.CreateWith<AlignRightCommand>(),
            "AlignTop" => ServiceLocator.CreateWith<AlignTopCommand>(),
            "AlignMiddle" => ServiceLocator.CreateWith<AlignMiddleCommand>(),
            "AlignBottom" => ServiceLocator.CreateWith<AlignBottomCommand>(),
            "DistributeH" => ServiceLocator.CreateWith<DistributeHorizontallyCommand>(),
            "DistributeV" => ServiceLocator.CreateWith<DistributeVerticallyCommand>(),
            "AssignMaterial" => ServiceLocator.CreateWith<AssignMaterialCommand>(),
            "SexyRevit" => ServiceLocator.CreateWith<SexyRevitCommand>(),
            "Purge" => ServiceLocator.CreateWith<PurgeCommand>(),
            "Offsets" => ServiceLocator.CreateWith<OffsetElevationsCommand>(),
            "ResetSlabs" => ServiceLocator.CreateWith<ResetSlabsCommand>(),
            "SimplifyPoints" => ServiceLocator.CreateWith<SimplifyPointsCommand>(),
            "AlignEdges" => ServiceLocator.CreateWith<AlignEdgesCommand>(),
            "UpdateContours" => ServiceLocator.CreateWith<UpdateContoursCommand>(),
            "ChangeLevel" => ServiceLocator.CreateWith<ChangeLevelCommand>(),
            "CleanSchemas" => ServiceLocator.CreateWith<CleanSchemasCommand>(),
            "RenderMatch" => ServiceLocator.CreateWith<RenderAppearanceMatchCommand>(),
            "FixPoints" => ServiceLocator.CreateWith<FixPointsCommand>(),
            "SplitBoundaries" => ServiceLocator.CreateWith<SplitBoundariesCommand>(),
            "ConvertFloorToToposolid" => ServiceLocator.CreateWith<ConvertFloorToToposolidCommand>(),
            "ConvertToposolidToFloor" => ServiceLocator.CreateWith<ConvertToposolidToFloorCommand>(),
            "ConvertFamily" => ServiceLocator.CreateWith<ConvertFamilyCommand>(),
            "ConvertCad" => ServiceLocator.CreateWith<ConvertCadCommand>(),
            "BatchRename" => ServiceLocator.CreateWith<SearchReplaceCommand>(),
            _ => null,
        };
    }
}
