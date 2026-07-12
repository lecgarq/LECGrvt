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
                if (!string.IsNullOrEmpty(normalPath)) _materialBitmapPropertyService.SetupBumpBitmapProperty(editableAsset, editableAsset.FindByName("generic_bump_map"), normalPath, scaleXMillimeters, scaleYMillimeters, offsetXMillimeters, offsetYMillimeters, rotationDegrees, linkTextureTransforms, 1);
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

                // Normal Map
                if (!string.IsNullOrEmpty(request.NormalPath))
                {
                    AssetProperty? bumpMapProp = asset.FindByName("generic_bump_map");
                    _materialBitmapPropertyService.SetupBumpBitmapProperty(asset, bumpMapProp, request.NormalPath, sx, sy, ox, oy, rot, link, request.BumpMapType);
                    string bumpTypeLabel = request.BumpMapType == 1 ? "Normal Maps" : "Height Maps";
                    logCallback?.Invoke($"    -> Relief Pattern: {System.IO.Path.GetFileName(request.NormalPath)} (type={bumpTypeLabel})");
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
