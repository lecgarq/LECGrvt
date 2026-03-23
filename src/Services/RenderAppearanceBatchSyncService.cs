using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
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
            Action<string>? logCallback = null,
            Action<double, string>? progressCallback = null)
        {
            BatchSync(doc, materials, new LegacyProgressReporter(progressCallback, logCallback));
        }

        public void BatchSync(
            Document doc,
            IEnumerable<Material> materials,
            IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(materials);
            ArgumentNullException.ThrowIfNull(reporter);

            var matsList = materials.ToList();
            if (!matsList.Any()) return;

            int total = matsList.Count;
            int processed = 0;
            int skipped = 0;
            int updated = 0;

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

                    if (!_syncExecutionService.TrySync(mat, solidId))
                    {
                        skipped++;
                        continue;
                    }

                    updated++;
                }
            });

            reporter.Log($"Sync Complete: {updated} updated, {skipped} skipped.");
            reporter.Report("Done", 100);
        }
    }
}
