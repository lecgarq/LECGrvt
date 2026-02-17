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
        private readonly IPurgeReferenceScannerService _referenceScanner;
        private readonly IPurgeMaterialService _purgeMaterialService;
        private readonly IPurgeLineStyleService _purgeLineStyleService;
        private readonly IPurgeFillPatternService _purgeFillPatternService;
        private readonly IPurgeLevelService _purgeLevelService;
        private readonly IPurgeSummaryService _purgeSummaryService;
        private readonly IPurgePassMessagingService _purgePassMessagingService;
        private readonly IPurgePassSequenceService _purgePassSequenceService;
        private readonly IPurgePassExecutionService _purgePassExecutionService;

        public PurgeService() : this(new PurgeReferenceScannerService(), new PurgeMaterialService(), new PurgeLineStyleService(), new PurgeFillPatternService(), new PurgeLevelService(), new PurgeSummaryService(), new PurgePassMessagingService(), new PurgePassSequenceService(), new PurgePassExecutionService(new PurgeLineStyleService(), new PurgeFillPatternService(), new PurgeMaterialService(), new PurgeLevelService(), new PurgePassMessagingService()))
        {
        }

        public PurgeService(IPurgeReferenceScannerService referenceScanner, IPurgeMaterialService purgeMaterialService, IPurgeLineStyleService purgeLineStyleService, IPurgeFillPatternService purgeFillPatternService, IPurgeLevelService purgeLevelService, IPurgeSummaryService purgeSummaryService, IPurgePassMessagingService purgePassMessagingService, IPurgePassSequenceService purgePassSequenceService, IPurgePassExecutionService purgePassExecutionService)
        {
            _referenceScanner = referenceScanner;
            _purgeMaterialService = purgeMaterialService;
            _purgeLineStyleService = purgeLineStyleService;
            _purgeFillPatternService = purgeFillPatternService;
            _purgeLevelService = purgeLevelService;
            _purgeSummaryService = purgeSummaryService;
            _purgePassMessagingService = purgePassMessagingService;
            _purgePassSequenceService = purgePassSequenceService;
            _purgePassExecutionService = purgePassExecutionService;
        }

        public void PurgeAll(Document doc, bool lineStyles, bool fillPatterns, bool materials, bool levels, Action<string> logCallback, Action<double, string> progressCallback)
        {
            int lineStylesDeleted = 0;
            int fillPatternsDeleted = 0;
            int materialsDeleted = 0;
            int levelsDeleted = 0;

            using (Transaction t = new Transaction(doc, "Purge Unused Elements"))
            {
                t.Start();

                // Run 3 times to catch dependent elements
                foreach (int i in _purgePassSequenceService.GetPasses())
                {
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
                }

                t.Commit();
            }

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

        // ============================================
        // HELPERS
        // ============================================

        private bool DeleteElement(Document doc, ElementId id, string name, Action<string>? logCallback)
        {
            try
            {
                doc.Delete(id);
                logCallback?.Invoke($"  Deleted: {name}");
                return true;
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"  Could not delete '{name}': {ex.Message}");
                return false;
            }
        }

    }
}

