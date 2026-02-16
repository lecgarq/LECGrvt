using System.Reflection;
using Autodesk.Revit.UI;
using LECG.Configuration;
using LECG.Utils;

namespace LECG.Core.Ribbon
{
    public class RibbonService : IRibbonService
    {
        public void InitializeRibbon(UIControlledApplication app)
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string tabName = AppConstants.TabName;

            // 1. Create Tab
            try { app.CreateRibbonTab(tabName); } catch { }

            // 2. Build Panels (Alphabetical Order)
            CreateHomePanel(app, tabName, assemblyPath);
            CreateHealthPanel(app, tabName, assemblyPath);
            CreateStandardsPanel(app, tabName, assemblyPath);
            CreateToposolidsPanel(app, tabName, assemblyPath);
            CreateAlignPanel(app, tabName, assemblyPath);
            CreateVisualizationPanel(app, tabName, assemblyPath);
        }

        private void CreateHomePanel(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(app, tabName, AppConstants.Panels.Home);
            string availability = "LECG.Core.ProjectDocumentAvailability";

            RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
                UIConstants.ButtonHome_Name,
                UIConstants.ButtonHome_Text,
                "LECG.Commands.HomeCommand",
                UIConstants.ButtonHome_Tooltip,
                AppImages.Home
            ), assemblyPath, availability);
        }

        private void CreateToposolidsPanel(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(app, tabName, AppConstants.Panels.Toposolids);
            string availability = "LECG.Core.ProjectDocumentAvailability";

            // Alphabetical: Assign, Offset, Reset
            RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
                UIConstants.ButtonAssignMaterial_Name,
                UIConstants.ButtonAssignMaterial_Text,
                "LECG.Commands.AssignMaterialCommand",
                UIConstants.ButtonAssignMaterial_Tooltip,
                AppImages.AssignMaterial
            ), assemblyPath, availability);

            RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
                UIConstants.ButtonOffset_Name,
                UIConstants.ButtonOffset_Text,
                "LECG.Commands.OffsetElevationsCommand",
                UIConstants.ButtonOffset_Tooltip,
                AppImages.ArrowUpDown
            ), assemblyPath, availability);

            RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
                UIConstants.ButtonResetSlabs_Name,
                UIConstants.ButtonResetSlabs_Text,
                "LECG.Commands.ResetSlabsCommand",
                UIConstants.ButtonResetSlabs_Tooltip,
                AppImages.ResetSlabs
            ), assemblyPath, availability);

            RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
                UIConstants.ButtonSimplifyPoints_Name,
                UIConstants.ButtonSimplifyPoints_Text,
                "LECG.Commands.SimplifyPointsCommand",
                UIConstants.ButtonSimplifyPoints_Tooltip,
                AppImages.SimplifyPoints
            ), assemblyPath, availability);

            RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
                UIConstants.ButtonAlignEdges_Name,
                UIConstants.ButtonAlignEdges_Text,
                "LECG.Commands.AlignEdgesCommand",
                UIConstants.ButtonAlignEdges_Tooltip,
                AppImages.AlignEdges
            ), assemblyPath, availability);

            RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
                UIConstants.ButtonUpdateContours_Name,
                UIConstants.ButtonUpdateContours_Text,
                "LECG.Commands.UpdateContoursCommand",
                UIConstants.ButtonUpdateContours_Tooltip,
                AppImages.UpdateContours
            ), assemblyPath, availability);

            RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
                UIConstants.ButtonChangeLevel_Name,
                UIConstants.ButtonChangeLevel_Text,
                "LECG.Commands.ChangeLevelCommand",
                UIConstants.ButtonChangeLevel_Tooltip,
                AppImages.ChangeLevel
            ), assemblyPath, availability);
        }

        private void CreateHealthPanel(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(app, tabName, AppConstants.Panels.ProjectHealth);
            string availability = "LECG.Core.ProjectDocumentAvailability";

            RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
                UIConstants.ButtonClean_Name,
                UIConstants.ButtonClean_Text,
                "LECG.Commands.CleanSchemasCommand",
                UIConstants.ButtonClean_Tooltip,
                AppImages.Eraser
            ), assemblyPath, availability);

            RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
                UIConstants.ButtonPurge_Name,
                UIConstants.ButtonPurge_Text,
                "LECG.Commands.PurgeCommand",
                UIConstants.ButtonPurge_Tooltip,
                AppImages.Trash
            ), assemblyPath, availability);
        }

        private void CreateVisualizationPanel(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(app, tabName, AppConstants.Panels.Visualization);
            string availability = "LECG.Core.ProjectDocumentAvailability";

            // Alphabetical: Render Match, Sexy Revit
            RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
                UIConstants.ButtonRenderMatch_Name,
                UIConstants.ButtonRenderMatch_Text,
                "LECG.Commands.RenderAppearanceMatchCommand",
                UIConstants.ButtonRenderMatch_Tooltip,
                AppImages.Palette
            ), assemblyPath, availability);



            RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
                UIConstants.ButtonSexy_Name,
                UIConstants.ButtonSexy_Text,
                "LECG.Commands.SexyRevitCommand",
                UIConstants.ButtonSexy_Tooltip,
                AppImages.Sparkles
            ), assemblyPath, availability);
        }

        private void CreateAlignPanel(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(app, tabName, AppConstants.Panels.Align);

            // Create Master Pulldown Button
            PulldownButton alignBtn = RibbonFactory.CreatePulldownButton(
                panel,
                "btnAlignMaster",
                "Align\nElements",
                "Align and Distribute Elements",
                AppImages.AlignMaster32,
                AppImages.AlignMaster16
            );

            if (alignBtn == null) return;

            // Add Items to Pulldown - NO AVAILABILITY RESTRICTION (Available always due to Master availability)
            string availability = ""; 

            // 1. Align Left/Center/Right
            RibbonFactory.AddItemToPulldown(alignBtn, new RibbonButtonConfig(
                UIConstants.ButtonAlignLeft_Name, "Align Left", "LECG.Commands.AlignLeftCommand", UIConstants.ButtonAlignLeft_Tooltip, 
                AppImages.AlignLeft32, AppImages.AlignLeft16), 
                assemblyPath, availability);

            RibbonFactory.AddItemToPulldown(alignBtn, new RibbonButtonConfig(
                UIConstants.ButtonAlignCenter_Name, "Align Center", "LECG.Commands.AlignCenterCommand", UIConstants.ButtonAlignCenter_Tooltip, 
                AppImages.AlignCenter32, AppImages.AlignCenter16), 
                assemblyPath, availability);

            RibbonFactory.AddItemToPulldown(alignBtn, new RibbonButtonConfig(
                UIConstants.ButtonAlignRight_Name, "Align Right", "LECG.Commands.AlignRightCommand", UIConstants.ButtonAlignRight_Tooltip, 
                AppImages.AlignRight32, AppImages.AlignRight16), 
                assemblyPath, availability);

            alignBtn.AddSeparator();

            // 2. Align Top/Middle/Bottom
            RibbonFactory.AddItemToPulldown(alignBtn, new RibbonButtonConfig(
                UIConstants.ButtonAlignTop_Name, "Align Top", "LECG.Commands.AlignTopCommand", UIConstants.ButtonAlignTop_Tooltip, 
                AppImages.AlignTop32, AppImages.AlignTop16), 
                assemblyPath, availability);

            RibbonFactory.AddItemToPulldown(alignBtn, new RibbonButtonConfig(
                UIConstants.ButtonAlignMiddle_Name, "Align Middle", "LECG.Commands.AlignMiddleCommand", UIConstants.ButtonAlignMiddle_Tooltip, 
                AppImages.AlignMiddle32, AppImages.AlignMiddle16), 
                assemblyPath, availability);

            RibbonFactory.AddItemToPulldown(alignBtn, new RibbonButtonConfig(
                UIConstants.ButtonAlignBottom_Name, "Align Bottom", "LECG.Commands.AlignBottomCommand", UIConstants.ButtonAlignBottom_Tooltip, 
                AppImages.AlignBottom32, AppImages.AlignBottom16), 
                assemblyPath, availability);

            alignBtn.AddSeparator();

            // 3. Distribute
            RibbonFactory.AddItemToPulldown(alignBtn, new RibbonButtonConfig(
                UIConstants.ButtonDistributeH_Name, "Distribute Horiz.", "LECG.Commands.DistributeHorizontallyCommand", UIConstants.ButtonDistributeH_Tooltip, 
                AppImages.DistributeH32, AppImages.DistributeH16), 
                assemblyPath, availability);

            RibbonFactory.AddItemToPulldown(alignBtn, new RibbonButtonConfig(
                UIConstants.ButtonDistributeV_Name, "Distribute Vert.", "LECG.Commands.DistributeVerticallyCommand", UIConstants.ButtonDistributeV_Tooltip, 
                AppImages.DistributeV32, AppImages.DistributeV16), 
                assemblyPath, availability);
        }

        private void CreateStandardsPanel(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = GetOrCreatePanel(app, tabName, AppConstants.Panels.Standards);
            string projectAvailability = "LECG.Core.ProjectDocumentAvailability";
            string familyAvailability = "LECG.Core.FamilyDocumentAvailability";
            string anyAvailability = ""; // Available everywhere

            RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
                UIConstants.ButtonConvertCad_Name,
                UIConstants.ButtonConvertCad_Text,
                "LECG.Commands.ConvertCadCommand",
                UIConstants.ButtonConvertCad_Tooltip,
                AppImages.ConvertFamily // Reusing ConvertFamily icon
            ), assemblyPath, projectAvailability);

            RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
                UIConstants.ButtonSearchReplace_Name,
                UIConstants.ButtonSearchReplace_Text,
                "LECG.Commands.SearchReplaceCommand",
                UIConstants.ButtonSearchReplace_Tooltip,
                AppImages.SearchReplace
            ), assemblyPath, projectAvailability);

            // Convert Family: Now available in both Project and Family environments
            RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
                UIConstants.ButtonConvertFamily_Name,
                UIConstants.ButtonConvertFamily_Text,
                "LECG.Commands.ConvertFamilyCommand",
                UIConstants.ButtonConvertFamily_Tooltip,
                AppImages.ConvertFamily
            ), assemblyPath, anyAvailability);

            // Convert Shared: Only available in Family environment
            RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
                "btnConvertShared",
                "Convert\nShared",
                "LECG.Commands.ConvertSharedCommand",
                "Convert Shared Family to Non-Shared",
                AppImages.ConvertShared
            ), assemblyPath, familyAvailability);

            // Filter Copy
            RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
                UIConstants.ButtonFilterCopy_Name,
                UIConstants.ButtonFilterCopy_Text,
                "LECG.Commands.FilterCopyCommand",
                UIConstants.ButtonFilterCopy_Tooltip,
                AppImages.SearchReplace // Placeholder icon
            ), assemblyPath, projectAvailability);
        }

        private RibbonPanel GetOrCreatePanel(UIControlledApplication app, string tabName, string panelName)
        {
            foreach (RibbonPanel p in app.GetRibbonPanels(tabName))
            {
                if (p.Name == panelName) return p;
            }
            return app.CreateRibbonPanel(tabName, panelName);
        }
    }
}
