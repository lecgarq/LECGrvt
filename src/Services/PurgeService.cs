#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Configuration;
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
        private readonly IPurgeSummaryService _purgeSummaryService;
        private readonly IPurgeExecutionCoordinatorService _purgeExecutionCoordinatorService;

        public PurgeService() : this(
            new PurgeMaterialService(),
            new PurgeLineStyleService(),
            new PurgeFillPatternService(),
            new PurgeLevelService(),
            new PurgeSummaryService(),
            new PurgeExecutionCoordinatorService(
                new PurgePassSequenceService(),
                new PurgePassExecutionService(
                    new PurgeLineStyleService(),
                    new PurgeFillPatternService(),
                    new PurgeMaterialService(),
                    new PurgeLevelService(),
                    new PurgePassMessagingService())))
        {
        }

        public PurgeService(
            IPurgeMaterialService purgeMaterialService,
            IPurgeLineStyleService purgeLineStyleService,
            IPurgeFillPatternService purgeFillPatternService,
            IPurgeLevelService purgeLevelService,
            IPurgeSummaryService purgeSummaryService,
            IPurgeExecutionCoordinatorService purgeExecutionCoordinatorService)
        {
            _purgeMaterialService = purgeMaterialService;
            _purgeLineStyleService = purgeLineStyleService;
            _purgeFillPatternService = purgeFillPatternService;
            _purgeLevelService = purgeLevelService;
            _purgeSummaryService = purgeSummaryService;
            _purgeExecutionCoordinatorService = purgeExecutionCoordinatorService;
        }

        public void PurgeAll(Document doc, bool lineStyles, bool fillPatterns, bool materials, bool levels, Action<string> logCallback, Action<double, string> progressCallback)
        {
            (int lineStylesDeleted, int fillPatternsDeleted, int materialsDeleted, int levelsDeleted) = _purgeExecutionCoordinatorService.Execute(
                doc,
                lineStyles,
                fillPatterns,
                materials,
                levels,
                logCallback,
                progressCallback);

            _purgeSummaryService.Report(logCallback, progressCallback, lineStylesDeleted, fillPatternsDeleted, materialsDeleted, levelsDeleted);
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
        /// </summary>
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

    }
}

