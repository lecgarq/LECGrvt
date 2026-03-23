using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace LECG.Services.Interfaces
{
    public interface IAlignEdgesService
    {
        IReadOnlyList<AlignEdgesSourceResult> AlignEdges(Document doc, IList<Reference> targets, IList<Reference> references);
        IReadOnlyList<AlignEdgesSourceResult> AlignEdgesFindMyEdge(Document doc, IList<Reference> targets);
    }
}
