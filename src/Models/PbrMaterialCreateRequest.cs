namespace LECG.Models
{
    public sealed record PbrMaterialCreateRequest(
        string MaterialName,
        string AppearanceAssetName,
        string? Description,
        string? MaterialClass,
        string DiffusePath,
        string? RoughnessPath,
        string? NormalPath,
        bool UseRenderAppearanceForShading,
        double ScaleXMillimeters,
        double ScaleYMillimeters,
        double OffsetXMillimeters,
        double OffsetYMillimeters,
        double RotationDegrees,
        bool LinkTextureTransforms,
        int BumpMapType);
}
