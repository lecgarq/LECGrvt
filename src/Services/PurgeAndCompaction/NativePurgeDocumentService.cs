using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

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
            int skippedCount = 0;
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
                catch (Exception ex) when (IsExpectedDeleteException(ex))
                {
                    // Some entries surfaced by Revit's purge window are informational or blocked
                    // (locked, system-owned, or required by another element). Skip those and
                    // continue so the pass can remove everything that is deletable. We narrow to
                    // the expected Revit/argument exceptions so a genuinely fatal failure
                    // (e.g. a broken transaction) still surfaces instead of being swallowed.
                    skippedCount++;
                }
            }

            doc.Regenerate();

            if (skippedCount > 0)
            {
                reporter.LogWarning($"  {skippedCount} unused element(s) could not be deleted (locked, system-owned, or still required) and were skipped.");
            }

            return deletedCount;
        }

        private static bool IsExpectedDeleteException(Exception ex)
        {
            return ex is ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.ArgumentException
                || ex is RevitExceptions.InvalidOperationException;
        }
    }
}
