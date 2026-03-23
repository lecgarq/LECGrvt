using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using LECG.Models;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class MaterialAppearanceAssetService : IMaterialAppearanceAssetService
    {
        private readonly IMaterialBitmapPropertyService _materialBitmapPropertyService;
        private readonly ITransactionService _transactionService;

        public MaterialAppearanceAssetService(
            IMaterialBitmapPropertyService materialBitmapPropertyService,
            ITransactionService transactionService)
        {
            _materialBitmapPropertyService = materialBitmapPropertyService;
            _transactionService = transactionService;
        }

        public void ApplyTextures(
            Document doc,
            Material mat,
            string name,
            string? diffusePath,
            string? normalPath,
            string? roughPath,
            double scaleXMillimeters,
            double scaleYMillimeters,
            double offsetXMillimeters,
            double offsetYMillimeters,
            double rotationDegrees,
            bool linkTextureTransforms,
            Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(mat);
            ArgumentNullException.ThrowIfNull(name);

            _transactionService.Run(doc, "Apply Material Appearance", _ =>
            {
                using AppearanceAssetEditScope editScope = new AppearanceAssetEditScope(doc);
                ElementId assetId = EnsureAppearanceAsset(doc, mat, name, logCallback);
                if (assetId == ElementId.InvalidElementId) return;

                Asset editableAsset = editScope.Start(assetId);
                if (!string.IsNullOrEmpty(diffusePath)) _materialBitmapPropertyService.SetupBitmapProperty(editableAsset.FindByName("generic_diffuse"), diffusePath, scaleXMillimeters, scaleYMillimeters, offsetXMillimeters, offsetYMillimeters, rotationDegrees, linkTextureTransforms);
                if (!string.IsNullOrEmpty(normalPath)) _materialBitmapPropertyService.SetupBitmapProperty(editableAsset.FindByName("generic_bump_map"), normalPath, scaleXMillimeters, scaleYMillimeters, offsetXMillimeters, offsetYMillimeters, rotationDegrees, linkTextureTransforms);
                if (!string.IsNullOrEmpty(roughPath)) _materialBitmapPropertyService.SetupBitmapProperty(editableAsset.FindByName("generic_glossiness") ?? editableAsset.FindByName("generic_reflectivity_at_0deg"), roughPath, scaleXMillimeters, scaleYMillimeters, offsetXMillimeters, offsetYMillimeters, rotationDegrees, linkTextureTransforms);

                editScope.Commit(true);

                if (mat.AppearanceAssetId == ElementId.InvalidElementId)
                {
                    mat.AppearanceAssetId = assetId;
                }
            });
        }

        public void ApplyPbrTextures(
            Document doc,
            Material mat,
            string name,
            PbrMaterialCreateRequest request,
            Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(mat);
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(request);

            double sx = request.ScaleXMillimeters;
            double sy = request.ScaleYMillimeters;
            double ox = request.OffsetXMillimeters;
            double oy = request.OffsetYMillimeters;
            double rot = request.RotationDegrees;
            bool link = request.LinkTextureTransforms;

            _transactionService.Run(doc, "Apply PBR Textures", _ =>
            {
                using AppearanceAssetEditScope editScope = new AppearanceAssetEditScope(doc);
                ElementId assetId = EnsureAppearanceAsset(doc, mat, name, logCallback);
                if (assetId == ElementId.InvalidElementId) return;

                Asset asset = editScope.Start(assetId);

                // Diffuse / Base Color
                if (!string.IsNullOrEmpty(request.DiffusePath))
                {
                    _materialBitmapPropertyService.SetupBitmapProperty(
                        asset.FindByName("generic_diffuse"), request.DiffusePath, sx, sy, ox, oy, rot, link);
                    logCallback?.Invoke($"    -> Diffuse: {System.IO.Path.GetFileName(request.DiffusePath)}");
                }

                // Normal Map - set bump map type to "Normal" (value 1)
                if (!string.IsNullOrEmpty(request.NormalPath))
                {
                    AssetProperty? bumpMapProp = asset.FindByName("generic_bump_map");
                    _materialBitmapPropertyService.SetupBitmapProperty(bumpMapProp, request.NormalPath, sx, sy, ox, oy, rot, link);

                    // Set bump map type: 0 = Bump, 1 = Normal
                    SetAssetInteger(asset, "generic_bump_map_type", 1);
                    logCallback?.Invoke($"    -> Normal: {System.IO.Path.GetFileName(request.NormalPath)} (type=Normal)");
                }

                // Roughness -> maps to glossiness (inverted in Revit's Generic schema)
                if (!string.IsNullOrEmpty(request.RoughnessPath))
                {
                    AssetProperty? glossProp = asset.FindByName("generic_glossiness")
                        ?? asset.FindByName("generic_reflectivity_at_0deg");
                    _materialBitmapPropertyService.SetupBitmapProperty(glossProp, request.RoughnessPath, sx, sy, ox, oy, rot, link);

                    // Enable "use image" for glossiness if available
                    SetAssetBoolean(asset, "generic_glossiness_is_roughness", true);
                    logCallback?.Invoke($"    -> Roughness: {System.IO.Path.GetFileName(request.RoughnessPath)}");
                }

                // Metallic
                if (!string.IsNullOrEmpty(request.MetallicPath))
                {
                    // Set material to metallic mode
                    SetAssetBoolean(asset, "generic_is_metal", true);
                    AssetProperty? metalProp = asset.FindByName("generic_metalness")
                        ?? asset.FindByName("generic_is_metal");
                    if (metalProp != null)
                    {
                        _materialBitmapPropertyService.SetupBitmapProperty(metalProp, request.MetallicPath, sx, sy, ox, oy, rot, link);
                    }
                    logCallback?.Invoke($"    -> Metallic: {System.IO.Path.GetFileName(request.MetallicPath)}");
                }

                // Ambient Occlusion -> diffuse secondary or self-illumination channel
                if (!string.IsNullOrEmpty(request.AoPath))
                {
                    // AO is typically baked into diffuse, but Revit has no dedicated AO slot.
                    // Map to self-illumination filter map as closest equivalent for rendering hints.
                    AssetProperty? aoProp = asset.FindByName("generic_self_illum_filter_map");
                    if (aoProp != null)
                    {
                        _materialBitmapPropertyService.SetupBitmapProperty(aoProp, request.AoPath, sx, sy, ox, oy, rot, link);
                    }
                    logCallback?.Invoke($"    -> AO: {System.IO.Path.GetFileName(request.AoPath)}");
                }

                // Displacement / Height
                if (!string.IsNullOrEmpty(request.DisplacementPath))
                {
                    // If there's no normal already, use bump_map with type = Bump (0)
                    // If normal is already assigned, use a separate displacement approach
                    if (string.IsNullOrEmpty(request.NormalPath))
                    {
                        AssetProperty? bumpProp = asset.FindByName("generic_bump_map");
                        _materialBitmapPropertyService.SetupBitmapProperty(bumpProp, request.DisplacementPath, sx, sy, ox, oy, rot, link);
                        SetAssetInteger(asset, "generic_bump_map_type", 0); // Bump type
                    }
                    else
                    {
                        // Displacement goes to a secondary channel when normal is already present
                        AssetProperty? dispProp = asset.FindByName("generic_displacement")
                            ?? asset.FindByName("generic_roundcorners_radius");
                        if (dispProp != null)
                        {
                            _materialBitmapPropertyService.SetupBitmapProperty(dispProp, request.DisplacementPath, sx, sy, ox, oy, rot, link);
                        }
                    }
                    logCallback?.Invoke($"    -> Displacement: {System.IO.Path.GetFileName(request.DisplacementPath)}");
                }

                // Opacity / Transparency
                if (!string.IsNullOrEmpty(request.OpacityPath))
                {
                    AssetProperty? transparencyProp = asset.FindByName("generic_transparency")
                        ?? asset.FindByName("generic_cutout_opacity");
                    if (transparencyProp != null)
                    {
                        _materialBitmapPropertyService.SetupBitmapProperty(transparencyProp, request.OpacityPath, sx, sy, ox, oy, rot, link);
                    }
                    logCallback?.Invoke($"    -> Opacity: {System.IO.Path.GetFileName(request.OpacityPath)}");
                }

                editScope.Commit(true);

                if (mat.AppearanceAssetId == ElementId.InvalidElementId)
                {
                    mat.AppearanceAssetId = assetId;
                }
            });
        }

        private ElementId EnsureAppearanceAsset(Document doc, Material mat, string name, Action<string>? logCallback)
        {
            ElementId assetId = mat.AppearanceAssetId;
            if (assetId != ElementId.InvalidElementId) return assetId;

            try
            {
                var assetLib = doc.Application.GetAssets(AssetType.Appearance);
                var template = assetLib.FirstOrDefault(a => a.Name == "Generic") ?? assetLib.FirstOrDefault();
                if (template != null)
                {
                    assetId = AppearanceAssetElement.Create(doc, name, template).Id;
                }
            }
            catch
            {
                logCallback?.Invoke("  Failed to create base appearance asset.");
            }

            return assetId;
        }

        private static void SetAssetInteger(Asset asset, string propName, int value)
        {
            try
            {
                AssetPropertyInteger? prop = asset.FindByName(propName) as AssetPropertyInteger;
                if (prop != null && !prop.IsReadOnly) prop.Value = value;
            }
            catch { }
        }

        private static void SetAssetBoolean(Asset asset, string propName, bool value)
        {
            try
            {
                AssetPropertyBoolean? prop = asset.FindByName(propName) as AssetPropertyBoolean;
                if (prop != null && !prop.IsReadOnly) prop.Value = value;
            }
            catch { }
        }
    }
}
