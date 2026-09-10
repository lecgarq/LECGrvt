using System;
using Autodesk.Revit.DB;
using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface IAdvancedAppearanceAssetService
    {
        ElementId EnsureAdvancedOpaqueAsset(Document doc, Material mat, string assetName, Action<string>? log = null);
        void ApplyBakedTextures(Document doc, ElementId assetId, BakedTextureSet set, TextureTransform transform, Action<string>? log = null);
    }
}
