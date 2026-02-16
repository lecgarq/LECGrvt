using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using LECG.Interfaces;
using System.Linq;

namespace LECG.Services
{
    public class ToposolidService : IToposolidService
    {
        public void UpdateContours(Document doc, ElementId toposolidTypeId, bool enablePrimary, double primaryInterval, bool enableSecondary, double secondaryInterval, bool isApplyMode)
        {
            var type = doc.GetElement(toposolidTypeId) as ToposolidType;
            if (type == null) return;

            ContourSetting contour = type.GetContourSetting();
            
            if (isApplyMode)
            {
                // Clear existing contours
                var existingItems = contour.GetContourSettingItems().ToList();
                foreach (var item in existingItems)
                {
                    contour.RemoveItem(item);
                }

                // Add Primary Contours
                if (enablePrimary)
                {
                    ElementId primarySubcat = new ElementId(BuiltInCategory.OST_ToposolidContours);
                    double intervalFeet = primarySubcat != ElementId.InvalidElementId ? primaryInterval * 3.28084 : 1.0; // Check valid just in case
                    intervalFeet = primaryInterval * 3.28084; // m to ft

                    // Re-verify the subCategory logic from original command
                    // Original: ElementId primarySubcat = new ElementId(BuiltInCategory.OST_ToposolidContours);
                    // It seems the original code instantiated ElementId directly with BuiltInCategory which is valid for Category lookup usually but AddContourRange expects a GraphicsStyleId or similar? 
                    // Actually AddContourRange takes "ElementId linePatternId" or "ElementId graphicsStyleId"?
                    // Let's re-read the original command carefully.
                    // Original: ElementId primarySubcat = new ElementId(BuiltInCategory.OST_ToposolidContours);
                    
                    contour.AddContourRange(0, 19685, intervalFeet, primarySubcat); // 6000m in feet
                }

                // Add Secondary Contours
                if (enableSecondary)
                {
                    ElementId secondarySubcat = new ElementId(BuiltInCategory.OST_ToposolidSecondaryContours);
                    double intervalFeet = secondaryInterval * 3.28084; // m to ft
                    contour.AddContourRange(0, 19685, intervalFeet, secondarySubcat); 
                }
            }
            else
            {
                // Remove mode - clear all contours
                var existingItems = contour.GetContourSettingItems().ToList();
                foreach (var item in existingItems)
                {
                    contour.RemoveItem(item);
                }
            }

            type.SetContourSettting(contour);
        }
    }
}
