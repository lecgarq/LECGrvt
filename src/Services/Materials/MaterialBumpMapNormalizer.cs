using Autodesk.Revit.DB.Visual;
using LECG.Models;
using System.Text;

namespace LECG.Services
{
    internal sealed record BumpMapNormalizationResult(
        NormalMapSyncStatus Status,
        string? SlotName = null,
        string? PropertyName = null,
        string? PropertyKind = null,
        string? AssetName = null,
        string? Detail = null)
    {
        public bool Changed => Status == NormalMapSyncStatus.Updated;

        public bool IsSuccessful => Status is NormalMapSyncStatus.Updated or NormalMapSyncStatus.AlreadyNormal;
    }

    internal sealed record BumpMapPropertyProbe(
        string AssetName,
        string PropertyName,
        string PropertyKind,
        bool IsReadOnly,
        Func<int>? ReadValue,
        Action<int>? WriteValue);

    internal static class MaterialBumpMapNormalizer
    {
        private static readonly string[] ConnectedBumpTypePropertyNames =
        {
            BumpMap.BumpmapType,
            "unifiedbitmap_Bump_Type",
        };

        internal static BumpMapNormalizationResult NormalizeConnectedAsset(
            Asset ownerAsset,
            AssetProperty slotProperty,
            Asset connectedAsset,
            int desiredValue)
        {
            ArgumentNullException.ThrowIfNull(ownerAsset);
            ArgumentNullException.ThrowIfNull(slotProperty);
            ArgumentNullException.ThrowIfNull(connectedAsset);

            return NormalizeSlot(ownerAsset, slotProperty, connectedAsset, desiredValue);
        }

        internal static BumpMapNormalizationResult NormalizeProbes(
            string slotName,
            IReadOnlyList<BumpMapPropertyProbe> probes,
            int desiredValue)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(slotName);
            ArgumentNullException.ThrowIfNull(probes);

            if (probes.Count == 0)
            {
                return new BumpMapNormalizationResult(
                    NormalMapSyncStatus.NoWritableBumpTypeProperty,
                    slotName,
                    Detail: "No supported bump type property was found.");
            }

            BumpMapNormalizationResult? writeFailure = null;
            bool sawReadOnlyProbe = false;

            foreach (BumpMapPropertyProbe probe in probes)
            {
                if (probe.ReadValue != null)
                {
                    int currentValue = probe.ReadValue();
                    if (currentValue == desiredValue)
                    {
                        return new BumpMapNormalizationResult(
                            NormalMapSyncStatus.AlreadyNormal,
                            slotName,
                            probe.PropertyName,
                            probe.PropertyKind,
                            probe.AssetName,
                            "Bump type was already set to the requested value.");
                    }
                }

                if (probe.IsReadOnly || probe.WriteValue == null)
                {
                    sawReadOnlyProbe |= probe.IsReadOnly;
                    continue;
                }

                try
                {
                    probe.WriteValue(desiredValue);
                    return new BumpMapNormalizationResult(
                        NormalMapSyncStatus.Updated,
                        slotName,
                        probe.PropertyName,
                        probe.PropertyKind,
                        probe.AssetName,
                        "Bump type updated.");
                }
                catch (Exception ex)
                {
                    writeFailure ??= new BumpMapNormalizationResult(
                        NormalMapSyncStatus.WriteFailed,
                        slotName,
                        probe.PropertyName,
                        probe.PropertyKind,
                        probe.AssetName,
                        ex.Message);
                }
            }

            if (writeFailure != null)
            {
                return writeFailure;
            }

            return new BumpMapNormalizationResult(
                NormalMapSyncStatus.NoWritableBumpTypeProperty,
                slotName,
                Detail: sawReadOnlyProbe
                    ? "Supported bump type properties were read-only."
                    : "No writable supported bump type property was found.");
        }

        private static BumpMapNormalizationResult NormalizeSlot(
            Asset ownerAsset,
            AssetProperty slotProperty,
            Asset connectedAsset,
            int desiredValue)
        {
            string slotName = slotProperty.Name;
            string connectedAssetName = connectedAsset.Name;

            BumpMapNormalizationResult result = NormalizeProbes(slotName, BuildProbes(ownerAsset, slotProperty, connectedAsset), desiredValue);
            if (result.Status == NormalMapSyncStatus.NoWritableBumpTypeProperty
                && LooksLikeUnifiedBitmapSchema(connectedAsset))
            {
                return TryRewrapUnifiedBitmapAsBumpMap(ownerAsset, slotProperty, connectedAsset, desiredValue);
            }

            if (result.Status != NormalMapSyncStatus.NoWritableBumpTypeProperty)
            {
                return result;
            }

            string unresolvedDetail = BuildNoWritablePropertyDetailSafe(result.Detail, connectedAsset);
            return result with
            {
                AssetName = connectedAssetName,
                Detail = unresolvedDetail,
            };
        }

