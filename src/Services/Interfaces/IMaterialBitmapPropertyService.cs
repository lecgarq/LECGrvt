using Autodesk.Revit.DB.Visual;

namespace LECG.Services.Interfaces
{
    public interface IMaterialBitmapPropertyService
    {
        void SetupBitmapProperty(AssetProperty? prop, string path);
        void SetupBitmapProperty(AssetProperty? prop, string path, double scaleXMillimeters, double scaleYMillimeters, double offsetXMillimeters, double offsetYMillimeters, double rotationDegrees, bool linkTextureTransforms);
    }
}
