using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Core
{
    /// <summary>
    /// Safe failure handler that only deletes warnings and rolls back on errors.
    /// Unlike WarningSwallower, this never auto-deletes elements on commit failures,
    /// preventing cascading native crashes in Revit 2026.4.
    /// </summary>
    public class SafeFailureHandler : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            ArgumentNullException.ThrowIfNull(failuresAccessor);

            IList<FailureMessageAccessor> fmas = failuresAccessor.GetFailureMessages();
            if (fmas.Count == 0) return FailureProcessingResult.Continue;

            bool hasErrors = false;
            foreach (FailureMessageAccessor fma in fmas)
            {
                if (fma.GetSeverity() == FailureSeverity.Warning)
                {
                    failuresAccessor.DeleteWarning(fma);
                }
                else
                {
                    hasErrors = true;
                }
            }

            return hasErrors
                ? FailureProcessingResult.ProceedWithRollBack
                : FailureProcessingResult.ProceedWithCommit;
        }
    }
}
