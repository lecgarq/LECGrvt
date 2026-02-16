using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace LECG.Interfaces
{
    public interface ISimplifyPointsService
    {
        void SimplifyPoints(Document doc, IEnumerable<Element> elements, Action<double, string> progressCallback, Action<string> logCallback);
    }
}
