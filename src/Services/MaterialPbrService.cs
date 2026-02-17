using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class MaterialPbrService : IMaterialPbrService
    {
        private readonly IMaterialCreationService _materialCreationService;
        private readonly IMaterialColorSequenceService _materialColorSequenceService;
        private readonly IMaterialTextureLookupService _materialTextureLookupService;
        private readonly IMaterialBitmapPropertyService _materialBitmapPropertyService;

        public MaterialPbrService() : this(new MaterialCreationService(), new MaterialColorSequenceService(), new MaterialTextureLookupService(), new MaterialBitmapPropertyService())
        {
        }

        public MaterialPbrService(IMaterialCreationService materialCreationService, IMaterialColorSequenceService materialColorSequenceService, IMaterialTextureLookupService materialTextureLookupService, IMaterialBitmapPropertyService materialBitmapPropertyService)
        {
            _materialCreationService = materialCreationService;
            _materialColorSequenceService = materialColorSequenceService;
            _materialTextureLookupService = materialTextureLookupService;
            _materialBitmapPropertyService = materialBitmapPropertyService;
        }

        public ElementId CreatePBRMaterial(Document doc, string name, string folderPath, Action<string>? logCallback = null)
        {
            ElementId matId = ElementId.InvalidElementId;
            using (Transaction t = new Transaction(doc, "Create Material"))
            {
                t.Start();
                matId = _materialCreationService.GetOrCreateMaterial(doc, name, _materialColorSequenceService.GetNextColor(), logCallback);
                t.Commit();
            }
            Material? mat = doc.GetElement(matId) as Material;
            if (mat == null) return matId;

            string? diffusePath = _materialTextureLookupService.FindTextureFile(folderPath, "_Color");
            string? normalPath = _materialTextureLookupService.FindTextureFile(folderPath, "Normal GL");
            string? roughPath = _materialTextureLookupService.FindTextureFile(folderPath, "roughness");

            if (!string.IsNullOrEmpty(diffusePath)) logCallback?.Invoke($"  âœ“ Found diffuse: {System.IO.Path.GetFileName(diffusePath)}");
            else logCallback?.Invoke($"  âš  No diffuse texture found in {folderPath}");

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
                    catch { logCallback?.Invoke("  Failed to create base appearance asset."); return matId; }
                }

                if (assetId != ElementId.InvalidElementId)
                {
                    Asset editableAsset = editScope.Start(assetId);

                    if (!string.IsNullOrEmpty(diffusePath)) _materialBitmapPropertyService.SetupBitmapProperty(editableAsset.FindByName("generic_diffuse"), diffusePath!);
                    if (!string.IsNullOrEmpty(normalPath)) _materialBitmapPropertyService.SetupBitmapProperty(editableAsset.FindByName("generic_bump_map"), normalPath!);
                    if (!string.IsNullOrEmpty(roughPath)) _materialBitmapPropertyService.SetupBitmapProperty(editableAsset.FindByName("generic_reflectivity_at_0deg"), roughPath!);

                    editScope.Commit(true);

                    if (mat.AppearanceAssetId == ElementId.InvalidElementId)
                    {
                        using (Transaction t = new Transaction(doc, "Assign Appearance"))
                        {
                            t.Start();
                            mat.AppearanceAssetId = assetId;
                            t.Commit();
                        }
                    }
                }
            }
            return matId;
        }

    }
}
