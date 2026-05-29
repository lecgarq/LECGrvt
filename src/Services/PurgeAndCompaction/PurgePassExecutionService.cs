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

        public (int lineStylesDeleted, int linePatternsDeleted, int fillPatternsDeleted, int materialsDeleted, int levelsDeleted, int groupsDeleted, int gridTypesDeleted, int levelTypesDeleted, int constraintsDeleted, int unplacedRoomsDeleted, int viewTemplatesDeleted, int viewFiltersDeleted) ExecutePass(
            Document doc,
            int passIndex,
            bool lineStyles,
            bool linePatterns,
            bool fillPatterns,
            bool materials,
            bool levels,
            bool groups,
            bool gridTypes,
            bool levelTypes,
            bool constraints,
            bool unplacedRooms,
            bool viewTemplates,
            bool viewFilters,
            IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
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

            bool needsContext = lineStyles || linePatterns || fillPatterns || materials || levels;
            PurgeContext? context = needsContext ? PurgeContext.Create(doc) : null;

            if (lineStyles)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Line Styles", 10 + (passIndex * 10));
                lineStylesDeleted += _purgeLineStyleService.PurgeUnusedLineStyles(doc, context!, reporter.Log);
            }

            if (linePatterns)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Line Patterns", 15 + (passIndex * 10));
                linePatternsDeleted += _purgeLinePatternService.PurgeUnusedLinePatterns(doc, context!, reporter.Log);
            }

            if (fillPatterns)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Fill Patterns", 25 + (passIndex * 10));
                fillPatternsDeleted += _purgeFillPatternService.PurgeUnusedFillPatterns(doc, context!, reporter.Log);
            }

            if (materials)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Materials", 35 + (passIndex * 10));
                materialsDeleted += _purgeMaterialService.PurgeUnusedMaterials(doc, context!, reporter.Log);
            }

            if (levels)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Levels", 45 + (passIndex * 10));
                levelsDeleted += _purgeLevelService.PurgeUnusedLevels(doc, context!, reporter.Log);
            }

            if (groups)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Groups", 50 + (passIndex * 10));
                groupsDeleted += _purgeExtendedElementService.PurgeUnusedGroups(doc, reporter.Log);
            }

            if (gridTypes)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Grid Types", 55 + (passIndex * 10));
                gridTypesDeleted += _purgeExtendedElementService.PurgeUnusedGridTypes(doc, reporter.Log);
            }

            if (levelTypes)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Level Types", 60 + (passIndex * 10));
                levelTypesDeleted += _purgeExtendedElementService.PurgeUnusedLevelTypes(doc, reporter.Log);
            }

            if (constraints)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Constraints", 65 + (passIndex * 10));
                constraintsDeleted += _purgeExtendedElementService.PurgeConstraints(doc, reporter.Log);
            }

            if (unplacedRooms)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "Unplaced Rooms", 70 + (passIndex * 10));
                unplacedRoomsDeleted += _purgeExtendedElementService.PurgeUnplacedRooms(doc, reporter.Log);
            }

            if (viewTemplates)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "View Templates", 72 + (passIndex * 10));
                viewTemplatesDeleted += _purgeExtendedElementService.PurgeUnusedViewTemplates(doc, reporter.Log);
            }

            if (viewFilters)
            {
                _purgePassMessagingService.LogCategoryCheck(reporter, passIndex, "View Filters", 74 + (passIndex * 10));
                viewFiltersDeleted += _purgeExtendedElementService.PurgeUnusedViewFilters(doc, reporter.Log);
            }

            return (lineStylesDeleted, linePatternsDeleted, fillPatternsDeleted, materialsDeleted, levelsDeleted, groupsDeleted, gridTypesDeleted, levelTypesDeleted, constraintsDeleted, unplacedRoomsDeleted, viewTemplatesDeleted, viewFiltersDeleted);
        }
    }
}