        private static List<BumpMapPropertyProbe> BuildOwnerAndSlotProbes(Asset ownerAsset, AssetProperty slotProperty)
        {
            List<BumpMapPropertyProbe> probes = new List<BumpMapPropertyProbe>();
            AddProbe(probes, ownerAsset, $"{slotProperty.Name}_type");

            HashSet<Asset> visitedAssets = new HashSet<Asset>();
            HashSet<AssetProperty> visitedProperties = new HashSet<AssetProperty>();
            AddConnectedPropertyProbes(probes, ownerAsset.Name, slotProperty, visitedAssets, visitedProperties);
            return probes;
        }

        private static List<BumpMapPropertyProbe> BuildProbes(
            Asset ownerAsset,
            AssetProperty slotProperty,
            Asset connectedAsset)
        {
            List<BumpMapPropertyProbe> probes = new List<BumpMapPropertyProbe>();
            HashSet<Asset> visitedAssets = new HashSet<Asset>();
            HashSet<AssetProperty> visitedProperties = new HashSet<AssetProperty>();

            probes.AddRange(BuildOwnerAndSlotProbes(ownerAsset, slotProperty));
            AddConnectedAssetProbes(probes, connectedAsset, visitedAssets, visitedProperties);

            return probes;
        }

        private static void AddConnectedAssetProbes(
            List<BumpMapPropertyProbe> probes,
            Asset asset,
            HashSet<Asset> visitedAssets,
            HashSet<AssetProperty> visitedProperties)
        {
            if (!visitedAssets.Add(asset))
            {
                return;
            }

            foreach (string propertyName in ConnectedBumpTypePropertyNames)
            {
                AddProbe(probes, asset, propertyName);
            }

            for (int i = 0; i < asset.Size; i++)
            {
                AssetProperty? property = asset[i];
                if (property == null)
                {
                    continue;
                }

                AddConnectedPropertyProbes(probes, asset.Name, property, visitedAssets, visitedProperties);
            }
        }

        private static void AddConnectedPropertyProbes(
            List<BumpMapPropertyProbe> probes,
            string ownerName,
            AssetProperty property,
            HashSet<Asset> visitedAssets,
            HashSet<AssetProperty> visitedProperties)
        {
            if (!visitedProperties.Add(property))
            {
                return;
            }

            AddPropertyProbe(probes, ownerName, property);

            if (property.NumberOfConnectedProperties <= 0)
            {
                return;
            }

            foreach (AssetProperty connectedProperty in property.GetAllConnectedProperties())
            {
                AddPropertyProbe(probes, ownerName, connectedProperty);

                Asset? connectedAsset = connectedProperty.GetSingleConnectedAsset();
                if (connectedAsset != null)
                {
                    AddConnectedAssetProbes(probes, connectedAsset, visitedAssets, visitedProperties);
                }

                AddConnectedPropertyProbes(probes, ownerName, connectedProperty, visitedAssets, visitedProperties);
            }
        }

        private static void AddProbe(List<BumpMapPropertyProbe> probes, Asset asset, string propertyName)
        {
            AssetProperty? property = asset.FindByName(propertyName);
            if (property == null)
            {
                return;
            }

            AddPropertyProbe(probes, asset.Name, property);
        }

        private static void AddPropertyProbe(List<BumpMapPropertyProbe> probes, string ownerName, AssetProperty property)
        {
            if (!IsCandidateBumpTypePropertyName(property.Name))
            {
                return;
            }

            if (property is AssetPropertyInteger integerProperty)
            {
                probes.Add(new BumpMapPropertyProbe(
                    ownerName,
                    property.Name,
                    nameof(AssetPropertyInteger),
                    integerProperty.IsReadOnly,
                    () => integerProperty.Value,
                    value => integerProperty.Value = value));
                return;
            }

            if (property is AssetPropertyEnum enumProperty)
            {
                probes.Add(new BumpMapPropertyProbe(
                    ownerName,
                    property.Name,
                    nameof(AssetPropertyEnum),
                    enumProperty.IsReadOnly,
                    () => enumProperty.Value,
                    value => enumProperty.Value = value));
            }
        }

