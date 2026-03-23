using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System.Collections.Generic;

namespace LECG.Services
{
    public class PurgeReferencedLevelService : IPurgeReferencedLevelService
    {
        public PurgeReferencedLevelService()
        {
        }

        public HashSet<ElementId> CollectReferencedLevelIds(Document doc, HashSet<ElementId> validLevelIds)
        {
            return CollectReferencedLevelIds(PurgeContext.Create(doc), validLevelIds);
        }

        public HashSet<ElementId> CollectReferencedLevelIds(PurgeContext context, HashSet<ElementId> validLevelIds)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(validLevelIds);

            var referencedLevelIds = new HashSet<ElementId>();
            foreach (ElementId referencedId in context.ParameterReferencedIds)
            {
                if (validLevelIds.Contains(referencedId))
                {
                    referencedLevelIds.Add(referencedId);
                }
            }

            return referencedLevelIds;
        }
    }
}
