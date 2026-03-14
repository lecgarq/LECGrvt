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
        private readonly ITransactionService _transactionService;

        public PurgeExecutionCoordinatorService(
            IPurgePassSequenceService purgePassSequenceService,
            IPurgePassExecutionService purgePassExecutionService,
            IPurgeParameterService purgeParameterService,
            ITransactionService transactionService)
        {
            _purgePassSequenceService = purgePassSequenceService;
            _purgePassExecutionService = purgePassExecutionService;
            _purgeParameterService = purgeParameterService;
            _transactionService = transactionService;
        }

        public (int lineStylesDeleted, int linePatternsDeleted, int fillPatternsDeleted, int materialsDeleted, int levelsDeleted, int parametersDeleted) Execute(
            Document doc,
            int passCount,
            bool lineStyles,
            bool linePatterns,
            bool fillPatterns,
            bool materials,
            bool levels,
            bool parameters,
            Action<string> logCallback,
            Action<double, string> progressCallback)
        {
            return Execute(doc, passCount, lineStyles, linePatterns, fillPatterns, materials, levels, parameters, new LegacyProgressReporter(progressCallback, logCallback));
        }

        public (int lineStylesDeleted, int linePatternsDeleted, int fillPatternsDeleted, int materialsDeleted, int levelsDeleted, int parametersDeleted) Execute(
            Document doc,
            int passCount,
            bool lineStyles,
            bool linePatterns,
            bool fillPatterns,
            bool materials,
            bool levels,
            bool parameters,
            IProgressReporter reporter)
        {
            int lineStylesDeleted = 0;
            int linePatternsDeleted = 0;
            int fillPatternsDeleted = 0;
            int materialsDeleted = 0;
            int levelsDeleted = 0;
            int parametersDeleted = 0;

            // Multi-pass purge for line styles, fill patterns, materials, levels.
            // Use one transaction per pass to reduce memory pressure on large models.
            foreach (int i in _purgePassSequenceService.GetPasses(passCount))
            {
                _transactionService.Run(doc, $"Purge Unused Elements - Pass {i + 1}", currentDoc =>
                {
                    (int lineStylesPass, int linePatternsPass, int fillPatternsPass, int materialsPass, int levelsPass) = _purgePassExecutionService.ExecutePass(
                        currentDoc,
                        i,
                        lineStyles,
                        linePatterns,
                        fillPatterns,
                        materials,
                        levels,
                        reporter);

                    lineStylesDeleted += lineStylesPass;
                    linePatternsDeleted += linePatternsPass;
                    fillPatternsDeleted += fillPatternsPass;
                    materialsDeleted += materialsPass;
                    levelsDeleted += levelsPass;
                });
            }

            // Parameter purge runs ONCE after multi-pass (EditFamily handles its own transactions)
            if (parameters)
            {
                reporter.Log("");
                reporter.Log("--- FAMILY PARAMETERS ---");
                reporter.Report("Purging unused family parameters...", 80);
                parametersDeleted = _purgeParameterService.PurgeUnusedParameters(doc, reporter.Log);
            }

            return (lineStylesDeleted, linePatternsDeleted, fillPatternsDeleted, materialsDeleted, levelsDeleted, parametersDeleted);
        }
    }
}
