using System;
using Autodesk.Revit.DB;
using LECG.Core.Purge;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgePassExecutionService
    {
        private readonly PurgeLineStyleService _purgeLineStyleService;
        private readonly PurgeLinePatternService _purgeLinePatternService;
        private readonly PurgeFillPatternService _purgeFillPatternService;
        private readonly PurgeMaterialService _purgeMaterialService;
        private readonly PurgeLevelService _purgeLevelService;
        private readonly PurgeExtendedElementService _purgeExtendedElementService;

        public PurgePassExecutionService(
            PurgeLineStyleService purgeLineStyleService,
            PurgeLinePatternService purgeLinePatternService,
            PurgeFillPatternService purgeFillPatternService,
            PurgeMaterialService purgeMaterialService,
            PurgeLevelService purgeLevelService,
            PurgeExtendedElementService purgeExtendedElementService)
        {
            _purgeLineStyleService = purgeLineStyleService;
            _purgeLinePatternService = purgeLinePatternService;
            _purgeFillPatternService = purgeFillPatternService;
            _purgeMaterialService = purgeMaterialService;
            _purgeLevelService = purgeLevelService;
            _purgeExtendedElementService = purgeExtendedElementService;
        }

        public PurgeResult ExecutePass(
            Document doc,
            int passIndex,
            PurgeOptions options,
            IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(reporter);

            int lineStylesDeleted = 0;
            int linePatternsDeleted = 0;
            int fillPatternsDeleted = 0;
            int materialsDeleted = 0;
            int levelsDeleted = 0;
            int groupsDeleted = 0;
            int gridTypesDeleted = 0;
            int levelTypesDeleted = 0;
            int constraintsDeleted = 0;
            int unplacedRoomsDeleted = 0;
            int viewTemplatesDeleted = 0;
            int viewFiltersDeleted = 0;

            LogPassStart(reporter, passIndex);

            bool needsContext = options.LineStyles || options.LinePatterns || options.FillPatterns || options.Materials || options.Levels;
            PurgeContext? context = needsContext ? PurgeContext.Create(doc) : null;

            if (options.LineStyles)
            {
                LogCategoryCheck(reporter, passIndex, "Line Styles", 10 + (passIndex * 10));
                lineStylesDeleted += _purgeLineStyleService.PurgeUnusedLineStyles(doc, context!, reporter.Log);
            }

            if (options.LinePatterns)
            {
                LogCategoryCheck(reporter, passIndex, "Line Patterns", 15 + (passIndex * 10));
                linePatternsDeleted += _purgeLinePatternService.PurgeUnusedLinePatterns(doc, context!, reporter.Log);
            }

            if (options.FillPatterns)
            {
                LogCategoryCheck(reporter, passIndex, "Fill Patterns", 25 + (passIndex * 10));
                fillPatternsDeleted += _purgeFillPatternService.PurgeUnusedFillPatterns(doc, context!, reporter.Log);
            }

            if (options.Materials)
            {
                LogCategoryCheck(reporter, passIndex, "Materials", 35 + (passIndex * 10));
                materialsDeleted += _purgeMaterialService.PurgeUnusedMaterials(doc, context!, reporter.Log);
            }

            if (options.Levels)
            {
                LogCategoryCheck(reporter, passIndex, "Levels", 45 + (passIndex * 10));
                levelsDeleted += _purgeLevelService.PurgeUnusedLevels(doc, context!, reporter.Log);
            }

            if (options.Groups)
            {
                LogCategoryCheck(reporter, passIndex, "Groups", 50 + (passIndex * 10));
                groupsDeleted += _purgeExtendedElementService.PurgeUnusedGroups(doc, reporter.Log);
            }

            if (options.GridTypes)
            {
                LogCategoryCheck(reporter, passIndex, "Grid Types", 55 + (passIndex * 10));
                gridTypesDeleted += _purgeExtendedElementService.PurgeUnusedGridTypes(doc, reporter.Log);
            }

            if (options.LevelTypes)
            {
                LogCategoryCheck(reporter, passIndex, "Level Types", 60 + (passIndex * 10));
                levelTypesDeleted += _purgeExtendedElementService.PurgeUnusedLevelTypes(doc, reporter.Log);
            }

            if (options.Constraints)
            {
                LogCategoryCheck(reporter, passIndex, "Constraints", 65 + (passIndex * 10));
                constraintsDeleted += _purgeExtendedElementService.PurgeConstraints(doc, reporter.Log);
            }

            if (options.UnplacedRooms)
            {
                LogCategoryCheck(reporter, passIndex, "Unplaced Rooms", 70 + (passIndex * 10));
                unplacedRoomsDeleted += _purgeExtendedElementService.PurgeUnplacedRooms(doc, reporter.Log);
            }

            if (options.ViewTemplates)
            {
                LogCategoryCheck(reporter, passIndex, "View Templates", 72 + (passIndex * 10));
                viewTemplatesDeleted += _purgeExtendedElementService.PurgeUnusedViewTemplates(doc, reporter.Log);
            }

            if (options.ViewFilters)
            {
                LogCategoryCheck(reporter, passIndex, "View Filters", 74 + (passIndex * 10));
                viewFiltersDeleted += _purgeExtendedElementService.PurgeUnusedViewFilters(doc, reporter.Log);
            }

            return new PurgeResult(
                lineStylesDeleted,
                linePatternsDeleted,
                fillPatternsDeleted,
                materialsDeleted,
                levelsDeleted,
                0,
                groupsDeleted,
                gridTypesDeleted,
                levelTypesDeleted,
                constraintsDeleted,
                unplacedRoomsDeleted,
                viewTemplatesDeleted,
                viewFiltersDeleted);
        }

        private static void LogPassStart(IProgressReporter reporter, int passIndex, int totalPasses = 3)
        {
            ArgumentNullException.ThrowIfNull(reporter);
            reporter.Log($"--- PASS {passIndex}/{totalPasses} ---");
        }

        private static void LogCategoryCheck(
            IProgressReporter reporter,
            int passIndex,
            string categoryLabel,
            double progressValue)
        {
            ArgumentNullException.ThrowIfNull(reporter);
            ArgumentNullException.ThrowIfNull(categoryLabel);

            reporter.Log($"Checking {categoryLabel}...");
            reporter.Report($"Pass {passIndex}: Purging {categoryLabel.ToLower()}...", progressValue);
        }
    }
}
