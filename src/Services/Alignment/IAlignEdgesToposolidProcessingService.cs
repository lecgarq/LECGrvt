using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace LECG.Services.Interfaces
{
    public interface IAlignEdgesToposolidProcessingService
    {
        AlignEdgesSourceResult Process(Document doc, Reference source, ReferenceIntersector intersector, IList<ElementId>? referenceIds = null);
    }
}
