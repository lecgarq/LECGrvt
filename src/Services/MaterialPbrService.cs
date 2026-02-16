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

        public MaterialPbrService() : this(new MaterialCreationService(), new MaterialColorSequenceService())
        {
        }

        public MaterialPbrService(IMaterialCreationService materialCreationService, IMaterialColorSequenceService materialColorSequenceService)
        {
            _materialCreationService = materialCreationService;
            _materialColorSequenceService = materialColorSequenceService;
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

            string? diffusePath = FindTextureFile(folderPath, "_Color");
            string? normalPath = FindTextureFile(folderPath, "Normal GL");
            string? roughPath = FindTextureFile(folderPath, "roughness");

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

                    if (!string.IsNullOrEmpty(diffusePath)) SetupBitmapProperty(editableAsset.FindByName("generic_diffuse"), diffusePath!);
                    if (!string.IsNullOrEmpty(normalPath)) SetupBitmapProperty(editableAsset.FindByName("generic_bump_map"), normalPath!);
                    if (!string.IsNullOrEmpty(roughPath)) SetupBitmapProperty(editableAsset.FindByName("generic_reflectivity_at_0deg"), roughPath!);

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

        private string? FindTextureFile(string folder, string partialName)
        {
            if (!System.IO.Directory.Exists(folder)) return null;
            return System.IO.Directory.GetFiles(folder).FirstOrDefault(f => System.IO.Path.GetFileName(f).IndexOf(partialName, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void SetupBitmapProperty(AssetProperty? prop, string path)
        {
            if (prop == null) return;
            Asset? connectedAsset = null;

            if (prop.GetSingleConnectedAsset() != null)
            {
                connectedAsset = prop.GetSingleConnectedAsset();
            }
            else
            {
                try
                {
                    System.Reflection.MethodInfo? minfo = prop.GetType().GetMethod("AddConnectedAsset", new Type[] { typeof(string) });
                    if (minfo != null)
                    {
                        connectedAsset = minfo.Invoke(prop, new object[] { "UnifiedBitmapSchema" }) as Asset;
                    }
                }
                catch { }
            }

            if (connectedAsset != null)
            {
                AssetPropertyString? pathProp = connectedAsset.FindByName("unifiedbitmap_Bitmap") as AssetPropertyString;
                if (pathProp != null) pathProp.Value = path;
                double scale = 304.8;
                SetAssetDouble(connectedAsset, "texture_RealWorldScaleX", scale);
                SetAssetDouble(connectedAsset, "texture_RealWorldScaleY", scale);
            }
        }

        private void SetAssetDouble(Asset asset, string propName, double value)
        {
            AssetPropertyDouble? prop = asset.FindByName(propName) as AssetPropertyDouble;
            if (prop != null) prop.Value = value;
        }
    }
}
