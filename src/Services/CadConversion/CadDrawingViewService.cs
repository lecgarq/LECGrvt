using System;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadDrawingViewService : ICadDrawingViewService
    {
        public View ResolveFamilyDrawingView(Document familyDoc)
        {
            View? planView = new FilteredElementCollector(familyDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .FirstOrDefault(v => !v.IsTemplate && v.ViewType == ViewType.FloorPlan)
                ?? new FilteredElementCollector(familyDoc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .FirstOrDefault(v => !v.IsTemplate && v.CanBePrinted);

            if (planView == null)
            {
                throw new Exception("Could not find a valid plan view in the family template to draw geometry.");
            }

            return planView;
        }
    }
}
