using System;
using Autodesk.Revit.DB.Visual;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class MaterialBitmapPropertyService : IMaterialBitmapPropertyService
    {
        public void SetupBitmapProperty(AssetProperty? prop, string path)
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
                catch
                {
                }
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
