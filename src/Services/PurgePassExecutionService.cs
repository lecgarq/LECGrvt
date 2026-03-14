using System;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgePassExecutionService : IPurgePassExecutionService
    {
        private readonly IPurgeLineStyleService _purgeLineStyleService;
        private readonly IPurgeLinePatternService _purgeLinePatternService;
        private readonly IPurgeFillPatternService _purgeFillPatternService;
        private readonly IPurgeMaterialService _purgeMaterialService;
        private readonly IPurgeLevelService _purgeLevelService;
        private readonly IPurgePassMessagingService _purgePassMessagingService;

        public PurgePassExecutionService(
            IPurgeLineStyleService purgeLineStyleService,
            IPurgeLinePatternService purgeLinePatternService,
            IPurgeFillPatternService purgeFillPatternService,
            IPurgeMaterialService purgeMaterialService,
            IPurgeLevelService purgeLevelService,
            IPurgePassMessagingService purgePassMessagingService)
        {
            _purgeLineStyleService = purgeLineStyleService;
            _purgeLinePatternService = purgeLinePatternService;
            _purgeFillPatternService = purgeFillPatternService;
            _purgeMaterialService = purgeMaterialService;
            _purgeLevelService = purgeLevelService;
            _purgePassMessagingService = purgePassMessagingService;
        }

        public (int lineStylesDeleted, int linePatternsDeleted, int fillPatternsDeleted, int materialsDeleted, int levelsDeleted) ExecutePass(
            Document doc,
            int passIndex,
            bool lineStyles,
            bool linePatterns,
            bool fillPatterns,
            bool materials,
            bool levels,
            Action<string> logCallback,
            Action<double, string> progressCallback)
        {
            return ExecutePass(doc, passIndex, lineStyles, linePatterns, fillPatterns, materials, levels, new LegacyProgressReporter(progressCallback, logCallback));
        }

        public (int lineStylesDeleted, int linePatternsDeleted, int fillPatternsDeleted, int materialsDeleted, int levelsDeleted) ExecutePass(
            Document doc,
            int passIndex,
            bool lineStyles,
            bool linePatterns,
            bool fillPatterns,
            bool materials,
            bool levels,
            IProgressReporter reporter)
        {
            int lineStylesDeleted = 0;
            int linePatternsDeleted = 0;
            int fillPatternsDeleted = 0;
            int materialsDeleted = 0;
            int levelsDeleted = 0;

            _purgePassMessagingService.LogPassStart(reporter, passIndex);
            PurgeContext context = PurgeContext.Create(doc);

            if (lineStyles)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Line Styles", 10 + (passIndex * 10));
                lineStylesDeleted += _purgeLineStyleService.PurgeUnusedLineStyles(doc, context, reporter.Log);
            }

            if (linePatterns)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Line Patterns", 15 + (passIndex * 10));
                linePatternsDeleted += _purgeLinePatternService.PurgeUnusedLinePatterns(doc, context, reporter.Log);
            }

            if (fillPatterns)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Fill Patterns", 25 + (passIndex * 10));
                fillPatternsDeleted += _purgeFillPatternService.PurgeUnusedFillPatterns(doc, context, reporter.Log);
            }

            if (materials)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Materials", 35 + (passIndex * 10));
                materialsDeleted += _purgeMaterialService.PurgeUnusedMaterials(doc, context, reporter.Log);
            }

            if (levels)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Levels", 45 + (passIndex * 10));
                levelsDeleted += _purgeLevelService.PurgeUnusedLevels(doc, context, reporter.Log);
            }

            return (lineStylesDeleted, linePatternsDeleted, fillPatternsDeleted, materialsDeleted, levelsDeleted);
        }
    }
}