        private static bool IsCandidateBumpTypePropertyName(string propertyName)
        {
            if (ConnectedBumpTypePropertyNames.Contains(propertyName, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            if (propertyName.EndsWith("_bump_map_type", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return propertyName.Contains("bump", StringComparison.OrdinalIgnoreCase)
                && propertyName.Contains("type", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeUnifiedBitmapSchema(Asset asset)
        {
            return asset.Name.Equals("UnifiedBitmapSchema", StringComparison.OrdinalIgnoreCase)
                || asset.Name.Equals("UnifiedBitmap", StringComparison.OrdinalIgnoreCase);
        }

        private static BumpMapNormalizationResult TryRewrapUnifiedBitmapAsBumpMap(
            Asset ownerAsset,
            AssetProperty slotProperty,
            Asset connectedAsset,
            int desiredValue)
        {
            string slotName = slotProperty.Name;
            string connectedAssetName = connectedAsset.Name;

            try
            {
                Dictionary<string, object> bitmapSnapshot = CaptureBitmapSnapshot(connectedAsset);

                slotProperty.RemoveConnectedAsset();

                AssetProperty? refreshedSlotProperty = ownerAsset.FindByName(slotName);
                if (refreshedSlotProperty == null)
                {
                    return new BumpMapNormalizationResult(
                        NormalMapSyncStatus.WriteFailed,
                        slotName,
                        AssetName: connectedAssetName,
                        Detail: "Failed to reacquire the bump slot after removing the existing connected asset.");
                }

                Asset? bumpMapAsset = TryAddConnectedAsset(refreshedSlotProperty, "BumpMap");
                if (bumpMapAsset == null)
                {
                    return new BumpMapNormalizationResult(
                        NormalMapSyncStatus.NoWritableBumpTypeProperty,
                        slotName,
                        AssetName: connectedAssetName,
                        Detail: "Failed to create BumpMap wrapper for UnifiedBitmapSchema.");
                }

                Asset bitmapTarget = ResolveBitmapTargetAsset(bumpMapAsset);
                ApplyBitmapSnapshot(bitmapTarget, bitmapSnapshot);

                BumpMapNormalizationResult result = NormalizeProbes(slotName, BuildProbes(ownerAsset, refreshedSlotProperty, bumpMapAsset), desiredValue);
                if (result.IsSuccessful)
                {
                    return result with
                    {
                        Detail = "Rewrapped UnifiedBitmapSchema as BumpMap and updated bump type.",
                    };
                }

                return result with
                {
                    AssetName = bumpMapAsset.Name,
                    Detail = $"Rewrapped UnifiedBitmapSchema as BumpMap but could not set bump type. {result.Detail}".Trim(),
                };
            }
            catch (Exception ex)
            {
                return new BumpMapNormalizationResult(
                    NormalMapSyncStatus.WriteFailed,
                    slotName,
                    AssetName: connectedAssetName,
                    Detail: $"Failed to rewrap UnifiedBitmapSchema as BumpMap: {ex.Message}");
            }
        }

        private static Dictionary<string, object> CaptureBitmapSnapshot(Asset asset)
        {
            string[] propertyNames =
            {
                "unifiedbitmap_Bitmap",
                "texture_Bitmap",
                "texture_LinkTextureTransforms",
                "unifiedbitmap_LinkTextureTransforms",
                "texture_UScale",
                "texture_VScale",
                "texture_Scale_X",
                "texture_Scale_Y",
                "texture_RealWorldScaleX",
                "texture_RealWorldScaleY",
                "unifiedbitmap_RealWorldScaleX",
                "unifiedbitmap_RealWorldScaleY",
                "texture_RealWorldOffsetX",
                "texture_RealWorldOffsetY",
                "unifiedbitmap_RealWorldOffsetX",
                "unifiedbitmap_RealWorldOffsetY",
                "texture_WAngle",
                "unifiedbitmap_WAngle",
            };

            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (string propertyName in propertyNames)
            {
                AssetProperty? property = asset.FindByName(propertyName);
                switch (property)
                {
                    case AssetPropertyString stringProperty when !string.IsNullOrWhiteSpace(stringProperty.Value):
                        values[propertyName] = stringProperty.Value;
                        break;
                    case AssetPropertyBoolean boolProperty:
                        values[propertyName] = boolProperty.Value;
                        break;
                    case AssetPropertyDistance distanceProperty:
                        values[propertyName] = distanceProperty.Value;
                        break;
                    case AssetPropertyDouble doubleProperty:
                        values[propertyName] = doubleProperty.Value;
                        break;
                    case AssetPropertyFloat floatProperty:
                        values[propertyName] = floatProperty.Value;
                        break;
                    case AssetPropertyInteger integerProperty:
                        values[propertyName] = integerProperty.Value;
                        break;
                }
            }

            return values;
        }

        private static Asset ResolveBitmapTargetAsset(Asset bumpMapAsset)
        {
            for (int i = 0; i < bumpMapAsset.Size; i++)
            {
                Asset? nestedAsset = bumpMapAsset[i]?.GetSingleConnectedAsset();
                if (nestedAsset != null)
                {
                    return nestedAsset;
                }
            }

            AssetProperty? bitmapProperty = bumpMapAsset.FindByName(BumpMap.BumpmapBitmap);
            if (bitmapProperty != null)
            {
                Asset? nestedAsset = TryAddConnectedAsset(bitmapProperty, "UnifiedBitmap")
                    ?? TryAddConnectedAsset(bitmapProperty, "UnifiedBitmapSchema");
                if (nestedAsset != null)
                {
                    return nestedAsset;
                }
            }

            return bumpMapAsset;
        }

        private static void ApplyBitmapSnapshot(Asset asset, IReadOnlyDictionary<string, object> values)
        {
            foreach ((string propertyName, object value) in values)
            {
                AssetProperty? property = asset.FindByName(propertyName);
                if (property == null || property.IsReadOnly)
                {
                    continue;
                }

                switch (property)
                {
                    case AssetPropertyString stringProperty when value is string stringValue:
                        stringProperty.Value = stringValue;
                        break;
                    case AssetPropertyBoolean booleanProperty when value is bool boolValue:
                        booleanProperty.Value = boolValue;
                        break;
                    case AssetPropertyDistance distanceProperty when value is double distanceValue:
                        distanceProperty.Value = distanceValue;
                        break;
                    case AssetPropertyDouble doubleProperty when value is double doubleValue:
                        doubleProperty.Value = doubleValue;
                        break;
                    case AssetPropertyFloat floatProperty when value is float floatValue:
                        floatProperty.Value = floatValue;
                        break;
                    case AssetPropertyFloat floatProperty when value is double floatAsDoubleValue:
                        floatProperty.Value = (float)floatAsDoubleValue;
                        break;
                    case AssetPropertyInteger integerProperty when value is int integerValue:
                        integerProperty.Value = integerValue;
                        break;
                }
            }
        }

        private static Asset? TryAddConnectedAsset(AssetProperty property, string schemaName)
        {
            System.Reflection.MethodInfo? method = property.GetType().GetMethod("AddConnectedAsset", new Type[] { typeof(string) });
            if (method == null)
            {
                method = property.GetType().GetMethods().FirstOrDefault(m => m.Name == "AddConnectedAsset" && m.GetParameters().Length == 1);
            }

            if (method == null)
            {
                return null;
            }

            try
            {
                object? result = method.Invoke(property, new object[] { schemaName });
                return result as Asset ?? property.GetSingleConnectedAsset();
            }
            catch
            {
                return null;
            }
        }

        private static string BuildNoWritablePropertyDetail(string? detail, Asset connectedAsset)
        {
            string baseDetail = string.IsNullOrWhiteSpace(detail)
                ? "No supported bump type property was found."
                : detail;

            return $"{baseDetail} {DescribeAssetForDiagnostics(connectedAsset)}".TrimEnd();
        }

        private static string BuildNoWritablePropertyDetailSafe(string? detail, Asset connectedAsset)
        {
            try
            {
                return BuildNoWritablePropertyDetail(detail, connectedAsset);
            }
            catch
            {
                return string.IsNullOrWhiteSpace(detail)
                    ? "No supported bump type property was found."
                    : detail;
            }
        }

        private static string DescribeAssetForDiagnostics(Asset asset)
        {
            var sb = new StringBuilder();
            sb.Append("Connected asset props: ");

            int emitted = 0;
            for (int i = 0; i < asset.Size; i++)
            {
                AssetProperty? property = asset[i];
                if (property == null)
                {
                    continue;
                }

                if (emitted > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(DescribePropertyForDiagnostics(property));
                emitted++;

                if (emitted >= 12)
                {
                    if (asset.Size > emitted)
                    {
                        sb.Append(", ...");
                    }

                    break;
                }
            }

            if (emitted == 0)
            {
                sb.Append("<none>");
            }

            return sb.ToString();
        }

        private static string DescribePropertyForDiagnostics(AssetProperty property)
        {
            var sb = new StringBuilder();
            sb.Append(property.Name);
            sb.Append(" [");
            sb.Append(property.Type);
            sb.Append("]");

            if (property.NumberOfConnectedProperties > 0)
            {
                sb.Append(" -> ");
                int emitted = 0;
                foreach (AssetProperty connectedProperty in property.GetAllConnectedProperties())
                {
                    if (emitted > 0)
                    {
                        sb.Append("|");
                    }

                    sb.Append(connectedProperty.Name);
                    emitted++;

                    if (emitted >= 4)
                    {
                        sb.Append("|...");
                        break;
                    }
                }
            }

            return sb.ToString();
        }
    }
}
