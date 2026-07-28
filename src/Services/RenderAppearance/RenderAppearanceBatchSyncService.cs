using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Models;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class RenderAppearanceBatchSyncService
    {
        private readonly IRenderSolidFillPatternService _solidFillPatternService;
        private readonly RenderMaterialSyncExecutionService _syncExecutionService;
        private readonly ITransactionService _transactionService;

        public RenderAppearanceBatchSyncService(
            IRenderSolidFillPatternService solidFillPatternService,
            RenderMaterialSyncExecutionService syncExecutionService,
            ITransactionService transactionService)
        {
            _solidFillPatternService = solidFillPatternService;
            _syncExecutionService = syncExecutionService;
            _transactionService = transactionService;
        }

        public void BatchSync(
            Document doc,
            IEnumerable<Material> materials,
            RenderAppearanceSettings settings,
            IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(materials);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(reporter);

            var matsList = materials.ToList();
            if (!matsList.Any()) return;

            int total = matsList.Count;
            int processed = 0;
            int unchanged = 0;
            int updated = 0;
            int normalMapUpdated = 0;
            int normalMapAlreadyNormal = 0;
            int normalMapNotApplicable = 0;
            int normalMapFailures = 0;

            reporter.Log($"Analyzing {total} materials...");
            reporter.Report("Analyzing materials...", 0);

            _transactionService.Run(doc, "Sync Render Appearance", _ =>
            {
                ElementId solidId = _solidFillPatternService.GetSolidFillPatternId(doc);

                foreach (Material mat in matsList)
                {
                    processed++;
                    if (processed % 10 == 0)
                    {
                        reporter.Report($"Processing: {mat.Name}", (double)processed / total * 100);
                    }

                    RenderMaterialSyncResult result = _syncExecutionService.TrySync(mat, solidId, settings, reporter.Log);
                    if (!result.Changed)
                    {
                        unchanged++;
                    }
                    else
                    {
                        updated++;
                    }

                    if (result.NormalMapChanged)
                    {
                        normalMapUpdated++;
                    }

                    if (result.NormalMapStatus == NormalMapSyncStatus.AlreadyNormal)
                    {
                        normalMapAlreadyNormal++;
                    }

                    if (result.NormalMapNotApplicable)
                    {
                        normalMapNotApplicable++;
                    }

                    if (result.NormalMapFailed)
                    {
                        normalMapFailures++;
                        continue;
                    }
                }
            });

            reporter.Log($"Sync Complete: {updated} updated, {unchanged} unchanged. Normal maps set: {normalMapUpdated}.");
            reporter.Report("Done", 100);
        }
    }
}
