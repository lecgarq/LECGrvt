using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using LECG.Models;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class MaterialBitmapPropertyService
    {
        private const double MillimetersPerFoot = 304.8;
        private readonly ILogger _logger;

        public MaterialBitmapPropertyService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void ConnectBitmap(AssetProperty? prop, string path, TextureTransform transform)
        {
            ArgumentNullException.ThrowIfNull(transform);
            Asset? connected = EnsureConnected(prop, "UnifiedBitmap");
            if (connected == null) return;

            SetAssetString(connected, UnifiedBitmap.UnifiedbitmapBitmap, path);
            ApplyTransform(connected, transform);
        }

        public void ConnectNormalMap(AssetProperty? prop, string path, TextureTransform transform, double normalScale = 1.0)
        {
            ArgumentNullException.ThrowIfNull(transform);
            Asset? connected = EnsureConnected(prop, "BumpMap");
            if (connected == null) return;

            SetAssetString(connected, BumpMap.BumpmapBitmap, path);
            SetAssetInteger(connected, BumpMap.BumpmapType, 1);
            SetAssetDouble(connected, BumpMap.BumpmapNormalScale, normalScale);
            ApplyTransform(connected, transform);
        }

        private Asset? EnsureConnected(AssetProperty? prop, string schema)
        {
            if (prop == null) return null;
            Asset? existing = prop.GetSingleConnectedAsset();
            if (existing != null && existing.Name.Contains(schema, StringComparison.OrdinalIgnoreCase)) return existing;

            try
            {
                if (existing != null) prop.RemoveConnectedAsset();
                prop.AddConnectedAsset(schema);
                return prop.GetSingleConnectedAsset();
            }
            catch (Exception ex) when (IsExpectedMaterialBitmapPropertyException(ex))
            {
                _logger.LogWarning($"Connect '{schema}' to '{prop.Name}': {ex.Message}", scope: "MaterialBitmapProperty");
                return null;
            }
        }

        private void ApplyTransform(Asset asset, TextureTransform transform)
        {
            SetAssetBoolean(asset, UnifiedBitmap.TextureLinkTextureTransforms, false);
            SetAssetDistance(asset, UnifiedBitmap.TextureRealWorldScaleX, transform.ScaleXMillimeters);
            SetAssetDistance(asset, UnifiedBitmap.TextureRealWorldScaleY, transform.ScaleYMillimeters);
            SetAssetDistance(asset, UnifiedBitmap.TextureRealWorldOffsetX, transform.OffsetXMillimeters);
            SetAssetDistance(asset, UnifiedBitmap.TextureRealWorldOffsetY, transform.OffsetYMillimeters);
            SetAssetDouble(asset, UnifiedBitmap.TextureWAngle, transform.RotationDegrees);
            if (transform.LinkTransforms)
                SetAssetBoolean(asset, UnifiedBitmap.TextureLinkTextureTransforms, true);
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

            // Connect a plain UnifiedBitmap directly to the bump slot — the same structure Revit's own UI
            // creates and, crucially, the one Enscape reads (it looks for unifiedbitmap_Bitmap). A BumpMap
            // ("Bump Texture") node stores the path in bumpmap_Bitmap, which Enscape ignores, leaving its
            // Normal slot empty. If a non-UnifiedBitmap node is already connected, drop it first.
            Asset? connected = prop.GetSingleConnectedAsset();
            if (connected != null && connected.Name.IndexOf("UnifiedBitmap", StringComparison.OrdinalIgnoreCase) < 0)
            {
                try { prop.RemoveConnectedAsset(); }
                catch (Exception ex) when (IsExpectedMaterialBitmapPropertyException(ex))
                {
                    _logger.LogWarning($"SetupBumpBitmapProperty RemoveConnectedAsset failed: {ex.Message}", scope: "MaterialBitmapProperty");
                }
                connected = prop.GetSingleConnectedAsset();
            }

            if (connected == null)
            {
                try
                {
                    connected = TryAddConnectedAsset(prop, "UnifiedBitmap")
                        ?? TryAddConnectedAsset(prop, "UnifiedBitmapSchema");
                }
                catch (Exception ex) when (IsExpectedMaterialBitmapPropertyException(ex))
                {
                    _logger.LogWarning($"SetupBumpBitmapProperty AddConnectedAsset failed: {ex.Message}", scope: "MaterialBitmapProperty");
                }
            }

            connected ??= prop.GetSingleConnectedAsset();
            if (connected == null) return;

            ApplyBitmapProperties(connected, path, scaleXMillimeters, scaleYMillimeters, offsetXMillimeters, offsetYMillimeters, rotationDegrees, linkTextureTransforms);

            // Set the Advanced -> Data Type = Normal(1)/Height(0) flag if this schema exposes it.
            MaterialBumpMapNormalizer.ForceBumpTypeEverywhere(ownerAsset, prop, connected, bumpmapType);
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

        private void SetAssetInteger(Asset asset, string propName, int value)
        {
            try
            {
                AssetProperty? prop = asset.FindByName(propName);
                if (prop is AssetPropertyInteger integer && !integer.IsReadOnly) integer.Value = value;
                else if (prop is AssetPropertyEnum enumeration && !enumeration.IsReadOnly) enumeration.Value = value;
            }
            catch (Exception ex) when (IsExpectedMaterialBitmapPropertyException(ex))
            {
                _logger.LogWarning($"SetAssetInteger '{propName}': {ex.Message}", scope: "MaterialBitmapProperty");
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
