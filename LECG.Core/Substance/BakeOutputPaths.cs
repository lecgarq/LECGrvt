namespace LECG.Core.Substance;

public sealed record BakeOutputPaths(
    string Folder,
    string BaseColor,
    string NormalGl,
    string Roughness,
    string F0,
    string Opacity,
    string Sidecar)
{
    public static string DefaultOutputRoot(string libraryRoot)
    {
        ArgumentNullException.ThrowIfNull(libraryRoot);
        return Path.Combine(libraryRoot, "_revit");
    }

    public static BakeOutputPaths For(SubstanceMaterialEntry entry, string outputRoot)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(outputRoot);
        string folder = Path.Combine(outputRoot, entry.Category, entry.Slug);
        string P(string suffix) => Path.Combine(folder, $"{entry.Slug}_{suffix}");
        return new BakeOutputPaths(
            folder,
            P("basecolor.png"),
            P("normal_gl.png"),
            P("roughness.png"),
            P("f0.png"),
            P("opacity.png"),
            P("bake.json"));
    }
}
