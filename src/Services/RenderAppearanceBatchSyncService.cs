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
        private readonly IRenderMaterialSyncCheckService _syncCheckService;
        private readonly IRenderMaterialGraphicsApplyService _graphicsApplyService;

        public RenderAppearanceBatchSyncService(
            IRenderAppearanceRefreshService refreshService,
            IRenderSolidFillPatternService solidFillPatternService,
            IRenderMaterialSyncCheckService syncCheckService,
            IRenderMaterialGraphicsApplyService graphicsApplyService)
        {
            _refreshService = refreshService;
            _solidFillPatternService = solidFillPatternService;
            _syncCheckService = syncCheckService;
            _graphicsApplyService = graphicsApplyService;
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
                    if (processed % 10 == 0)
                    {
                        progressCallback?.Invoke((double)processed / total * 100, $"Processing: {mat.Name}");
                    }

                    Color renderColor = mat.Color;

                    if (_syncCheckService.IsMaterialSynced(mat, renderColor, solidId))
                    {
                        skipped++;
                        continue;
                    }

                    _graphicsApplyService.Apply(mat, renderColor, solidId);
                    updated++;
                }

                t.Commit();
            }

            logCallback?.Invoke($"Sync Complete: {updated} updated, {skipped} skipped.");
            progressCallback?.Invoke(100, "Done");
        }
    }
}
