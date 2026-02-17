using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IMaterialAppearanceAssetService
    {
        void ApplyTextures(
            Document doc,
            Material mat,
            string name,
            string? diffusePath,
            string? normalPath,
            string? roughPath,
            Action<string>? logCallback = null);
    }
}
