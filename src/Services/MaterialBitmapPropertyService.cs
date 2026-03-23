using System;
using Autodesk.Revit.DB.Visual;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class MaterialBitmapPropertyService : IMaterialBitmapPropertyService
    {
        private const double MillimetersPerFoot = 304.8;

        public void SetupBitmapProperty(AssetProperty? prop, string path)
        {
            SetupBitmapProperty(prop, path, MillimetersPerFoot, MillimetersPerFoot, 0, 0, 0, true);
        }

        public void SetupBitmapProperty(AssetProperty? prop, string path, double scaleXMillimeters, double scaleYMillimeters, double offsetXMillimeters, double offsetYMillimeters, double rotationDegrees, bool linkTextureTransforms)
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
                    connectedAsset = TryAddConnectedAsset(prop, "UnifiedBitmap")
                        ?? TryAddConnectedAsset(prop, "UnifiedBitmapSchema");
                }
                catch (Exception ex) when (IsExpectedMaterialBitmapPropertyException(ex))
                {
                    Logging.Logger.Instance.LogWarning($"[MaterialBitmapPropertyService] AddConnectedAsset failed: {ex.Message}");
                }
            }

            if (connectedAsset != null)
            {
                SetAssetString(connectedAsset, "unifiedbitmap_Bitmap", path);
                SetAssetString(connectedAsset, "texture_Bitmap", path);

                // Disable link first so all scale props are writable
                SetAssetBoolean(connectedAsset, "texture_LinkTextureTransforms", false);
                SetAssetBoolean(connectedAsset, "unifiedbitmap_LinkTextureTransforms", false);

                // Set scale/offset using distance-aware setter (handles AssetPropertyDistance in feet)
                SetAssetDistance(connectedAsset, "texture_UScale", scaleXMillimeters);
                SetAssetDistance(connectedAsset, "texture_VScale", scaleYMillimeters);
                SetAssetDistance(connectedAsset, "texture_Scale_X", scaleXMillimeters);
                SetAssetDistance(connectedAsset, "texture_Scale_Y", scaleYMillimeters);
                SetAssetDistance(connectedAsset, "texture_RealWorldScaleX", scaleXMillimeters);
                SetAssetDistance(connectedAsset, "texture_RealWorldScaleY", scaleYMillimeters);
                SetAssetDistance(connectedAsset, "unifiedbitmap_RealWorldScaleX", scaleXMillimeters);
                SetAssetDistance(connectedAsset, "unifiedbitmap_RealWorldScaleY", scaleYMillimeters);
                SetAssetDistance(connectedAsset, "texture_RealWorldOffsetX", offsetXMillimeters);
                SetAssetDistance(connectedAsset, "texture_RealWorldOffsetY", offsetYMillimeters);
                SetAssetDistance(connectedAsset, "unifiedbitmap_RealWorldOffsetX", offsetXMillimeters);
                SetAssetDistance(connectedAsset, "unifiedbitmap_RealWorldOffsetY", offsetYMillimeters);
                SetAssetDouble(connectedAsset, "texture_WAngle", rotationDegrees);
                SetAssetDouble(connectedAsset, "unifiedbitmap_WAngle", rotationDegrees);

                // Now enable link if requested
                if (linkTextureTransforms)
                {
                    SetAssetBoolean(connectedAsset, "texture_LinkTextureTransforms", true);
                    SetAssetBoolean(connectedAsset, "unifiedbitmap_LinkTextureTransforms", true);
                }
            }
        }

        private Asset? TryAddConnectedAsset(AssetProperty prop, string schemaName)
        {
            System.Reflection.MethodInfo? method = prop.GetType().GetMethod("AddConnectedAsset", new Type[] { typeof(string) });
            if (method == null)
            {
                method = prop.GetType().GetMethods().FirstOrDefault(m => m.Name == "AddConnectedAsset" && m.GetParameters().Length == 1);
            }

            if (method == null)
            {
                return null;
            }

            object? result = method.Invoke(prop, new object[] { schemaName });
            return result as Asset ?? prop.GetSingleConnectedAsset();
        }

        private void SetAssetDistance(Asset asset, string propName, double valueMillimeters)
        {
            try
            {
                AssetProperty? baseProp = asset.FindByName(propName);
                if (baseProp == null || baseProp.IsReadOnly) return;

                if (baseProp is AssetPropertyDistance distProp)
                {
                    distProp.Value = valueMillimeters / MillimetersPerFoot;
                }
                else if (baseProp is AssetPropertyDouble doubleProp)
                {
                    doubleProp.Value = valueMillimeters;
                }
                else if (baseProp is AssetPropertyFloat floatProp)
                {
                    floatProp.Value = (float)valueMillimeters;
                }
            }
            catch (Exception ex) when (IsExpectedMaterialBitmapPropertyException(ex))
            {
                Logging.Logger.Instance.LogWarning($"[MaterialBitmapPropertyService] SetAssetDistance '{propName}': {ex.Message}");
            }
        }

        private void SetAssetDouble(Asset asset, string propName, double value)
        {
            try
            {
                AssetPropertyDouble? prop = asset.FindByName(propName) as AssetPropertyDouble;
                if (prop != null && !prop.IsReadOnly) prop.Value = value;
            }
            catch (Exception ex) when (IsExpectedMaterialBitmapPropertyException(ex))
            {
                Logging.Logger.Instance.LogWarning($"[MaterialBitmapPropertyService] SetAssetDouble '{propName}': {ex.Message}");
            }
        }

        private void SetAssetString(Asset asset, string propName, string value)
        {
            try
            {
                AssetPropertyString? prop = asset.FindByName(propName) as AssetPropertyString;
                if (prop != null && !prop.IsReadOnly) prop.Value = value;
            }
            catch (Exception ex) when (IsExpectedMaterialBitmapPropertyException(ex))
            {
                Logging.Logger.Instance.LogWarning($"[MaterialBitmapPropertyService] SetAssetString '{propName}': {ex.Message}");
            }
        }

        private void SetAssetBoolean(Asset asset, string propName, bool value)
        {
            try
            {
                AssetPropertyBoolean? prop = asset.FindByName(propName) as AssetPropertyBoolean;
                if (prop != null && !prop.IsReadOnly) prop.Value = value;
            }
            catch (Exception ex) when (IsExpectedMaterialBitmapPropertyException(ex))
            {
                Logging.Logger.Instance.LogWarning($"[MaterialBitmapPropertyService] SetAssetBoolean '{propName}': {ex.Message}");
            }
        }

        private static bool IsExpectedMaterialBitmapPropertyException(Exception ex)
        {
            return ex is ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.InvalidOperationException;
        }
    }
}
