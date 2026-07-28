using System;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using LECG.Models;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class RenderMaterialSyncExecutionService
    {
        private readonly IRenderMaterialGraphicsApplyService _graphicsApplyService;
        private readonly IImageColorExtractionService _imageColorExtractionService;

        public RenderMaterialSyncExecutionService(
            IRenderMaterialGraphicsApplyService graphicsApplyService,
            IImageColorExtractionService imageColorExtractionService)
        {
            _graphicsApplyService = graphicsApplyService;
            _imageColorExtractionService = imageColorExtractionService;
        }

        public RenderMaterialSyncResult TrySync(Material material, ElementId solidFillPatternId, RenderAppearanceSettings settings, Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(material);
            ArgumentNullException.ThrowIfNull(solidFillPatternId);
            ArgumentNullException.ThrowIfNull(settings);

            // Normal-map recognition runs independently of graphics sync state, so materials that are
            // already graphics-compliant still get their bump slot converted to the UnifiedBitmap
            // structure Enscape reads to fill its Normal slot.
            NormalMapSyncStatus normalStatus = TryNormalizeNormalMap(material, logCallback);

            // Cheap in-memory check — skips image I/O for materials already fully synced.
            // Focuses expensive extraction on materials missing the pipeline (no UseRenderAppearance,
            // wrong fill IDs, or fill colors that have drifted from mat.Color).
            if (IsFullySynced(material, solidFillPatternId))
                return new RenderMaterialSyncResult(false, false, normalStatus);

            Color renderColor = GetRenderAppearanceColor(material);
            _graphicsApplyService.Apply(material, renderColor, solidFillPatternId, logCallback);
            return new RenderMaterialSyncResult(true, true, normalStatus);
        }

        /// <summary>
        /// Flips the bump slot's DataType to "Normal Map" (bump type 1) for materials whose bump
        /// texture looks like a normal map, so Enscape recognizes it. Genuine height/bump maps are
        /// left untouched.
        /// MUST be called with a transaction already open — AppearanceAssetEditScope does not open one
        /// itself, and Commit throws "EditScope cannot be closed, there is no opened transaction".
        /// </summary>
        private static NormalMapSyncStatus TryNormalizeNormalMap(Material material, Action<string>? logCallback)
        {
            if (material.AppearanceAssetId == ElementId.InvalidElementId)
                return NormalMapSyncStatus.NoAppearanceAsset;

            try
            {
                using var editScope = new AppearanceAssetEditScope(material.Document);
                Asset asset = editScope.Start(material.AppearanceAssetId);

                AssetProperty? bumpSlot = FindBumpSlot(asset);
                if (bumpSlot == null)
                    return NormalMapSyncStatus.NoBumpSlot;

                Asset? connected = bumpSlot.GetSingleConnectedAsset();
                if (connected == null)
                {
                    logCallback?.Invoke($"[NM] {material.Name}: bump slot '{bumpSlot.Name}' has no connected asset.");
                    return NormalMapSyncStatus.NoConnectedBumpAsset;
                }

                string? bumpPath = FindBitmapPath(connected);
                bool isNormalName = MaterialTextureLookupService.IsNormalMapFileName(bumpPath);
                // Only touch genuine normal maps; leave real height/bump maps as-is.
                if (!isNormalName)
                    return NormalMapSyncStatus.NotRequested;

                bool isUnifiedBitmap = connected.Name.IndexOf("UnifiedBitmap", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isUnifiedBitmap)
                {
                    // Already the structure Enscape reads (unifiedbitmap_Bitmap in the slot). Just make
                    // sure the normal/height flag is set if the schema exposes it.
                    int forced = MaterialBumpMapNormalizer.ForceBumpTypeEverywhere(asset, bumpSlot, connected, 1);
                    logCallback?.Invoke($"[NM] {material.Name}: already UnifiedBitmap, forcedWrites={forced}");
                    if (forced > 0) editScope.Commit(true);
                    return forced > 0 ? NormalMapSyncStatus.Updated : NormalMapSyncStatus.AlreadyNormal;
                }

                // BumpMap node -> Enscape ignores its bumpmap_Bitmap. Convert to a UnifiedBitmap so the
                // path lives in unifiedbitmap_Bitmap, which Enscape reads for the Normal slot.
                bool converted = MaterialBumpMapNormalizer.ConvertBumpMapToUnifiedBitmap(asset, bumpSlot, connected);
                logCallback?.Invoke($"[NM] {material.Name}: convert BumpMap->UnifiedBitmap = {converted}");

                if (converted)
                {
                    editScope.Commit(true);
                    logCallback?.Invoke($"    -> Normal map recognized: {Path.GetFileName(bumpPath)}");
                    return NormalMapSyncStatus.Updated;
                }

                return NormalMapSyncStatus.WriteFailed;
            }
            catch (Exception ex)
            {
                // Revit API can throw on locked/invalid appearance assets; don't abort the whole batch.
                logCallback?.Invoke($"[NM] {material.Name}: EXCEPTION {ex.GetType().Name}: {ex.Message}");
                return NormalMapSyncStatus.WriteFailed;
            }
        }

        private static AssetProperty? FindBumpSlot(Asset asset)
        {
            for (int i = 0; i < asset.Size; i++)
            {
                AssetProperty? property = asset[i];
                if (property != null
                    && property.Name.EndsWith("_bump_map", StringComparison.OrdinalIgnoreCase)
                    && property.NumberOfConnectedProperties > 0)
                {
                    return property;
                }
            }

            return null;
        }

        private static string? FindBitmapPath(Asset asset)
        {
            string? direct = TryGetAssetString(asset, "unifiedbitmap_Bitmap")
                ?? TryGetAssetString(asset, "texture_Bitmap")
                ?? TryGetAssetString(asset, "bumpmap_Bitmap");
            if (direct != null) return direct;

            for (int i = 0; i < asset.Size; i++)
            {
                Asset? nested = asset[i]?.GetSingleConnectedAsset();
                if (nested != null)
                {
                    string? found = FindBitmapPath(nested);
                    if (found != null) return found;
                }
            }

            return null;
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
