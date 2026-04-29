using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace LECG.Services.Interfaces
{
    public interface IFixPointsService
    {
        void FixPoints(Document doc, IList<Element> elements, int sensitivity, IProgressReporter reporter);
    }
}
