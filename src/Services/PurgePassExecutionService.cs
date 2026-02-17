using System;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgePassExecutionService : IPurgePassExecutionService
    {
        private readonly IPurgeLineStyleService _purgeLineStyleService;
        private readonly IPurgeFillPatternService _purgeFillPatternService;
        private readonly IPurgeMaterialService _purgeMaterialService;
        private readonly IPurgeLevelService _purgeLevelService;
        private readonly IPurgePassMessagingService _purgePassMessagingService;

        public PurgePassExecutionService(
            IPurgeLineStyleService purgeLineStyleService,
            IPurgeFillPatternService purgeFillPatternService,
            IPurgeMaterialService purgeMaterialService,
            IPurgeLevelService purgeLevelService,
            IPurgePassMessagingService purgePassMessagingService)
        {
            _purgeLineStyleService = purgeLineStyleService;
            _purgeFillPatternService = purgeFillPatternService;
            _purgeMaterialService = purgeMaterialService;
            _purgeLevelService = purgeLevelService;
            _purgePassMessagingService = purgePassMessagingService;
        }

        public (int lineStylesDeleted, int fillPatternsDeleted, int materialsDeleted, int levelsDeleted) ExecutePass(
            Document doc,
            int passIndex,
            bool lineStyles,
            bool fillPatterns,
            bool materials,
            bool levels,
            Action<string> logCallback,
            Action<double, string> progressCallback)
        {
            int lineStylesDeleted = 0;
            int fillPatternsDeleted = 0;
            int materialsDeleted = 0;
            int levelsDeleted = 0;

            _purgePassMessagingService.LogPassStart(logCallback, passIndex);

            if (lineStyles)
            {
                _purgePassMessagingService.LogCategoryCheck(logCallback, progressCallback, passIndex, "Line Styles", 10 + (passIndex * 10));
                lineStylesDeleted += _purgeLineStyleService.PurgeUnusedLineStyles(doc, logCallback);
            }

            if (fillPatterns)
            {
                _purgePassMessagingService.LogCategoryCheck(logCallback, progressCallback, passIndex, "Fill Patterns", 20 + (passIndex * 10));
                fillPatternsDeleted += _purgeFillPatternService.PurgeUnusedFillPatterns(doc, logCallback);
            }

            if (materials)
            {
                _purgePassMessagingService.LogCategoryCheck(logCallback, progressCallback, passIndex, "Materials", 30 + (passIndex * 10));
                materialsDeleted += _purgeMaterialService.PurgeUnusedMaterials(doc, logCallback);
            }

            if (levels)
            {
                _purgePassMessagingService.LogCategoryCheck(logCallback, progressCallback, passIndex, "Levels", 40 + (passIndex * 10));
                levelsDeleted += _purgeLevelService.PurgeUnusedLevels(doc, logCallback);
            }

            return (lineStylesDeleted, fillPatternsDeleted, materialsDeleted, levelsDeleted);
        }
    }
}
