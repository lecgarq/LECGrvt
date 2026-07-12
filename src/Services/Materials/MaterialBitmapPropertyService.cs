using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class MaterialBitmapPropertyService : IMaterialBitmapPropertyService
    {
        private const double MillimetersPerFoot = 304.8;
        private readonly ILogger _logger;

        public MaterialBitmapPropertyService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void SetupBitmapProperty(AssetProperty? prop, string path, double scaleXMillimeters, double scaleYMillimeters, double offsetXMillimeters, double offsetYMillimeters, double rotationDegrees, bool linkTextureTransforms)
        {
            if (prop == null) return;
            Asset? connectedAsset = null;

            // Always try to create a fresh connected asset first. Template-provided connected assets
            // (e.g. from the Generic appearance template) have read-only scale properties; a freshly
            // added UnifiedBitmap is fully writable. Fall back to reusing the existing asset only if
            // AddConnectedAsset fails (e.g. property type doesn't support it).
            try
            {
                connectedAsset = TryAddConnectedAsset(prop, "UnifiedBitmap")
                    ?? TryAddConnectedAsset(prop, "UnifiedBitmapSchema");
            }
            catch (Exception ex) when (IsExpectedMaterialBitmapPropertyException(ex))
            {
                _logger.LogWarning($"AddConnectedAsset failed: {ex.Message}", scope: "MaterialBitmapProperty");
            }

            if (connectedAsset == null)
                connectedAsset = prop.GetSingleConnectedAsset();

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
                    _logger.LogWarning($"SetupBumpBitmapProperty AddConnectedAsset failed: {ex.Message}", scope: "MaterialBitmapProperty");
                }
            }

            if (bumpMapAsset == null) return;

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
                _logger.LogWarning(
                    $"Bump normalization unresolved for slot '{normalizationResult.SlotName}': {normalizationResult.Detail}",
                    scope: "MaterialBitmapProperty");
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

        private void ApplyBitmapProperties(Asset asset, string path, double scaleXMillimeters, double scaleYMillimeters, double offsetXMillimeters, double offsetYMillimeters, double rotationDegrees, bool linkTextureTransforms)
        {
            // BumpMap schema stores the image as a plain string property (not a connected sub-asset).
            SetAssetString(asset, "bumpmap_Bitmap", path);
            // UnifiedBitmap / texture schemas —- these are no-ops when applied to a BumpMap asset.
            SetAssetString(asset, "unifiedbitmap_Bitmap", path);
            SetAssetString(asset, "texture_Bitmap", path);

            SetAssetBoolean(asset, "texture_LinkTextureTransforms", false);
            SetAssetBoolean(asset, "unifiedbitmap_LinkTextureTransforms", false);

            // texture_UScale/VScale are unitless tiling multipliers, NOT distances — the physical
            // sample size is carried by texture_RealWorldScaleX/Y below. Anything other than 1.0
            // here multiplies the tiling and shrinks the apparent sample size.
            // They throw on BumpMap schema — TrySetAssetDouble swallows that silently.
            TrySetAssetDouble(asset, "texture_UScale", 1.0);
            TrySetAssetDouble(asset, "texture_VScale", 1.0);
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
                    // AssetPropertyDistance.Value is NOT in Revit internal units (feet) — it is in
                    // the unit reported by GetUnitTypeId() (inches for UnifiedBitmap real-world
                    // scale/offset). Assuming feet made sample sizes come out 12x too small.
                    ForgeTypeId unitTypeId = distProp.GetUnitTypeId();
                    distProp.Value = unitTypeId != null && !unitTypeId.Empty() && UnitUtils.IsUnit(unitTypeId)
                        ? UnitUtils.Convert(valueMillimeters, UnitTypeId.Millimeters, unitTypeId)
                        : valueMillimeters / MillimetersPerFoot;
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
                _logger.LogWarning($"SetAssetDistance '{propName}': {ex.Message}", scope: "MaterialBitmapProperty");
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
                _logger.LogWarning($"SetAssetDouble '{propName}': {ex.Message}", scope: "MaterialBitmapProperty");
            }
        }

        /// <summary>Silent version — does not log if the property throws. Use for properties that are
        /// conditionally non-editable depending on schema (e.g. BumpMap's texture_UScale/VScale).</summary>
        private static void TrySetAssetDouble(Asset asset, string propName, double value)
        {
            try
            {
                AssetPropertyDouble? prop = asset.FindByName(propName) as AssetPropertyDouble;
                if (prop != null && !prop.IsReadOnly) prop.Value = value;
            }
            catch { /* intentionally swallowed */ }
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
                _logger.LogWarning($"SetAssetString '{propName}': {ex.Message}", scope: "MaterialBitmapProperty");
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
                _logger.LogWarning($"SetAssetBoolean '{propName}': {ex.Message}", scope: "MaterialBitmapProperty");
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
