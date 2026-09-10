using System.Text.Json;
using System.Text.Json.Serialization;

namespace LECG.Core.Substance;

public sealed class BakeSidecar
{
    public const int CurrentContractVersion = 1;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    [JsonPropertyName("contractVersion")] public int ContractVersion { get; set; }
    [JsonPropertyName("consumer")] public string Consumer { get; set; } = string.Empty;
    [JsonPropertyName("targetSize")] public int TargetSize { get; set; }
    [JsonPropertyName("sourceTicks")] public Dictionary<string, long> SourceTicks { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    [JsonPropertyName("sourceNormalConvention")] public string SourceNormalConvention { get; set; } = string.Empty;
    [JsonPropertyName("sourceNormalConventionDeclared")] public bool SourceNormalConventionDeclared { get; set; }
    [JsonPropertyName("outputNormalConvention")] public string OutputNormalConvention { get; set; } = string.Empty;
    [JsonPropertyName("aoBakedIntoBaseColor")] public bool AoBakedIntoBaseColor { get; set; }
    [JsonPropertyName("derivedOutput")] public bool DerivedOutput { get; set; }
    [JsonPropertyName("f0Written")] public bool F0Written { get; set; }
    [JsonPropertyName("opacityWritten")] public bool OpacityWritten { get; set; }

    public static BakeSidecar Build(
        int targetSize,
        IEnumerable<(string path, long ticks)> sources,
        string sourceNormalConvention,
        bool sourceNormalConventionDeclared,
        bool aoBakedIntoBaseColor,
        bool f0Written,
        bool opacityWritten)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var s = new BakeSidecar
        {
            ContractVersion = CurrentContractVersion,
            Consumer = "Revit",
            TargetSize = targetSize,
            SourceNormalConvention = sourceNormalConvention,
            SourceNormalConventionDeclared = sourceNormalConventionDeclared,
            OutputNormalConvention = "OpenGL",
            AoBakedIntoBaseColor = aoBakedIntoBaseColor,
            DerivedOutput = true,
            F0Written = f0Written,
            OpacityWritten = opacityWritten,
        };
        foreach (var (path, ticks) in sources) s.SourceTicks[path] = ticks;
        return s;
    }

    public static bool IsFresh(
        BakeSidecar? existing,
        int targetSize,
        IEnumerable<(string path, long ticks)> sources,
        string sourceNormalConvention,
        bool sourceNormalConventionDeclared,
        bool aoBakedIntoBaseColor)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (existing is null
            || existing.ContractVersion != CurrentContractVersion
            || !string.Equals(existing.Consumer, "Revit", StringComparison.Ordinal)
            || existing.TargetSize != targetSize
            || !string.Equals(existing.SourceNormalConvention, sourceNormalConvention, StringComparison.Ordinal)
            || existing.SourceNormalConventionDeclared != sourceNormalConventionDeclared
            || !string.Equals(existing.OutputNormalConvention, "OpenGL", StringComparison.Ordinal)
            || existing.AoBakedIntoBaseColor != aoBakedIntoBaseColor
            || !existing.DerivedOutput)
        {
            return false;
        }
        var list = sources.ToList();
        if (list.Count != existing.SourceTicks.Count) return false;
        foreach (var (path, ticks) in list)
        {
            if (!existing.SourceTicks.TryGetValue(path, out long known) || known < ticks) return false;
        }
        return true;
    }

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public static BakeSidecar? FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            var sidecar = JsonSerializer.Deserialize<BakeSidecar>(json, Options);
            if (sidecar is not null)
            {
                sidecar.SourceTicks = new Dictionary<string, long>(sidecar.SourceTicks, StringComparer.OrdinalIgnoreCase);
            }
            return sidecar;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
