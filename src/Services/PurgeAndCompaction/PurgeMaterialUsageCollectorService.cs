using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgeMaterialUsageCollectorService : IPurgeMaterialUsageCollectorService
    {
        public PurgeMaterialUsageCollectorService()
        {
        }

        public HashSet<ElementId> CollectUsedMaterialIds(Document doc, HashSet<ElementId> validMaterialIds)
        {
            return CollectUsedMaterialIds(PurgeContext.Create(doc), validMaterialIds);
        }

        public HashSet<ElementId> CollectUsedMaterialIds(PurgeContext context, HashSet<ElementId> validMaterialIds)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(validMaterialIds);

            var usedIds = new HashSet<ElementId>();

            foreach (ElementId usedMaterialId in context.UsedMaterialIds)
            {
                if (validMaterialIds.Contains(usedMaterialId))
                {
                    usedIds.Add(usedMaterialId);
                }
            }

            foreach (ElementId referencedId in context.ParameterReferencedIds)
            {
                if (validMaterialIds.Contains(referencedId))
                {
                    usedIds.Add(referencedId);
                }
            }

            return usedIds;
        }
    }
}
