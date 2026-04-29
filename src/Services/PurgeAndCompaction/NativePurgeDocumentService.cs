using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class NativePurgeDocumentService : INativePurgeDocumentService
    {
        public int PurgeUnused(Document doc, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(reporter);

            ISet<ElementId> categories = new HashSet<ElementId>();
            ICollection<ElementId> unusedIds = doc.GetAllUnusedElements(categories);
            if (unusedIds.Count == 0)
            {
                return 0;
            }

            int deletedCount = 0;
            foreach (ElementId unusedId in unusedIds)
            {
                try
                {
                    ICollection<ElementId> deletedIds = doc.Delete(unusedId);
                    if (deletedIds.Count > 0)
                    {
                        deletedCount++;
                    }
                }
                catch
                {
                    // Some entries surfaced by Revit's purge window are informational or blocked.
                    // Ignore those and continue so the pass can remove everything that is deletable.
                }
            }

            doc.Regenerate();
            return deletedCount;
        }
    }
}
