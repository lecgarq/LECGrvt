using FluentAssertions;
using LECG.Models;
using LECG.Services;

namespace LECG.Tests.Services;

public class MaterialBumpMapNormalizerTests
{
    [Fact]
    public void NormalizeProbes_UpdatesSiblingSlotTypeProperty_WhenWritable()
    {
        int currentValue = 0;

        BumpMapNormalizationResult result = MaterialBumpMapNormalizer.NormalizeProbes(
            "generic_bump_map",
            new[]
            {
                CreateProbe("GenericSchema", "generic_bump_map_type", nameof(Int32), false, () => currentValue, value => currentValue = value),
            },
            desiredValue: 1);

        result.Status.Should().Be(NormalMapSyncStatus.Updated);
        result.PropertyName.Should().Be("generic_bump_map_type");
        currentValue.Should().Be(1);
    }

    [Fact]
    public void NormalizeProbes_UpdatesEnumProperty_WhenWritable()
    {
        int currentValue = 0;

        BumpMapNormalizationResult result = MaterialBumpMapNormalizer.NormalizeProbes(
            "generic_bump_map",
            new[]
            {
                CreateProbe("UnifiedBitmapSchema", "unifiedbitmap_Bump_Type", "AssetPropertyEnum", false, () => currentValue, value => currentValue = value),
            },
            desiredValue: 1);

        result.Status.Should().Be(NormalMapSyncStatus.Updated);
        result.PropertyKind.Should().Be("AssetPropertyEnum");
        currentValue.Should().Be(1);
    }

    [Fact]
    public void NormalizeProbes_ReturnsAlreadyNormal_WhenMatchingValueIsFound()
    {
        BumpMapNormalizationResult result = MaterialBumpMapNormalizer.NormalizeProbes(
            "generic_bump_map",
            new[]
            {
                CreateProbe("BumpMap", "bump_map_type", "AssetPropertyInteger", false, () => 1, _ => throw new InvalidOperationException("Setter should not run")),
            },
            desiredValue: 1);

        result.Status.Should().Be(NormalMapSyncStatus.AlreadyNormal);
        result.PropertyName.Should().Be("bump_map_type");
    }

    [Fact]
    public void NormalizeProbes_ReturnsNoWritableProperty_WhenProbeListIsEmpty()
    {
        BumpMapNormalizationResult result = MaterialBumpMapNormalizer.NormalizeProbes("generic_bump_map", Array.Empty<BumpMapPropertyProbe>(), 1);

        result.Status.Should().Be(NormalMapSyncStatus.NoWritableBumpTypeProperty);
    }

    [Fact]
    public void NormalizeProbes_ReturnsNoWritableProperty_WhenOnlyReadOnlyPropertiesExist()
    {
        BumpMapNormalizationResult result = MaterialBumpMapNormalizer.NormalizeProbes(
            "generic_bump_map",
            new[]
            {
                CreateProbe("BumpMap", "generic_bump_map_type", "AssetPropertyInteger", true, () => 0, null),
            },
            desiredValue: 1);

        result.Status.Should().Be(NormalMapSyncStatus.NoWritableBumpTypeProperty);
        result.Detail.Should().Contain("read-only");
    }

    [Fact]
    public void NormalizeProbes_ReturnsWriteFailed_WhenWritablePropertyThrows()
    {
        BumpMapNormalizationResult result = MaterialBumpMapNormalizer.NormalizeProbes(
            "generic_bump_map",
            new[]
            {
                CreateProbe("BumpMap", "generic_bump_map_type", "AssetPropertyInteger", false, () => 0, _ => throw new InvalidOperationException("boom")),
            },
            desiredValue: 1);

        result.Status.Should().Be(NormalMapSyncStatus.WriteFailed);
        result.Detail.Should().Be("boom");
    }

    private static BumpMapPropertyProbe CreateProbe(
        string assetName,
        string propertyName,
        string propertyKind,
        bool isReadOnly,
        Func<int>? readValue,
        Action<int>? writeValue)
    {
        return new BumpMapPropertyProbe(assetName, propertyName, propertyKind, isReadOnly, readValue, writeValue);
    }
}
