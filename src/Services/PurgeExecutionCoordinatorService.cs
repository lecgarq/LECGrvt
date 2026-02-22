using System;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgeExecutionCoordinatorService : IPurgeExecutionCoordinatorService
    {
        private readonly IPurgePassSequenceService _purgePassSequenceService;
        private readonly IPurgePassExecutionService _purgePassExecutionService;
        private readonly IPurgeParameterService _purgeParameterService;

        public PurgeExecutionCoordinatorService(
            IPurgePassSequenceService purgePassSequenceService,
            IPurgePassExecutionService purgePassExecutionService,
            IPurgeParameterService purgeParameterService)
        {
            _purgePassSequenceService = purgePassSequenceService;
            _purgePassExecutionService = purgePassExecutionService;
            _purgeParameterService = purgeParameterService;
        }

        public (int lineStylesDeleted, int fillPatternsDeleted, int materialsDeleted, int levelsDeleted, int parametersDeleted) Execute(
            Document doc,
            int passCount,
            bool lineStyles,
            bool fillPatterns,
            bool materials,
            bool levels,
            bool parameters,
            Action<string> logCallback,
            Action<double, string> progressCallback)
        {
            int lineStylesDeleted = 0;
            int fillPatternsDeleted = 0;
            int materialsDeleted = 0;
            int levelsDeleted = 0;
            int parametersDeleted = 0;

            // Multi-pass purge for line styles, fill patterns, materials, levels.
            // Use one transaction per pass to reduce memory pressure on large models.
            foreach (int i in _purgePassSequenceService.GetPasses(passCount))
            {
                using (Transaction t = new Transaction(doc, $"Purge Unused Elements - Pass {i + 1}"))
                {
                    t.Start();

                    (int lineStylesPass, int fillPatternsPass, int materialsPass, int levelsPass) = _purgePassExecutionService.ExecutePass(
                        doc,
                        i,
                        lineStyles,
                        fillPatterns,
                        materials,
                        levels,
                        logCallback,
                        progressCallback);

                    lineStylesDeleted += lineStylesPass;
                    fillPatternsDeleted += fillPatternsPass;
                    materialsDeleted += materialsPass;
                    levelsDeleted += levelsPass;

                    doc.Regenerate();
                    t.Commit();
                }
            }

            // Parameter purge runs ONCE after multi-pass (EditFamily handles its own transactions)
            if (parameters)
            {
                logCallback?.Invoke("");
                logCallback?.Invoke("--- FAMILY PARAMETERS ---");
                progressCallback?.Invoke(80, "Purging unused family parameters...");
                parametersDeleted = _purgeParameterService.PurgeUnusedParameters(doc, logCallback);
            }

            return (lineStylesDeleted, fillPatternsDeleted, materialsDeleted, levelsDeleted, parametersDeleted);
        }
    }
}
