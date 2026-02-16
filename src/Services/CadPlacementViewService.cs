using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadPlacementViewService : ICadPlacementViewService
    {
        public View? ResolvePlacementView(Document doc, View? preferredView)
        {
            if (IsValidForDetailItem(preferredView))
            {
                return preferredView;
            }

            return new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .FirstOrDefault(v => !v.IsTemplate && IsValidForDetailItem(v));
        }

        private static bool IsValidForDetailItem(View? view)
        {
            if (view == null) return false;

            return view.ViewType == ViewType.FloorPlan
                || view.ViewType == ViewType.Section
                || view.ViewType == ViewType.Elevation
                || view.ViewType == ViewType.DraftingView
                || view.ViewType == ViewType.Detail;
        }
    }
}
