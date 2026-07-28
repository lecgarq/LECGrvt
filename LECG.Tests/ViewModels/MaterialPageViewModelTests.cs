using FluentAssertions;
using LECG.Services;
using LECG.ViewModels;

namespace LECG.Tests.ViewModels;

public sealed class MaterialPageViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "LECG.MaterialPageVm.Tests", Guid.NewGuid().ToString("N"));

    public MaterialPageViewModelTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void FolderPath_AutofillsMaterialNameFromDetectedDiffuse()
    {
        string folder = CreateChild("Negro");
        Touch(folder, "LECG_MAT_PLASTICO (NEGRO)_Albedo.png");
        var sut = new MaterialPageViewModel(1, new MaterialTextureLookupService());

        sut.FolderPath = folder;

        sut.MaterialName.Should().Be("LECG_MAT_PLASTICO (NEGRO)");
        sut.DiffusePath.Should().EndWith("LECG_MAT_PLASTICO (NEGRO)_Albedo.png");
    }

    [Fact]
    public void FolderPath_AutofillsMaterialNameFromFolderNameWhenNoDiffuseDetected()
    {
        string folder = CreateChild("Concreto Pulido");
        var sut = new MaterialPageViewModel(1, new MaterialTextureLookupService());

        sut.FolderPath = folder;

        sut.MaterialName.Should().Be("Concreto Pulido");
    }

    [Fact]
    public void FolderPath_DoesNotOverwriteExistingMaterialName()
    {
        string folder = CreateChild("Madera");
        Touch(folder, "Madera_Albedo.png");
        var sut = new MaterialPageViewModel(1, new MaterialTextureLookupService())
        {
            MaterialName = "My Custom Name"
        };

        sut.FolderPath = folder;

        sut.MaterialName.Should().Be("My Custom Name");
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
        // Valid 1x1 PNG — an empty file makes the WPF preview decoder throw mid-load
        // and hold the file handle, which breaks directory cleanup in Dispose.
        File.WriteAllBytes(Path.Combine(folder, fileName), Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="));
    }
}
