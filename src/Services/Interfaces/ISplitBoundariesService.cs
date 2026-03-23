using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ISplitBoundariesService
    {
        void SplitBoundaries(Document doc, IList<Element> elements, IProgressReporter reporter);
    }
}
