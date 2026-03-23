using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace LECG.Services.Interfaces
{
    public interface IAlignEdgesBoundaryCollectionService
    {
        List<XYZ> Collect(Document doc, Element slab, ReferenceIntersector intersector, double minSpacing, double maxSpacing);
    }
}
