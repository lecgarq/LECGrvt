namespace LECG.Models
{
    public sealed record BakedTextureSet(
        string BaseColor,
        string NormalGl,
        string Roughness,
        string? F0,
        string? Opacity,
        bool WasSkipped);
}
