using System.Text.Json;
using System.Text.Json.Serialization;

namespace LECG.Core.Substance;

public sealed class SubstanceManifest
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    [JsonPropertyName("material")] public string Material { get; set; } = string.Empty;
    [JsonPropertyName("resolution")] public int Resolution { get; set; }
    [JsonPropertyName("normal_format")] public string? NormalFormat { get; set; }
    [JsonPropertyName("channels")] public List<SubstanceChannel> Channels { get; set; } = new();
    [JsonPropertyName("extended")] public List<SubstanceChannel> Extended { get; set; } = new();
    [JsonPropertyName("missing_canonical")] public List<string> MissingCanonical { get; set; } = new();

    public static SubstanceManifest Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize<SubstanceManifest>(json, Options)
               ?? throw new JsonException("Manifest deserialized to null.");
    }

    public Result<SubstanceMaterialEntry> ToEntry(string category, string folderPath)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(folderPath);

        string? Find(string channel) =>
            Channels.Concat(Extended)
                .FirstOrDefault(c => string.Equals(c.Channel, channel, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(c.File))
                ?.File;

        string? Full(string? file) => file is null ? null : Path.Combine(folderPath, file);

        var missing = new List<string>();
        string? baseColor = Find("BaseColor"); if (baseColor is null) missing.Add("BaseColor");
        string? normal = Find("Normal"); if (normal is null) missing.Add("Normal");
        string? roughness = Find("Roughness"); if (roughness is null) missing.Add("Roughness");
        string? metallic = Find("Metallic"); if (metallic is null) missing.Add("Metallic");

        if (missing.Count > 0)
        {
            return Result<SubstanceMaterialEntry>.Failure(
                $"{Material}: manifest lacks required channel(s): {string.Join(", ", missing)}");
        }

        if (string.IsNullOrWhiteSpace(NormalFormat))
        {
            return Result<SubstanceMaterialEntry>.Failure(
                $"{Material}: manifest lacks required normal_format (expected DirectX or OpenGL)");
        }

        string normalFormat = NormalFormat.Trim();
        if (!string.Equals(normalFormat, "DirectX", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalFormat, "OpenGL", StringComparison.OrdinalIgnoreCase))
        {
            return Result<SubstanceMaterialEntry>.Failure(
                $"{Material}: unsupported normal_format '{NormalFormat}' (expected DirectX or OpenGL)");
        }

        normalFormat = string.Equals(normalFormat, "OpenGL", StringComparison.OrdinalIgnoreCase)
            ? "OpenGL"
            : "DirectX";

        double? ior = Extended
            .FirstOrDefault(c => string.Equals(c.Channel, "IOR", StringComparison.OrdinalIgnoreCase) && c.Value.HasValue)
            ?.Value;

        return Result<SubstanceMaterialEntry>.Success(new SubstanceMaterialEntry(
            category,
            Material,
            SubstanceDisplayName.FromSlug(Material),
            folderPath,
            Full(baseColor)!,
            Full(normal)!,
            Full(roughness)!,
            Full(metallic)!,
            Full(Find("AmbientOcclusion")),
            Full(Find("Opacity")),
            ior,
            Resolution,
            normalFormat));
    }
}

public sealed class SubstanceChannel
{
    [JsonPropertyName("channel")] public string Channel { get; set; } = string.Empty;
    [JsonPropertyName("node")] public string? Node { get; set; }
    [JsonPropertyName("file")] public string? File { get; set; }
    [JsonPropertyName("width")] public int? Width { get; set; }
    [JsonPropertyName("height")] public int? Height { get; set; }
    [JsonPropertyName("bit_depth")] public int? BitDepth { get; set; }
    [JsonPropertyName("colorspace")] public string? Colorspace { get; set; }
    [JsonPropertyName("value")] public double? Value { get; set; }
    [JsonPropertyName("numeric")] public bool? Numeric { get; set; }
    [JsonPropertyName("extended")] public bool? Extended { get; set; }
    [JsonPropertyName("rendered")] public bool? Rendered { get; set; }
}
