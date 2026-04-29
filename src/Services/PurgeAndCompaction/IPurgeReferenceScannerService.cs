using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IPurgeReferenceScannerService
    {
        void AddIfValid(HashSet<ElementId> set, ElementId id);
        void CollectUsedIds(Element elem, HashSet<ElementId> validIds, HashSet<ElementId> usedIds);
    }
}
