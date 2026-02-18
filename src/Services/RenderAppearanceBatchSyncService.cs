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

        public RenderAppearanceBatchSyncService(
            IRenderAppearanceRefreshService refreshService,
            IRenderSolidFillPatternService solidFillPatternService,
            IRenderMaterialSyncExecutionService syncExecutionService,
            IRenderBatchProgressService renderBatchProgressService)
        {
            _refreshService = refreshService;
            _solidFillPatternService = solidFillPatternService;
            _syncExecutionService = syncExecutionService;
            _renderBatchProgressService = renderBatchProgressService;
        }

        public void BatchSync(
            Document doc,
            IEnumerable<Material> materials,
            Action<string>? logCallback = null,
            Action<double, string>? progressCallback = null)
        {
            var matsList = materials.ToList();
            if (!matsList.Any()) return;

            int total = matsList.Count;
            int processed = 0;
            int skipped = 0;
            int updated = 0;

            logCallback?.Invoke($"Analyzing {total} materials...");
            progressCallback?.Invoke(0, "Analyzing materials...");

            using (Transaction t = new Transaction(doc, "Sync Render Appearance"))
            {
                t.Start();

                _refreshService.Refresh(doc, matsList, logCallback);

                ElementId solidId = _solidFillPatternService.GetSolidFillPatternId(doc);

                foreach (Material mat in matsList)
                {
                    processed++;
                    if (_renderBatchProgressService.ShouldReport(processed))
                    {
                        progressCallback?.Invoke(_renderBatchProgressService.ToPercent(processed, total), $"Processing: {mat.Name}");
                    }

                    if (!_syncExecutionService.TrySync(mat, solidId))
                    {
                        skipped++;
                        continue;
                    }

                    updated++;
                }

                t.Commit();
            }

            logCallback?.Invoke($"Sync Complete: {updated} updated, {skipped} skipped.");
            progressCallback?.Invoke(100, "Done");
        }
    }
}
