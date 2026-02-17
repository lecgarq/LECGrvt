using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IAlignElementsDistributionItemService
    {
        List<(Element Element, BoundingBoxXYZ Box, double Position)> BuildAndSort(Document doc, List<Element> elements, AlignMode mode);
    }
}
