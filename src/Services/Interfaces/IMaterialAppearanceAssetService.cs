using System;
using Autodesk.Revit.DB;
using LECG.Models;

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
            double scaleXMillimeters,
            double scaleYMillimeters,
            double offsetXMillimeters,
            double offsetYMillimeters,
            double rotationDegrees,
            bool linkTextureTransforms,
            Action<string>? logCallback = null);

        void ApplyPbrTextures(
            Document doc,
            Material mat,
            string name,
            PbrMaterialCreateRequest request,
            Action<string>? logCallback = null);
    }
}
