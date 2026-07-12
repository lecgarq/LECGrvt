using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ISlabService
    {
        bool TryResetSlabShape(Element element, out string statusMessage);
        Element? DuplicateElement(Document doc, Element element);

        /// <summary>
        /// Snapshots only interior SlabShapeVertex positions into a safe list before modification.
        /// Enables the editor if it is not already enabled.
        /// </summary>
        List<XYZ> GetInteriorVertexPositions(Element element);

        /// <summary>
        /// Gets the SlabShapeEditor from a Floor or Toposolid.
        /// Returns null if the element is neither a Floor nor a Toposolid.
        /// </summary>
        SlabShapeEditor? GetEditor(Element element);
    }
}
