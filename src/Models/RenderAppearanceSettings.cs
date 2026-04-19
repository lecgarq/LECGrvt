namespace LECG.Models
{
    public sealed record RenderAppearanceSettings(
        bool SkipCompliant = true,
        bool UseRenderAppearanceForShading = true,
        bool MatchShadingColorToRenderAppearance = true,
        bool SetSurfacePatternsToSolidFill = true,
        bool SetCutPatternsToSolidFill = true);
}
