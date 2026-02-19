using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace LECG.Utils
{
    public class FamilyInstanceFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem is FamilyInstance instance)
            {
                Family family = instance.Symbol.Family;
                Parameter? p = family.get_Parameter(BuiltInParameter.FAMILY_WORK_PLANE_BASED);
                bool isWorkPlaneBased = p != null && p.AsInteger() == 1;
                return LECG.Core.Naming.FamilySelectionPolicy.IsSafeToConvert(isWorkPlaneBased);
            }
            return false;
        }
        public bool AllowReference(Reference reference, XYZ position) => true;
    }
}
