using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IMaterialElementGroupingService
    {
        Dictionary<ElementId, List<Element>> GroupByType(IList<Element> elements);
    }
}
