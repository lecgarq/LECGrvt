using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using LECG.Core.Substance;
using LECG.Models;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class MaterialAppearanceAssetService : IMaterialAppearanceAssetService
    {
        private readonly IMaterialBitmapPropertyService _materialBitmapPropertyService;
        private readonly ITransactionService _transactionService;
        private readonly IPbrTextureBakeService _bake;
        private readonly IAdvancedAppearanceAssetService _advanced;

        public MaterialAppearanceAssetService(
            IMaterialBitmapPropertyService materialBitmapPropertyService,
            ITransactionService transactionService,
            IPbrTextureBakeService bake,
            IAdvancedAppearanceAssetService advanced)
        {
            _materialBitmapPropertyService = materialBitmapPropertyService;
            _transactionService = transactionService;
            _bake = bake;
            _advanced = advanced;
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

            if (string.IsNullOrEmpty(request.NormalPath) || string.IsNullOrEmpty(request.RoughnessPath) || string.IsNullOrEmpty(request.MetallicPath))
            {
                throw new InvalidOperationException("Advanced materials need base color, normal, roughness and metallic maps. Metallic may be a flat black image.");
            }

            string folder = System.IO.Path.GetDirectoryName(request.DiffusePath) ?? ".";
            string slug = SanitizeSlug(request.MaterialName);
            var entry = new SubstanceMaterialEntry(
                Category: "Custom",
                Slug: slug,
                DisplayName: request.MaterialName,
                FolderPath: folder,
                BaseColorPath: request.DiffusePath,
                NormalPath: request.NormalPath,
                RoughnessPath: request.RoughnessPath,
                MetallicPath: request.MetallicPath,
                AoPath: request.AoPath,
                OpacityPath: request.OpacityPath,
                Ior: null,
                Resolution: 0);

            var bakeOptions = new BakeOptions(System.IO.Path.Combine(folder, "_revit"), 2048, ForceRebake: false);
            BakedTextureSet baked = _bake.Bake(entry, bakeOptions, logCallback);

            var transform = new TextureTransform(
                request.ScaleXMillimeters, request.ScaleYMillimeters,
                request.OffsetXMillimeters, request.OffsetYMillimeters,
                request.RotationDegrees, request.LinkTextureTransforms);

            _transactionService.Run(doc, "Apply PBR Textures", d =>
            {
                ElementId assetId = _advanced.EnsureAdvancedOpaqueAsset(d, mat, name, logCallback);
                _advanced.ApplyBakedTextures(d, assetId, baked, transform, logCallback);
            });
        }

        private static string SanitizeSlug(string name)
        {
            var chars = name.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
            return new string(chars).Trim('_');
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
    }
}
