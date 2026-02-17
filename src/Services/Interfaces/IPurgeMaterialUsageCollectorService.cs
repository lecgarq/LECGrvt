using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IPurgeMaterialUsageCollectorService
    {
        HashSet<ElementId> CollectUsedMaterialIds(Document doc, HashSet<ElementId> validMaterialIds);
    }
}
