using System;
using Autodesk.Revit.DB;
using LECG.Core.Purge;
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
        private readonly IPurgeExtendedElementService _purgeExtendedElementService;
        private readonly IPurgePassMessagingService _purgePassMessagingService;

        public PurgePassExecutionService(
            IPurgeLineStyleService purgeLineStyleService,
            IPurgeLinePatternService purgeLinePatternService,
            IPurgeFillPatternService purgeFillPatternService,
            IPurgeMaterialService purgeMaterialService,
            IPurgeLevelService purgeLevelService,
            IPurgeExtendedElementService purgeExtendedElementService,
            IPurgePassMessagingService purgePassMessagingService)
        {
            _purgeLineStyleService = purgeLineStyleService;
            _purgeLinePatternService = purgeLinePatternService;
            _purgeFillPatternService = purgeFillPatternService;
            _purgeMaterialService = purgeMaterialService;
            _purgeLevelService = purgeLevelService;
            _purgeExtendedElementService = purgeExtendedElementService;
            _purgePassMessagingService = purgePassMessagingService;
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

            _purgePassMessagingService.LogPassStart(reporter, passIndex);

            bool needsContext = options.LineStyles || options.LinePatterns || options.FillPatterns || options.Materials || options.Levels;
            PurgeContext? context = needsContext ? PurgeContext.Create(doc) : null;

            if (options.LineStyles)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Line Styles", 10 + (passIndex * 10));
                lineStylesDeleted += _purgeLineStyleService.PurgeUnusedLineStyles(doc, context!, reporter.Log);
            }

            if (options.LinePatterns)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Line Patterns", 15 + (passIndex * 10));
                linePatternsDeleted += _purgeLinePatternService.PurgeUnusedLinePatterns(doc, context!, reporter.Log);
            }

            if (options.FillPatterns)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Fill Patterns", 25 + (passIndex * 10));
                fillPatternsDeleted += _purgeFillPatternService.PurgeUnusedFillPatterns(doc, context!, reporter.Log);
            }

            if (options.Materials)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Materials", 35 + (passIndex * 10));
                materialsDeleted += _purgeMaterialService.PurgeUnusedMaterials(doc, context!, reporter.Log);
            }

            if (options.Levels)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Levels", 45 + (passIndex * 10));
                levelsDeleted += _purgeLevelService.PurgeUnusedLevels(doc, context!, reporter.Log);
            }

            if (options.Groups)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Groups", 50 + (passIndex * 10));
                groupsDeleted += _purgeExtendedElementService.PurgeUnusedGroups(doc, reporter.Log);
            }

            if (options.GridTypes)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Grid Types", 55 + (passIndex * 10));
                gridTypesDeleted += _purgeExtendedElementService.PurgeUnusedGridTypes(doc, reporter.Log);
            }

            if (options.LevelTypes)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Level Types", 60 + (passIndex * 10));
                levelTypesDeleted += _purgeExtendedElementService.PurgeUnusedLevelTypes(doc, reporter.Log);
            }

            if (options.Constraints)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Constraints", 65 + (passIndex * 10));
                constraintsDeleted += _purgeExtendedElementService.PurgeConstraints(doc, reporter.Log);
            }

            if (options.UnplacedRooms)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Unplaced Rooms", 70 + (passIndex * 10));
                unplacedRoomsDeleted += _purgeExtendedElementService.PurgeUnplacedRooms(doc, reporter.Log);
            }

            if (options.ViewTemplates)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "View Templates", 72 + (passIndex * 10));
                viewTemplatesDeleted += _purgeExtendedElementService.PurgeUnusedViewTemplates(doc, reporter.Log);
            }

            if (options.ViewFilters)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "View Filters", 74 + (passIndex * 10));
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
    }
}
