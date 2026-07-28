using System;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using LECG.Models;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class MaterialPbrService : IMaterialPbrService
    {
        private const double MillimetersPerFoot = 304.8;

        private readonly IMaterialCreationService _materialCreationService;
        private readonly IMaterialColorSequenceService _materialColorSequenceService;
        private readonly IMaterialTextureLookupService _materialTextureLookupService;
        private readonly IMaterialAppearanceAssetService _materialAppearanceAssetService;
        private readonly IImageColorExtractionService _imageColorExtractionService;
        private readonly IRenderSolidFillPatternService _renderSolidFillPatternService;
        private readonly IRenderMaterialGraphicsApplyService _renderMaterialGraphicsApplyService;
        private readonly ITransactionService _transactionService;

        public MaterialPbrService(
            IMaterialCreationService materialCreationService,
            IMaterialColorSequenceService materialColorSequenceService,
            IMaterialTextureLookupService materialTextureLookupService,
            IMaterialAppearanceAssetService materialAppearanceAssetService,
            IImageColorExtractionService imageColorExtractionService,
            IRenderSolidFillPatternService renderSolidFillPatternService,
            IRenderMaterialGraphicsApplyService renderMaterialGraphicsApplyService,
            ITransactionService transactionService)
        {
            _materialCreationService = materialCreationService;
            _materialColorSequenceService = materialColorSequenceService;
            _materialTextureLookupService = materialTextureLookupService;
            _materialAppearanceAssetService = materialAppearanceAssetService;
            _imageColorExtractionService = imageColorExtractionService;
            _renderSolidFillPatternService = renderSolidFillPatternService;
            _renderMaterialGraphicsApplyService = renderMaterialGraphicsApplyService;
            _transactionService = transactionService;
        }

        public ElementId CreatePBRMaterial(Document doc, string name, string folderPath, Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(folderPath);

            ElementId matId = _transactionService.Run(
                doc,
                "Create Material",
                _ => _materialCreationService.GetOrCreateMaterial(doc, name, _materialColorSequenceService.GetNextColor(), logCallback));
            Material? mat = doc.GetElement(matId) as Material;
            if (mat == null) return matId;

            string? diffusePath = _materialTextureLookupService.FindTextureFile(folderPath, "_Color");
            string? normalPath = _materialTextureLookupService.FindTextureFile(folderPath, "Normal GL");
            string? roughPath = _materialTextureLookupService.FindTextureFile(folderPath, "roughness");

            if (!string.IsNullOrEmpty(diffusePath)) logCallback?.Invoke($"  \u2713 Found diffuse: {System.IO.Path.GetFileName(diffusePath)}");
            else logCallback?.Invoke($"  \u26a0 No diffuse texture found in {folderPath}");

            _materialAppearanceAssetService.ApplyTextures(doc, mat, name, diffusePath, normalPath, roughPath, MillimetersPerFoot, MillimetersPerFoot, 0, 0, 0, true, logCallback);
            return matId;
        }

        public ElementId CreatePBRMaterial(Document doc, PbrMaterialCreateRequest request, Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(request);

            string materialName = GetUniqueMaterialName(doc, request.MaterialName);
            string appearanceAssetName = GetUniqueAppearanceAssetName(doc, request.AppearanceAssetName);

            if (!materialName.Equals(request.MaterialName, StringComparison.OrdinalIgnoreCase))
            {
                logCallback?.Invoke($"  Material name exists. Using '{materialName}'.");
            }

            if (!appearanceAssetName.Equals(request.AppearanceAssetName, StringComparison.OrdinalIgnoreCase))
            {
                logCallback?.Invoke($"  Appearance asset name exists. Using '{appearanceAssetName}'.");
            }

            Color materialColor = TryGetDiffuseColor(request.DiffusePath, logCallback);
            ElementId materialId = _transactionService.Run(doc, "Create PBR Material", currentDoc =>
            {
                ElementId newId = Material.Create(currentDoc, materialName);
                Material? material = currentDoc.GetElement(newId) as Material;
                if (material == null)
                {
                    return newId;
                }

                ElementId solidFillId = _renderSolidFillPatternService.GetSolidFillPatternId(currentDoc);
                _renderMaterialGraphicsApplyService.Apply(material, materialColor, solidFillId, logCallback);
                material.UseRenderAppearanceForShading = request.UseRenderAppearanceForShading;

                if (!string.IsNullOrEmpty(request.MaterialClass))
                {
                    material.MaterialClass = request.MaterialClass;
                    logCallback?.Invoke($"    -> Material class: {request.MaterialClass}");
                }

                TrySetDescription(material, "LECG Arquitectura");
                logCallback?.Invoke($"    -> Material description set");

                TrySetParameter(material, BuiltInParameter.ALL_MODEL_MODEL, "Arq. Luis Eduardo Cort\u00e9s");
                TrySetParameter(material, BuiltInParameter.ALL_MODEL_MANUFACTURER, "LECG Arquitectura");

                return newId;
            });

            Material? createdMaterial = doc.GetElement(materialId) as Material;
            if (createdMaterial == null)
            {
                return materialId;
            }

            _materialAppearanceAssetService.ApplyPbrTextures(
                doc,
                createdMaterial,
                appearanceAssetName,
                request,
                logCallback);

            return materialId;
        }

        private static void TrySetDescription(Element element, string description)
        {
            TrySetParameter(element, BuiltInParameter.ALL_MODEL_DESCRIPTION, description);
        }

        private static void TrySetParameter(Element element, BuiltInParameter bip, string value)
        {
            Parameter? param = element.get_Parameter(bip);
            if (param != null && !param.IsReadOnly)
            {
                param.Set(value);
            }
        }

        private Color TryGetDiffuseColor(string diffusePath, Action<string>? logCallback)
        {
            try
            {
                Color color = _imageColorExtractionService.GetAverageColor(diffusePath);
                logCallback?.Invoke($"    -> Derived color: RGB({color.Red}, {color.Green}, {color.Blue})");
                return color;
            }
            catch (Exception ex) when (IsExpectedDiffuseColorException(ex))
            {
                logCallback?.Invoke($"    -> Failed to derive color from diffuse map. Using neutral fallback. {ex.Message}");
                return new Color(128, 128, 128);
            }
        }

        private static bool IsExpectedDiffuseColorException(Exception ex)
        {
            return ex is IOException
                || ex is UnauthorizedAccessException
                || ex is ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.ArgumentException
                || ex is RevitExceptions.InvalidOperationException;
        }

        private static string GetUniqueMaterialName(Document doc, string baseName)
        {
            var names = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .Select(material => material.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return GetUniqueName(baseName, names);
        }

        private static string GetUniqueAppearanceAssetName(Document doc, string baseName)
        {
            var names = new FilteredElementCollector(doc)
                .OfClass(typeof(AppearanceAssetElement))
                .Cast<AppearanceAssetElement>()
                .Select(asset => asset.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return GetUniqueName(baseName, names);
        }

        private static string GetUniqueName(string baseName, System.Collections.Generic.HashSet<string> existingNames)
        {
            string candidate = baseName.Trim();
            int suffix = 1;
            while (existingNames.Contains(candidate))
            {
                candidate = $"{baseName.Trim()} ({suffix})";
                suffix++;
            }

            return candidate;
        }
    }
}
