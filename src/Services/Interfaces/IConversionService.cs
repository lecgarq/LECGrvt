using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IConversionService
    {
        /// <summary>
        /// Converts Floor elements to Toposolid elements, preserving boundary and shape.
        /// </summary>
        void ConvertFloorToToposolid(Document doc, IList<Element> floors, ElementId toposolidTypeId, ElementId levelId, bool deleteSource, IProgressReporter reporter);

        /// <summary>
        /// Converts Toposolid elements to Floor elements, preserving boundary and shape.
        /// </summary>
        void ConvertToposolidToFloor(Document doc, IList<Element> toposolids, ElementId floorTypeId, ElementId levelId, bool deleteSource, IProgressReporter reporter);

        /// <summary>
        /// Gets all FloorType elements in the document.
        /// </summary>
        IList<ElementType> GetFloorTypes(Document doc);

        /// <summary>
        /// Gets all ToposolidType elements in the document.
        /// </summary>
        IList<ElementType> GetToposolidTypes(Document doc);

        /// <summary>
        /// Gets all Level elements in the document, ordered by elevation.
        /// </summary>
        IList<Level> GetLevels(Document doc);

        /// <summary>
        /// Attempts to find a matching target type by name similarity (case-insensitive).
        /// </summary>
        ElementType? FindMatchingType(string sourceName, IList<ElementType> targetTypes);
    }
}
