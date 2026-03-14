using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
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
            Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(mat);
            ArgumentNullException.ThrowIfNull(name);

            using (AppearanceAssetEditScope editScope = new AppearanceAssetEditScope(doc))
            {
                ElementId assetId = mat.AppearanceAssetId;
                if (assetId == ElementId.InvalidElementId)
                {
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
                        return;
                    }
                }

                if (assetId == ElementId.InvalidElementId)
                {
                    return;
                }

                Asset editableAsset = editScope.Start(assetId);
                if (!string.IsNullOrEmpty(diffusePath)) _materialBitmapPropertyService.SetupBitmapProperty(editableAsset.FindByName("generic_diffuse"), diffusePath);
                if (!string.IsNullOrEmpty(normalPath)) _materialBitmapPropertyService.SetupBitmapProperty(editableAsset.FindByName("generic_bump_map"), normalPath);
                if (!string.IsNullOrEmpty(roughPath)) _materialBitmapPropertyService.SetupBitmapProperty(editableAsset.FindByName("generic_reflectivity_at_0deg"), roughPath);

                editScope.Commit(true);

                if (mat.AppearanceAssetId == ElementId.InvalidElementId)
                {
                    _transactionService.Run(doc, "Assign Appearance", _ =>
                    {
                        mat.AppearanceAssetId = assetId;
                    });
                }
            }
        }
    }
}
