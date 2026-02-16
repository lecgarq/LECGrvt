using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace LECG.Utils
{
    public class FamilyInstanceFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is FamilyInstance;
        public bool AllowReference(Reference reference, XYZ position) => true;
    }
}
