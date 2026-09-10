using FluentAssertions;
using LECG.Core.Substance;
using Xunit;

namespace LECG.Tests.Substance;

public sealed class SubstanceLibraryScannerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lecg-scan-" + Guid.NewGuid().ToString("N"));

    public SubstanceLibraryScannerTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private void WriteManifest(string category, string slug, string json)
    {
        string dir = Path.Combine(_root, category, slug);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, slug + "_manifest.json"), json);
    }

    private static string Good(string slug) => $$"""
    {
      "material": "{{slug}}", "resolution": 4096,
      "channels": [
        { "channel": "BaseColor", "file": "{{slug}}_basecolor.png" },
        { "channel": "Metallic", "file": "{{slug}}_metallic.png" },
        { "channel": "Normal", "file": "{{slug}}_normal.png" },
        { "channel": "Roughness", "file": "{{slug}}_roughness.png" }
      ],
      "extended": [], "missing_canonical": ["AmbientOcclusion"]
    }
    """;

    [Fact]
    public void Scan_FindsManifestsTwoLevelsDeep_SortedByCategoryThenSlug()
    {
        WriteManifest("Wood", "oak", Good("oak"));
        WriteManifest("Asphalt", "rough", Good("rough"));
        WriteManifest("Asphalt", "fine", Good("fine"));

        var result = SubstanceLibraryScanner.Scan(_root);

        result.Warnings.Should().BeEmpty();
        result.Entries.Select(e => $"{e.Category}/{e.Slug}")
            .Should().Equal("Asphalt/fine", "Asphalt/rough", "Wood/oak");
    }

    [Fact]
    public void Scan_SkipsUnderscoreFolders()
    {
        WriteManifest("_revit", "x", Good("x"));
        WriteManifest("_cache", "y", Good("y"));
        WriteManifest("Stone", "z", Good("z"));

        var result = SubstanceLibraryScanner.Scan(_root);

        result.Entries.Should().ContainSingle(e => e.Slug == "z");
    }

    [Fact]
    public void Scan_MalformedJson_WarnsAndContinues()
    {
        WriteManifest("Stone", "bad", "{ nope");
        WriteManifest("Stone", "ok", Good("ok"));

        var result = SubstanceLibraryScanner.Scan(_root);

        result.Entries.Should().ContainSingle(e => e.Slug == "ok");
        result.Warnings.Should().ContainSingle(w => w.Contains("bad", StringComparison.Ordinal));
    }

    [Fact]
    public void Scan_MissingRequiredChannel_WarnsAndExcludes()
    {
        WriteManifest("Stone", "half", """
        { "material": "half", "resolution": 4096,
          "channels": [ { "channel": "BaseColor", "file": "half_basecolor.png" } ],
          "extended": [], "missing_canonical": [] }
        """);

        var result = SubstanceLibraryScanner.Scan(_root);

        result.Entries.Should().BeEmpty();
        result.Warnings.Should().ContainSingle(w => w.Contains("Normal", StringComparison.Ordinal));
    }

    [Fact]
    public void Scan_MissingRoot_ReturnsWarningOnly()
    {
        var result = SubstanceLibraryScanner.Scan(Path.Combine(_root, "does-not-exist"));
        result.Entries.Should().BeEmpty();
        result.Warnings.Should().ContainSingle();
    }

    [Fact]
    public void Scan_UnreadableManifest_WarnsAndContinues()
    {
        WriteManifest("Stone", "locked", Good("locked"));
        WriteManifest("Stone", "ok", Good("ok"));

        string lockedManifestPath = Path.Combine(_root, "Stone", "locked", "locked_manifest.json");

        // Hold the file open to make ReadAllText fail with IOException (sharing violation on Windows)
        using (new FileStream(lockedManifestPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = SubstanceLibraryScanner.Scan(_root);

            // The ok entry should still be scanned
            result.Entries.Should().ContainSingle(e => e.Slug == "ok");
            // The locked entry should produce a warning
            result.Warnings.Should().ContainSingle(w => w.Contains("locked", StringComparison.Ordinal)
                && w.Contains("cannot read manifest", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Scan_SkipsUnderscoreSlugFolders()
    {
        WriteManifest("Stone", "_tmp", Good("_tmp"));
        WriteManifest("Stone", "z", Good("z"));

        var result = SubstanceLibraryScanner.Scan(_root);

        // Should only contain the z entry, _tmp should be skipped
        result.Entries.Should().ContainSingle(e => e.Slug == "z");
        result.Warnings.Should().BeEmpty();
    }
}
