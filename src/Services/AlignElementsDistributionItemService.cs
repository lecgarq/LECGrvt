using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class AlignElementsDistributionItemService : IAlignElementsDistributionItemService
    {
        public List<(Element Element, BoundingBoxXYZ Box, double Position)> BuildAndSort(Document doc, List<Element> elements, AlignMode mode)
        {
            List<(Element Element, BoundingBoxXYZ Box, double Position)> sortedItems = new List<(Element, BoundingBoxXYZ, double)>();

            foreach (Element el in elements)
            {
                BoundingBoxXYZ? box = el.get_BoundingBox(doc.ActiveView);
                if (box == null) continue;

                double pos = (mode == AlignMode.DistributeHorizontally)
                    ? (box.Min.X + box.Max.X) / 2.0
                    : (box.Min.Y + box.Max.Y) / 2.0;

                sortedItems.Add((el, box, pos));
            }

            return sortedItems.OrderBy(x => x.Position).ToList();
        }
    }
}
