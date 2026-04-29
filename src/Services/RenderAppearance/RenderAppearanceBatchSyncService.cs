using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Models;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class RenderAppearanceBatchSyncService : IRenderAppearanceBatchSyncService
    {
        private readonly IRenderAppearanceRefreshService _refreshService;
        private readonly IRenderSolidFillPatternService _solidFillPatternService;
        private readonly IRenderMaterialSyncExecutionService _syncExecutionService;
        private readonly IRenderBatchProgressService _renderBatchProgressService;
        private readonly ITransactionService _transactionService;

        public RenderAppearanceBatchSyncService(
            IRenderAppearanceRefreshService refreshService,
            IRenderSolidFillPatternService solidFillPatternService,
            IRenderMaterialSyncExecutionService syncExecutionService,
            IRenderBatchProgressService renderBatchProgressService,
            ITransactionService transactionService)
        {
            _refreshService = refreshService;
            _solidFillPatternService = solidFillPatternService;
            _syncExecutionService = syncExecutionService;
            _renderBatchProgressService = renderBatchProgressService;
            _transactionService = transactionService;
        }

        public void BatchSync(
            Document doc,
            IEnumerable<Material> materials,
            RenderAppearanceSettings settings,
            Action<string>? logCallback = null,
            Action<double, string>? progressCallback = null)
        {
            BatchSync(doc, materials, settings, new LegacyProgressReporter(progressCallback, logCallback));
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
            int normalMapAlreadyNormal = 0;
            int normalMapNotApplicable = 0;
            int normalMapFailures = 0;

            reporter.Log($"Analyzing {total} materials...");
            reporter.Report("Analyzing materials...", 0);

            _transactionService.Run(doc, "Sync Render Appearance", _ =>
            {
                _refreshService.Refresh(doc, matsList, reporter.Log);

                ElementId solidId = _solidFillPatternService.GetSolidFillPatternId(doc);

                foreach (Material mat in matsList)
                {
                    processed++;
                    if (_renderBatchProgressService.ShouldReport(processed))
                    {
                        reporter.Report($"Processing: {mat.Name}", _renderBatchProgressService.ToPercent(processed, total));
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

            reporter.Log($"Sync Complete: {updated} updated, {unchanged} unchanged.");
            reporter.Report("Done", 100);
        }
    }
}
