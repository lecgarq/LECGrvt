using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using LECG.Models;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class AdvancedAppearanceAssetService : IAdvancedAppearanceAssetService
    {
        private const string BaseSchemaProperty = "BaseSchema";
        private const string OpaqueMarker = "Opaque";

        private const string OpaqueAlbedo = "opaque_albedo";
        private const string OpaqueF0 = "opaque_f0";
        private const string SurfaceRoughness = "surface_roughness";
        private const string SurfaceNormal = "surface_normal";
        private const string SurfaceCutout = "surface_cutout";

        private readonly MaterialBitmapPropertyService _bitmaps;
        private Asset? _template;

        public AdvancedAppearanceAssetService(MaterialBitmapPropertyService bitmaps)
        {
            _bitmaps = bitmaps;
        }

        public ElementId EnsureAdvancedOpaqueAsset(Document doc, Material mat, string assetName, Action<string>? log = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(mat);
            ArgumentNullException.ThrowIfNull(assetName);

            if (mat.AppearanceAssetId != ElementId.InvalidElementId
                && doc.GetElement(mat.AppearanceAssetId) is AppearanceAssetElement existing
                && IsOpaqueSchema(existing.GetRenderingAsset()))
            {
                return existing.Id;
            }

            Asset template = FindTemplate(doc);
            string uniqueName = UniqueAssetName(doc, assetName);
            AppearanceAssetElement created = AppearanceAssetElement.Create(doc, uniqueName, template);
            mat.AppearanceAssetId = created.Id;
            log?.Invoke($"    -> Appearance asset '{uniqueName}' (Advanced Opaque)");
            return created.Id;
        }

        public void ApplyBakedTextures(Document doc, ElementId assetId, BakedTextureSet set, TextureTransform transform, Action<string>? log = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(assetId);
            ArgumentNullException.ThrowIfNull(set);
            ArgumentNullException.ThrowIfNull(transform);

            using var scope = new AppearanceAssetEditScope(doc);
            Asset asset = scope.Start(assetId);

            Connect(asset, OpaqueAlbedo, set.BaseColor, transform, log, isNormal: false);
            Connect(asset, SurfaceRoughness, set.Roughness, transform, log, isNormal: false);
            Connect(asset, SurfaceNormal, set.NormalGl, transform, log, isNormal: true);
            if (set.F0 is not null) Connect(asset, OpaqueF0, set.F0, transform, log, isNormal: false);
            else ClearStaleSlot(asset, OpaqueF0, log);
            if (set.Opacity is not null) Connect(asset, SurfaceCutout, set.Opacity, transform, log, isNormal: false);
            else ClearStaleSlot(asset, SurfaceCutout, log);

            scope.Commit(true);
        }

        private static void ClearStaleSlot(Asset asset, string propertyName, Action<string>? log)
        {
            AssetProperty? prop = asset.FindByName(propertyName);
            if (prop == null || prop.GetSingleConnectedAsset() == null) return;

            try
            {
                prop.RemoveConnectedAsset();
                log?.Invoke($"    -> {propertyName}: cleared stale bitmap");
            }
            catch (Exception ex) when (IsExpected(ex))
            {
                log?.Invoke($"    !! {propertyName}: failed to clear stale bitmap: {ex.Message}");
            }
        }

        private static bool IsExpected(Exception ex) =>
            ex is ArgumentException
            || ex is InvalidOperationException
            || ex is Autodesk.Revit.Exceptions.InvalidOperationException
            || ex is Autodesk.Revit.Exceptions.ArgumentException;

        private void Connect(Asset asset, string propertyName, string path, TextureTransform transform, Action<string>? log, bool isNormal)
        {
            AssetProperty? prop = asset.FindByName(propertyName);
            if (prop == null)
            {
                log?.Invoke($"    !! property '{propertyName}' not found on asset; skipped {System.IO.Path.GetFileName(path)}");
                return;
            }

            if (isNormal) _bitmaps.ConnectNormalMap(prop, path, transform);
            else _bitmaps.ConnectBitmap(prop, path, transform);

            log?.Invoke($"    -> {propertyName}: {System.IO.Path.GetFileName(path)}");
        }

        private Asset FindTemplate(Document doc)
        {
            if (_template != null) return _template;

            IList<Asset> assets = doc.Application.GetAssets(AssetType.Appearance);
            Asset? match = assets.FirstOrDefault(IsOpaqueSchema);
            if (match == null)
            {
                string names = string.Join(", ", assets.Select(a => a.Name).Distinct().OrderBy(n => n).Take(60));
                throw new InvalidOperationException(
                    "No Advanced Opaque appearance asset template found in the application asset library. " +
                    $"Available asset names: {names}");
            }

            _template = match;
            return match;
        }

        private static bool IsOpaqueSchema(Asset? asset)
        {
            if (asset == null) return false;
            string? schema = (asset.FindByName(BaseSchemaProperty) as AssetPropertyString)?.Value;
            if (!string.IsNullOrEmpty(schema) && schema.Contains(OpaqueMarker, StringComparison.OrdinalIgnoreCase)) return true;
            return asset.Name.Contains(OpaqueMarker, StringComparison.OrdinalIgnoreCase)
                && asset.Name.Contains("Advanced", StringComparison.OrdinalIgnoreCase);
        }

        private static string UniqueAssetName(Document doc, string baseName)
        {
            var names = new FilteredElementCollector(doc)
                .OfClass(typeof(AppearanceAssetElement))
                .Cast<AppearanceAssetElement>()
                .Select(a => a.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            string candidate = baseName.Trim();
            int i = 1;
            while (names.Contains(candidate)) candidate = $"{baseName.Trim()} ({i++})";
            return candidate;
        }
    }
}
