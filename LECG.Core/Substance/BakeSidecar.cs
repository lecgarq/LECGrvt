using System.Text.Json;
using System.Text.Json.Serialization;

namespace LECG.Core.Substance;

public sealed class BakeSidecar
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    [JsonPropertyName("targetSize")] public int TargetSize { get; set; }
    [JsonPropertyName("sourceTicks")] public Dictionary<string, long> SourceTicks { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    [JsonPropertyName("f0Written")] public bool F0Written { get; set; }
    [JsonPropertyName("opacityWritten")] public bool OpacityWritten { get; set; }

    public static BakeSidecar Build(int targetSize, IEnumerable<(string path, long ticks)> sources, bool f0Written, bool opacityWritten)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var s = new BakeSidecar { TargetSize = targetSize, F0Written = f0Written, OpacityWritten = opacityWritten };
        foreach (var (path, ticks) in sources) s.SourceTicks[path] = ticks;
        return s;
    }

    public static bool IsFresh(BakeSidecar? existing, int targetSize, IEnumerable<(string path, long ticks)> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (existing is null || existing.TargetSize != targetSize) return false;
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
