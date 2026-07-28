using System.Collections.Generic;
using FluentAssertions;
using LECG.Models;
using LECG.Services.Interfaces;
using LECG.ViewModels;

namespace LECG.Tests.ViewModels;

public sealed class PbrMaterialCreatorViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "LECG.PbrVm.Tests", Guid.NewGuid().ToString("N"));

    public PbrMaterialCreatorViewModelTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void ImportFolder_ReplacesEmptyInitialPageWithImportedPages()
    {
        var lookup = new FakeTextureLookupService(CreateCandidate("Mat A"), CreateCandidate("Mat B"));
        var sut = new PbrMaterialCreatorViewModel(lookup);

        sut.ImportFolder(_root);

        sut.MaterialPages.Should().HaveCount(2);
        sut.MaterialPages[0].PageNumber.Should().Be(1);
        sut.MaterialPages[0].MaterialName.Should().Be("Mat A");
        sut.MaterialPages[1].PageNumber.Should().Be(2);
        sut.MaterialPages[1].MaterialName.Should().Be("Mat B");
        sut.SelectedPage.Should().Be(sut.MaterialPages[0]);
        sut.ImportSummary.Should().Be("Loaded 2 material folders.");
    }

    [Fact]
    public void ImportFolder_AppendsImportedPagesWhenExistingPageIsNotEmpty()
    {
        var lookup = new FakeTextureLookupService(CreateCandidate("Imported"));
        var sut = new PbrMaterialCreatorViewModel(lookup);
        sut.MaterialPages[0].MaterialName = "Existing";

        sut.ImportFolder(_root);

        sut.MaterialPages.Should().HaveCount(2);
        sut.MaterialPages[0].MaterialName.Should().Be("Existing");
        sut.MaterialPages[1].MaterialName.Should().Be("Imported");
        sut.MaterialPages[1].PageNumber.Should().Be(2);
        sut.ImportSummary.Should().Be("Loaded 1 material folder.");
    }

    [Fact]
    public void ImportFolder_ImportedPagesCreateValidRequests()
    {
        var lookup = new FakeTextureLookupService(CreateCandidate("Imported"));
        var sut = new PbrMaterialCreatorViewModel(lookup);

        sut.ImportFolder(_root);
        PbrMaterialCreateRequest request = sut.CreateRequests().Single();

        request.MaterialName.Should().Be("Imported");
        request.AppearanceAssetName.Should().Be("Imported");
        request.DiffusePath.Should().EndWith("Imported_Albedo.png");
        request.RoughnessPath.Should().EndWith("Imported_Roughness.png");
        request.NormalPath.Should().EndWith("Imported_Normal.png");
        request.Description.Should().Be("LECG Arquitectura");
        request.MaterialClass.Should().BeNull();
        request.ScaleXMillimeters.Should().Be(2500.0);
        request.ScaleYMillimeters.Should().Be(2500.0);
        request.BumpMapType.Should().Be(1);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private PbrMaterialFolderCandidate CreateCandidate(string materialName)
    {
        string folder = Path.Combine(_root, materialName);
        Directory.CreateDirectory(folder);
        string diffuse = Touch(folder, $"{materialName}_Albedo.png");
        string roughness = Touch(folder, $"{materialName}_Roughness.png");
        string normal = Touch(folder, $"{materialName}_Normal.png");

        return new PbrMaterialFolderCandidate(
            folder,
            materialName,
            diffuse,
            roughness,
            normal,
            3,
            null);
    }

    private static string Touch(string folder, string fileName)
    {
        string path = Path.Combine(folder, fileName);
        File.WriteAllBytes(path, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="));
        return path;
    }

    private sealed class FakeTextureLookupService : IMaterialTextureLookupService
    {
        private readonly IReadOnlyList<PbrMaterialFolderCandidate> _candidates;

        public FakeTextureLookupService(params PbrMaterialFolderCandidate[] candidates)
        {
            _candidates = candidates;
        }

        public string? FindTextureFile(string folder, string partialName) => null;

        public string DeriveMaterialName(string diffusePath) => Path.GetFileNameWithoutExtension(diffusePath);

        public Dictionary<string, string?> ScanFolder(string folder) => new();

        public IReadOnlyList<PbrMaterialFolderCandidate> ScanImmediateMaterialFolders(string rootFolder) => _candidates;
    }
}
