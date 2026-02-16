using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IAlignEdgesIntersectorService
    {
        ReferenceIntersector Create(Document doc, IList<Reference> references);
    }
}
