using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class AlignElementsDistributionMoveService : IAlignElementsDistributionMoveService
    {
        public void MoveIntermediateElements(
            Document doc,
            List<(Element Element, BoundingBoxXYZ Box, double Position)> sortedItems,
            AlignMode mode)
        {
            var first = sortedItems[0];
            var last = sortedItems[sortedItems.Count - 1];

            double totalDistance = last.Position - first.Position;
            double step = totalDistance / (sortedItems.Count - 1);

            for (int i = 1; i < sortedItems.Count - 1; i++)
            {
                var item = sortedItems[i];
                double targetPos = first.Position + (step * i);
                double currentPos = item.Position;
                double diff = targetPos - currentPos;

                XYZ translation = (mode == AlignMode.DistributeHorizontally)
                    ? new XYZ(diff, 0, 0)
                    : new XYZ(0, diff, 0);

                if (!translation.IsZeroLength())
                {
                    ElementTransformUtils.MoveElement(doc, item.Element.Id, translation);
                }
            }
        }
    }
}
