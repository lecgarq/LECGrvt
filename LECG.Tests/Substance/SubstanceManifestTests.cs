using FluentAssertions;
using LECG.Core.Substance;
using Xunit;

namespace LECG.Tests.Substance;

public class SubstanceManifestTests
{
    private const string FullManifest = """
    {
      "material": "concrete_slats",
      "resolution": 4096,
      "normal_format": "DirectX",
      "channels": [
        { "channel": "BaseColor", "node": "basecolor", "file": "concrete_slats_basecolor.png", "bit_depth": 8, "colorspace": "sRGB", "extended": false, "rendered": true },
        { "channel": "Metallic", "node": "metallic", "file": "concrete_slats_metallic.png", "bit_depth": 8, "colorspace": "linear", "extended": false, "rendered": true },
        { "channel": "AmbientOcclusion", "node": "ambient_occlusion", "file": "concrete_slats_ambient_occlusion.png", "bit_depth": 8, "colorspace": "linear", "extended": false, "rendered": true },
        { "channel": "Normal", "node": "normal", "file": "concrete_slats_normal.png", "bit_depth": 16, "colorspace": "linear", "extended": false, "rendered": true },
        { "channel": "Roughness", "node": "roughness", "file": "concrete_slats_roughness.png", "bit_depth": 16, "colorspace": "linear", "extended": false, "rendered": true },
        { "channel": "Height", "node": "height", "file": "concrete_slats_height.png", "bit_depth": 16, "colorspace": "linear", "extended": false, "rendered": true }
      ],
      "extended": [
        { "channel": "Opacity", "node": "opacity", "file": "concrete_slats_opacity.png", "bit_depth": 16, "colorspace": "linear", "extended": true, "rendered": true },
        { "channel": "IOR", "node": "ior", "value": 1.4, "numeric": true, "extended": true, "rendered": true }
      ],
      "missing_canonical": []
    }
    """;

    private const string MissingNormalManifest = """
    {
      "material": "broken",
      "resolution": 4096,
      "channels": [
        { "channel": "BaseColor", "file": "broken_basecolor.png" },
        { "channel": "Metallic", "file": "broken_metallic.png" },
        { "channel": "Roughness", "file": "broken_roughness.png" }
      ],
      "extended": [],
      "missing_canonical": ["AmbientOcclusion"]
    }
    """;

    [Fact]
    public void Parse_ReadsMaterialAndResolution()
    {
        var m = SubstanceManifest.Parse(FullManifest);
        m.Material.Should().Be("concrete_slats");
        m.Resolution.Should().Be(4096);
        m.NormalFormat.Should().Be("DirectX");
        m.Channels.Should().HaveCount(6);
        m.Extended.Should().HaveCount(2);
    }

    [Fact]
    public void ToEntry_MapsChannelsByNameNotFilename()
    {
        var entry = SubstanceManifest.Parse(FullManifest).ToEntry("Concrete", @"C:\lib\Concrete\concrete_slats");
        entry.IsSuccess.Should().BeTrue();
        var e = entry.Value!;
        e.Category.Should().Be("Concrete");
        e.Slug.Should().Be("concrete_slats");
        e.DisplayName.Should().Be("Concrete Slats");
        e.BaseColorPath.Should().Be(@"C:\lib\Concrete\concrete_slats\concrete_slats_basecolor.png");
        e.NormalPath.Should().EndWith("concrete_slats_normal.png");
        e.RoughnessPath.Should().EndWith("concrete_slats_roughness.png");
        e.MetallicPath.Should().EndWith("concrete_slats_metallic.png");
        e.AoPath.Should().EndWith("concrete_slats_ambient_occlusion.png");
        e.OpacityPath.Should().EndWith("concrete_slats_opacity.png");
        e.Ior.Should().Be(1.4);
        e.Resolution.Should().Be(4096);
        e.NormalFormat.Should().Be("DirectX");
    }

    [Fact]
    public void ToEntry_MissingRequiredChannel_Fails()
    {
        var entry = SubstanceManifest.Parse(MissingNormalManifest).ToEntry("Concrete", @"C:\lib\Concrete\broken");
        entry.IsFailure.Should().BeTrue();
        entry.Error.Should().Contain("Normal");
        entry.Error.Should().Contain("broken");
    }

    [Fact]
    public void ToEntry_NoAoNoOpacity_LeavesNulls()
    {
        const string json = """
        {
          "material": "x", "resolution": 2048,
          "channels": [
            { "channel": "BaseColor", "file": "x_basecolor.png" },
            { "channel": "Metallic", "file": "x_metallic.png" },
            { "channel": "Normal", "file": "x_normal.png" },
            { "channel": "Roughness", "file": "x_roughness.png" }
          ],
          "extended": [], "missing_canonical": ["AmbientOcclusion"]
        }
        """;
        var e = SubstanceManifest.Parse(json).ToEntry("Cat", @"C:\lib\Cat\x").Value!;
        e.AoPath.Should().BeNull();
        e.OpacityPath.Should().BeNull();
        e.Ior.Should().BeNull();
    }

    [Fact]
    public void Parse_InvalidJson_Throws()
    {
        var act = () => SubstanceManifest.Parse("{ not json");
        act.Should().Throw<System.Text.Json.JsonException>();
    }

    [Fact]
    public void ToEntry_UnknownDeclaredNormalFormat_Fails()
    {
        string json = FullManifest.Replace("DirectX", "Unexpected");
        var entry = SubstanceManifest.Parse(json).ToEntry("Concrete", @"C:\lib\Concrete\concrete_slats");
        entry.IsFailure.Should().BeTrue();
        entry.Error.Should().Contain("normal_format");
    }
}
