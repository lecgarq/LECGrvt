using System;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class RenderAppearanceSingleSyncService : IRenderAppearanceSingleSyncService
    {
        private readonly IRenderSolidFillPatternService _solidFillPatternService;
        private readonly IRenderMaterialGraphicsApplyService _graphicsApplyService;
        private readonly ILogger _logger;

        public RenderAppearanceSingleSyncService(
            IRenderSolidFillPatternService solidFillPatternService,
            IRenderMaterialGraphicsApplyService graphicsApplyService,
            ILogger logger)
        {
            _solidFillPatternService = solidFillPatternService;
            _graphicsApplyService = graphicsApplyService;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            catch (ArgumentException ex)
            {
                _logger.LogWarning($"Failed to set UseRenderAppearanceForShading for {mat.Name}: {ex.Message}", scope: "RenderAppearanceSingleSync");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning($"Failed to set UseRenderAppearanceForShading for {mat.Name}: {ex.Message}", scope: "RenderAppearanceSingleSync");
            }
            catch (RevitExceptions.InvalidOperationException ex)
            {
                _logger.LogWarning($"Failed to set UseRenderAppearanceForShading for {mat.Name}: {ex.Message}", scope: "RenderAppearanceSingleSync");
            }

            doc.Regenerate();
            Color renderColor = mat.Color;
            ElementId solidId = _solidFillPatternService.GetSolidFillPatternId(doc);
            _graphicsApplyService.Apply(mat, renderColor, solidId, null);
            reporter.Log($"Synced graphics for: {mat.Name}");
        }
    }
}
