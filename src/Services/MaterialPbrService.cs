using System;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class MaterialPbrService : IMaterialPbrService
    {
        private readonly IMaterialCreationService _materialCreationService;
        private readonly IMaterialColorSequenceService _materialColorSequenceService;
        private readonly IMaterialTextureLookupService _materialTextureLookupService;
        private readonly IMaterialAppearanceAssetService _materialAppearanceAssetService;

        public MaterialPbrService() : this(new MaterialCreationService(), new MaterialColorSequenceService(), new MaterialTextureLookupService(), new MaterialAppearanceAssetService(new MaterialBitmapPropertyService()))
        {
        }

        public MaterialPbrService(IMaterialCreationService materialCreationService, IMaterialColorSequenceService materialColorSequenceService, IMaterialTextureLookupService materialTextureLookupService, IMaterialAppearanceAssetService materialAppearanceAssetService)
        {
            _materialCreationService = materialCreationService;
            _materialColorSequenceService = materialColorSequenceService;
            _materialTextureLookupService = materialTextureLookupService;
            _materialAppearanceAssetService = materialAppearanceAssetService;
        }

        public ElementId CreatePBRMaterial(Document doc, string name, string folderPath, Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(folderPath);

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

            _materialAppearanceAssetService.ApplyTextures(doc, mat, name, diffusePath, normalPath, roughPath, logCallback);
            return matId;
        }

    }
}
