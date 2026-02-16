using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace LECG.Interfaces
{
    public interface IAlignEdgesService
    {
        void AlignEdges(Document doc, IList<Reference> targets, IList<Reference> references);
    }
}
