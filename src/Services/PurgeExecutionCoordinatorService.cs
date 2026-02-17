using System;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgeExecutionCoordinatorService : IPurgeExecutionCoordinatorService
    {
        private readonly IPurgePassSequenceService _purgePassSequenceService;
        private readonly IPurgePassExecutionService _purgePassExecutionService;

        public PurgeExecutionCoordinatorService(
            IPurgePassSequenceService purgePassSequenceService,
            IPurgePassExecutionService purgePassExecutionService)
        {
            _purgePassSequenceService = purgePassSequenceService;
            _purgePassExecutionService = purgePassExecutionService;
        }

        public (int lineStylesDeleted, int fillPatternsDeleted, int materialsDeleted, int levelsDeleted) Execute(
            Document doc,
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

            using (Transaction t = new Transaction(doc, "Purge Unused Elements"))
            {
                t.Start();

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

            return (lineStylesDeleted, fillPatternsDeleted, materialsDeleted, levelsDeleted);
        }
    }
}
