using Autodesk.Revit.DB.Visual;
using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface IMaterialBitmapPropertyService
    {
        void SetupBitmapProperty(AssetProperty? prop, string path);
        void SetupBitmapProperty(AssetProperty? prop, string path, double scaleXMillimeters, double scaleYMillimeters, double offsetXMillimeters, double offsetYMillimeters, double rotationDegrees, bool linkTextureTransforms);

        /// <summary>Connects a UnifiedBitmap asset carrying <paramref name="path"/> to <paramref name="prop"/>.</summary>
        void ConnectBitmap(AssetProperty? prop, string path, TextureTransform transform);

        /// <summary>Connects a BumpMap asset of type NormalMap carrying <paramref name="path"/> to <paramref name="prop"/>.</summary>
        void ConnectNormalMap(AssetProperty? prop, string path, TextureTransform transform, double normalScale = 1.0);
    }
}
