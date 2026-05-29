using System;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using LECG.Models;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class RenderMaterialSyncExecutionService : IRenderMaterialSyncExecutionService
    {
        private readonly IRenderMaterialSyncCheckService _syncCheckService;
        private readonly IRenderMaterialGraphicsApplyService _graphicsApplyService;
        private readonly IImageColorExtractionService _imageColorExtractionService;

        public RenderMaterialSyncExecutionService(
            IRenderMaterialSyncCheckService syncCheckService,
            IRenderMaterialGraphicsApplyService graphicsApplyService,
            IImageColorExtractionService imageColorExtractionService)
        {
            _syncCheckService = syncCheckService;
            _graphicsApplyService = graphicsApplyService;
            _imageColorExtractionService = imageColorExtractionService;
        }

        public RenderMaterialSyncResult TrySync(Material material, ElementId solidFillPatternId, RenderAppearanceSettings settings, Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(material);
            ArgumentNullException.ThrowIfNull(solidFillPatternId);
            ArgumentNullException.ThrowIfNull(settings);

            // Cheap in-memory check — skips image I/O for materials already fully synced.
            // Focuses expensive extraction on materials missing the pipeline (no UseRenderAppearance,
            // wrong fill IDs, or fill colors that have drifted from mat.Color).
            if (IsFullySynced(material, solidFillPatternId))
                return new RenderMaterialSyncResult(false, false, NormalMapSyncStatus.NotRequested);

            Color renderColor = GetRenderAppearanceColor(material);
            _graphicsApplyService.Apply(material, renderColor, solidFillPatternId, logCallback);
            return new RenderMaterialSyncResult(true, true, NormalMapSyncStatus.NotRequested);
        }

        private static bool IsFullySynced(Material mat, ElementId solidId)
        {
            if (!mat.UseRenderAppearanceForShading) return false;
            if (mat.SurfaceForegroundPatternId != solidId) return false;
            if (mat.SurfaceBackgroundPatternId != solidId) return false;
            if (mat.CutForegroundPatternId != solidId) return false;
            if (mat.CutBackgroundPatternId != solidId) return false;
            Color c = mat.Color;
            if (!ColorEquals(mat.SurfaceForegroundPatternColor, c)) return false;
            if (!ColorEquals(mat.SurfaceBackgroundPatternColor, c)) return false;
            if (!ColorEquals(mat.CutForegroundPatternColor, c)) return false;
            if (!ColorEquals(mat.CutBackgroundPatternColor, c)) return false;
            return true;
        }

        private static bool ColorEquals(Color? a, Color? b)
        {
            if (a == null || b == null) return false;
            return a.Red == b.Red && a.Green == b.Green && a.Blue == b.Blue;
        }

        private Color GetRenderAppearanceColor(Material material)
        {
            string? diffusePath = TryGetDiffuseBitmapPath(material);
            if (string.IsNullOrWhiteSpace(diffusePath) || !File.Exists(diffusePath))
            {
                return material.Color;
            }

            try
            {
                return _imageColorExtractionService.GetAverageColor(diffusePath);
            }
            catch (Exception)
            {
                return material.Color;
            }
        }

        private string? TryGetDiffuseBitmapPath(Material material)
        {
            if (material.AppearanceAssetId == ElementId.InvalidElementId)
            {
                return null;
            }

            var assetElement = material.Document.GetElement(material.AppearanceAssetId) as AppearanceAssetElement;
            Asset? asset = assetElement?.GetRenderingAsset();
            AssetProperty? diffuseProperty = asset?.FindByName("generic_diffuse");
            Asset? bitmapAsset = diffuseProperty?.GetSingleConnectedAsset();

            return TryGetAssetString(bitmapAsset, "unifiedbitmap_Bitmap")
                ?? TryGetAssetString(bitmapAsset, "texture_Bitmap");
        }

        private static string? TryGetAssetString(Asset? asset, string propertyName)
        {
            return asset?.FindByName(propertyName) is AssetPropertyString property && !string.IsNullOrWhiteSpace(property.Value)
                ? property.Value
                : null;
        }
    }
}
