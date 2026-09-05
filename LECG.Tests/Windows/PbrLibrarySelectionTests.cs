using System.Text.Json;
using LECG.ViewModels;

namespace LECG.Tests.Windows;

public sealed class PbrLibrarySelectionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lecg-pbr-selection-" + Guid.NewGuid().ToString("N"));

    public PbrLibrarySelectionTests()
    {
        foreach (string category in new[] { "Asphalt", "Metal" })
        {
            string slug = category.ToLowerInvariant();
            string folder = Path.Combine(_root, category, slug);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, slug + "_manifest.json"), JsonSerializer.Serialize(new
            {
                material = slug, resolution = 4096,
                channels = new[] { "BaseColor", "Normal", "Roughness", "Metallic" }
                    .Select(channel => new { channel, file = slug + "_" + channel + ".png" }),
                extended = Array.Empty<object>(), missing_canonical = Array.Empty<string>()
            }));
        }
    }

    [Fact]
    public void SelectingLibraryRootInPbrCreatorEnablesBatchWithoutSingleMaterialFields()
    {
        var vm = new PbrMaterialCreatorViewModel();
        vm.SelectedPage!.FolderPath = _root;
        Assert.True(vm.CanRun);
        Assert.Equal(_root, vm.BatchLibraryRoot);
        Assert.Equal(2, vm.SelectedPage.BatchMaterialCount);
        Assert.Equal("Review Batch", vm.CreateButtonText);

        var batch = new SubstanceBatchViewModel();
        batch.SizeMillimeters = 100;
        batch.LoadLibrary(vm.BatchLibraryRoot!);
        Assert.Equal(2, batch.SelectedCount);
        Assert.Equal(2500, batch.SizeMillimeters);
        Assert.Equal(new[] { "Asphalt", "Metal" }, batch.SelectedEntries().Select(e => e.Category));
        Assert.Equal(_root, batch.LibraryRoot);

        vm.SelectedPage.FolderPath = Path.Combine(_root, "Asphalt", "asphalt");
        Assert.Null(vm.BatchLibraryRoot);
        Assert.False(vm.CanRun);
        Assert.Equal("Create Materials", vm.CreateButtonText);
    }

    [Fact]
    public void SelectingEmptyFolderStillRequiresSingleMaterialTextures()
    {
        var vm = new PbrMaterialCreatorViewModel();
        vm.SelectedPage!.FolderPath = Path.Combine(_root, "Asphalt", "asphalt");
        Assert.False(vm.CanRun);
        Assert.Null(vm.BatchLibraryRoot);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
