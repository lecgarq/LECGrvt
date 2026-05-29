using System;
using Autodesk.Revit.DB;
using LECG.Core;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgeExecutionCoordinatorService : IPurgeExecutionCoordinatorService
    {
        private readonly IPurgePassSequenceService _purgePassSequenceService;
        private readonly IPurgePassExecutionService _purgePassExecutionService;
        private readonly IPurgeParameterService _purgeParameterService;
        private readonly ITransactionService _transactionService;

        public PurgeExecutionCoordinatorService(
            IPurgePassSequenceService purgePassSequenceService,
            IPurgePassExecutionService purgePassExecutionService,
            IPurgeParameterService purgeParameterService,
            ITransactionService transactionService)
        {
            _purgePassSequenceService = purgePassSequenceService;
            _purgePassExecutionService = purgePassExecutionService;
            _purgeParameterService = purgeParameterService;
            _transactionService = transactionService;
        }

        public (int lineStylesDeleted, int linePatternsDeleted, int fillPatternsDeleted, int materialsDeleted, int levelsDeleted, int parametersDeleted, int groupsDeleted, int gridTypesDeleted, int levelTypesDeleted, int constraintsDeleted, int unplacedRoomsDeleted, int viewTemplatesDeleted, int viewFiltersDeleted) Execute(
            Document doc,
            int passCount,
            bool lineStyles,
            bool linePatterns,
            bool fillPatterns,
            bool materials,
            bool levels,
            bool parameters,
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
            int parametersDeleted = 0;
            int groupsDeleted = 0;
            int gridTypesDeleted = 0;
            int levelTypesDeleted = 0;
            int constraintsDeleted = 0;
            int unplacedRoomsDeleted = 0;
            int viewTemplatesDeleted = 0;
            int viewFiltersDeleted = 0;

            // Multi-pass purge for line styles, fill patterns, materials, levels.
            // Use one transaction per pass to reduce memory pressure on large models.
            // SafeFailureHandler only deletes warnings and rolls back on errors —
            // prevents cascading native crashes from auto-deleting elements (Revit 2026.4).
            var failureHandler = new SafeFailureHandler();
            foreach (int i in _purgePassSequenceService.GetPasses(passCount))
            {
                _transactionService.RunWithWarningHandler(doc, $"Purge Unused Elements - Pass {i + 1}", currentDoc =>
                {
                    (int lineStylesPass, int linePatternsPass, int fillPatternsPass, int materialsPass, int levelsPass, int groupsPass, int gridTypesPass, int levelTypesPass, int constraintsPass, int unplacedRoomsPass, int viewTemplatesPass, int viewFiltersPass) = _purgePassExecutionService.ExecutePass(
                        currentDoc,
                        i,
                        lineStyles,
                        linePatterns,
                        fillPatterns,
                        materials,
                        levels,
                        groups,
                        gridTypes,
                        levelTypes,
                        constraints,
                        unplacedRooms,
                        viewTemplates,
                        viewFilters,
                        reporter);

                    lineStylesDeleted += lineStylesPass;
                    linePatternsDeleted += linePatternsPass;
                    fillPatternsDeleted += fillPatternsPass;
                    materialsDeleted += materialsPass;
                    levelsDeleted += levelsPass;
                    groupsDeleted += groupsPass;
                    gridTypesDeleted += gridTypesPass;
                    levelTypesDeleted += levelTypesPass;
                    constraintsDeleted += constraintsPass;
                    unplacedRoomsDeleted += unplacedRoomsPass;
                    viewTemplatesDeleted += viewTemplatesPass;
                    viewFiltersDeleted += viewFiltersPass;
                }, failureHandler);
            }

            // Parameter purge runs ONCE after multi-pass (EditFamily handles its own transactions)
            if (parameters)
            {
                reporter.Log("");
                reporter.Log("--- FAMILY PARAMETERS ---");
                reporter.Report("Purging unused family parameters...", 80);
                parametersDeleted = _purgeParameterService.PurgeUnusedParameters(doc, reporter.Log);
            }

            return (lineStylesDeleted, linePatternsDeleted, fillPatternsDeleted, materialsDeleted, levelsDeleted, parametersDeleted, groupsDeleted, gridTypesDeleted, levelTypesDeleted, constraintsDeleted, unplacedRoomsDeleted, viewTemplatesDeleted, viewFiltersDeleted);
        }
    }
}
