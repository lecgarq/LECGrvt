using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgeReferenceScannerService : IPurgeReferenceScannerService
    {
        public void AddIfValid(HashSet<ElementId> set, ElementId id)
        {
            ArgumentNullException.ThrowIfNull(set);
            ArgumentNullException.ThrowIfNull(id);

            if (id != ElementId.InvalidElementId)
            {
                set.Add(id);
            }
        }

        public void CollectUsedIds(Element elem, HashSet<ElementId> validIds, HashSet<ElementId> usedIds)
        {
            ArgumentNullException.ThrowIfNull(elem);
            ArgumentNullException.ThrowIfNull(validIds);
            ArgumentNullException.ThrowIfNull(usedIds);

            try
            {
                foreach (Parameter param in elem.Parameters)
                {
                    if (param.StorageType == StorageType.ElementId)
                    {
                        ElementId id = param.AsElementId();
                        if (validIds.Contains(id))
                        {
                            usedIds.Add(id);
                        }
                    }
                }
            }
            catch
            {
            }
        }
    }
}
