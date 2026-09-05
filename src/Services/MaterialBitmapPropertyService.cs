using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using LECG.Models;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class MaterialBitmapPropertyService : IMaterialBitmapPropertyService
    {
        private const string UnifiedBitmapSchema = "UnifiedBitmap";
        private const string BumpMapSchema = "BumpMap";
        private const int BumpTypeNormalMap = 1; // Autodesk.Revit.DB.Visual.BumpmapType.NormalMap

        public void SetupBitmapProperty(AssetProperty? prop, string path)
        {
            ConnectBitmap(prop, path, TextureTransform.Uniform(TextureTransform.MillimetersPerFoot));
        }

        public void SetupBitmapProperty(AssetProperty? prop, string path, double scaleXMillimeters, double scaleYMillimeters, double offsetXMillimeters, double offsetYMillimeters, double rotationDegrees, bool linkTextureTransforms)
        {
            ConnectBitmap(prop, path, new TextureTransform(scaleXMillimeters, scaleYMillimeters, offsetXMillimeters, offsetYMillimeters, rotationDegrees, linkTextureTransforms));
        }

        public void ConnectBitmap(AssetProperty? prop, string path, TextureTransform transform)
        {
            ArgumentNullException.ThrowIfNull(transform);
            if (prop == null) return;

            Asset? connected = EnsureConnected(prop, UnifiedBitmapSchema);
            if (connected == null) return;

            SetString(connected, UnifiedBitmap.UnifiedbitmapBitmap, path);
            ApplyTransform(connected, transform);
        }

        public void ConnectNormalMap(AssetProperty? prop, string path, TextureTransform transform, double normalScale = 1.0)
        {
            ArgumentNullException.ThrowIfNull(transform);
            if (prop == null) return;

            Asset? connected = EnsureConnected(prop, BumpMapSchema);
            if (connected == null) return;

            SetString(connected, BumpMap.BumpmapBitmap, path);
            SetInteger(connected, BumpMap.BumpmapType, BumpTypeNormalMap);
            SetDouble(connected, BumpMap.BumpmapNormalScale, normalScale);
            ApplyTransform(connected, transform);
        }

        private static Asset? EnsureConnected(AssetProperty prop, string schema)
        {
            Asset? existing = prop.GetSingleConnectedAsset();
            if (existing != null)
            {
                // Replace a connected asset of the wrong schema (e.g. UnifiedBitmap on a normal slot).
                if (string.Equals(existing.Name, schema, StringComparison.OrdinalIgnoreCase) ||
                    existing.Name.Contains(schema, StringComparison.OrdinalIgnoreCase))
                {
                    return existing;
                }
                try { prop.RemoveConnectedAsset(); }
                catch (Exception ex) when (IsExpected(ex))
                {
                    Logging.Logger.Instance.LogWarning($"[MaterialBitmapPropertyService] RemoveConnectedAsset: {ex.Message}");
                    return existing;
                }
            }

            try
            {
                prop.AddConnectedAsset(schema);
                return prop.GetSingleConnectedAsset();
            }
            catch (Exception ex) when (IsExpected(ex))
            {
                Logging.Logger.Instance.LogWarning($"[MaterialBitmapPropertyService] AddConnectedAsset('{schema}') on '{prop.Name}': {ex.Message}");
                return null;
            }
        }

        private static void ApplyTransform(Asset connected, TextureTransform t)
        {
            // Unlink first so every scale/offset property is writable.
            SetBoolean(connected, UnifiedBitmap.TextureLinkTextureTransforms, false);
            SetDistance(connected, UnifiedBitmap.TextureRealWorldScaleX, t.ScaleXFeet);
            SetDistance(connected, UnifiedBitmap.TextureRealWorldScaleY, t.ScaleYFeet);
            SetDistance(connected, UnifiedBitmap.TextureRealWorldOffsetX, t.OffsetXFeet);
            SetDistance(connected, UnifiedBitmap.TextureRealWorldOffsetY, t.OffsetYFeet);
            SetDouble(connected, UnifiedBitmap.TextureWAngle, t.RotationDegrees);
            if (t.LinkTransforms)
            {
                SetBoolean(connected, UnifiedBitmap.TextureLinkTextureTransforms, true);
            }
        }

        private static void SetDistance(Asset asset, string name, double feet)
        {
            Try(name, () =>
            {
                AssetProperty? p = asset.FindByName(name);
                if (p == null || p.IsReadOnly) return;
                switch (p)
                {
                    case AssetPropertyDistance d: d.Value = UnitUtils.ConvertFromInternalUnits(feet, d.GetUnitTypeId()); break;
                    case AssetPropertyDouble d: d.Value = feet; break;
                    case AssetPropertyFloat f: f.Value = (float)feet; break;
                }
            });
        }

        private static void SetDouble(Asset asset, string name, double value)
        {
            Try(name, () =>
            {
                AssetProperty? p = asset.FindByName(name);
                if (p == null || p.IsReadOnly) return;
                switch (p)
                {
                    case AssetPropertyDouble d: d.Value = value; break;
                    case AssetPropertyFloat f: f.Value = (float)value; break;
                }
            });
        }

        private static void SetInteger(Asset asset, string name, int value)
        {
            Try(name, () =>
            {
                AssetProperty? p = asset.FindByName(name);
                if (p == null || p.IsReadOnly) return;
                switch (p)
                {
                    case AssetPropertyInteger i: i.Value = value; break;
                    case AssetPropertyEnum e: e.Value = value; break;
                }
            });
        }

        private static void SetString(Asset asset, string name, string value)
        {
            Try(name, () =>
            {
                if (asset.FindByName(name) is AssetPropertyString s && !s.IsReadOnly) s.Value = value;
            });
        }

        private static void SetBoolean(Asset asset, string name, bool value)
        {
            Try(name, () =>
            {
                if (asset.FindByName(name) is AssetPropertyBoolean b && !b.IsReadOnly) b.Value = value;
            });
        }

        private static void Try(string name, Action action)
        {
            try { action(); }
            catch (Exception ex) when (IsExpected(ex))
            {
                Logging.Logger.Instance.LogWarning($"[MaterialBitmapPropertyService] '{name}': {ex.Message}");
            }
        }

        private static bool IsExpected(Exception ex) =>
            ex is ArgumentException
            || ex is InvalidOperationException
            || ex is RevitExceptions.InvalidOperationException
            || ex is RevitExceptions.ArgumentException;
    }
}
