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
            SyncWithRenderAppearance(doc, mat, new LegacyProgressReporter(logCallback: logCallback));
        }

        public void SyncWithRenderAppearance(Document doc, Material mat, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(reporter);

            if (mat == null) return;

            try
            {
                mat.UseRenderAppearanceForShading = true;
            }
            catch (Exception ex)
            {
                Logging.Logger.Instance.LogWarning($"[RenderAppearanceSingleSyncService] Failed to set UseRenderAppearanceForShading for {mat.Name}: {ex.Message}");
            }

            doc.Regenerate();
            Color renderColor = mat.Color;
            ElementId solidId = _solidFillPatternService.GetSolidFillPatternId(doc);
            _graphicsApplyService.Apply(mat, renderColor, solidId, null);
            reporter.Log($"Synced graphics for: {mat.Name}");
        }
    }
}
