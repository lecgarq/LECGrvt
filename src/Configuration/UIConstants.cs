namespace LECG.Configuration
{
    /// <summary>
    /// Centralized constants for user-facing UI text (Button names, labels, tooltips).
    /// </summary>
    public static class UIConstants
    {
        // ... (Existing constants) ...
        // Assign Material
        public const string ButtonAssignMaterial_Name = "btnAssignMaterial";
        public const string ButtonAssignMaterial_Text = "Assign\nMaterial";
        public const string ButtonAssignMaterial_Tooltip = "Create and assign materials based on Floor/Toposolid Type Names.";

        // Clean Schemas
        public const string ButtonClean_Name = "cmdCleanSchemas";
        public const string ButtonClean_Text = "Clean\nSchemas";
        public const string ButtonClean_Tooltip = "Remove third-party plugin schemas from the project";

        // Home
        public const string ButtonHome_Name = "cmdHome";
        public const string ButtonHome_Text = "Home";
        public const string ButtonHome_Tooltip = "Open the LECG Home Window";

        // Offset Elevations
        public const string ButtonOffset_Name = "cmdOffsetElevations";
        public const string ButtonOffset_Text = "Offset\nElevations";
        public const string ButtonOffset_Tooltip = "Offset Height from Level for Toposolids and Floors";

        // Purge
        public const string ButtonPurge_Name = "cmdPurge";
        public const string ButtonPurge_Text = "Purge\nUnused";
        public const string ButtonPurge_Tooltip = "Purge unused line styles, fill patterns, and materials";

        // Render Appearance Match
        public const string ButtonRenderMatch_Name = "btnRenderMatch";
        public const string ButtonRenderMatch_Text = "Render\nMatch";
        public const string ButtonRenderMatch_Tooltip = "Sync graphics and identity with Render Appearance.";

        // Material Creator
        public const string ButtonMaterialCreator_Name = "btnMaterialCreator";
        public const string ButtonMaterialCreator_Text = "Create\nMaterials";
        public const string ButtonMaterialCreator_Tooltip = "Create materials from texture folders (Diffuse, Normal, Roughness, AO).";

        // Reset Slabs
        public const string ButtonResetSlabs_Name = "cmdResetSlabs";
        public const string ButtonResetSlabs_Text = "Reset\nSlabs"; // Newline for ribbon layout
        public const string ButtonResetSlabs_Tooltip = "Reset Slab Shapes for Toposolids and Floors";

        // Standards (Batch Rename)
        public const string ButtonSearchReplace_Name = "btnBatchRename";
        public const string ButtonSearchReplace_Text = "Batch\nRename";
        public const string ButtonSearchReplace_Tooltip = "Find and replace text or remove characters from element names.";

        // Toposolids (Simplify Points)
        public const string ButtonSimplifyPoints_Name = "btnSimplifyPoints";
        public const string ButtonSimplifyPoints_Text = "Simplify\nPoints";
        public const string ButtonSimplifyPoints_Tooltip = "Remove redundant sub-element points from Toposolids while preserving shape.";

        // Toposolids (Align Edges)
        public const string ButtonAlignEdges_Name = "btnAlignEdges";
        public const string ButtonAlignEdges_Text = "Align\nEdges";
        public const string ButtonAlignEdges_Tooltip = "Align points of one Toposolid to the surface of another.";

        // Update Contours
        public const string ButtonUpdateContours_Name = "btnUpdateContours";
        public const string ButtonUpdateContours_Text = "Update\nContours";
        public const string ButtonUpdateContours_Tooltip = "Apply or remove contour display on Toposolids.";

        // Change Level
        public const string ButtonChangeLevel_Name = "btnChangeLevel";
        public const string ButtonChangeLevel_Text = "Change\nLevel";
        public const string ButtonChangeLevel_Tooltip = "Move Toposolids to a new level while maintaining absolute elevation.";

        // Visualization (Sexy Revit)
        public const string ButtonSexy_Name = "btnSexyRevit";
        public const string ButtonSexy_Text = "Sexy\nRevit";
        public const string ButtonSexy_Tooltip = "Optimizes view for presentation.";

        // Standards (Convert Family)
        public const string ButtonConvertFamily_Name = "btnConvertFamily";
        public const string ButtonConvertFamily_Text = "Convert\nFamily";
        public const string ButtonConvertFamily_Tooltip = "Converts a hosted family instance to a work plane-based generic model.";

        // Align Elements
        public const string ButtonAlignLeft_Name = "btnAlignLeft";
        public const string ButtonAlignLeft_Text = "Align\nLeft";
        public const string ButtonAlignLeft_Tooltip = "Align selected elements to the left of the reference element.";

        public const string ButtonAlignCenter_Name = "btnAlignCenter";
        public const string ButtonAlignCenter_Text = "Align\nCenter";
        public const string ButtonAlignCenter_Tooltip = "Align selected elements to the horizontal center of the reference element.";

        public const string ButtonAlignRight_Name = "btnAlignRight";
        public const string ButtonAlignRight_Text = "Align\nRight";
        public const string ButtonAlignRight_Tooltip = "Align selected elements to the right of the reference element.";

        public const string ButtonAlignTop_Name = "btnAlignTop";
        public const string ButtonAlignTop_Text = "Align\nTop";
        public const string ButtonAlignTop_Tooltip = "Align selected elements to the top of the reference element.";

        public const string ButtonAlignMiddle_Name = "btnAlignMiddle";
        public const string ButtonAlignMiddle_Text = "Align\nMiddle";
        public const string ButtonAlignMiddle_Tooltip = "Align selected elements to the vertical center of the reference element.";

        public const string ButtonAlignBottom_Name = "btnAlignBottom";
        public const string ButtonAlignBottom_Text = "Align\nBottom";
        public const string ButtonAlignBottom_Tooltip = "Align selected elements to the bottom of the reference element.";

        public const string ButtonDistributeH_Name = "btnDistributeH";
        public const string ButtonDistributeH_Text = "Distribute\nHorizontally";
        public const string ButtonDistributeH_Tooltip = "Distribute selected elements evenly horizontally.";

        public const string ButtonDistributeV_Name = "btnDistributeV";
        public const string ButtonDistributeV_Text = "Distribute\nVertically";
        public const string ButtonDistributeV_Tooltip = "Distribute selected elements evenly vertically.";

        // Standards (Convert CAD)
        public const string ButtonConvertCad_Name = "btnConvertCad";
        public const string ButtonConvertCad_Text = "CAD\nBlocks";
        public const string ButtonConvertCad_Tooltip = "Convert Imported CAD to cleaned Detail Item families.";

        // Standards (Filter Copy)
        public const string ButtonFilterCopy_Name = "btnFilterCopy";
        public const string ButtonFilterCopy_Text = "Filter\nCopy";
        public const string ButtonFilterCopy_Tooltip = "Copy View Filters between Views and Templates.";
    }
}
