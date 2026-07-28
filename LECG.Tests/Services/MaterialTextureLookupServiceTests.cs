using FluentAssertions;
using LECG.Services;

namespace LECG.Tests.Services;

public sealed class MaterialTextureLookupServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "LECG.PbrImport.Tests", Guid.NewGuid().ToString("N"));

    public MaterialTextureLookupServiceTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void ScanImmediateMaterialFolders_DerivesMaterialNameFromAlbedoSuffix()
    {
        string folder = CreateChild("Negro");
        Touch(folder, "LECG_MAT_PLASTICO (NEGRO)_Albedo.png");

        var candidates = new MaterialTextureLookupService().ScanImmediateMaterialFolders(_root);

        candidates.Should().ContainSingle();
        candidates[0].IsValid.Should().BeTrue();
        candidates[0].MaterialName.Should().Be("LECG_MAT_PLASTICO (NEGRO)");
        candidates[0].DiffusePath.Should().EndWith("LECG_MAT_PLASTICO (NEGRO)_Albedo.png");
    }

    [Fact]
    public void ScanImmediateMaterialFolders_DerivesMaterialNameFromHyphenatedAlbedoSuffix()
    {
        string folder = CreateChild("Azul");
        Touch(folder, "LECG_MAT_PLASTICO-GLAZED (AZUL)_Albedo.png");

        var candidates = new MaterialTextureLookupService().ScanImmediateMaterialFolders(_root);

        candidates.Should().ContainSingle();
        candidates[0].MaterialName.Should().Be("LECG_MAT_PLASTICO-GLAZED (AZUL)");
    }

    [Fact]
    public void ScanImmediateMaterialFolders_DetectsNormalRoughnessAndSpecularRoughness()
    {
        string folder = CreateChild("Full");
        Touch(folder, "Sample_Albedo.png");
        Touch(folder, "Sample_Normal.png");
        Touch(folder, "Sample_Roughness.png");
        Touch(folder, "Sample_Specularroughness.png");

        var candidates = new MaterialTextureLookupService().ScanImmediateMaterialFolders(_root);

        candidates.Should().ContainSingle();
        candidates[0].NormalPath.Should().EndWith("Sample_Normal.png");
        candidates[0].RoughnessPath.Should().NotBeNull();
        candidates[0].RoughnessPath.Should().Match(path =>
            path!.EndsWith("Sample_Roughness.png", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("Sample_Specularroughness.png", StringComparison.OrdinalIgnoreCase));
        candidates[0].DetectedCount.Should().Be(3);
    }

    [Fact]
    public void ScanImmediateMaterialFolders_IgnoresUnsupportedPbrChannels()
    {
        string folder = CreateChild("Unsupported");
        Touch(folder, "Sample_Albedo.png");
        Touch(folder, "Sample_Normal.png");
        Touch(folder, "Sample_Roughness.png");
        Touch(folder, "Sample_Metallic.png");
        Touch(folder, "Sample_AO.png");
        Touch(folder, "Sample_Displacement.png");
        Touch(folder, "Sample_Opacity.png");

        var candidates = new MaterialTextureLookupService().ScanImmediateMaterialFolders(_root);

        candidates.Should().ContainSingle();
        candidates[0].DetectedCount.Should().Be(3);
    }

    [Fact]
    public void ScanImmediateMaterialFolders_DoesNotScanNestedDescendants()
    {
        string child = CreateChild("Parent");
        string nested = Path.Combine(child, "Nested");
        Directory.CreateDirectory(nested);
        Touch(nested, "Nested_Albedo.png");

        var candidates = new MaterialTextureLookupService().ScanImmediateMaterialFolders(_root);

        candidates.Should().ContainSingle();
        candidates[0].IsValid.Should().BeFalse();
        candidates[0].SkipReason.Should().Be("No diffuse/albedo texture found.");
    }

    [Fact]
    public void ScanImmediateMaterialFolders_SkipsChildFoldersWithoutDiffuseOrAlbedo()
    {
        string folder = CreateChild("OnlyNormal");
        Touch(folder, "Only_Normal.png");

        var candidates = new MaterialTextureLookupService().ScanImmediateMaterialFolders(_root);

        candidates.Should().ContainSingle();
        candidates[0].IsValid.Should().BeFalse();
        candidates[0].MaterialName.Should().BeEmpty();
        candidates[0].SkipReason.Should().Be("No diffuse/albedo texture found.");
    }

    [Theory]
    [InlineData("Brick_Normal.png", true)]
    [InlineData("Brick_NormalGL.png", true)]
    [InlineData("Brick_nor_2k.jpg", true)]
    [InlineData("wood-nrm.tif", true)]
    [InlineData("Concrete_Height.png", false)]
    [InlineData("Concrete_Bump.png", false)]
    [InlineData("Sample_Albedo.png", false)]
    [InlineData("", false)]
    public void IsNormalMapFileName_MatchesNormalTexturesOnly(string fileName, bool expected)
    {
        MaterialTextureLookupService.IsNormalMapFileName(fileName).Should().Be(expected);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string CreateChild(string name)
    {
        string path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Touch(string folder, string fileName)
    {
        File.WriteAllBytes(Path.Combine(folder, fileName), Array.Empty<byte>());
    }
}
