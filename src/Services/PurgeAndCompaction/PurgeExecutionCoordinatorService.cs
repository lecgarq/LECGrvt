using System;
using Autodesk.Revit.DB;
using LECG.Core;
using LECG.Core.Purge;
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

        public PurgeResult Execute(
            Document doc,
            int passCount,
            PurgeOptions options,
            IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(reporter);

            PurgeResult total = PurgeResult.Empty;

            // Multi-pass purge for line styles, fill patterns, materials, levels.
            // Use one transaction per pass to reduce memory pressure on large models.
            // SafeFailureHandler only deletes warnings and rolls back on errors —
            // prevents cascading native crashes from auto-deleting elements (Revit 2026.4).
            var failureHandler = new SafeFailureHandler();
            foreach (int i in _purgePassSequenceService.GetPasses(passCount))
            {
                _transactionService.RunWithWarningHandler(doc, $"Purge Unused Elements - Pass {i + 1}", currentDoc =>
                {
                    PurgeResult passResult = _purgePassExecutionService.ExecutePass(currentDoc, i, options, reporter);
                    total = total.Add(passResult);
                }, failureHandler);
            }

            // Parameter purge runs ONCE after multi-pass (EditFamily handles its own transactions)
            if (options.Parameters)
            {
                reporter.Log("");
                reporter.Log("--- FAMILY PARAMETERS ---");
                reporter.Report("Purging unused family parameters...", 80);
                int parametersDeleted = _purgeParameterService.PurgeUnusedParameters(doc, reporter.Log);
                total = total with { Parameters = parametersDeleted };
            }

            return total;
        }
    }
}
