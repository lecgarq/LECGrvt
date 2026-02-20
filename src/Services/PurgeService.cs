#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618
using System;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    /// <summary>
    /// Service for purging unused elements from Revit documents.
    /// Refactored to use RevitConstants and cleaner logic.
    /// </summary>
    public class PurgeService : IPurgeService
    {
        private readonly IPurgeMaterialService _purgeMaterialService;
        private readonly IPurgeLineStyleService _purgeLineStyleService;
        private readonly IPurgeFillPatternService _purgeFillPatternService;
        private readonly IPurgeLevelService _purgeLevelService;
        private readonly IPurgeParameterService _purgeParameterService;
        private readonly IPurgeSummaryService _purgeSummaryService;
        private readonly IPurgeExecutionCoordinatorService _purgeExecutionCoordinatorService;

        public PurgeService() : this(
            new PurgeMaterialService(),
            new PurgeLineStyleService(),
            new PurgeFillPatternService(),
            new PurgeLevelService(),
            new PurgeParameterService(),
            new PurgeSummaryService(),
            new PurgeExecutionCoordinatorService(
                new PurgePassSequenceService(),
                new PurgePassExecutionService(
                    new PurgeLineStyleService(),
                    new PurgeFillPatternService(),
                    new PurgeMaterialService(),
                    new PurgeLevelService(),
                    new PurgePassMessagingService()),
                new PurgeParameterService()))
        {
        }

        public PurgeService(
            IPurgeMaterialService purgeMaterialService,
            IPurgeLineStyleService purgeLineStyleService,
            IPurgeFillPatternService purgeFillPatternService,
            IPurgeLevelService purgeLevelService,
            IPurgeParameterService purgeParameterService,
            IPurgeSummaryService purgeSummaryService,
            IPurgeExecutionCoordinatorService purgeExecutionCoordinatorService)
        {
            _purgeMaterialService = purgeMaterialService;
            _purgeLineStyleService = purgeLineStyleService;
            _purgeFillPatternService = purgeFillPatternService;
            _purgeLevelService = purgeLevelService;
            _purgeParameterService = purgeParameterService;
            _purgeSummaryService = purgeSummaryService;
            _purgeExecutionCoordinatorService = purgeExecutionCoordinatorService;
        }

        public void PurgeAll(Document doc, int passCount, bool lineStyles, bool fillPatterns, bool materials, bool levels, bool parameters, Action<string> logCallback, Action<double, string> progressCallback)
        {
            (int lineStylesDeleted, int fillPatternsDeleted, int materialsDeleted, int levelsDeleted, int parametersDeleted) = _purgeExecutionCoordinatorService.Execute(
                doc,
                passCount,
                lineStyles,
                fillPatterns,
                materials,
                levels,
                parameters,
                logCallback,
                progressCallback);

            _purgeSummaryService.Report(logCallback, progressCallback, lineStylesDeleted, fillPatternsDeleted, materialsDeleted, levelsDeleted, parametersDeleted);
        }

        /// <summary>
        /// Purge unused line styles.
        /// </summary>
        public int PurgeUnusedLineStyles(Document doc, Action<string>? logCallback = null)
        {
            return _purgeLineStyleService.PurgeUnusedLineStyles(doc, logCallback);
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
        /// Optimized to use HashSet lookups instead of expensive doc.GetElement calls.
        /// </summary>
        public int PurgeUnusedMaterials(Document doc, Action<string>? logCallback = null)
        {
            return _purgeMaterialService.PurgeUnusedMaterials(doc, logCallback);
        }

        /// <summary>
        /// Purge unused levels.
        /// A level is unused if:
        /// 1. No elements are placed on it (ElementLevelFilter).
        /// 2. It is not referenced by valid parameters (e.g. Top Constraint) - similar scan to Materials.
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
