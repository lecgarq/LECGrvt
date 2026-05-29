using System;
using Autodesk.Revit.DB;
using LECG.Core.Purge;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    /// <summary>
    /// Service for purging unused elements from Revit documents.
    /// </summary>
    public class PurgeService : IPurgeService
    {
        private readonly IPurgeMaterialService _purgeMaterialService;
        private readonly IPurgeLineStyleService _purgeLineStyleService;
        private readonly IPurgeLinePatternService _purgeLinePatternService;
        private readonly IPurgeFillPatternService _purgeFillPatternService;
        private readonly IPurgeLevelService _purgeLevelService;
        private readonly IPurgeParameterService _purgeParameterService;
        private readonly IPurgeSummaryService _purgeSummaryService;
        private readonly IPurgeExecutionCoordinatorService _purgeExecutionCoordinatorService;

        public PurgeService(
            IPurgeMaterialService purgeMaterialService,
            IPurgeLineStyleService purgeLineStyleService,
            IPurgeLinePatternService purgeLinePatternService,
            IPurgeFillPatternService purgeFillPatternService,
            IPurgeLevelService purgeLevelService,
            IPurgeParameterService purgeParameterService,
            IPurgeSummaryService purgeSummaryService,
            IPurgeExecutionCoordinatorService purgeExecutionCoordinatorService)
        {
            _purgeMaterialService = purgeMaterialService;
            _purgeLineStyleService = purgeLineStyleService;
            _purgeLinePatternService = purgeLinePatternService;
            _purgeFillPatternService = purgeFillPatternService;
            _purgeLevelService = purgeLevelService;
            _purgeParameterService = purgeParameterService;
            _purgeSummaryService = purgeSummaryService;
            _purgeExecutionCoordinatorService = purgeExecutionCoordinatorService;
        }

        public void PurgeAll(Document doc, int passCount, PurgeOptions options, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(options);

            PurgeResult result = _purgeExecutionCoordinatorService.Execute(doc, passCount, options, reporter);
            _purgeSummaryService.Report(reporter, result);
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
