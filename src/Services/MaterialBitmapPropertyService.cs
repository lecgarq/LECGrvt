using System;
using System.Linq;
using System.Text;
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
                ApplyBitmapProperties(connectedAsset, path, scaleXMillimeters, scaleYMillimeters, offsetXMillimeters, offsetYMillimeters, rotationDegrees, linkTextureTransforms);
        }

        public void SetupBumpBitmapProperty(Asset ownerAsset, AssetProperty? prop, string path, double scaleXMillimeters, double scaleYMillimeters, double offsetXMillimeters, double offsetYMillimeters, double rotationDegrees, bool linkTextureTransforms, int bumpmapType)
        {
            ArgumentNullException.ThrowIfNull(ownerAsset);
            if (prop == null) return;

            Asset? bumpMapAsset = prop.GetSingleConnectedAsset();
            if (bumpMapAsset == null)
            {
                try
                {
                    bumpMapAsset = TryAddConnectedAsset(prop, "BumpMap")
                        ?? TryAddConnectedAsset(prop, "UnifiedBitmap")
                        ?? TryAddConnectedAsset(prop, "UnifiedBitmapSchema");
                }
                catch (Exception ex) when (IsExpectedMaterialBitmapPropertyException(ex))
                {
                    Logging.Logger.Instance.LogWarning($"[MaterialBitmapPropertyService] SetupBumpBitmapProperty AddConnectedAsset failed: {ex.Message}");
                }
            }

            if (bumpMapAsset == null) return;

            // Dump the connected asset's property list to the log so we can see the runtime schema.
            LogBumpAssetDiagnostics(bumpMapAsset);

            // Find the nested bitmap asset inside a BumpMap wrapper, if any.
            Asset? bitmapAsset = null;
            for (int i = 0; i < bumpMapAsset.Size; i++)
            {
                Asset? nested = bumpMapAsset[i]?.GetSingleConnectedAsset();
                if (nested == null) continue;
                bitmapAsset = nested;
                break;
            }

            BumpMapNormalizationResult normalizationResult = MaterialBumpMapNormalizer.NormalizeConnectedAsset(ownerAsset, prop, bumpMapAsset, bumpmapType);
            if (!normalizationResult.IsSuccessful)
            {
                Logging.Logger.Instance.LogWarning(
                    $"[MaterialBitmapPropertyService] Bump normalization unresolved for slot '{normalizationResult.SlotName}': {normalizationResult.Detail}");
            }

            // REFRESH: NormalizeConnectedAsset may have replaced the connected asset tree (re-wrapped as BumpMap).
            // Re-read from the slot so we target the LIVE asset, not the old orphaned reference.
            Asset? activeBumpAsset = prop.GetSingleConnectedAsset();
            if (activeBumpAsset != null)
            {
                // For a BumpMap wrapper, the inner UnifiedBitmap lives under BumpMap.BumpmapBitmap.
                // For a raw UnifiedBitmapSchema, the asset IS the bitmap target.
                Asset bitmapTarget = ResolveBitmapTarget(activeBumpAsset);
                ApplyBitmapProperties(bitmapTarget, path, scaleXMillimeters, scaleYMillimeters, offsetXMillimeters, offsetYMillimeters, rotationDegrees, linkTextureTransforms);
            }
        }

        private static Asset ResolveBitmapTarget(Asset bumpAsset)
        {
            // BumpMap schema: inner UnifiedBitmap is connected to the BumpmapBitmap named property.
            AssetProperty? bitmapProp = bumpAsset.FindByName(BumpMap.BumpmapBitmap);
            if (bitmapProp != null)
            {
                Asset? inner = bitmapProp.GetSingleConnectedAsset();
                if (inner != null) return inner;

                // Inner not connected yet — add a UnifiedBitmap child so we have something to write to.
                inner = TryStaticAddConnectedAsset(bitmapProp, "UnifiedBitmap")
                    ?? TryStaticAddConnectedAsset(bitmapProp, "UnifiedBitmapSchema");
                if (inner != null) return inner;
            }

            // Fallback: raw UnifiedBitmapSchema directly in the slot — it IS the target.
            return bumpAsset;
        }

        private static Asset? TryStaticAddConnectedAsset(AssetProperty prop, string schemaName)
        {
            System.Reflection.MethodInfo? method = prop.GetType().GetMethod("AddConnectedAsset", new Type[] { typeof(string) })
                ?? prop.GetType().GetMethods().FirstOrDefault(m => m.Name == "AddConnectedAsset" && m.GetParameters().Length == 1);
            if (method == null) return null;
            try
            {
                object? result = method.Invoke(prop, new object[] { schemaName });
                return result as Asset ?? prop.GetSingleConnectedAsset();
            }
            catch { return null; }
        }

        private static void LogBumpAssetDiagnostics(Asset asset)
        {
            var sb = new StringBuilder();
            sb.Append($"[BumpDiag] schema='{asset.Name}' props={asset.Size}: ");
            for (int i = 0; i < asset.Size; i++)
            {
                AssetProperty? p = asset[i];
                if (p == null) continue;
                string connectedName = p.GetSingleConnectedAsset()?.Name ?? "-";
                sb.Append($"[{p.Name}|{p.Type}|ro={p.IsReadOnly}|conn={connectedName}] ");
            }
            Logging.Logger.Instance.Log(sb.ToString());
        }

        private void ApplyBitmapProperties(Asset asset, string path, double scaleXMillimeters, double scaleYMillimeters, double offsetXMillimeters, double offsetYMillimeters, double rotationDegrees, bool linkTextureTransforms)
        {
            SetAssetString(asset, "unifiedbitmap_Bitmap", path);
            SetAssetString(asset, "texture_Bitmap", path);

            SetAssetBoolean(asset, "texture_LinkTextureTransforms", false);
            SetAssetBoolean(asset, "unifiedbitmap_LinkTextureTransforms", false);

            SetAssetDistance(asset, "texture_UScale", scaleXMillimeters);
            SetAssetDistance(asset, "texture_VScale", scaleYMillimeters);
            SetAssetDistance(asset, "texture_Scale_X", scaleXMillimeters);
            SetAssetDistance(asset, "texture_Scale_Y", scaleYMillimeters);
            SetAssetDistance(asset, "texture_RealWorldScaleX", scaleXMillimeters);
            SetAssetDistance(asset, "texture_RealWorldScaleY", scaleYMillimeters);
            SetAssetDistance(asset, "unifiedbitmap_RealWorldScaleX", scaleXMillimeters);
            SetAssetDistance(asset, "unifiedbitmap_RealWorldScaleY", scaleYMillimeters);
            SetAssetDistance(asset, "texture_RealWorldOffsetX", offsetXMillimeters);
            SetAssetDistance(asset, "texture_RealWorldOffsetY", offsetYMillimeters);
            SetAssetDistance(asset, "unifiedbitmap_RealWorldOffsetX", offsetXMillimeters);
            SetAssetDistance(asset, "unifiedbitmap_RealWorldOffsetY", offsetYMillimeters);
            SetAssetDouble(asset, "texture_WAngle", rotationDegrees);
            SetAssetDouble(asset, "unifiedbitmap_WAngle", rotationDegrees);

            if (linkTextureTransforms)
            {
                SetAssetBoolean(asset, "texture_LinkTextureTransforms", true);
                SetAssetBoolean(asset, "unifiedbitmap_LinkTextureTransforms", true);
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

        private void SetAssetInteger(Asset asset, string propName, int value)
        {
            try
            {
                AssetPropertyInteger? prop = asset.FindByName(propName) as AssetPropertyInteger;
                if (prop != null && !prop.IsReadOnly) prop.Value = value;
            }
            catch (Exception ex) when (IsExpectedMaterialBitmapPropertyException(ex))
            {
                Logging.Logger.Instance.LogWarning($"[MaterialBitmapPropertyService] SetAssetInteger '{propName}': {ex.Message}");
            }
        }

        private static bool IsExpectedMaterialBitmapPropertyException(Exception ex)
        {
            return ex is ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.ArgumentException
                || ex is RevitExceptions.InvalidOperationException;
        }
    }
}
