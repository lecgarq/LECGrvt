using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class RenderAppearanceService : IRenderAppearanceService
    {
        private readonly IRenderSolidFillPatternService _solidFillPatternService;
        private readonly IRenderMaterialSyncCheckService _syncCheckService;
        private readonly IRenderAppearanceRefreshService _refreshService;

        public RenderAppearanceService() : this(new RenderSolidFillPatternService(), new RenderMaterialSyncCheckService(), new RenderAppearanceRefreshService())
        {
        }

        public RenderAppearanceService(IRenderSolidFillPatternService solidFillPatternService, IRenderMaterialSyncCheckService syncCheckService, IRenderAppearanceRefreshService refreshService)
        {
            _solidFillPatternService = solidFillPatternService;
            _syncCheckService = syncCheckService;
            _refreshService = refreshService;
        }

        public void SyncWithRenderAppearance(Document doc, Material mat, Action<string>? logCallback = null)
        {
            if (mat == null) return;
            try { mat.UseRenderAppearanceForShading = true; } catch { }
            doc.Regenerate();
            Color renderColor = mat.Color;
            ApplyMaterialProperties(doc, mat, renderColor, null);
            logCallback?.Invoke($"  âœ“ Synced graphics for: {mat.Name}");
        }

        public void BatchSyncWithRenderAppearance(Document doc, IEnumerable<Material> materials, Action<string>? logCallback = null, Action<double, string>? progressCallback = null)
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

                    mat.SurfaceForegroundPatternId = solidId;
                    mat.SurfaceForegroundPatternColor = renderColor;
                    mat.SurfaceBackgroundPatternId = solidId;
                    mat.SurfaceBackgroundPatternColor = renderColor;
                    mat.CutForegroundPatternId = solidId;
                    mat.CutForegroundPatternColor = renderColor;
                    mat.CutBackgroundPatternId = solidId;
                    mat.CutBackgroundPatternColor = renderColor;

                    updated++;
                }

                t.Commit();
            }

            logCallback?.Invoke($"Sync Complete: {updated} updated, {skipped} skipped.");
            progressCallback?.Invoke(100, "Done");
        }

        private void ApplyMaterialProperties(Document doc, Material mat, Color color, Action<string>? logCallback)
        {
            ElementId solidId = _solidFillPatternService.GetSolidFillPatternId(doc);
            mat.Color = color;
            mat.SurfaceForegroundPatternId = solidId; mat.SurfaceForegroundPatternColor = color;
            mat.SurfaceBackgroundPatternId = solidId; mat.SurfaceBackgroundPatternColor = color;
            mat.CutForegroundPatternId = solidId; mat.CutForegroundPatternColor = color;
            mat.CutBackgroundPatternId = solidId; mat.CutBackgroundPatternColor = color;
            logCallback?.Invoke($"    â†’ Color: RGB({color.Red}, {color.Green}, {color.Blue})");
        }
    }
}
