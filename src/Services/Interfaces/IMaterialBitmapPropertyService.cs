using Autodesk.Revit.DB.Visual;

namespace LECG.Services.Interfaces
{
    public interface IMaterialBitmapPropertyService
    {
        void SetupBitmapProperty(AssetProperty? prop, string path);
    }
}
