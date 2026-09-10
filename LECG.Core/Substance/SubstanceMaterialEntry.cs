namespace LECG.Core.Substance;

public sealed record SubstanceMaterialEntry(
    string Category,
    string Slug,
    string DisplayName,
    string FolderPath,
    string BaseColorPath,
    string NormalPath,
    string RoughnessPath,
    string MetallicPath,
    string? AoPath,
    string? OpacityPath,
    double? Ior,
    int Resolution,
    string NormalFormat)
{
    public bool HasAo => AoPath is not null;
    public bool HasOpacity => OpacityPath is not null;
}
