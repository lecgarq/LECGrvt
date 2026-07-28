using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using LECG.Services.Interfaces;
using System.Linq;

namespace LECG.Services
{
    public class ToposolidService
    {
        private const double MetersToFeet = 3.28084;
        private const double MaxElevationFeet = 19685; // ~6000m in feet

        public void UpdateContours(Document doc, ElementId toposolidTypeId, bool enablePrimary, double primaryInterval, bool enableSecondary, double secondaryInterval, bool isApplyMode)
        {
            ArgumentNullException.ThrowIfNull(doc);

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
                    double intervalFeet = primaryInterval * MetersToFeet; // m to ft
                    contour.AddContourRange(-MaxElevationFeet, MaxElevationFeet, intervalFeet, primarySubcat);
                }

                // Add Secondary Contours
                if (enableSecondary)
                {
                    ElementId secondarySubcat = new ElementId(BuiltInCategory.OST_ToposolidSecondaryContours);
                    double intervalFeet = secondaryInterval * MetersToFeet; // m to ft
                    contour.AddContourRange(-MaxElevationFeet, MaxElevationFeet, intervalFeet, secondarySubcat);
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
