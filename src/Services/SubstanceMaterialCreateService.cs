using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Core.Substance;
using LECG.Models;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class SubstanceMaterialCreateService : ISubstanceMaterialCreateService
    {
        private readonly IPbrTextureBakeService _bake;
        private readonly IAdvancedAppearanceAssetService _appearance;
        private readonly ITransactionService _transactions;
        private readonly IRenderSolidFillPatternService _solidFill;
        private readonly IRenderMaterialGraphicsApplyService _graphics;
        private readonly IImageColorExtractionService _colors;

        public SubstanceMaterialCreateService(
            IPbrTextureBakeService bake,
            IAdvancedAppearanceAssetService appearance,
            ITransactionService transactions,
            IRenderSolidFillPatternService solidFill,
            IRenderMaterialGraphicsApplyService graphics,
            IImageColorExtractionService colors)
        {
            _bake = bake;
            _appearance = appearance;
            _transactions = transactions;
            _solidFill = solidFill;
            _graphics = graphics;
            _colors = colors;
        }

        public HashSet<string> ExistingMaterialNames(Document doc)
        {
            ArgumentNullException.ThrowIfNull(doc);
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .Select(m => m.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public SubstanceMaterialReport Create(Document doc, SubstanceMaterialEntry entry, SubstanceBatchOptions options, Action<string>? log = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentNullException.ThrowIfNull(options);

            try
            {
                Material? existing = FindMaterial(doc, entry.DisplayName);
                if (existing != null && !options.OverwriteExisting)
                {
                    log?.Invoke($"  SKIP '{entry.DisplayName}' already in document");
                    return new SubstanceMaterialReport(entry.DisplayName, SubstanceMaterialOutcome.Skipped, null);
                }

                BakedTextureSet baked = _bake.Bake(entry, options.Bake, log);
                Color color = SafeAverageColor(baked.BaseColor, log);

                bool created = existing == null;
                _transactions.Run(doc, $"Substance material: {entry.DisplayName}", d =>
                {
                    Material mat = existing ?? (Material)d.GetElement(Material.Create(d, entry.DisplayName));
                    ApplyIdentity(mat, entry, log);
                    if (created)
                    {
                        _graphics.Apply(mat, color, _solidFill.GetSolidFillPatternId(d), log);
                    }
                    mat.UseRenderAppearanceForShading = true;

                    ElementId assetId = _appearance.EnsureAdvancedOpaqueAsset(d, mat, entry.DisplayName, log);
                    _appearance.ApplyBakedTextures(d, assetId, baked, options.Transform, log);
                });

                var outcome = created ? SubstanceMaterialOutcome.Created : SubstanceMaterialOutcome.Updated;
                log?.Invoke($"  DONE {outcome}: {entry.DisplayName}");
                return new SubstanceMaterialReport(entry.DisplayName, outcome, null);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                log?.Invoke($"  FAIL '{entry.DisplayName}': {ex.Message}");
                return new SubstanceMaterialReport(entry.DisplayName, SubstanceMaterialOutcome.Failed, ex.Message);
            }
        }

        public SubstanceTextureRepathResult RepathExisting(
            Document doc,
            IReadOnlyList<SubstanceMaterialEntry> entries,
            string outputRoot,
            Action<string>? log = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(entries);
            ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);

            int repathed = 0;
            int missing = 0;
            int failed = 0;
            int updatedPaths = 0;
            List<Material> documentMaterials = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .ToList();
            Dictionary<string, Material> materialsByName = documentMaterials
                .GroupBy(material => material.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            Dictionary<long, int> appearanceAssetUsers = documentMaterials
                .Where(material => material.AppearanceAssetId != ElementId.InvalidElementId)
                .GroupBy(material => material.AppearanceAssetId.Value)
                .ToDictionary(group => group.Key, group => group.Count());

            foreach (SubstanceMaterialEntry entry in entries)
            {
                if (!materialsByName.TryGetValue(entry.DisplayName, out Material? material))
                {
                    missing++;
                    log?.Invoke($"SKIP '{entry.DisplayName}': material is not in this document");
                    continue;
                }

                if (!HasSubstanceIdentity(material, entry))
                {
                    failed++;
                    log?.Invoke($"FAIL '{entry.DisplayName}': name matches, but the LECG Substance identity does not");
                    continue;
                }

                BakeOutputPaths paths = BakeOutputPaths.For(entry, outputRoot);
                string? f0 = File.Exists(paths.F0) ? paths.F0 : null;
                string? opacity = File.Exists(paths.Opacity) ? paths.Opacity : null;
                string[] required = { paths.BaseColor, paths.NormalGl, paths.Roughness };
                string? absent = required.FirstOrDefault(path => !File.Exists(path));
                if (absent != null)
                {
                    failed++;
                    log?.Invoke($"FAIL '{entry.DisplayName}': required texture is missing: {absent}");
                    continue;
                }

                if (material.AppearanceAssetId == ElementId.InvalidElementId)
                {
                    failed++;
                    log?.Invoke($"FAIL '{entry.DisplayName}': material has no appearance asset");
                    continue;
                }

                int assetUsers = appearanceAssetUsers[material.AppearanceAssetId.Value];
                if (assetUsers > 1)
                {
                    failed++;
                    log?.Invoke($"FAIL '{entry.DisplayName}': appearance asset is shared by {assetUsers} materials; duplicate the asset before repathing");
                    continue;
                }

                try
                {
                    int changed = 0;
                    var set = new BakedTextureSet(paths.BaseColor, paths.NormalGl, paths.Roughness, f0, opacity, WasSkipped: true);
                    _transactions.Run(doc, $"Repath Substance material: {entry.DisplayName}", d =>
                    {
                        changed = _appearance.RepathBakedTextures(d, material.AppearanceAssetId, set, log);
                    });
                    updatedPaths += changed;
                    repathed++;
                    log?.Invoke($"DONE '{entry.DisplayName}': {changed} bitmap path{(changed == 1 ? "" : "s")} updated");
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    failed++;
                    log?.Invoke($"FAIL '{entry.DisplayName}': {ex.Message}");
                }
            }

            return new SubstanceTextureRepathResult(entries.Count, repathed, missing, failed, updatedPaths);
        }

        private static bool HasSubstanceIdentity(Material material, SubstanceMaterialEntry entry)
        {
            string? manufacturer = material.get_Parameter(BuiltInParameter.ALL_MODEL_MANUFACTURER)?.AsString();
            string? description = material.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION)?.AsString();
            return string.Equals(manufacturer, SubstanceIdentityPolicy.Manufacturer, StringComparison.Ordinal)
                && string.Equals(description, SubstanceIdentityPolicy.Description(entry), StringComparison.Ordinal);
        }

        private static Material? FindMaterial(Document doc, string name)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private Color SafeAverageColor(string path, Action<string>? log)
        {
            try
            {
                return _colors.GetAverageColor(path);
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException)
            {
                log?.Invoke($"    -> average color failed ({ex.Message}); using grey");
                return new Color(128, 128, 128);
            }
        }

        private static void ApplyIdentity(Material mat, SubstanceMaterialEntry entry, Action<string>? log)
        {
            mat.MaterialClass = entry.Category;
            mat.MaterialCategory = entry.Category;
            SetParam(mat, BuiltInParameter.ALL_MODEL_DESCRIPTION, SubstanceIdentityPolicy.Description(entry));
            SetParam(mat, BuiltInParameter.ALL_MODEL_MANUFACTURER, SubstanceIdentityPolicy.Manufacturer);
            SetParam(mat, BuiltInParameter.ALL_MODEL_MODEL, SubstanceIdentityPolicy.Model);

            Parameter? keywords = mat.LookupParameter("Keywords");
            if (keywords != null && !keywords.IsReadOnly && keywords.StorageType == StorageType.String)
            {
                keywords.Set(SubstanceIdentityPolicy.Keywords(entry));
            }
            log?.Invoke($"    -> class/category '{entry.Category}', description set");
        }

        private static void SetParam(Element element, BuiltInParameter bip, string value)
        {
            Parameter? p = element.get_Parameter(bip);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.String) p.Set(value);
        }
    }
}
