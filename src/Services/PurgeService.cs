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

        public PurgeService() : this(new PurgeReferenceScannerService(), new PurgeMaterialService(), new PurgeLineStyleService(), new PurgeFillPatternService(), new PurgeLevelService(), new PurgeSummaryService(), new PurgePassMessagingService())
        {
        }

        public PurgeService(IPurgeReferenceScannerService referenceScanner, IPurgeMaterialService purgeMaterialService, IPurgeLineStyleService purgeLineStyleService, IPurgeFillPatternService purgeFillPatternService, IPurgeLevelService purgeLevelService, IPurgeSummaryService purgeSummaryService, IPurgePassMessagingService purgePassMessagingService)
        {
            _referenceScanner = referenceScanner;
            _purgeMaterialService = purgeMaterialService;
            _purgeLineStyleService = purgeLineStyleService;
            _purgeFillPatternService = purgeFillPatternService;
            _purgeLevelService = purgeLevelService;
            _purgeSummaryService = purgeSummaryService;
            _purgePassMessagingService = purgePassMessagingService;
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
                for (int i = 1; i <= 3; i++)
                {
                    _purgePassMessagingService.LogPassStart(logCallback, i);

                    if (lineStyles)
                    {
                        _purgePassMessagingService.LogCategoryCheck(logCallback, progressCallback, i, "Line Styles", 10 + (i * 10));
                        lineStylesDeleted += PurgeUnusedLineStyles(doc, logCallback);
                    }

                    if (fillPatterns)
                    {
                        _purgePassMessagingService.LogCategoryCheck(logCallback, progressCallback, i, "Fill Patterns", 20 + (i * 10));
                        fillPatternsDeleted += PurgeUnusedFillPatterns(doc, logCallback);
                    }

                    if (materials)
                    {
                        _purgePassMessagingService.LogCategoryCheck(logCallback, progressCallback, i, "Materials", 30 + (i * 10));
                        materialsDeleted += PurgeUnusedMaterials(doc, logCallback);
                    }
                    
                    if (levels)
                    {
                        _purgePassMessagingService.LogCategoryCheck(logCallback, progressCallback, i, "Levels", 40 + (i * 10));
                        levelsDeleted += PurgeUnusedLevels(doc, logCallback);
                    }
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

