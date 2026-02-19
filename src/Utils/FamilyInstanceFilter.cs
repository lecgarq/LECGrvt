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
                return !family.IsWorkPlaneBased;
            }
            return false;
        }
        public bool AllowReference(Reference reference, XYZ position) => true;
    }
}
