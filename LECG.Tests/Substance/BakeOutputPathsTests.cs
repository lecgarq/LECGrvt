using FluentAssertions;
using LECG.Core.Substance;
using Xunit;

namespace LECG.Tests.Substance;

public class BakeOutputPathsTests
{
    private static SubstanceMaterialEntry Entry() => new(
        "Asphalt", "asphalt_rough", "Asphalt Rough", @"C:\lib\Asphalt\asphalt_rough",
        @"C:\lib\Asphalt\asphalt_rough\asphalt_rough_basecolor.png",
        @"C:\lib\Asphalt\asphalt_rough\asphalt_rough_normal.png",
        @"C:\lib\Asphalt\asphalt_rough\asphalt_rough_roughness.png",
        @"C:\lib\Asphalt\asphalt_rough\asphalt_rough_metallic.png",
        null, null, null, 4096, "DirectX");

    [Fact]
    public void For_BuildsCategorySlugFolderAndFiles()
    {
        var p = BakeOutputPaths.For(Entry(), @"C:\lib\_revit");
        p.Folder.Should().Be(@"C:\lib\_revit\Asphalt\asphalt_rough");
        p.BaseColor.Should().Be(@"C:\lib\_revit\Asphalt\asphalt_rough\asphalt_rough_basecolor.png");
        p.NormalGl.Should().EndWith(@"\asphalt_rough_normal_gl.png");
        p.Roughness.Should().EndWith(@"\asphalt_rough_roughness.png");
        p.F0.Should().EndWith(@"\asphalt_rough_f0.png");
        p.Opacity.Should().EndWith(@"\asphalt_rough_opacity.png");
        p.Sidecar.Should().EndWith(@"\asphalt_rough_bake.json");
    }

    [Fact]
    public void DefaultOutputRoot_IsUnderscoreRevitUnderLibrary()
    {
        BakeOutputPaths.DefaultOutputRoot(@"C:\lib").Should().Be(@"C:\lib\_revit");
    }
}
