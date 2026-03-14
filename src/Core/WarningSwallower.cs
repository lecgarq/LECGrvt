using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace LECG.Core
{
    /// <summary>
    /// A canonical failures preprocessor that automatically swallows warnings 
    /// and resolves failures during Revit transactions.
    /// </summary>
    public class WarningSwallower : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            IList<FailureMessageAccessor> fmas = failuresAccessor.GetFailureMessages();
            if (fmas.Count == 0) return FailureProcessingResult.Continue;
            
            foreach (FailureMessageAccessor fma in fmas)
            {
                FailureSeverity severity = fma.GetSeverity();
                if (severity == FailureSeverity.Warning)
                {
                    failuresAccessor.DeleteWarning(fma);
                }
                else if (fma.HasResolutionOfType(FailureResolutionType.DeleteElements))
                {
                    fma.SetCurrentResolutionType(FailureResolutionType.DeleteElements);
                    failuresAccessor.ResolveFailure(fma);
                }
                else
                {
                    return FailureProcessingResult.ProceedWithRollBack;
                }
            }
            return FailureProcessingResult.ProceedWithCommit;
        }
    }
}
