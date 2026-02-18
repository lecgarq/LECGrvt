using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace LECG.Services.Interfaces
{
    public interface IPurgeReferencedLevelService
    {
        HashSet<ElementId> CollectReferencedLevelIds(Document doc, HashSet<ElementId> validLevelIds);
    }
}
