using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace LECG.Core
{
    /// <summary>
    /// Shared selection filters for LECG commands.
    /// </summary>
    public static class SelectionFilters
    {
        /// <summary>
        /// Filter that allows only Toposolid elements.
        /// </summary>
        public class ToposolidFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                return elem.GetType().Name == "Toposolid";
            }

            public bool AllowReference(Reference reference, XYZ position) => true;
        }

        /// <summary>
        /// Filter that allows Floor, Toposolid, and related slab elements.
        /// </summary>
        public class SlabFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                if (elem is Floor) return true;
                if (elem is Toposolid) return true;
                if (elem.Category != null && elem.Category.Id.Value == (long)BuiltInCategory.OST_Toposolid) return true;
                return false;
            }

            public bool AllowReference(Reference reference, XYZ position) => false;
        }

        /// <summary>
        /// Filter that allows only Floor elements.
        /// </summary>
        public class FloorFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                return elem is Floor;
            }

            public bool AllowReference(Reference reference, XYZ position) => true;
        }

        /// <summary>
        /// Filter that allows any element that can host materials (Walls, Floors, Roofs, etc).
        /// </summary>
        public class MaterialHostFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                if (elem is HostObject) return true; // Walls, Floors, Ceilings, Roofs, Facias, Gutters, etc.
                if (elem is FamilyInstance) return true; // Columns, Generic Models, Furniture
                if (elem.Category != null && elem.Category.Id.Value == (long)BuiltInCategory.OST_Toposolid) return true;
                return false;
            }

            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
