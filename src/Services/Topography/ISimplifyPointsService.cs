using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace LECG.Services.Interfaces
{
    public interface ISimplifyPointsService
    {
        void SimplifyPoints(Document doc, IEnumerable<Element> elements, IProgressReporter reporter);
    }
}
