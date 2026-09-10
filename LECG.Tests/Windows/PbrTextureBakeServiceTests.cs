using System.Windows.Media;
using System.Windows.Media.Imaging;
using FluentAssertions;
using LECG.Core.Substance;
using LECG.Models;
using LECG.Services;
using LECG.Services.Imaging;
using Xunit;

namespace LECG.Tests.Windows;

public sealed class PbrTextureBakeServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lecg-bake-" + Guid.NewGuid().ToString("N"));

    public PbrTextureBakeServiceTests() => Directory.CreateDirectory(_root);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    private string WriteRgb(string name, byte r, byte g, byte b, int size = 4)
    {
        var pixels = new byte[size * size * 4];
        for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = b; pixels[i + 1] = g; pixels[i + 2] = r; pixels[i + 3] = 255; }
        return WritePng(name, BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4));
    }

    private string WriteGray16(string name, ushort value, int size = 4)
    {
        var pixels = new byte[size * size * 2];
        for (int i = 0; i < pixels.Length; i += 2) { pixels[i] = (byte)(value & 0xFF); pixels[i + 1] = (byte)(value >> 8); }
        return WritePng(name, BitmapSource.Create(size, size, 96, 96, PixelFormats.Gray16, null, pixels, size * 2));
    }

    private string WritePng(string name, BitmapSource src)
    {
        string path = Path.Combine(_root, "Cat", "slug", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(src));
        using var fs = File.Create(path);
        enc.Save(fs);
        return path;
    }

    private SubstanceMaterialEntry MakeEntry(byte metallic, bool withAo, string? normalFormat = null) => new(
        "Cat", "slug", "Slug", Path.Combine(_root, "Cat", "slug"),
        WriteRgb("slug_basecolor.png", 200, 100, 50),
        WriteRgb("slug_normal.png", 128, 200, 255),
        WriteGray16("slug_roughness.png", 0x8000),
        WriteRgb("slug_metallic.png", metallic, metallic, metallic),
        withAo ? WriteRgb("slug_ao.png", 128, 128, 128) : null,
        null, null, 4, normalFormat);

    private static byte[] ReadBgra(string path)
    {
        var (px, _, _) = PngIo.LoadBgra32(path, 0);
        return px;
    }

    [Fact]
    public void Bake_NonMetal_WritesBaseNormalRoughness_NoF0()
    {
        var entry = MakeEntry(metallic: 0, withAo: false);
        var svc = new PbrTextureBakeService();

        var set = svc.Bake(entry, new BakeOptions(Path.Combine(_root, "_revit"), 4, false), null);

        File.Exists(set.BaseColor).Should().BeTrue();
        File.Exists(set.NormalGl).Should().BeTrue();
        File.Exists(set.Roughness).Should().BeTrue();
        set.F0.Should().BeNull();
        set.Opacity.Should().BeNull();
        set.WasSkipped.Should().BeFalse();

        var n = ReadBgra(set.NormalGl);
        n[1].Should().Be(55);   // green inverted 200 -> 55
        n[2].Should().Be(128);  // red untouched
        var (rough, _, _) = PngIo.LoadGray8(set.Roughness, 0);
        rough[0].Should().BeInRange(127, 129);
    }

    [Fact]
    public void Bake_WithAo_MultipliesBaseColor()
    {
        var entry = MakeEntry(metallic: 0, withAo: true);
        var set = new PbrTextureBakeService().Bake(entry, new BakeOptions(Path.Combine(_root, "_revit"), 4, false), null);
        var px = ReadBgra(set.BaseColor);
        px[2].Should().BeInRange(99, 101); // 200 * 128/255
    }

    [Fact]
    public void Bake_OpenGlNormal_PreservesGreenAndRecordsConvention()
    {
        var entry = MakeEntry(metallic: 0, withAo: false, normalFormat: "OpenGL");
        var set = new PbrTextureBakeService().Bake(entry, new BakeOptions(Path.Combine(_root, "_revit"), 4, false), null);

        ReadBgra(set.NormalGl)[1].Should().Be(200);
        BakeSidecar.FromJson(File.ReadAllText(Path.Combine(_root, "_revit", "Cat", "slug", "slug_bake.json")))!
            .SourceNormalConvention.Should().Be("OpenGL");
    }

    [Fact]
    public void Bake_Metal_WritesF0AndScalesAlbedo()
    {
        var entry = MakeEntry(metallic: 255, withAo: false);
        var set = new PbrTextureBakeService().Bake(entry, new BakeOptions(Path.Combine(_root, "_revit"), 4, false), null);
        set.F0.Should().NotBeNull();
        ReadBgra(set.F0!)[2].Should().Be(200);
        ReadBgra(set.BaseColor)[2].Should().Be(0);
    }

    [Fact]
    public void Bake_MetalWithAo_F0UsesUnoccludedBase()
    {
        var entry = MakeEntry(metallic: 255, withAo: true);
        var set = new PbrTextureBakeService().Bake(entry, new BakeOptions(Path.Combine(_root, "_revit"), 4, false), null);
        set.F0.Should().NotBeNull();
        ReadBgra(set.F0!)[2].Should().Be(200);
        ReadBgra(set.BaseColor)[2].Should().Be(0);
    }

    [Fact]
    public void Bake_SecondRun_IsSkippedUnlessForced()
    {
        var entry = MakeEntry(metallic: 0, withAo: false);
        var svc = new PbrTextureBakeService();
        var opts = new BakeOptions(Path.Combine(_root, "_revit"), 4, false);

        svc.Bake(entry, opts, null).WasSkipped.Should().BeFalse();
        svc.Bake(entry, opts, null).WasSkipped.Should().BeTrue();
        svc.Bake(entry, opts with { ForceRebake = true }, null).WasSkipped.Should().BeFalse();
        svc.Bake(entry, opts with { TargetSize = 2 }, null).WasSkipped.Should().BeFalse();
    }

    [Fact]
    public void Bake_ResizesToTargetSize()
    {
        var entry = MakeEntry(metallic: 0, withAo: false);
        var set = new PbrTextureBakeService().Bake(entry, new BakeOptions(Path.Combine(_root, "_revit"), 2, false), null);
        var (_, w, h) = PngIo.LoadBgra32(set.BaseColor, 0);
        (w, h).Should().Be((2, 2));
    }

    [Fact]
    public void MetallicProbe_DetectsMetal()
    {
        var metal = WriteRgb("m1.png", 255, 255, 255);
        var dull = WriteRgb("m0.png", 10, 10, 10);
        var probe = new MetallicProbeService();
        probe.IsMetallic(metal).Should().BeTrue();
        probe.IsMetallic(dull).Should().BeFalse();
    }

    [Fact]
    public void MetallicProbe_CorruptFile_ReturnsFalse()
    {
        string corrupt = Path.Combine(_root, "corrupt.png");
        File.WriteAllBytes(corrupt, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 });
        new MetallicProbeService().IsMetallic(corrupt).Should().BeFalse();
    }
}
