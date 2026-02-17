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
        private readonly IRenderMaterialGraphicsApplyService _graphicsApplyService;
        private readonly IRenderAppearanceBatchSyncService _batchSyncService;

        public RenderAppearanceService() : this(new RenderSolidFillPatternService(), new RenderMaterialGraphicsApplyService(), new RenderAppearanceBatchSyncService(new RenderAppearanceRefreshService(), new RenderSolidFillPatternService(), new RenderMaterialSyncCheckService(), new RenderMaterialGraphicsApplyService()))
        {
        }

        public RenderAppearanceService(IRenderSolidFillPatternService solidFillPatternService, IRenderMaterialGraphicsApplyService graphicsApplyService, IRenderAppearanceBatchSyncService batchSyncService)
        {
            _solidFillPatternService = solidFillPatternService;
            _graphicsApplyService = graphicsApplyService;
            _batchSyncService = batchSyncService;
        }

        public void SyncWithRenderAppearance(Document doc, Material mat, Action<string>? logCallback = null)
        {
            if (mat == null) return;
            try { mat.UseRenderAppearanceForShading = true; } catch { }
            doc.Regenerate();
            Color renderColor = mat.Color;
            ElementId solidId = _solidFillPatternService.GetSolidFillPatternId(doc);
            _graphicsApplyService.Apply(mat, renderColor, solidId, null);
            logCallback?.Invoke($"  âœ“ Synced graphics for: {mat.Name}");
        }

        public void BatchSyncWithRenderAppearance(Document doc, IEnumerable<Material> materials, Action<string>? logCallback = null, Action<double, string>? progressCallback = null)
        {
            _batchSyncService.BatchSync(doc, materials, logCallback, progressCallback);
        }
    }
}


