using System;
using Autodesk.Revit.DB;
using LECG.Core;
using LECG.Core.Purge;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    /// <summary>
    /// Service for purging unused elements from Revit documents.
    /// </summary>
    public class PurgeService
    {
        private readonly PurgeMaterialService _purgeMaterialService;
        private readonly PurgeLineStyleService _purgeLineStyleService;
        private readonly PurgeLinePatternService _purgeLinePatternService;
        private readonly PurgeFillPatternService _purgeFillPatternService;
        private readonly PurgeLevelService _purgeLevelService;
        private readonly PurgeParameterService _purgeParameterService;
        private readonly PurgePassExecutionService _purgePassExecutionService;
        private readonly ITransactionService _transactionService;

        public PurgeService(
            PurgeMaterialService purgeMaterialService,
            PurgeLineStyleService purgeLineStyleService,
            PurgeLinePatternService purgeLinePatternService,
            PurgeFillPatternService purgeFillPatternService,
            PurgeLevelService purgeLevelService,
            PurgeParameterService purgeParameterService,
            PurgePassExecutionService purgePassExecutionService,
            ITransactionService transactionService)
        {
            _purgeMaterialService = purgeMaterialService;
            _purgeLineStyleService = purgeLineStyleService;
            _purgeLinePatternService = purgeLinePatternService;
            _purgeFillPatternService = purgeFillPatternService;
            _purgeLevelService = purgeLevelService;
            _purgeParameterService = purgeParameterService;
            _purgePassExecutionService = purgePassExecutionService;
            _transactionService = transactionService;
        }

        public void PurgeAll(Document doc, int passCount, PurgeOptions options, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(options);

            PurgeResult result = Execute(doc, passCount, options, reporter);
            ReportSummary(reporter, result);
        }

        private PurgeResult Execute(
            Document doc,
            int passCount,
            PurgeOptions options,
            IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(reporter);

            PurgeResult total = PurgeResult.Empty;

            // Use one transaction per pass to reduce memory pressure on large models.
            var failureHandler = new SafeFailureHandler();
            foreach (int i in PurgeSequence.GetPasses(passCount))
            {
                _transactionService.RunWithWarningHandler(doc, $"Purge Unused Elements - Pass {i + 1}", currentDoc =>
                {
                    PurgeResult passResult = _purgePassExecutionService.ExecutePass(currentDoc, i, options, reporter);
                    total = total.Add(passResult);
                }, failureHandler);
            }

            // Parameter purge runs once after multi-pass; EditFamily owns its transactions.
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

        private static void ReportSummary(IProgressReporter reporter, PurgeResult result)
        {
            ArgumentNullException.ThrowIfNull(reporter);
            ArgumentNullException.ThrowIfNull(result);

            reporter.Report("Complete!", 100);
            reporter.Log("");
            reporter.Log("=== SUMMARY ===");
            LogIfActive(reporter, "Line Styles", result.LineStyles);
            LogIfActive(reporter, "Line Patterns", result.LinePatterns);
            LogIfActive(reporter, "Fill Patterns", result.FillPatterns);
            LogIfActive(reporter, "Materials", result.Materials);
            LogIfActive(reporter, "Levels", result.Levels);
            LogIfActive(reporter, "Groups", result.Groups);
            LogIfActive(reporter, "Grid Types", result.GridTypes);
            LogIfActive(reporter, "Level Types", result.LevelTypes);
            LogIfActive(reporter, "Constraints", result.Constraints);
            LogIfActive(reporter, "Unplaced Rooms", result.UnplacedRooms);
            LogIfActive(reporter, "View Templates", result.ViewTemplates);
            LogIfActive(reporter, "View Filters", result.ViewFilters);
            LogIfActive(reporter, "Family Parameters", result.Parameters);
            reporter.Log("");
            reporter.Log($"Total items purged: {result.Total}");
        }

        private static void LogIfActive(IProgressReporter reporter, string category, int count)
        {
            if (count > 0)
                reporter.Log($"{category} deleted: {count}");
        }

        /// <summary>
        /// Purge unused line styles.
        /// </summary>
        public int PurgeUnusedLineStyles(Document doc, Action<string>? logCallback = null)
        {
            return _purgeLineStyleService.PurgeUnusedLineStyles(doc, logCallback);
        }

        /// <summary>
        /// Purge unused line patterns.
        /// </summary>
        public int PurgeUnusedLinePatterns(Document doc, Action<string>? logCallback = null)
        {
            return _purgeLinePatternService.PurgeUnusedLinePatterns(doc, logCallback);
        }

        /// <summary>
        /// Purge unused fill patterns.
        /// </summary>
        public int PurgeUnusedFillPatterns(Document doc, Action<string>? logCallback = null)
        {
            return _purgeFillPatternService.PurgeUnusedFillPatterns(doc, logCallback);
        }

        /// <summary>
        /// Purge unused materials.
        /// </summary>
        public int PurgeUnusedMaterials(Document doc, Action<string>? logCallback = null)
        {
            return _purgeMaterialService.PurgeUnusedMaterials(doc, logCallback);
        }

        /// <summary>
        /// Purge unused levels.
        /// </summary>
        public int PurgeUnusedLevels(Document doc, Action<string>? logCallback = null)
        {
            return _purgeLevelService.PurgeUnusedLevels(doc, logCallback);
        }

        /// <summary>
        /// Purge unused family parameters across all editable families.
        /// </summary>
        public int PurgeUnusedParameters(Document doc, Action<string>? logCallback = null)
        {
            return _purgeParameterService.PurgeUnusedParameters(doc, logCallback);
        }
    }
}
