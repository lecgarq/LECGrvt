using System;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class RenderAppearanceSingleSyncService : IRenderAppearanceSingleSyncService
    {
        private readonly IRenderSolidFillPatternService _solidFillPatternService;
        private readonly IRenderMaterialGraphicsApplyService _graphicsApplyService;

        public RenderAppearanceSingleSyncService(
            IRenderSolidFillPatternService solidFillPatternService,
            IRenderMaterialGraphicsApplyService graphicsApplyService)
        {
            _solidFillPatternService = solidFillPatternService;
            _graphicsApplyService = graphicsApplyService;
        }

        public void SyncWithRenderAppearance(Document doc, Material mat, Action<string>? logCallback = null)
        {
            if (mat == null) return;
            try { mat.UseRenderAppearanceForShading = true; } catch { }
            doc.Regenerate();
            Color renderColor = mat.Color;
            ElementId solidId = _solidFillPatternService.GetSolidFillPatternId(doc);
            _graphicsApplyService.Apply(mat, renderColor, solidId, null);
            logCallback?.Invoke($"  Ã¢Å“â€œ Synced graphics for: {mat.Name}");
        }
    }
}
