using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IAlignElementsDistributionMoveService
    {
        void MoveIntermediateElements(
            Document doc,
            List<(Element Element, BoundingBoxXYZ Box, double Position)> sortedItems,
            AlignMode mode);
    }
}
