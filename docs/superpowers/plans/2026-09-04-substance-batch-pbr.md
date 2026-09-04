# Substance Batch PBR Materials Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Batch-create Revit Advanced Opaque materials from the Substance bake library at `C:\LECG\SubstanceBakes`, with baked 2K/4K 8-bit textures, a category/material picker window, and the existing single-material creator rerouted to the same Advanced wiring.

**Architecture:** Pure logic (manifest parsing, naming, bake pixel math, freshness, selection policy) lives in `LECG.Core` (net10.0, no WPF, CI-tested). WPF image I/O, Revit API services, view-models, views, and commands live in `LECG` (net10.0-windows). Commands are thin; every Revit API call is inside a Service. One Revit transaction per material.

**Tech Stack:** C# 13 / .NET 10, WPF (`System.Windows.Media.Imaging` for PNG decode/encode), Revit 2026 API (`Autodesk.Revit.DB.Visual`), CommunityToolkit.Mvvm, xUnit + FluentAssertions, `System.Text.Json`.

**Spec:** `docs/superpowers/specs/2026-09-04-substance-batch-pbr-design.md`

## Global Constraints

- Branch: `feature/substance-batch-pbr` (already created off `codex/review`). Work in `C:\LECG\Addin\LECGrvt`. Always `cd` there explicitly.
- .NET 10 SDK is NOT the one on PATH. Use `$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe` (`C:\Users\luis.cortes\AppData\Local\Microsoft\dotnet\dotnet.exe`). Every build/test command below uses `$DOTNET` as shorthand for that path.
- Test command (baseline: 69 passing): `$DOTNET test LECG.Tests/LECG.Tests.csproj -p:RevitVersion=2026 -p:SkipRevitDeploy=true`
- Build add-in without deploying: `$DOTNET build LECG.csproj -p:RevitVersion=2026 -p:SkipRevitDeploy=true -c Debug`
- Build + deploy to Revit 2026: `$DOTNET build LECG.csproj -p:RevitVersion=2026 -c Debug`
- `LECG.Core` builds with `EnforceCodeStyleInBuild` and CI uses `TreatWarningsAsErrors=true`. Validate public method arguments with `ArgumentNullException.ThrowIfNull` (CA1062). Use file-scoped namespaces in Core (matches `LECG.Core.Filtering.SearchTermPolicy`).
- `LECG.Tests` compiles against `LECG.csproj` locally and `LECG.Core.csproj` in CI. Any test that touches WPF or the `LECG` namespace goes under `LECG.Tests/Windows/` and Task 5 adds that folder to the CI `Compile Remove` list.
- Revit expects OpenGL (Y+) normals. Library normals are DirectX (Y-). Green channel is inverted at bake time.
- Advanced Opaque property names (verified): `opaque_albedo`, `opaque_f0`, `surface_roughness`, `surface_normal`, `surface_cutout`. BumpMap connected-asset names: `bumpmap_Bitmap`, `bumpmap_Type` (1 = NormalMap), `bumpmap_NormalScale`. UnifiedBitmap: `unifiedbitmap_Bitmap`. Transform names on both: `texture_RealWorldScaleX`, `texture_RealWorldScaleY`, `texture_RealWorldOffsetX`, `texture_RealWorldOffsetY`, `texture_WAngle`, `texture_LinkTextureTransforms`.
- Material name = slug title-cased (`asphalt_rough` → `Asphalt Rough`). No category prefix.
- Baked textures go to `<OutputRoot>\<Category>\<slug>\<slug>_<map>.png`; default OutputRoot is `<LibraryRoot>\_revit`.
- Real-world texture size: one global value in mm, default 2500. Revit stores feet: `feet = mm / 304.8`.
- F0 threshold: metallic map max > 0.10 (26/255) → write `f0.png` and scale albedo by `(1 - metallic)`.
- Commit after every task with the trailer `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.

---

## File map

| Path | Responsibility |
|---|---|
| `LECG.Core/Substance/SubstanceManifest.cs` | JSON model + parser for `<slug>_manifest.json` |
| `LECG.Core/Substance/SubstanceMaterialEntry.cs` | Record describing one library material |
| `LECG.Core/Substance/SubstanceDisplayName.cs` | Slug → display name |
| `LECG.Core/Substance/SubstanceLibraryScanner.cs` | Enumerate library, build entries, collect warnings |
| `LECG.Core/Substance/PbrBakeMath.cs` | Pure byte-array pixel math |
| `LECG.Core/Substance/BakeOutputPaths.cs` | Output folder/file naming |
| `LECG.Core/Substance/BakeSidecar.cs` | Freshness sidecar model + `IsFresh` |
| `LECG.Core/Substance/SubstanceIdentityPolicy.cs` | Description / keywords strings |
| `LECG.Core/Substance/SubstanceSelectionPolicy.cs` | Tri-state category logic, filter |
| `src/Models/BakedTextureSet.cs` | Result of a bake (paths) |
| `src/Models/TextureTransform.cs` | Scale/offset/angle in mm/deg |
| `src/Models/SubstanceBatchSettings.cs` | Persisted window settings |
| `src/Models/SubstanceBatchResult.cs` | Counts + failures |
| `src/Services/Interfaces/IPbrTextureBakeService.cs` + `src/Services/PbrTextureBakeService.cs` | WPF decode → Core math → PNG encode |
| `src/Services/Interfaces/IMetallicProbeService.cs` + `src/Services/MetallicProbeService.cs` | 64 px max-metallic probe for the UI chip |
| `src/Services/MaterialBitmapPropertyService.cs` (modify) | Schema-aware connected-asset setup |
| `src/Services/Interfaces/IAdvancedAppearanceAssetService.cs` + `src/Services/AdvancedAppearanceAssetService.cs` | Advanced Opaque template + wiring |
| `src/Services/Interfaces/ISubstanceMaterialCreateService.cs` + `src/Services/SubstanceMaterialCreateService.cs` | Per-material orchestration |
| `src/Services/MaterialAppearanceAssetService.cs` (modify) | Single creator reroute |
| `src/ViewModels/SubstanceBatchViewModel.cs`, `SubstanceMaterialRowViewModel.cs`, `SubstanceCategoryViewModel.cs` | Batch window VMs |
| `src/Views/SubstanceBatchView.xaml(.cs)` | Batch window |
| `src/Commands/SubstanceBatchCommand.cs` | Thin command |
| `src/Commands/DumpAppearanceAssetCommand.cs` | Diagnostic |
| `src/ViewModels/LogViewModel.cs`, `src/Views/LogView.xaml` (modify) | Cancel button |
| `src/Configuration/UIConstants.cs`, `src/Core/Ribbon/RibbonService.cs`, `src/Core/Bootstrapper.cs` (modify) | Buttons + DI |

---
### Task 1: Manifest model, entry record, display name (Core)

**Files:**
- Create: `LECG.Core/Substance/SubstanceManifest.cs`
- Create: `LECG.Core/Substance/SubstanceMaterialEntry.cs`
- Create: `LECG.Core/Substance/SubstanceDisplayName.cs`
- Test: `LECG.Tests/Substance/SubstanceDisplayNameTests.cs`
- Test: `LECG.Tests/Substance/SubstanceManifestTests.cs`

**Interfaces:**
- Produces: `SubstanceManifest.Parse(string json)` → `SubstanceManifest`; `SubstanceManifest.ToEntry(string category, string folderPath)` → `Result<SubstanceMaterialEntry>`; `SubstanceDisplayName.FromSlug(string slug)` → `string`; record `SubstanceMaterialEntry`.

- [ ] **Step 1: Write the failing display-name tests**

`LECG.Tests/Substance/SubstanceDisplayNameTests.cs`:
```csharp
using FluentAssertions;
using LECG.Core.Substance;
using Xunit;

namespace LECG.Tests.Substance;

public class SubstanceDisplayNameTests
{
    [Theory]
    [InlineData("asphalt_rough", "Asphalt Rough")]
    [InlineData("clean_asphalt_ground_patch_01", "Clean Asphalt Ground Patch 01")]
    [InlineData("ceramic_tiles_night_rider", "Ceramic Tiles Night Rider")]
    [InlineData("porcelain", "Porcelain")]
    public void FromSlug_TitleCasesTokens(string slug, string expected)
    {
        SubstanceDisplayName.FromSlug(slug).Should().Be(expected);
    }

    [Fact]
    public void FromSlug_CollapsesRepeatedUnderscores()
    {
        SubstanceDisplayName.FromSlug("a__b").Should().Be("A B");
    }

    [Fact]
    public void FromSlug_Empty_ReturnsEmpty()
    {
        SubstanceDisplayName.FromSlug("").Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Write the failing manifest tests**

`LECG.Tests/Substance/SubstanceManifestTests.cs`:
```csharp
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
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `$DOTNET test LECG.Tests/LECG.Tests.csproj -p:RevitVersion=2026 -p:SkipRevitDeploy=true --filter "FullyQualifiedName~LECG.Tests.Substance"`
Expected: build error, `LECG.Core.Substance` namespace does not exist.

- [ ] **Step 4: Implement the three Core files**

`LECG.Core/Substance/SubstanceDisplayName.cs`:
```csharp
using System.Globalization;

namespace LECG.Core.Substance;

public static class SubstanceDisplayName
{
    public static string FromSlug(string slug)
    {
        ArgumentNullException.ThrowIfNull(slug);
        var tokens = slug.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var text = CultureInfo.InvariantCulture.TextInfo;
        return string.Join(' ', tokens.Select(t => text.ToTitleCase(t.ToLowerInvariant())));
    }
}
```

`LECG.Core/Substance/SubstanceMaterialEntry.cs`:
```csharp
namespace LECG.Core.Substance;

public sealed record SubstanceMaterialEntry(
    string Category,
    string Slug,
    string DisplayName,
    string FolderPath,
    string BaseColorPath,
    string NormalPath,
    string RoughnessPath,
    string MetallicPath,
    string? AoPath,
    string? OpacityPath,
    double? Ior,
    int Resolution)
{
    public bool HasAo => AoPath is not null;
    public bool HasOpacity => OpacityPath is not null;
}
```

`LECG.Core/Substance/SubstanceManifest.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LECG.Core.Substance;

public sealed class SubstanceManifest
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    [JsonPropertyName("material")] public string Material { get; set; } = string.Empty;
    [JsonPropertyName("resolution")] public int Resolution { get; set; }
    [JsonPropertyName("channels")] public List<SubstanceChannel> Channels { get; set; } = new();
    [JsonPropertyName("extended")] public List<SubstanceChannel> Extended { get; set; } = new();
    [JsonPropertyName("missing_canonical")] public List<string> MissingCanonical { get; set; } = new();

    public static SubstanceManifest Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize<SubstanceManifest>(json, Options)
               ?? throw new JsonException("Manifest deserialized to null.");
    }

    public Result<SubstanceMaterialEntry> ToEntry(string category, string folderPath)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(folderPath);

        string? Find(string channel) =>
            Channels.Concat(Extended)
                .FirstOrDefault(c => string.Equals(c.Channel, channel, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(c.File))
                ?.File;

        string? Full(string? file) => file is null ? null : Path.Combine(folderPath, file);

        var missing = new List<string>();
        string? baseColor = Find("BaseColor"); if (baseColor is null) missing.Add("BaseColor");
        string? normal = Find("Normal"); if (normal is null) missing.Add("Normal");
        string? roughness = Find("Roughness"); if (roughness is null) missing.Add("Roughness");
        string? metallic = Find("Metallic"); if (metallic is null) missing.Add("Metallic");

        if (missing.Count > 0)
        {
            return Result<SubstanceMaterialEntry>.Failure(
                $"{Material}: manifest lacks required channel(s): {string.Join(", ", missing)}");
        }

        double? ior = Extended
            .FirstOrDefault(c => string.Equals(c.Channel, "IOR", StringComparison.OrdinalIgnoreCase) && c.Value.HasValue)
            ?.Value;

        return Result<SubstanceMaterialEntry>.Success(new SubstanceMaterialEntry(
            category,
            Material,
            SubstanceDisplayName.FromSlug(Material),
            folderPath,
            Full(baseColor)!,
            Full(normal)!,
            Full(roughness)!,
            Full(metallic)!,
            Full(Find("AmbientOcclusion")),
            Full(Find("Opacity")),
            ior,
            Resolution));
    }
}

public sealed class SubstanceChannel
{
    [JsonPropertyName("channel")] public string Channel { get; set; } = string.Empty;
    [JsonPropertyName("node")] public string? Node { get; set; }
    [JsonPropertyName("file")] public string? File { get; set; }
    [JsonPropertyName("width")] public int? Width { get; set; }
    [JsonPropertyName("height")] public int? Height { get; set; }
    [JsonPropertyName("bit_depth")] public int? BitDepth { get; set; }
    [JsonPropertyName("colorspace")] public string? Colorspace { get; set; }
    [JsonPropertyName("value")] public double? Value { get; set; }
    [JsonPropertyName("numeric")] public bool? Numeric { get; set; }
    [JsonPropertyName("extended")] public bool? Extended { get; set; }
    [JsonPropertyName("rendered")] public bool? Rendered { get; set; }
}
```

Note: `Result<T>` already exists in `LECG.Core/Result.cs` (`Success`, `Failure`, `IsSuccess`, `IsFailure`, `Value`, `Error`).

- [ ] **Step 5: Run tests to verify they pass**

Run: `$DOTNET test LECG.Tests/LECG.Tests.csproj -p:RevitVersion=2026 -p:SkipRevitDeploy=true --filter "FullyQualifiedName~LECG.Tests.Substance"`
Expected: 10 passed. Also run `$DOTNET build LECG.Core/LECG.Core.csproj -c Release /p:TreatWarningsAsErrors=true` — expected: 0 warnings, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add LECG.Core/Substance LECG.Tests/Substance
git commit -m "feat(core): substance manifest model, entry record, display name

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: Library scanner (Core)

**Files:**
- Create: `LECG.Core/Substance/SubstanceLibraryScanner.cs`
- Test: `LECG.Tests/Substance/SubstanceLibraryScannerTests.cs`

**Interfaces:**
- Consumes: `SubstanceManifest.Parse`, `SubstanceManifest.ToEntry` (Task 1).
- Produces: `SubstanceLibraryScanner.Scan(string root)` → `SubstanceScanResult(IReadOnlyList<SubstanceMaterialEntry> Entries, IReadOnlyList<string> Warnings)`.

- [ ] **Step 1: Write the failing tests**

`LECG.Tests/Substance/SubstanceLibraryScannerTests.cs`:
```csharp
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
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `$DOTNET test LECG.Tests/LECG.Tests.csproj -p:RevitVersion=2026 -p:SkipRevitDeploy=true --filter "FullyQualifiedName~SubstanceLibraryScannerTests"`
Expected: build error, `SubstanceLibraryScanner` not found.

- [ ] **Step 3: Implement the scanner**

`LECG.Core/Substance/SubstanceLibraryScanner.cs`:
```csharp
using System.Text.Json;

namespace LECG.Core.Substance;

public sealed record SubstanceScanResult(
    IReadOnlyList<SubstanceMaterialEntry> Entries,
    IReadOnlyList<string> Warnings);

public static class SubstanceLibraryScanner
{
    public static SubstanceScanResult Scan(string root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var entries = new List<SubstanceMaterialEntry>();
        var warnings = new List<string>();

        if (!Directory.Exists(root))
        {
            warnings.Add($"Library root not found: {root}");
            return new SubstanceScanResult(entries, warnings);
        }

        foreach (string categoryDir in Directory.EnumerateDirectories(root))
        {
            string category = Path.GetFileName(categoryDir);
            if (category.StartsWith('_')) continue;

            foreach (string materialDir in Directory.EnumerateDirectories(categoryDir))
            {
                string slugFolder = Path.GetFileName(materialDir);
                if (slugFolder.StartsWith('_')) continue;

                string? manifestPath = Directory.EnumerateFiles(materialDir, "*_manifest.json").FirstOrDefault();
                if (manifestPath is null)
                {
                    warnings.Add($"{category}/{slugFolder}: no *_manifest.json");
                    continue;
                }

                try
                {
                    var manifest = SubstanceManifest.Parse(File.ReadAllText(manifestPath));
                    var entry = manifest.ToEntry(category, materialDir);
                    if (entry.IsSuccess)
                    {
                        entries.Add(entry.Value!);
                    }
                    else
                    {
                        warnings.Add($"{category}/{slugFolder}: {entry.Error}");
                    }
                }
                catch (JsonException ex)
                {
                    warnings.Add($"{category}/{slugFolder}: invalid manifest JSON ({ex.Message})");
                }
                catch (IOException ex)
                {
                    warnings.Add($"{category}/{slugFolder}: cannot read manifest ({ex.Message})");
                }
            }
        }

        entries.Sort((a, b) =>
        {
            int c = string.Compare(a.Category, b.Category, StringComparison.OrdinalIgnoreCase);
            return c != 0 ? c : string.Compare(a.Slug, b.Slug, StringComparison.OrdinalIgnoreCase);
        });

        return new SubstanceScanResult(entries, warnings);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `$DOTNET test LECG.Tests/LECG.Tests.csproj -p:RevitVersion=2026 -p:SkipRevitDeploy=true --filter "FullyQualifiedName~LECG.Tests.Substance"`
Expected: 15 passed.

- [ ] **Step 5: Smoke against the real library**

Run (PowerShell, from repo root):
```powershell
& $DOTNET run --project LECG.Core -- 2>$null; # no entry point; skip
```
Instead, add a temporary test (do not commit) that calls `SubstanceLibraryScanner.Scan(@"C:\LECG\SubstanceBakes")` and asserts `Entries.Count == 534` and `Warnings.Count == 0`. Run it once, then delete it.

- [ ] **Step 6: Commit**

```bash
git add LECG.Core/Substance/SubstanceLibraryScanner.cs LECG.Tests/Substance/SubstanceLibraryScannerTests.cs
git commit -m "feat(core): substance library scanner with warnings

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---
### Task 3: Bake pixel math (Core)

**Files:**
- Create: `LECG.Core/Substance/PbrBakeMath.cs`
- Test: `LECG.Tests/Substance/PbrBakeMathTests.cs`

**Interfaces:**
- Produces (all static, operate on interleaved 8-bit BGRA buffers for color and 8-bit gray buffers for masks; `pixelCount = width * height`):
  - `void InvertGreen(Span<byte> bgra)`
  - `void MultiplyByGray(Span<byte> bgra, ReadOnlySpan<byte> gray)` — RGB *= gray/255, alpha untouched
  - `byte MaxGray(ReadOnlySpan<byte> gray)`
  - `bool IsMetallic(byte maxGray)` — `maxGray > 26`
  - `byte[] ComputeF0(ReadOnlySpan<byte> baseColorBgra, ReadOnlySpan<byte> metallicGray)` — returns new BGRA buffer
  - `void ScaleAlbedoByInverseMetallic(Span<byte> bgra, ReadOnlySpan<byte> metallicGray)`
  - `const double DielectricF0 = 0.04`, `const byte MetallicThreshold = 26`

- [ ] **Step 1: Write the failing tests**

`LECG.Tests/Substance/PbrBakeMathTests.cs`:
```csharp
using FluentAssertions;
using LECG.Core.Substance;
using Xunit;

namespace LECG.Tests.Substance;

public class PbrBakeMathTests
{
    // one pixel, BGRA
    private static byte[] Px(byte r, byte g, byte b, byte a = 255) => new[] { b, g, r, a };

    [Fact]
    public void InvertGreen_FlipsOnlyGreen()
    {
        var px = Px(10, 200, 30, 77);
        PbrBakeMath.InvertGreen(px);
        px.Should().Equal(30, 55, 10, 77);
    }

    [Fact]
    public void MultiplyByGray_ScalesRgbKeepsAlpha()
    {
        var px = Px(200, 100, 50, 255);
        PbrBakeMath.MultiplyByGray(px, new byte[] { 128 });
        px.Should().Equal(25, 50, 100, 255);
    }

    [Fact]
    public void MultiplyByGray_LengthMismatch_Throws()
    {
        var act = () => PbrBakeMath.MultiplyByGray(new byte[8], new byte[1]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MaxGray_ReturnsMax()
    {
        PbrBakeMath.MaxGray(new byte[] { 3, 250, 7 }).Should().Be(250);
        PbrBakeMath.MaxGray(Array.Empty<byte>()).Should().Be(0);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(26, false)]
    [InlineData(27, true)]
    [InlineData(255, true)]
    public void IsMetallic_UsesTenPercentThreshold(byte max, bool expected)
    {
        PbrBakeMath.IsMetallic(max).Should().Be(expected);
    }

    [Fact]
    public void ComputeF0_Metallic0_IsDielectric()
    {
        var f0 = PbrBakeMath.ComputeF0(Px(200, 100, 50), new byte[] { 0 });
        // 0.04 linear -> sRGB ≈ 0.216 -> 55
        f0.Should().Equal(55, 55, 55, 255);
    }

    [Fact]
    public void ComputeF0_Metallic255_IsBaseColor()
    {
        var f0 = PbrBakeMath.ComputeF0(Px(200, 100, 50), new byte[] { 255 });
        f0.Should().Equal(50, 100, 200, 255);
    }

    [Fact]
    public void ComputeF0_HalfMetallic_LerpsInLinearSpace()
    {
        var f0 = PbrBakeMath.ComputeF0(Px(255, 255, 255), new byte[] { 128 });
        // lerp(0.04, 1.0, 128/255) = 0.5219 linear -> sRGB 0.7457 -> 190
        f0[0].Should().BeInRange(188, 192);
        f0[1].Should().Be(f0[0]);
        f0[2].Should().Be(f0[0]);
    }

    [Fact]
    public void ScaleAlbedoByInverseMetallic_ZeroesFullMetal()
    {
        var px = Px(200, 100, 50, 255);
        PbrBakeMath.ScaleAlbedoByInverseMetallic(px, new byte[] { 255 });
        px.Should().Equal(0, 0, 0, 255);
    }

    [Fact]
    public void ScaleAlbedoByInverseMetallic_NoMetalUnchanged()
    {
        var px = Px(200, 100, 50, 255);
        PbrBakeMath.ScaleAlbedoByInverseMetallic(px, new byte[] { 0 });
        px.Should().Equal(50, 100, 200, 255);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `$DOTNET test LECG.Tests/LECG.Tests.csproj -p:RevitVersion=2026 -p:SkipRevitDeploy=true --filter "FullyQualifiedName~PbrBakeMathTests"`
Expected: build error, `PbrBakeMath` not found.

- [ ] **Step 3: Implement**

`LECG.Core/Substance/PbrBakeMath.cs`:
```csharp
namespace LECG.Core.Substance;

/// <summary>
/// Pure pixel math for the PBR bake. Color buffers are interleaved 8-bit BGRA
/// (WPF Bgra32 layout). Mask buffers are 8-bit gray, one byte per pixel.
/// </summary>
public static class PbrBakeMath
{
    public const double DielectricF0 = 0.04;
    public const byte MetallicThreshold = 26; // > 10 % of 255

    private static readonly byte[] SrgbToLinearLut = BuildSrgbToLinear();
    private static readonly byte[] LinearToSrgbLut = BuildLinearToSrgb();

    public static void InvertGreen(Span<byte> bgra)
    {
        for (int i = 1; i < bgra.Length; i += 4)
        {
            bgra[i] = (byte)(255 - bgra[i]);
        }
    }

    public static void MultiplyByGray(Span<byte> bgra, ReadOnlySpan<byte> gray)
    {
        EnsureSameLength(bgra, gray);
        for (int p = 0; p < gray.Length; p++)
        {
            int o = p * 4;
            int g = gray[p];
            bgra[o] = Mul(bgra[o], g);
            bgra[o + 1] = Mul(bgra[o + 1], g);
            bgra[o + 2] = Mul(bgra[o + 2], g);
        }
    }

    public static byte MaxGray(ReadOnlySpan<byte> gray)
    {
        byte max = 0;
        foreach (byte b in gray) if (b > max) max = b;
        return max;
    }

    public static bool IsMetallic(byte maxGray) => maxGray > MetallicThreshold;

    public static byte[] ComputeF0(ReadOnlySpan<byte> baseColorBgra, ReadOnlySpan<byte> metallicGray)
    {
        EnsureSameLength(baseColorBgra, metallicGray);
        var f0 = new byte[baseColorBgra.Length];
        for (int p = 0; p < metallicGray.Length; p++)
        {
            int o = p * 4;
            double m = metallicGray[p] / 255.0;
            for (int c = 0; c < 3; c++)
            {
                double baseLinear = SrgbToLinearLut[baseColorBgra[o + c]] / 255.0;
                double f0Linear = DielectricF0 + (baseLinear - DielectricF0) * m;
                f0[o + c] = LinearToSrgbLut[(int)Math.Round(Math.Clamp(f0Linear, 0, 1) * 255)];
            }
            f0[o + 3] = 255;
        }
        return f0;
    }

    public static void ScaleAlbedoByInverseMetallic(Span<byte> bgra, ReadOnlySpan<byte> metallicGray)
    {
        EnsureSameLength(bgra, metallicGray);
        for (int p = 0; p < metallicGray.Length; p++)
        {
            int o = p * 4;
            int inv = 255 - metallicGray[p];
            bgra[o] = Mul(bgra[o], inv);
            bgra[o + 1] = Mul(bgra[o + 1], inv);
            bgra[o + 2] = Mul(bgra[o + 2], inv);
        }
    }

    private static byte Mul(byte value, int factor255) => (byte)((value * factor255 + 127) / 255);

    private static void EnsureSameLength(ReadOnlySpan<byte> bgra, ReadOnlySpan<byte> gray)
    {
        if (bgra.Length != gray.Length * 4)
        {
            throw new ArgumentException($"BGRA buffer ({bgra.Length} bytes) must be exactly 4x the gray buffer ({gray.Length} bytes).");
        }
    }

    private static byte[] BuildSrgbToLinear()
    {
        var lut = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            double c = i / 255.0;
            double lin = c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            lut[i] = (byte)Math.Round(lin * 255);
        }
        return lut;
    }

    private static byte[] BuildLinearToSrgb()
    {
        var lut = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            double lin = i / 255.0;
            double c = lin <= 0.0031308 ? lin * 12.92 : 1.055 * Math.Pow(lin, 1 / 2.4) - 0.055;
            lut[i] = (byte)Math.Round(Math.Clamp(c, 0, 1) * 255);
        }
        return lut;
    }
}
```

Note on `ComputeF0_Metallic0_IsDielectric`: 0.04 linear → LUT index 10 → sRGB ≈ 0.216 → 55. If the rounding lands on 54 or 56, adjust the test expectation to the produced value and keep the ±1 tolerance in the half-metallic test.

- [ ] **Step 4: Run tests to verify they pass**

Run: `$DOTNET test LECG.Tests/LECG.Tests.csproj -p:RevitVersion=2026 -p:SkipRevitDeploy=true --filter "FullyQualifiedName~LECG.Tests.Substance"`
Expected: all Substance tests pass (25).

- [ ] **Step 5: Commit**

```bash
git add LECG.Core/Substance/PbrBakeMath.cs LECG.Tests/Substance/PbrBakeMathTests.cs
git commit -m "feat(core): pbr bake pixel math (green flip, AO, F0)

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: Output paths and freshness sidecar (Core)

**Files:**
- Create: `LECG.Core/Substance/BakeOutputPaths.cs`
- Create: `LECG.Core/Substance/BakeSidecar.cs`
- Test: `LECG.Tests/Substance/BakeOutputPathsTests.cs`
- Test: `LECG.Tests/Substance/BakeSidecarTests.cs`

**Interfaces:**
- Consumes: `SubstanceMaterialEntry` (Task 1).
- Produces:
  - `BakeOutputPaths.For(SubstanceMaterialEntry entry, string outputRoot)` → `BakeOutputPaths` with `Folder`, `BaseColor`, `NormalGl`, `Roughness`, `F0`, `Opacity`, `Sidecar`.
  - `BakeSidecar` (JSON-serializable): `int TargetSize`, `Dictionary<string,long> SourceTicks`, `bool F0Written`, `bool OpacityWritten`.
  - `BakeSidecar.Build(int targetSize, IEnumerable<(string path, long ticks)> sources, bool f0Written, bool opacityWritten)`
  - `bool BakeSidecar.IsFresh(BakeSidecar? existing, int targetSize, IEnumerable<(string path, long ticks)> sources)`
  - `string BakeSidecar.ToJson()`, `BakeSidecar? BakeSidecar.FromJson(string)`

- [ ] **Step 1: Write the failing tests**

`LECG.Tests/Substance/BakeOutputPathsTests.cs`:
```csharp
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
        null, null, null, 4096);

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
```

`LECG.Tests/Substance/BakeSidecarTests.cs`:
```csharp
using FluentAssertions;
using LECG.Core.Substance;
using Xunit;

namespace LECG.Tests.Substance;

public class BakeSidecarTests
{
    private static readonly (string, long)[] Sources = { (@"C:\a.png", 100), (@"C:\b.png", 200) };

    [Fact]
    public void IsFresh_NullSidecar_False()
    {
        BakeSidecar.IsFresh(null, 2048, Sources).Should().BeFalse();
    }

    [Fact]
    public void IsFresh_SameSizeSameTicks_True()
    {
        var s = BakeSidecar.Build(2048, Sources, f0Written: false, opacityWritten: false);
        BakeSidecar.IsFresh(s, 2048, Sources).Should().BeTrue();
    }

    [Fact]
    public void IsFresh_DifferentSize_False()
    {
        var s = BakeSidecar.Build(2048, Sources, false, false);
        BakeSidecar.IsFresh(s, 4096, Sources).Should().BeFalse();
    }

    [Fact]
    public void IsFresh_SourceNewer_False()
    {
        var s = BakeSidecar.Build(2048, Sources, false, false);
        BakeSidecar.IsFresh(s, 2048, new[] { (@"C:\a.png", 101L), (@"C:\b.png", 200L) }).Should().BeFalse();
    }

    [Fact]
    public void IsFresh_ExtraSource_False()
    {
        var s = BakeSidecar.Build(2048, Sources, false, false);
        BakeSidecar.IsFresh(s, 2048, Sources.Append((@"C:\c.png", 5L))).Should().BeFalse();
    }

    [Fact]
    public void Json_RoundTrips()
    {
        var s = BakeSidecar.Build(4096, Sources, f0Written: true, opacityWritten: false);
        var back = BakeSidecar.FromJson(s.ToJson());
        back.Should().NotBeNull();
        back!.TargetSize.Should().Be(4096);
        back.F0Written.Should().BeTrue();
        back.OpacityWritten.Should().BeFalse();
        back.SourceTicks.Should().Equal(s.SourceTicks);
    }

    [Fact]
    public void FromJson_Garbage_ReturnsNull()
    {
        BakeSidecar.FromJson("nope").Should().BeNull();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `$DOTNET test LECG.Tests/LECG.Tests.csproj -p:RevitVersion=2026 -p:SkipRevitDeploy=true --filter "FullyQualifiedName~Bake"`
Expected: build error.

- [ ] **Step 3: Implement**

`LECG.Core/Substance/BakeOutputPaths.cs`:
```csharp
namespace LECG.Core.Substance;

public sealed record BakeOutputPaths(
    string Folder,
    string BaseColor,
    string NormalGl,
    string Roughness,
    string F0,
    string Opacity,
    string Sidecar)
{
    public static string DefaultOutputRoot(string libraryRoot)
    {
        ArgumentNullException.ThrowIfNull(libraryRoot);
        return Path.Combine(libraryRoot, "_revit");
    }

    public static BakeOutputPaths For(SubstanceMaterialEntry entry, string outputRoot)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(outputRoot);
        string folder = Path.Combine(outputRoot, entry.Category, entry.Slug);
        string P(string suffix) => Path.Combine(folder, $"{entry.Slug}_{suffix}");
        return new BakeOutputPaths(
            folder,
            P("basecolor.png"),
            P("normal_gl.png"),
            P("roughness.png"),
            P("f0.png"),
            P("opacity.png"),
            P("bake.json"));
    }
}
```

`LECG.Core/Substance/BakeSidecar.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LECG.Core.Substance;

public sealed class BakeSidecar
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    [JsonPropertyName("targetSize")] public int TargetSize { get; set; }
    [JsonPropertyName("sourceTicks")] public Dictionary<string, long> SourceTicks { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    [JsonPropertyName("f0Written")] public bool F0Written { get; set; }
    [JsonPropertyName("opacityWritten")] public bool OpacityWritten { get; set; }

    public static BakeSidecar Build(int targetSize, IEnumerable<(string path, long ticks)> sources, bool f0Written, bool opacityWritten)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var s = new BakeSidecar { TargetSize = targetSize, F0Written = f0Written, OpacityWritten = opacityWritten };
        foreach (var (path, ticks) in sources) s.SourceTicks[path] = ticks;
        return s;
    }

    public static bool IsFresh(BakeSidecar? existing, int targetSize, IEnumerable<(string path, long ticks)> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (existing is null || existing.TargetSize != targetSize) return false;
        var list = sources.ToList();
        if (list.Count != existing.SourceTicks.Count) return false;
        foreach (var (path, ticks) in list)
        {
            if (!existing.SourceTicks.TryGetValue(path, out long known) || known < ticks) return false;
        }
        return true;
    }

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public static BakeSidecar? FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            return JsonSerializer.Deserialize<BakeSidecar>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `$DOTNET test LECG.Tests/LECG.Tests.csproj -p:RevitVersion=2026 -p:SkipRevitDeploy=true --filter "FullyQualifiedName~LECG.Tests.Substance"`
Expected: 34 passed. Then `$DOTNET build LECG.Core/LECG.Core.csproj -c Release /p:TreatWarningsAsErrors=true` — 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add LECG.Core/Substance/BakeOutputPaths.cs LECG.Core/Substance/BakeSidecar.cs LECG.Tests/Substance/BakeOutputPathsTests.cs LECG.Tests/Substance/BakeSidecarTests.cs
git commit -m "feat(core): bake output paths and freshness sidecar

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---
### Task 5: WPF texture bake service and metallic probe (LECG)

**Files:**
- Create: `src/Models/BakedTextureSet.cs`
- Create: `src/Models/BakeOptions.cs`
- Create: `src/Services/Interfaces/IPbrTextureBakeService.cs`
- Create: `src/Services/PbrTextureBakeService.cs`
- Create: `src/Services/Interfaces/IMetallicProbeService.cs`
- Create: `src/Services/MetallicProbeService.cs`
- Create: `src/Services/Imaging/PngIo.cs`
- Test: `LECG.Tests/Windows/PbrTextureBakeServiceTests.cs`
- Modify: `LECG.Tests/LECG.Tests.csproj` (CI compile exclusion for `Windows\**`)
- Modify: `src/Core/Bootstrapper.cs` (register the two services next to `IImageColorExtractionService`, line ~98)

**Interfaces:**
- Consumes: `PbrBakeMath`, `BakeOutputPaths`, `BakeSidecar`, `SubstanceMaterialEntry` (Tasks 1-4).
- Produces:
  - `record BakedTextureSet(string BaseColor, string NormalGl, string Roughness, string? F0, string? Opacity, bool WasSkipped)`
  - `record BakeOptions(string OutputRoot, int TargetSize, bool ForceRebake)`
  - `IPbrTextureBakeService.Bake(SubstanceMaterialEntry entry, BakeOptions options, Action<string>? log)` → `BakedTextureSet`
  - `IMetallicProbeService.IsMetallic(string metallicPath)` → `bool` (64 px decode, cached per path)
  - `PngIo.LoadBgra32(string path, int targetSize)` → `(byte[] pixels, int width, int height)`; `PngIo.LoadGray8(string path, int targetSize)` → `(byte[] pixels, int width, int height)`; `PngIo.SaveBgra32(...)`, `PngIo.SaveGray8(...)`

- [ ] **Step 1: Exclude Windows tests from CI**

In `LECG.Tests/LECG.Tests.csproj`, inside the existing `<ItemGroup Condition="'$(IsCiBuild)' == 'true'">`, add:
```xml
    <Compile Remove="Windows\**\*.cs" />
```

- [ ] **Step 2: Write the failing test**

`LECG.Tests/Windows/PbrTextureBakeServiceTests.cs`:
```csharp
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FluentAssertions;
using LECG.Core.Substance;
using LECG.Models;
using LECG.Services;
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

    private SubstanceMaterialEntry MakeEntry(byte metallic, bool withAo) => new(
        "Cat", "slug", "Slug", Path.Combine(_root, "Cat", "slug"),
        WriteRgb("slug_basecolor.png", 200, 100, 50),
        WriteRgb("slug_normal.png", 128, 200, 255),
        WriteGray16("slug_roughness.png", 0x8000),
        WriteRgb("slug_metallic.png", metallic, metallic, metallic),
        withAo ? WriteRgb("slug_ao.png", 128, 128, 128) : null,
        null, null, 4);

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
    public void Bake_Metal_WritesF0AndScalesAlbedo()
    {
        var entry = MakeEntry(metallic: 255, withAo: false);
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
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `$DOTNET test LECG.Tests/LECG.Tests.csproj -p:RevitVersion=2026 -p:SkipRevitDeploy=true --filter "FullyQualifiedName~PbrTextureBakeServiceTests"`
Expected: build error (missing types).

- [ ] **Step 4: Implement models**

`src/Models/BakedTextureSet.cs`:
```csharp
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
```

`src/Models/BakeOptions.cs`:
```csharp
namespace LECG.Models
{
    public sealed record BakeOptions(string OutputRoot, int TargetSize, bool ForceRebake);
}
```

- [ ] **Step 5: Implement PngIo**

`src/Services/Imaging/PngIo.cs`:
```csharp
using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LECG.Services.Imaging
{
    /// <summary>PNG decode/encode helpers. targetSize 0 keeps the source size.</summary>
    public static class PngIo
    {
        public static (byte[] pixels, int width, int height) LoadBgra32(string path, int targetSize)
            => Load(path, targetSize, PixelFormats.Bgra32, 4);

        public static (byte[] pixels, int width, int height) LoadGray8(string path, int targetSize)
            => Load(path, targetSize, PixelFormats.Gray8, 1);

        public static void SaveBgra32(string path, byte[] pixels, int width, int height)
            => Save(path, BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4));

        public static void SaveBgr24FromBgra(string path, byte[] bgra, int width, int height)
        {
            var bgr = new byte[width * height * 3];
            for (int p = 0, s = 0, d = 0; p < width * height; p++, s += 4, d += 3)
            {
                bgr[d] = bgra[s]; bgr[d + 1] = bgra[s + 1]; bgr[d + 2] = bgra[s + 2];
            }
            Save(path, BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr24, null, bgr, width * 3));
        }

        public static void SaveGray8(string path, byte[] pixels, int width, int height)
            => Save(path, BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray8, null, pixels, width));

        private static (byte[] pixels, int width, int height) Load(string path, int targetSize, PixelFormat format, int bytesPerPixel)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            using FileStream stream = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            BitmapSource source = decoder.Frames[0];

            if (targetSize > 0 && (source.PixelWidth != targetSize || source.PixelHeight != targetSize))
            {
                double sx = (double)targetSize / source.PixelWidth;
                double sy = (double)targetSize / source.PixelHeight;
                source = new TransformedBitmap(source, new ScaleTransform(sx, sy));
            }

            if (source.Format != format)
            {
                source = new FormatConvertedBitmap(source, format, null, 0);
            }

            int stride = source.PixelWidth * bytesPerPixel;
            var pixels = new byte[stride * source.PixelHeight];
            source.CopyPixels(pixels, stride, 0);
            return (pixels, source.PixelWidth, source.PixelHeight);
        }

        private static void Save(string path, BitmapSource source)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using FileStream fs = File.Create(path);
            encoder.Save(fs);
        }
    }
}
```

- [ ] **Step 6: Implement the bake service**

`src/Services/Interfaces/IPbrTextureBakeService.cs`:
```csharp
using System;
using LECG.Core.Substance;
using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface IPbrTextureBakeService
    {
        BakedTextureSet Bake(SubstanceMaterialEntry entry, BakeOptions options, Action<string>? log = null);
    }
}
```

`src/Services/PbrTextureBakeService.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using LECG.Core.Substance;
using LECG.Models;
using LECG.Services.Imaging;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PbrTextureBakeService : IPbrTextureBakeService
    {
        public BakedTextureSet Bake(SubstanceMaterialEntry entry, BakeOptions options, Action<string>? log = null)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentNullException.ThrowIfNull(options);

            BakeOutputPaths paths = BakeOutputPaths.For(entry, options.OutputRoot);
            List<(string path, long ticks)> sources = CollectSources(entry);

            BakeSidecar? existing = File.Exists(paths.Sidecar) ? BakeSidecar.FromJson(File.ReadAllText(paths.Sidecar)) : null;
            if (!options.ForceRebake && BakeSidecar.IsFresh(existing, options.TargetSize, sources) && OutputsExist(paths, existing!))
            {
                log?.Invoke($"    -> Bake up to date: {paths.Folder}");
                return new BakedTextureSet(
                    paths.BaseColor, paths.NormalGl, paths.Roughness,
                    existing!.F0Written ? paths.F0 : null,
                    existing.OpacityWritten ? paths.Opacity : null,
                    WasSkipped: true);
            }

            Directory.CreateDirectory(paths.Folder);
            int size = options.TargetSize;

            // Base color (+AO, +metal albedo scaling)
            var (baseColor, w, h) = PngIo.LoadBgra32(entry.BaseColorPath, size);
            if (entry.AoPath is not null)
            {
                var (ao, _, _) = PngIo.LoadGray8(entry.AoPath, size);
                PbrBakeMath.MultiplyByGray(baseColor, ao);
            }

            var (metallic, _, _) = PngIo.LoadGray8(entry.MetallicPath, size);
            bool isMetal = PbrBakeMath.IsMetallic(PbrBakeMath.MaxGray(metallic));
            string? f0Path = null;
            if (isMetal)
            {
                byte[] f0 = PbrBakeMath.ComputeF0(baseColor, metallic);
                PngIo.SaveBgr24FromBgra(paths.F0, f0, w, h);
                PbrBakeMath.ScaleAlbedoByInverseMetallic(baseColor, metallic);
                f0Path = paths.F0;
            }
            PngIo.SaveBgr24FromBgra(paths.BaseColor, baseColor, w, h);

            // Normal: DirectX -> OpenGL
            var (normal, nw, nh) = PngIo.LoadBgra32(entry.NormalPath, size);
            PbrBakeMath.InvertGreen(normal);
            PngIo.SaveBgr24FromBgra(paths.NormalGl, normal, nw, nh);

            // Roughness: 16 -> 8 bit
            var (rough, rw, rh) = PngIo.LoadGray8(entry.RoughnessPath, size);
            PngIo.SaveGray8(paths.Roughness, rough, rw, rh);

            // Opacity
            string? opacityPath = null;
            if (entry.OpacityPath is not null)
            {
                var (op, ow, oh) = PngIo.LoadGray8(entry.OpacityPath, size);
                PngIo.SaveGray8(paths.Opacity, op, ow, oh);
                opacityPath = paths.Opacity;
            }

            File.WriteAllText(paths.Sidecar, BakeSidecar.Build(size, sources, f0Path is not null, opacityPath is not null).ToJson());
            log?.Invoke($"    -> Baked {size}px to {paths.Folder}{(isMetal ? " (metal: F0 written)" : string.Empty)}");

            return new BakedTextureSet(paths.BaseColor, paths.NormalGl, paths.Roughness, f0Path, opacityPath, WasSkipped: false);
        }

        private static List<(string path, long ticks)> CollectSources(SubstanceMaterialEntry entry)
        {
            var list = new List<(string, long)>();
            void Add(string? p) { if (p is not null) list.Add((p, File.GetLastWriteTimeUtc(p).Ticks)); }
            Add(entry.BaseColorPath);
            Add(entry.NormalPath);
            Add(entry.RoughnessPath);
            Add(entry.MetallicPath);
            Add(entry.AoPath);
            Add(entry.OpacityPath);
            return list;
        }

        private static bool OutputsExist(BakeOutputPaths p, BakeSidecar s)
        {
            return File.Exists(p.BaseColor) && File.Exists(p.NormalGl) && File.Exists(p.Roughness)
                && (!s.F0Written || File.Exists(p.F0))
                && (!s.OpacityWritten || File.Exists(p.Opacity));
        }
    }
}
```

- [ ] **Step 7: Implement the metallic probe**

`src/Services/Interfaces/IMetallicProbeService.cs`:
```csharp
namespace LECG.Services.Interfaces
{
    public interface IMetallicProbeService
    {
        bool IsMetallic(string metallicPath);
    }
}
```

`src/Services/MetallicProbeService.cs`:
```csharp
using System;
using System.Collections.Concurrent;
using LECG.Core.Substance;
using LECG.Services.Imaging;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class MetallicProbeService : IMetallicProbeService
    {
        private const int ProbeSize = 64;
        private readonly ConcurrentDictionary<string, bool> _cache = new(StringComparer.OrdinalIgnoreCase);

        public bool IsMetallic(string metallicPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(metallicPath);
            return _cache.GetOrAdd(metallicPath, p =>
            {
                try
                {
                    var (gray, _, _) = PngIo.LoadGray8(p, ProbeSize);
                    return PbrBakeMath.IsMetallic(PbrBakeMath.MaxGray(gray));
                }
                catch (Exception ex) when (ex is System.IO.IOException || ex is NotSupportedException || ex is ArgumentException)
                {
                    return false;
                }
            });
        }
    }
}
```

- [ ] **Step 8: Register in DI**

In `src/Core/Bootstrapper.cs`, after the line `services.AddSingleton<IImageColorExtractionService, ImageColorExtractionService>();` add:
```csharp
            services.AddSingleton<IPbrTextureBakeService, PbrTextureBakeService>();
            services.AddSingleton<IMetallicProbeService, MetallicProbeService>();
```

- [ ] **Step 9: Run tests to verify they pass**

Run: `$DOTNET test LECG.Tests/LECG.Tests.csproj -p:RevitVersion=2026 -p:SkipRevitDeploy=true --filter "FullyQualifiedName~PbrTextureBakeServiceTests"`
Expected: 6 passed. Note: a 64 px probe of a 4x4 image upscales; fine. If `Gray16` decode yields 0 for roughness, switch `WriteGray16` to `PixelFormats.Gray8` with value 128 and keep the assertion.

- [ ] **Step 10: Time a real bake (manual, do not commit output)**

Add a temporary `[Fact(Skip = "manual")]`-style test or run via a throwaway console call: bake `C:\LECG\SubstanceBakes\Asphalt\asphalt_rough` at 2048 into `%TEMP%\bake-probe`. Expected under 5 s. If over 10 s, replace `TransformedBitmap` with a two-step half-size downscale loop in `PngIo.Load` (scale by 0.5 repeatedly until ≤ 2×target, then final scale). Record the timing in the commit message.

- [ ] **Step 11: Commit**

```bash
git add src/Models/BakedTextureSet.cs src/Models/BakeOptions.cs src/Services/Imaging/PngIo.cs src/Services/PbrTextureBakeService.cs src/Services/MetallicProbeService.cs src/Services/Interfaces/IPbrTextureBakeService.cs src/Services/Interfaces/IMetallicProbeService.cs src/Core/Bootstrapper.cs LECG.Tests/Windows/PbrTextureBakeServiceTests.cs LECG.Tests/LECG.Tests.csproj
git commit -m "feat: WPF texture bake service (AO, green flip, F0) and metallic probe

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 6: Schema-aware bitmap property service (LECG, Revit API)

**Files:**
- Modify: `src/Services/Interfaces/IMaterialBitmapPropertyService.cs`
- Modify: `src/Services/MaterialBitmapPropertyService.cs`
- Create: `src/Models/TextureTransform.cs`

**Interfaces:**
- Produces:
  - `record TextureTransform(double ScaleXMillimeters, double ScaleYMillimeters, double OffsetXMillimeters, double OffsetYMillimeters, double RotationDegrees, bool LinkTransforms)` with `static TextureTransform Uniform(double sizeMm)`.
  - `IMaterialBitmapPropertyService.ConnectBitmap(AssetProperty? prop, string path, TextureTransform xf)` — UnifiedBitmap schema.
  - `IMaterialBitmapPropertyService.ConnectNormalMap(AssetProperty? prop, string path, TextureTransform xf, double normalScale = 1.0)` — BumpMap schema, type NormalMap.
  - The two existing `SetupBitmapProperty` overloads stay (legacy Generic path uses them) but are reimplemented on top of `ConnectBitmap`.

No unit test (Revit API types). Verification is a clean build plus the manual run in Task 14.

- [ ] **Step 1: Add the transform model**

`src/Models/TextureTransform.cs`:
```csharp
namespace LECG.Models
{
    public sealed record TextureTransform(
        double ScaleXMillimeters,
        double ScaleYMillimeters,
        double OffsetXMillimeters,
        double OffsetYMillimeters,
        double RotationDegrees,
        bool LinkTransforms)
    {
        public const double MillimetersPerFoot = 304.8;

        public static TextureTransform Uniform(double sizeMillimeters) =>
            new(sizeMillimeters, sizeMillimeters, 0, 0, 0, true);

        public double ScaleXFeet => ScaleXMillimeters / MillimetersPerFoot;
        public double ScaleYFeet => ScaleYMillimeters / MillimetersPerFoot;
        public double OffsetXFeet => OffsetXMillimeters / MillimetersPerFoot;
        public double OffsetYFeet => OffsetYMillimeters / MillimetersPerFoot;
    }
}
```

- [ ] **Step 2: Extend the interface**

Replace `src/Services/Interfaces/IMaterialBitmapPropertyService.cs` with:
```csharp
using Autodesk.Revit.DB.Visual;
using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface IMaterialBitmapPropertyService
    {
        void SetupBitmapProperty(AssetProperty? prop, string path);
        void SetupBitmapProperty(AssetProperty? prop, string path, double scaleXMillimeters, double scaleYMillimeters, double offsetXMillimeters, double offsetYMillimeters, double rotationDegrees, bool linkTextureTransforms);

        /// <summary>Connects a UnifiedBitmap asset carrying <paramref name="path"/> to <paramref name="prop"/>.</summary>
        void ConnectBitmap(AssetProperty? prop, string path, TextureTransform transform);

        /// <summary>Connects a BumpMap asset of type NormalMap carrying <paramref name="path"/> to <paramref name="prop"/>.</summary>
        void ConnectNormalMap(AssetProperty? prop, string path, TextureTransform transform, double normalScale = 1.0);
    }
}
```

- [ ] **Step 3: Rewrite the service**

Replace `src/Services/MaterialBitmapPropertyService.cs` with:
```csharp
using System;
using Autodesk.Revit.DB.Visual;
using LECG.Models;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class MaterialBitmapPropertyService : IMaterialBitmapPropertyService
    {
        private const string UnifiedBitmapSchema = "UnifiedBitmap";
        private const string BumpMapSchema = "BumpMap";
        private const int BumpTypeNormalMap = 1; // Autodesk.Revit.DB.Visual.BumpmapType.NormalMap

        public void SetupBitmapProperty(AssetProperty? prop, string path)
        {
            ConnectBitmap(prop, path, TextureTransform.Uniform(TextureTransform.MillimetersPerFoot));
        }

        public void SetupBitmapProperty(AssetProperty? prop, string path, double scaleXMillimeters, double scaleYMillimeters, double offsetXMillimeters, double offsetYMillimeters, double rotationDegrees, bool linkTextureTransforms)
        {
            ConnectBitmap(prop, path, new TextureTransform(scaleXMillimeters, scaleYMillimeters, offsetXMillimeters, offsetYMillimeters, rotationDegrees, linkTextureTransforms));
        }

        public void ConnectBitmap(AssetProperty? prop, string path, TextureTransform transform)
        {
            ArgumentNullException.ThrowIfNull(transform);
            if (prop == null) return;

            Asset? connected = EnsureConnected(prop, UnifiedBitmapSchema);
            if (connected == null) return;

            SetString(connected, UnifiedBitmap.UnifiedbitmapBitmap, path);
            ApplyTransform(connected, transform);
        }

        public void ConnectNormalMap(AssetProperty? prop, string path, TextureTransform transform, double normalScale = 1.0)
        {
            ArgumentNullException.ThrowIfNull(transform);
            if (prop == null) return;

            Asset? connected = EnsureConnected(prop, BumpMapSchema);
            if (connected == null) return;

            SetString(connected, BumpMap.BumpmapBitmap, path);
            SetInteger(connected, BumpMap.BumpmapType, BumpTypeNormalMap);
            SetDouble(connected, BumpMap.BumpmapNormalScale, normalScale);
            ApplyTransform(connected, transform);
        }

        private static Asset? EnsureConnected(AssetProperty prop, string schema)
        {
            Asset? existing = prop.GetSingleConnectedAsset();
            if (existing != null)
            {
                // Replace a connected asset of the wrong schema (e.g. UnifiedBitmap on a normal slot).
                if (string.Equals(existing.Name, schema, StringComparison.OrdinalIgnoreCase) ||
                    existing.Name.Contains(schema, StringComparison.OrdinalIgnoreCase))
                {
                    return existing;
                }
                try { prop.RemoveConnectedAsset(); }
                catch (Exception ex) when (IsExpected(ex))
                {
                    Logging.Logger.Instance.LogWarning($"[MaterialBitmapPropertyService] RemoveConnectedAsset: {ex.Message}");
                    return existing;
                }
            }

            try
            {
                prop.AddConnectedAsset(schema);
                return prop.GetSingleConnectedAsset();
            }
            catch (Exception ex) when (IsExpected(ex))
            {
                Logging.Logger.Instance.LogWarning($"[MaterialBitmapPropertyService] AddConnectedAsset('{schema}') on '{prop.Name}': {ex.Message}");
                return null;
            }
        }

        private static void ApplyTransform(Asset connected, TextureTransform t)
        {
            // Unlink first so every scale/offset property is writable.
            SetBoolean(connected, UnifiedBitmap.TextureLinkTextureTransforms, false);
            SetDistance(connected, UnifiedBitmap.TextureRealWorldScaleX, t.ScaleXFeet);
            SetDistance(connected, UnifiedBitmap.TextureRealWorldScaleY, t.ScaleYFeet);
            SetDistance(connected, UnifiedBitmap.TextureRealWorldOffsetX, t.OffsetXFeet);
            SetDistance(connected, UnifiedBitmap.TextureRealWorldOffsetY, t.OffsetYFeet);
            SetDouble(connected, UnifiedBitmap.TextureWAngle, t.RotationDegrees);
            if (t.LinkTransforms)
            {
                SetBoolean(connected, UnifiedBitmap.TextureLinkTextureTransforms, true);
            }
        }

        private static void SetDistance(Asset asset, string name, double feet)
        {
            Try(name, () =>
            {
                AssetProperty? p = asset.FindByName(name);
                if (p == null || p.IsReadOnly) return;
                switch (p)
                {
                    case AssetPropertyDistance d: d.Value = feet; break;
                    case AssetPropertyDouble d: d.Value = feet; break;
                    case AssetPropertyFloat f: f.Value = (float)feet; break;
                }
            });
        }

        private static void SetDouble(Asset asset, string name, double value)
        {
            Try(name, () =>
            {
                AssetProperty? p = asset.FindByName(name);
                if (p == null || p.IsReadOnly) return;
                switch (p)
                {
                    case AssetPropertyDouble d: d.Value = value; break;
                    case AssetPropertyFloat f: f.Value = (float)value; break;
                }
            });
        }

        private static void SetInteger(Asset asset, string name, int value)
        {
            Try(name, () =>
            {
                AssetProperty? p = asset.FindByName(name);
                if (p == null || p.IsReadOnly) return;
                switch (p)
                {
                    case AssetPropertyInteger i: i.Value = value; break;
                    case AssetPropertyEnum e: e.Value = value; break;
                }
            });
        }

        private static void SetString(Asset asset, string name, string value)
        {
            Try(name, () =>
            {
                if (asset.FindByName(name) is AssetPropertyString s && !s.IsReadOnly) s.Value = value;
            });
        }

        private static void SetBoolean(Asset asset, string name, bool value)
        {
            Try(name, () =>
            {
                if (asset.FindByName(name) is AssetPropertyBoolean b && !b.IsReadOnly) b.Value = value;
            });
        }

        private static void Try(string name, Action action)
        {
            try { action(); }
            catch (Exception ex) when (IsExpected(ex))
            {
                Logging.Logger.Instance.LogWarning($"[MaterialBitmapPropertyService] '{name}': {ex.Message}");
            }
        }

        private static bool IsExpected(Exception ex) =>
            ex is ArgumentException
            || ex is InvalidOperationException
            || ex is RevitExceptions.InvalidOperationException
            || ex is RevitExceptions.ArgumentException;
    }
}
```

Notes for the implementer:
- `UnifiedBitmap.UnifiedbitmapBitmap`, `UnifiedBitmap.TextureRealWorldScaleX`, `BumpMap.BumpmapBitmap`, `BumpMap.BumpmapType`, `BumpMap.BumpmapNormalScale` are `string` constants in `Autodesk.Revit.DB.Visual` (verified in `docs/review/revit-api/revitapi.public.members.jsonl`). If any name fails to compile, replace with the literal: `"unifiedbitmap_Bitmap"`, `"texture_RealWorldScaleX"`, `"texture_RealWorldScaleY"`, `"texture_RealWorldOffsetX"`, `"texture_RealWorldOffsetY"`, `"texture_WAngle"`, `"texture_LinkTextureTransforms"`, `"bumpmap_Bitmap"`, `"bumpmap_Type"`, `"bumpmap_NormalScale"`.
- `AssetProperty.RemoveConnectedAsset()` exists on the API; if the compiler says otherwise, drop the replace branch and just return the existing asset.
- `Logging.Logger.Instance.LogWarning` is the existing pattern in this file.

- [ ] **Step 4: Build**

Run: `$DOTNET build LECG.csproj -p:RevitVersion=2026 -p:SkipRevitDeploy=true -c Debug`
Expected: Build succeeded. `MaterialAppearanceAssetService` still compiles because the two `SetupBitmapProperty` overloads remain.

- [ ] **Step 5: Commit**

```bash
git add src/Models/TextureTransform.cs src/Services/Interfaces/IMaterialBitmapPropertyService.cs src/Services/MaterialBitmapPropertyService.cs
git commit -m "refactor: schema-aware bitmap connections (UnifiedBitmap / BumpMap normal)

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---
### Task 7: Advanced Opaque appearance service (LECG, Revit API)

**Files:**
- Create: `src/Services/Interfaces/IAdvancedAppearanceAssetService.cs`
- Create: `src/Services/AdvancedAppearanceAssetService.cs`
- Modify: `src/Core/Bootstrapper.cs` (register after `IMaterialAppearanceAssetService`)

**Interfaces:**
- Consumes: `IMaterialBitmapPropertyService.ConnectBitmap/ConnectNormalMap` (Task 6), `BakedTextureSet`, `TextureTransform`, `ITransactionService`.
- Produces:
  - `ElementId EnsureAdvancedOpaqueAsset(Document doc, Material mat, string assetName, Action<string>? log)` — creates (or reuses the material's existing) appearance asset from the Advanced Opaque template, assigns it to the material, returns its id. Must be called inside a transaction.
  - `void ApplyBakedTextures(Document doc, ElementId assetId, BakedTextureSet set, TextureTransform transform, Action<string>? log)` — opens an edit scope and wires the five slots. Must be called inside a transaction.

- [ ] **Step 1: Interface**

`src/Services/Interfaces/IAdvancedAppearanceAssetService.cs`:
```csharp
using System;
using Autodesk.Revit.DB;
using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface IAdvancedAppearanceAssetService
    {
        ElementId EnsureAdvancedOpaqueAsset(Document doc, Material mat, string assetName, Action<string>? log = null);
        void ApplyBakedTextures(Document doc, ElementId assetId, BakedTextureSet set, TextureTransform transform, Action<string>? log = null);
    }
}
```

- [ ] **Step 2: Implementation**

`src/Services/AdvancedAppearanceAssetService.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using LECG.Models;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class AdvancedAppearanceAssetService : IAdvancedAppearanceAssetService
    {
        private const string BaseSchemaProperty = "BaseSchema";
        private const string OpaqueMarker = "Opaque";

        private const string OpaqueAlbedo = "opaque_albedo";
        private const string OpaqueF0 = "opaque_f0";
        private const string SurfaceRoughness = "surface_roughness";
        private const string SurfaceNormal = "surface_normal";
        private const string SurfaceCutout = "surface_cutout";

        private readonly IMaterialBitmapPropertyService _bitmaps;
        private Asset? _template;

        public AdvancedAppearanceAssetService(IMaterialBitmapPropertyService bitmaps)
        {
            _bitmaps = bitmaps;
        }

        public ElementId EnsureAdvancedOpaqueAsset(Document doc, Material mat, string assetName, Action<string>? log = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(mat);
            ArgumentNullException.ThrowIfNull(assetName);

            if (mat.AppearanceAssetId != ElementId.InvalidElementId
                && doc.GetElement(mat.AppearanceAssetId) is AppearanceAssetElement existing
                && IsOpaqueSchema(existing.GetRenderingAsset()))
            {
                return existing.Id;
            }

            Asset template = FindTemplate(doc);
            string uniqueName = UniqueAssetName(doc, assetName);
            AppearanceAssetElement created = AppearanceAssetElement.Create(doc, uniqueName, template);
            mat.AppearanceAssetId = created.Id;
            log?.Invoke($"    -> Appearance asset '{uniqueName}' (Advanced Opaque)");
            return created.Id;
        }

        public void ApplyBakedTextures(Document doc, ElementId assetId, BakedTextureSet set, TextureTransform transform, Action<string>? log = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(assetId);
            ArgumentNullException.ThrowIfNull(set);
            ArgumentNullException.ThrowIfNull(transform);

            using var scope = new AppearanceAssetEditScope(doc);
            Asset asset = scope.Start(assetId);

            Connect(asset, OpaqueAlbedo, set.BaseColor, transform, log, isNormal: false);
            Connect(asset, SurfaceRoughness, set.Roughness, transform, log, isNormal: false);
            Connect(asset, SurfaceNormal, set.NormalGl, transform, log, isNormal: true);
            if (set.F0 is not null) Connect(asset, OpaqueF0, set.F0, transform, log, isNormal: false);
            if (set.Opacity is not null) Connect(asset, SurfaceCutout, set.Opacity, transform, log, isNormal: false);

            scope.Commit(true);
        }

        private void Connect(Asset asset, string propertyName, string path, TextureTransform transform, Action<string>? log, bool isNormal)
        {
            AssetProperty? prop = asset.FindByName(propertyName);
            if (prop == null)
            {
                log?.Invoke($"    !! property '{propertyName}' not found on asset; skipped {System.IO.Path.GetFileName(path)}");
                return;
            }

            if (isNormal) _bitmaps.ConnectNormalMap(prop, path, transform);
            else _bitmaps.ConnectBitmap(prop, path, transform);

            log?.Invoke($"    -> {propertyName}: {System.IO.Path.GetFileName(path)}");
        }

        private Asset FindTemplate(Document doc)
        {
            if (_template != null) return _template;

            IList<Asset> assets = doc.Application.GetAssets(AssetType.Appearance);
            Asset? match = assets.FirstOrDefault(IsOpaqueSchema);
            if (match == null)
            {
                string names = string.Join(", ", assets.Select(a => a.Name).Distinct().OrderBy(n => n).Take(60));
                throw new InvalidOperationException(
                    "No Advanced Opaque appearance asset template found in the application asset library. " +
                    $"Available asset names: {names}");
            }

            _template = match;
            return match;
        }

        private static bool IsOpaqueSchema(Asset? asset)
        {
            if (asset == null) return false;
            string? schema = (asset.FindByName(BaseSchemaProperty) as AssetPropertyString)?.Value;
            if (!string.IsNullOrEmpty(schema) && schema.Contains(OpaqueMarker, StringComparison.OrdinalIgnoreCase)) return true;
            return asset.Name.Contains(OpaqueMarker, StringComparison.OrdinalIgnoreCase)
                && asset.Name.Contains("Advanced", StringComparison.OrdinalIgnoreCase);
        }

        private static string UniqueAssetName(Document doc, string baseName)
        {
            var names = new FilteredElementCollector(doc)
                .OfClass(typeof(AppearanceAssetElement))
                .Cast<AppearanceAssetElement>()
                .Select(a => a.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            string candidate = baseName.Trim();
            int i = 1;
            while (names.Contains(candidate)) candidate = $"{baseName.Trim()} ({i++})";
            return candidate;
        }
    }
}
```

Notes:
- `AppearanceAssetElement.GetRenderingAsset()` returns the read-only `Asset`. `Asset.Name` for library assets is the schema-ish name (existing code matched `"Generic"` on it). The Advanced Opaque library asset is expected to be named like `PrismOpaqueSchema` or `AdvancedOpaque`; `IsOpaqueSchema` checks `BaseSchema` first, then the name. Task 13's dump command prints the real values; if neither matches, update `IsOpaqueSchema` with the observed name and note it in the commit.
- `doc.Application.GetAssets` is on `Autodesk.Revit.ApplicationServices.Application`.

- [ ] **Step 3: Register**

In `src/Core/Bootstrapper.cs`, after `services.AddSingleton<IMaterialAppearanceAssetService, MaterialAppearanceAssetService>();` add:
```csharp
            services.AddSingleton<IAdvancedAppearanceAssetService, AdvancedAppearanceAssetService>();
```

- [ ] **Step 4: Build**

Run: `$DOTNET build LECG.csproj -p:RevitVersion=2026 -p:SkipRevitDeploy=true -c Debug`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/Services/Interfaces/IAdvancedAppearanceAssetService.cs src/Services/AdvancedAppearanceAssetService.cs src/Core/Bootstrapper.cs
git commit -m "feat: Advanced Opaque appearance asset service

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 8: Identity policy (Core) and per-material create service (LECG)

**Files:**
- Create: `LECG.Core/Substance/SubstanceIdentityPolicy.cs`
- Test: `LECG.Tests/Substance/SubstanceIdentityPolicyTests.cs`
- Create: `src/Models/SubstanceBatchResult.cs`
- Create: `src/Services/Interfaces/ISubstanceMaterialCreateService.cs`
- Create: `src/Services/SubstanceMaterialCreateService.cs`
- Modify: `src/Core/Bootstrapper.cs`

**Interfaces:**
- Consumes: `IPbrTextureBakeService` (Task 5), `IAdvancedAppearanceAssetService` (Task 7), `ITransactionService`, `IRenderSolidFillPatternService`, `IRenderMaterialGraphicsApplyService`, `IImageColorExtractionService` (existing).
- Produces:
  - `SubstanceIdentityPolicy.Description(SubstanceMaterialEntry e)` → `"Asphalt / asphalt_rough / Substance 4K bake"`; `Keywords(e)` → `"substance, pbr, asphalt"`; `const string Manufacturer = "LECG Arquitectura"`, `const string Model = "Arq. Luis Eduardo Cortés"`.
  - `record SubstanceBatchOptions(BakeOptions Bake, TextureTransform Transform, bool OverwriteExisting)`
  - `enum SubstanceMaterialOutcome { Created, Updated, Skipped, Failed }`
  - `record SubstanceMaterialReport(string DisplayName, SubstanceMaterialOutcome Outcome, string? Error)`
  - `class SubstanceBatchResult { int Created, Updated, Skipped, Failed; List<SubstanceMaterialReport> Reports; void Add(SubstanceMaterialReport) }`
  - `ISubstanceMaterialCreateService.Create(Document doc, SubstanceMaterialEntry entry, SubstanceBatchOptions options, Action<string>? log)` → `SubstanceMaterialReport` (never throws for a single material; catches and reports Failed).
  - `ISubstanceMaterialCreateService.ExistingMaterialNames(Document doc)` → `HashSet<string>` (ordinal ignore-case).

- [ ] **Step 1: Write the failing identity tests**

`LECG.Tests/Substance/SubstanceIdentityPolicyTests.cs`:
```csharp
using FluentAssertions;
using LECG.Core.Substance;
using Xunit;

namespace LECG.Tests.Substance;

public class SubstanceIdentityPolicyTests
{
    private static SubstanceMaterialEntry Entry(int res = 4096) => new(
        "Asphalt", "asphalt_rough", "Asphalt Rough", @"C:\lib\Asphalt\asphalt_rough",
        "b", "n", "r", "m", null, null, null, res);

    [Fact]
    public void Description_IncludesCategorySlugAndResolution()
    {
        SubstanceIdentityPolicy.Description(Entry()).Should().Be("Asphalt / asphalt_rough / Substance 4K bake");
        SubstanceIdentityPolicy.Description(Entry(2048)).Should().Be("Asphalt / asphalt_rough / Substance 2K bake");
    }

    [Fact]
    public void Keywords_LowercaseCategory()
    {
        SubstanceIdentityPolicy.Keywords(Entry()).Should().Be("substance, pbr, asphalt");
    }

    [Fact]
    public void Keywords_MultiWordCategoryKept()
    {
        var e = Entry() with { Category = "Car Paint" };
        SubstanceIdentityPolicy.Keywords(e).Should().Be("substance, pbr, car paint");
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `$DOTNET test LECG.Tests/LECG.Tests.csproj -p:RevitVersion=2026 -p:SkipRevitDeploy=true --filter "FullyQualifiedName~SubstanceIdentityPolicyTests"`
Expected: build error.

- [ ] **Step 3: Implement the policy**

`LECG.Core/Substance/SubstanceIdentityPolicy.cs`:
```csharp
namespace LECG.Core.Substance;

public static class SubstanceIdentityPolicy
{
    public const string Manufacturer = "LECG Arquitectura";
    public const string Model = "Arq. Luis Eduardo Cortés";

    public static string Description(SubstanceMaterialEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        int k = Math.Max(1, entry.Resolution / 1024);
        return $"{entry.Category} / {entry.Slug} / Substance {k}K bake";
    }

    public static string Keywords(SubstanceMaterialEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return $"substance, pbr, {entry.Category.ToLowerInvariant()}";
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run the same filter. Expected: 3 passed.

- [ ] **Step 5: Result models**

`src/Models/SubstanceBatchResult.cs`:
```csharp
using System.Collections.Generic;

namespace LECG.Models
{
    public sealed record SubstanceBatchOptions(BakeOptions Bake, TextureTransform Transform, bool OverwriteExisting);

    public enum SubstanceMaterialOutcome { Created, Updated, Skipped, Failed }

    public sealed record SubstanceMaterialReport(string DisplayName, SubstanceMaterialOutcome Outcome, string? Error);

    public sealed class SubstanceBatchResult
    {
        public int Created { get; private set; }
        public int Updated { get; private set; }
        public int Skipped { get; private set; }
        public int Failed { get; private set; }
        public List<SubstanceMaterialReport> Reports { get; } = new();

        public void Add(SubstanceMaterialReport report)
        {
            System.ArgumentNullException.ThrowIfNull(report);
            Reports.Add(report);
            switch (report.Outcome)
            {
                case SubstanceMaterialOutcome.Created: Created++; break;
                case SubstanceMaterialOutcome.Updated: Updated++; break;
                case SubstanceMaterialOutcome.Skipped: Skipped++; break;
                case SubstanceMaterialOutcome.Failed: Failed++; break;
            }
        }

        public string Summary => $"{Created} created, {Updated} updated, {Skipped} skipped, {Failed} failed";
    }
}
```

- [ ] **Step 6: Create service**

`src/Services/Interfaces/ISubstanceMaterialCreateService.cs`:
```csharp
using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Core.Substance;
using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface ISubstanceMaterialCreateService
    {
        HashSet<string> ExistingMaterialNames(Document doc);
        SubstanceMaterialReport Create(Document doc, SubstanceMaterialEntry entry, SubstanceBatchOptions options, Action<string>? log = null);
    }
}
```

`src/Services/SubstanceMaterialCreateService.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Core.Substance;
using LECG.Models;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class SubstanceMaterialCreateService : ISubstanceMaterialCreateService
    {
        private readonly IPbrTextureBakeService _bake;
        private readonly IAdvancedAppearanceAssetService _appearance;
        private readonly ITransactionService _transactions;
        private readonly IRenderSolidFillPatternService _solidFill;
        private readonly IRenderMaterialGraphicsApplyService _graphics;
        private readonly IImageColorExtractionService _colors;

        public SubstanceMaterialCreateService(
            IPbrTextureBakeService bake,
            IAdvancedAppearanceAssetService appearance,
            ITransactionService transactions,
            IRenderSolidFillPatternService solidFill,
            IRenderMaterialGraphicsApplyService graphics,
            IImageColorExtractionService colors)
        {
            _bake = bake;
            _appearance = appearance;
            _transactions = transactions;
            _solidFill = solidFill;
            _graphics = graphics;
            _colors = colors;
        }

        public HashSet<string> ExistingMaterialNames(Document doc)
        {
            ArgumentNullException.ThrowIfNull(doc);
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .Select(m => m.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public SubstanceMaterialReport Create(Document doc, SubstanceMaterialEntry entry, SubstanceBatchOptions options, Action<string>? log = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentNullException.ThrowIfNull(options);

            try
            {
                Material? existing = FindMaterial(doc, entry.DisplayName);
                if (existing != null && !options.OverwriteExisting)
                {
                    log?.Invoke($"  SKIP '{entry.DisplayName}' already in document");
                    return new SubstanceMaterialReport(entry.DisplayName, SubstanceMaterialOutcome.Skipped, null);
                }

                BakedTextureSet baked = _bake.Bake(entry, options.Bake, log);
                Color color = SafeAverageColor(baked.BaseColor, log);

                bool created = existing == null;
                _transactions.Run(doc, $"Substance material: {entry.DisplayName}", d =>
                {
                    Material mat = existing ?? (Material)d.GetElement(Material.Create(d, entry.DisplayName));
                    ApplyIdentity(mat, entry, log);
                    if (created)
                    {
                        _graphics.Apply(mat, color, _solidFill.GetSolidFillPatternId(d), log);
                    }
                    mat.UseRenderAppearanceForShading = true;

                    ElementId assetId = _appearance.EnsureAdvancedOpaqueAsset(d, mat, entry.DisplayName, log);
                    _appearance.ApplyBakedTextures(d, assetId, baked, options.Transform, log);
                });

                var outcome = created ? SubstanceMaterialOutcome.Created : SubstanceMaterialOutcome.Updated;
                log?.Invoke($"  DONE {outcome}: {entry.DisplayName}");
                return new SubstanceMaterialReport(entry.DisplayName, outcome, null);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                log?.Invoke($"  FAIL '{entry.DisplayName}': {ex.Message}");
                return new SubstanceMaterialReport(entry.DisplayName, SubstanceMaterialOutcome.Failed, ex.Message);
            }
        }

        private static Material? FindMaterial(Document doc, string name)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private Color SafeAverageColor(string path, Action<string>? log)
        {
            try
            {
                return _colors.GetAverageColor(path);
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException)
            {
                log?.Invoke($"    -> average color failed ({ex.Message}); using grey");
                return new Color(128, 128, 128);
            }
        }

        private static void ApplyIdentity(Material mat, SubstanceMaterialEntry entry, Action<string>? log)
        {
            mat.MaterialClass = entry.Category;
            mat.MaterialCategory = entry.Category;
            SetParam(mat, BuiltInParameter.ALL_MODEL_DESCRIPTION, SubstanceIdentityPolicy.Description(entry));
            SetParam(mat, BuiltInParameter.ALL_MODEL_MANUFACTURER, SubstanceIdentityPolicy.Manufacturer);
            SetParam(mat, BuiltInParameter.ALL_MODEL_MODEL, SubstanceIdentityPolicy.Model);

            Parameter? keywords = mat.LookupParameter("Keywords");
            if (keywords != null && !keywords.IsReadOnly && keywords.StorageType == StorageType.String)
            {
                keywords.Set(SubstanceIdentityPolicy.Keywords(entry));
            }
            log?.Invoke($"    -> class/category '{entry.Category}', description set");
        }

        private static void SetParam(Element element, BuiltInParameter bip, string value)
        {
            Parameter? p = element.get_Parameter(bip);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.String) p.Set(value);
        }
    }
}
```

- [ ] **Step 7: Register**

In `src/Core/Bootstrapper.cs`, after the `IAdvancedAppearanceAssetService` line add:
```csharp
            services.AddSingleton<ISubstanceMaterialCreateService, SubstanceMaterialCreateService>();
```

- [ ] **Step 8: Build and run all tests**

Run: `$DOTNET build LECG.csproj -p:RevitVersion=2026 -p:SkipRevitDeploy=true -c Debug` then the full test command.
Expected: build ok; all tests pass (69 baseline + 37 Substance + 6 Windows = 112).

- [ ] **Step 9: Commit**

```bash
git add LECG.Core/Substance/SubstanceIdentityPolicy.cs LECG.Tests/Substance/SubstanceIdentityPolicyTests.cs src/Models/SubstanceBatchResult.cs src/Services/Interfaces/ISubstanceMaterialCreateService.cs src/Services/SubstanceMaterialCreateService.cs src/Core/Bootstrapper.cs
git commit -m "feat: substance material create service with identity policy

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---
### Task 9: Reroute the single-material creator to Advanced (LECG)

**Files:**
- Modify: `src/Models/PbrMaterialCreateRequest.cs` (drop `DisplacementPath`)
- Modify: `src/Services/MaterialAppearanceAssetService.cs` (`ApplyPbrTextures` body)
- Modify: `src/ViewModels/MaterialPageViewModel.cs` (drop Displacement members)
- Modify: `src/Views/PbrMaterialCreatorView.xaml` (drop Displacement row, update info text)
- Modify: `src/Commands/PbrMaterialCreatorCommand.cs` (no change needed unless it references DisplacementPath; verify)

**Interfaces:**
- Consumes: `IPbrTextureBakeService`, `IAdvancedAppearanceAssetService`, `SubstanceMaterialEntry`, `BakeOptions`, `TextureTransform`.
- Produces: `PbrMaterialCreateRequest` without `DisplacementPath` (positional record: `MaterialName, AppearanceAssetName, Description, MaterialClass, DiffusePath, RoughnessPath, NormalPath, MetallicPath, AoPath, OpacityPath, UseRenderAppearanceForShading, ScaleXMillimeters, ScaleYMillimeters, OffsetXMillimeters, OffsetYMillimeters, RotationDegrees, LinkTextureTransforms`).

- [ ] **Step 1: Trim the request record**

Replace `src/Models/PbrMaterialCreateRequest.cs` with:
```csharp
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
        string? MetallicPath,
        string? AoPath,
        string? OpacityPath,
        bool UseRenderAppearanceForShading,
        double ScaleXMillimeters,
        double ScaleYMillimeters,
        double OffsetXMillimeters,
        double OffsetYMillimeters,
        double RotationDegrees,
        bool LinkTextureTransforms);
}
```

- [ ] **Step 2: Remove Displacement from the page view-model**

In `src/ViewModels/MaterialPageViewModel.cs` delete: the `_displacementPath` and `_displacementPreview` observable fields, `OnDisplacementPathChanged`, the `"Displacement"` case in `SetPathByTarget`, the `Displacement` block in `ScanFolderForTextures`, `HasValidOptionalPath(DisplacementPath)` from `CanRun`, and the `NormalizeOptionalPath(DisplacementPath)` argument in `CreateRequest()` (the call now passes 17 arguments in the record order above).

- [ ] **Step 3: Remove the Displacement row from XAML**

In `src/Views/PbrMaterialCreatorView.xaml` delete the `<!-- Displacement -->` TextBlock and its following `<Grid>`. Replace the INFO text with:
```
Materials use Revit's Advanced Opaque schema. Textures are re-baked to 8-bit PNG under a _revit folder next to the sources: AO is multiplied into base color, the normal map's green channel is flipped to OpenGL, and a metallic map becomes a reflectance (F0) map. Height maps are not used.
```
Also update the header description sentence's keyword list to `(basecolor, normal, roughness, metallic, ao, opacity)`.

- [ ] **Step 4: Rewrite `ApplyPbrTextures`**

In `src/Services/MaterialAppearanceAssetService.cs`:
- Add constructor dependencies `IPbrTextureBakeService bake` and `IAdvancedAppearanceAssetService advanced` (keep the existing ones). Add `using LECG.Core.Substance;`.
- Replace the whole `ApplyPbrTextures` method with:
```csharp
        public void ApplyPbrTextures(
            Document doc,
            Material mat,
            string name,
            PbrMaterialCreateRequest request,
            Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(mat);
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrEmpty(request.NormalPath) || string.IsNullOrEmpty(request.RoughnessPath) || string.IsNullOrEmpty(request.MetallicPath))
            {
                throw new InvalidOperationException("Advanced materials need base color, normal, roughness and metallic maps. Metallic may be a flat black image.");
            }

            string folder = System.IO.Path.GetDirectoryName(request.DiffusePath) ?? ".";
            string slug = SanitizeSlug(request.MaterialName);
            var entry = new SubstanceMaterialEntry(
                Category: "Custom",
                Slug: slug,
                DisplayName: request.MaterialName,
                FolderPath: folder,
                BaseColorPath: request.DiffusePath,
                NormalPath: request.NormalPath,
                RoughnessPath: request.RoughnessPath,
                MetallicPath: request.MetallicPath,
                AoPath: request.AoPath,
                OpacityPath: request.OpacityPath,
                Ior: null,
                Resolution: 0);

            var bakeOptions = new BakeOptions(System.IO.Path.Combine(folder, "_revit"), 2048, ForceRebake: false);
            BakedTextureSet baked = _bake.Bake(entry, bakeOptions, logCallback);

            var transform = new TextureTransform(
                request.ScaleXMillimeters, request.ScaleYMillimeters,
                request.OffsetXMillimeters, request.OffsetYMillimeters,
                request.RotationDegrees, request.LinkTextureTransforms);

            _transactionService.Run(doc, "Apply PBR Textures", d =>
            {
                ElementId assetId = _advanced.EnsureAdvancedOpaqueAsset(d, mat, name, logCallback);
                _advanced.ApplyBakedTextures(d, assetId, baked, transform, logCallback);
            });
        }

        private static string SanitizeSlug(string name)
        {
            var chars = name.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
            return new string(chars).Trim('_');
        }
```
- Delete `SetAssetInteger` and `SetAssetBoolean` if nothing else uses them, and delete `EnsureAppearanceAsset` only if `ApplyTextures` (legacy Generic path) does not call it. `ApplyTextures` must keep working unchanged.
- Note: the single creator now requires a metallic map. `MaterialPageViewModel.CanRun` must also require `NormalPath`, `RoughnessPath`, `MetallicPath` to be non-empty and existing. Update the "Normal Map" / "Roughness" / "Metallic" labels in the XAML to append `(required)`.

- [ ] **Step 5: Build, run tests, deploy, smoke**

Run: `$DOTNET build LECG.csproj -p:RevitVersion=2026 -c Debug` (deploys). Full test command: expected all green.
Manual smoke (record result in commit message): open Revit 2026, PBR Material button, pick `C:\LECG\SubstanceBakes\Ceiling\ceiling_perforated_tiles`, name `Test Ceiling`, create. Expected log lines: `Baked 2048px to ...\_revit\Custom\test_ceiling`, `opaque_albedo: ...`, `surface_normal: ...`. Material Browser shows an Advanced Opaque appearance with images in Base Color, Roughness, Normal.

- [ ] **Step 6: Commit**

```bash
git add src/Models/PbrMaterialCreateRequest.cs src/Services/MaterialAppearanceAssetService.cs src/ViewModels/MaterialPageViewModel.cs src/Views/PbrMaterialCreatorView.xaml
git commit -m "refactor: single PBR creator uses Advanced Opaque via bake service; drop displacement

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 10: Selection policy (Core)

**Files:**
- Create: `LECG.Core/Substance/SubstanceSelectionPolicy.cs`
- Test: `LECG.Tests/Substance/SubstanceSelectionPolicyTests.cs`

**Interfaces:**
- Produces:
  - `record SubstanceRowState(string Category, string Slug, string DisplayName, bool IsSelected)`
  - `bool? SubstanceSelectionPolicy.CategoryState(IEnumerable<SubstanceRowState> rows, string category)` — true all, false none, null mixed; false when category has no rows.
  - `IReadOnlyList<SubstanceRowState> SubstanceSelectionPolicy.SetCategory(IEnumerable<SubstanceRowState> rows, string category, bool selected)`
  - `IReadOnlyList<SubstanceRowState> SubstanceSelectionPolicy.SetAll(IEnumerable<SubstanceRowState> rows, bool selected)`
  - `bool SubstanceSelectionPolicy.Matches(SubstanceRowState row, string? filter)` — empty filter matches everything; substring on DisplayName or Category, ignore case.
  - `IReadOnlyList<(string Category, int Count)> SubstanceSelectionPolicy.CategoryCounts(IEnumerable<SubstanceRowState> rows)` — ordered by category.

- [ ] **Step 1: Write the failing tests**

`LECG.Tests/Substance/SubstanceSelectionPolicyTests.cs`:
```csharp
using FluentAssertions;
using LECG.Core.Substance;
using Xunit;

namespace LECG.Tests.Substance;

public class SubstanceSelectionPolicyTests
{
    private static readonly SubstanceRowState[] Rows =
    {
        new("Asphalt", "a1", "A One", true),
        new("Asphalt", "a2", "A Two", false),
        new("Wood", "w1", "Walnut", true),
        new("Wood", "w2", "Oak", true),
    };

    [Fact]
    public void CategoryState_Mixed_Null_All_True_None_False()
    {
        SubstanceSelectionPolicy.CategoryState(Rows, "Asphalt").Should().BeNull();
        SubstanceSelectionPolicy.CategoryState(Rows, "Wood").Should().BeTrue();
        SubstanceSelectionPolicy.CategoryState(Rows, "Glass").Should().BeFalse();
    }

    [Fact]
    public void SetCategory_OnlyTouchesThatCategory()
    {
        var r = SubstanceSelectionPolicy.SetCategory(Rows, "Asphalt", true);
        r.Where(x => x.Category == "Asphalt").Should().OnlyContain(x => x.IsSelected);
        r.Single(x => x.Slug == "w1").IsSelected.Should().BeTrue();
        var r2 = SubstanceSelectionPolicy.SetCategory(Rows, "Wood", false);
        r2.Where(x => x.Category == "Wood").Should().OnlyContain(x => !x.IsSelected);
        r2.Single(x => x.Slug == "a1").IsSelected.Should().BeTrue();
    }

    [Fact]
    public void SetAll_AppliesEverywhere()
    {
        SubstanceSelectionPolicy.SetAll(Rows, false).Should().OnlyContain(x => !x.IsSelected);
        SubstanceSelectionPolicy.SetAll(Rows, true).Should().OnlyContain(x => x.IsSelected);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("  ", true)]
    [InlineData("wal", true)]
    [InlineData("WOOD", true)]
    [InlineData("oak", false)]
    public void Matches_FilterOnNameOrCategory(string? filter, bool expected)
    {
        SubstanceSelectionPolicy.Matches(Rows[2], filter).Should().Be(expected);
    }

    [Fact]
    public void CategoryCounts_OrderedWithCounts()
    {
        SubstanceSelectionPolicy.CategoryCounts(Rows).Should().Equal(("Asphalt", 2), ("Wood", 2));
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `$DOTNET test LECG.Tests/LECG.Tests.csproj -p:RevitVersion=2026 -p:SkipRevitDeploy=true --filter "FullyQualifiedName~SubstanceSelectionPolicyTests"`
Expected: build error.

- [ ] **Step 3: Implement**

`LECG.Core/Substance/SubstanceSelectionPolicy.cs`:
```csharp
namespace LECG.Core.Substance;

public sealed record SubstanceRowState(string Category, string Slug, string DisplayName, bool IsSelected);

public static class SubstanceSelectionPolicy
{
    public static bool? CategoryState(IEnumerable<SubstanceRowState> rows, string category)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(category);
        var inCat = rows.Where(r => Same(r.Category, category)).ToList();
        if (inCat.Count == 0) return false;
        int selected = inCat.Count(r => r.IsSelected);
        if (selected == 0) return false;
        if (selected == inCat.Count) return true;
        return null;
    }

    public static IReadOnlyList<SubstanceRowState> SetCategory(IEnumerable<SubstanceRowState> rows, string category, bool selected)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(category);
        return rows.Select(r => Same(r.Category, category) ? r with { IsSelected = selected } : r).ToList();
    }

    public static IReadOnlyList<SubstanceRowState> SetAll(IEnumerable<SubstanceRowState> rows, bool selected)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return rows.Select(r => r with { IsSelected = selected }).ToList();
    }

    public static bool Matches(SubstanceRowState row, string? filter)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (string.IsNullOrWhiteSpace(filter)) return true;
        string f = filter.Trim();
        return row.DisplayName.Contains(f, StringComparison.OrdinalIgnoreCase)
            || row.Category.Contains(f, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<(string Category, int Count)> CategoryCounts(IEnumerable<SubstanceRowState> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return rows.GroupBy(r => r.Category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.Key, g.Count()))
            .ToList();
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 4: Run to verify pass**

Run the Substance filter. Expected: all pass (43 Substance tests). Then `$DOTNET build LECG.Core/LECG.Core.csproj -c Release /p:TreatWarningsAsErrors=true` — 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add LECG.Core/Substance/SubstanceSelectionPolicy.cs LECG.Tests/Substance/SubstanceSelectionPolicyTests.cs
git commit -m "feat(core): substance batch selection policy

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---
### Task 11: Batch window view-models, settings, view (LECG)

**Files:**
- Create: `src/Models/SubstanceBatchSettings.cs`
- Create: `src/ViewModels/SubstanceMaterialRowViewModel.cs`
- Create: `src/ViewModels/SubstanceCategoryViewModel.cs`
- Create: `src/ViewModels/SubstanceBatchViewModel.cs`
- Create: `src/Views/SubstanceBatchView.xaml`
- Create: `src/Views/SubstanceBatchView.xaml.cs`
- Modify: `src/Core/Bootstrapper.cs` (`ConfigureViewModels`: `services.AddTransient<SubstanceBatchViewModel>();` `ConfigureViews`: `services.AddTransient<Views.SubstanceBatchView>();`)

**Interfaces:**
- Consumes: `SubstanceLibraryScanner`, `SubstanceSelectionPolicy`, `BakeOutputPaths.DefaultOutputRoot`, `IMetallicProbeService`, `SettingsManager`.
- Produces:
  - `SubstanceBatchSettings { string LibraryRoot; string OutputRoot; int TargetSize; double SizeMillimeters; bool OverwriteExisting; bool ForceRebake }` with defaults `C:\LECG\SubstanceBakes`, `""` (means default), 2048, 2500, false, false. Saved as `substance-batch.json`.
  - `SubstanceBatchViewModel` public surface used by the command: `bool ShouldRun` (from `BaseViewModel`), `IReadOnlyList<SubstanceMaterialEntry> SelectedEntries()`, `SubstanceBatchSettings CurrentSettings()`, `void SetExistingNames(ISet<string> names)`.

- [ ] **Step 1: Settings model**

`src/Models/SubstanceBatchSettings.cs`:
```csharp
namespace LECG.Models
{
    public sealed class SubstanceBatchSettings
    {
        public const string FileName = "substance-batch.json";

        public string LibraryRoot { get; set; } = @"C:\LECG\SubstanceBakes";
        public string OutputRoot { get; set; } = string.Empty;
        public int TargetSize { get; set; } = 2048;
        public double SizeMillimeters { get; set; } = 2500;
        public bool OverwriteExisting { get; set; }
        public bool ForceRebake { get; set; }
    }
}
```

- [ ] **Step 2: Row view-model**

`src/ViewModels/SubstanceMaterialRowViewModel.cs`:
```csharp
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using LECG.Core.Substance;
using LECG.Services.Interfaces;

namespace LECG.ViewModels
{
    public partial class SubstanceMaterialRowViewModel : ObservableObject
    {
        private readonly IMetallicProbeService? _probe;
        private bool? _isMetallic;

        public SubstanceMaterialEntry Entry { get; }

        [ObservableProperty]
        private bool _isSelected = true;

        [ObservableProperty]
        private bool _existsInDocument;

        public SubstanceMaterialRowViewModel(SubstanceMaterialEntry entry, IMetallicProbeService? probe)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            _probe = probe;
        }

        public string DisplayName => Entry.DisplayName;
        public string Category => Entry.Category;
        public bool HasBaseColor => true;
        public bool HasNormal => true;
        public bool HasRoughness => true;
        public bool HasAo => Entry.HasAo;
        public bool HasOpacity => Entry.HasOpacity;

        /// <summary>Lazy 64 px probe. Bound by the Maps column; computed on first access.</summary>
        public bool HasMetal
        {
            get
            {
                _isMetallic ??= _probe?.IsMetallic(Entry.MetallicPath) ?? false;
                return _isMetallic.Value;
            }
        }

        public string Status => ExistsInDocument ? "In doc" : string.Empty;

        public Action? SelectionChanged { get; set; }

        partial void OnIsSelectedChanged(bool value) => SelectionChanged?.Invoke();
        partial void OnExistsInDocumentChanged(bool value) => OnPropertyChanged(nameof(Status));

        public SubstanceRowState ToState() => new(Entry.Category, Entry.Slug, Entry.DisplayName, IsSelected);
    }
}
```

- [ ] **Step 3: Category view-model**

`src/ViewModels/SubstanceCategoryViewModel.cs`:
```csharp
using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LECG.ViewModels
{
    public partial class SubstanceCategoryViewModel : ObservableObject
    {
        public string Name { get; }
        public int Count { get; }
        public string Label => $"{Name} ({Count})";

        /// <summary>true = all, false = none, null = mixed.</summary>
        [ObservableProperty]
        private bool? _isChecked;

        private bool _suppress;
        public Action<string, bool>? CheckedByUser { get; set; }

        public SubstanceCategoryViewModel(string name, int count, bool? isChecked)
        {
            Name = name;
            Count = count;
            _isChecked = isChecked;
        }

        public void SetFromRows(bool? state)
        {
            _suppress = true;
            IsChecked = state;
            _suppress = false;
        }

        partial void OnIsCheckedChanged(bool? value)
        {
            if (_suppress) return;
            // A click on a mixed (null) box goes to true; on true goes to false.
            CheckedByUser?.Invoke(Name, value ?? true);
        }
    }
}
```

- [ ] **Step 4: Batch view-model**

`src/ViewModels/SubstanceBatchViewModel.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.Core.Substance;
using LECG.Models;
using LECG.Services;
using LECG.Services.Interfaces;

namespace LECG.ViewModels
{
    public partial class SubstanceBatchViewModel : BaseViewModel
    {
        private readonly IMetallicProbeService? _probe;
        private ISet<string> _existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public ObservableCollection<SubstanceMaterialRowViewModel> Rows { get; } = new();
        public ObservableCollection<SubstanceCategoryViewModel> Categories { get; } = new();
        public ICollectionView RowsView { get; }

        public IReadOnlyList<int> ResolutionChoices { get; } = new[] { 2048, 4096 };

        [ObservableProperty] private string _libraryRoot = string.Empty;
        [ObservableProperty] private string _outputRoot = string.Empty;
        [ObservableProperty] private int _targetSize = 2048;
        [ObservableProperty] private double _sizeMillimeters = 2500;
        [ObservableProperty] private bool _overwriteExisting;
        [ObservableProperty] private bool _forceRebake;
        [ObservableProperty] private string _filterText = string.Empty;
        [ObservableProperty] private string _scanSummary = string.Empty;
        [ObservableProperty] private IReadOnlyList<string> _warnings = Array.Empty<string>();

        public int SelectedCount => Rows.Count(r => r.IsSelected);
        public int SelectedInDocumentCount => Rows.Count(r => r.IsSelected && r.ExistsInDocument);
        public string Footer => $"{SelectedCount} selected · {SelectedInDocumentCount} already in document";
        public bool CanRun => SelectedCount > 0 && SizeMillimeters > 0 && Directory.Exists(LibraryRoot);

        public SubstanceBatchViewModel() : this(null) { }

        public SubstanceBatchViewModel(IMetallicProbeService? probe)
        {
            _probe = probe;
            Title = "SUBSTANCE BATCH MATERIALS";
            RowsView = CollectionViewSource.GetDefaultView(Rows);
            RowsView.Filter = o => o is SubstanceMaterialRowViewModel r && SubstanceSelectionPolicy.Matches(r.ToState(), FilterText);

            var s = SettingsManager.Load<SubstanceBatchSettings>(SubstanceBatchSettings.FileName);
            LibraryRoot = s.LibraryRoot;
            OutputRoot = string.IsNullOrWhiteSpace(s.OutputRoot) ? BakeOutputPaths.DefaultOutputRoot(s.LibraryRoot) : s.OutputRoot;
            TargetSize = s.TargetSize;
            SizeMillimeters = s.SizeMillimeters;
            OverwriteExisting = s.OverwriteExisting;
            ForceRebake = s.ForceRebake;

            Rescan();
        }

        public void SetExistingNames(ISet<string> names)
        {
            ArgumentNullException.ThrowIfNull(names);
            _existingNames = names;
            foreach (var r in Rows) r.ExistsInDocument = names.Contains(r.DisplayName);
            NotifyCounts();
        }

        public IReadOnlyList<SubstanceMaterialEntry> SelectedEntries() =>
            Rows.Where(r => r.IsSelected).Select(r => r.Entry).ToList();

        public SubstanceBatchSettings CurrentSettings() => new()
        {
            LibraryRoot = LibraryRoot,
            OutputRoot = OutputRoot,
            TargetSize = TargetSize,
            SizeMillimeters = SizeMillimeters,
            OverwriteExisting = OverwriteExisting,
            ForceRebake = ForceRebake,
        };

        public override void Apply()
        {
            if (!CanRun) return;
            SettingsManager.Save(CurrentSettings(), SubstanceBatchSettings.FileName);
            base.Apply();
        }

        [RelayCommand]
        private void BrowseLibrary()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select Substance bake library root" };
            if (dialog.ShowDialog() == true)
            {
                LibraryRoot = dialog.FolderName;
                OutputRoot = BakeOutputPaths.DefaultOutputRoot(LibraryRoot);
                Rescan();
            }
        }

        [RelayCommand]
        private void BrowseOutput()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select baked texture output folder" };
            if (dialog.ShowDialog() == true) OutputRoot = dialog.FolderName;
        }

        [RelayCommand] private void SelectAll() => ApplyStates(SubstanceSelectionPolicy.SetAll(States(), true));
        [RelayCommand] private void SelectNone() => ApplyStates(SubstanceSelectionPolicy.SetAll(States(), false));
        [RelayCommand] private void Rescan()
        {
            Rows.Clear();
            Categories.Clear();

            SubstanceScanResult scan = SubstanceLibraryScanner.Scan(LibraryRoot);
            foreach (SubstanceMaterialEntry e in scan.Entries)
            {
                var row = new SubstanceMaterialRowViewModel(e, _probe)
                {
                    ExistsInDocument = _existingNames.Contains(e.DisplayName),
                };
                row.SelectionChanged = OnRowSelectionChanged;
                Rows.Add(row);
            }

            foreach (var (category, count) in SubstanceSelectionPolicy.CategoryCounts(States()))
            {
                var cat = new SubstanceCategoryViewModel(category, count, SubstanceSelectionPolicy.CategoryState(States(), category));
                cat.CheckedByUser = (name, selected) => ApplyStates(SubstanceSelectionPolicy.SetCategory(States(), name, selected));
                Categories.Add(cat);
            }

            Warnings = scan.Warnings;
            ScanSummary = scan.Warnings.Count == 0
                ? $"{scan.Entries.Count} materials in {Categories.Count} categories"
                : $"{scan.Entries.Count} materials in {Categories.Count} categories · {scan.Warnings.Count} skipped (see log)";
            NotifyCounts();
        }

        partial void OnFilterTextChanged(string value) => RowsView.Refresh();
        partial void OnSizeMillimetersChanged(double value) => OnPropertyChanged(nameof(CanRun));
        partial void OnLibraryRootChanged(string value) => OnPropertyChanged(nameof(CanRun));

        private List<SubstanceRowState> States() => Rows.Select(r => r.ToState()).ToList();

        private void ApplyStates(IReadOnlyList<SubstanceRowState> states)
        {
            var bySlug = states.ToDictionary(s => s.Category + "/" + s.Slug, s => s.IsSelected, StringComparer.OrdinalIgnoreCase);
            foreach (var r in Rows)
            {
                if (bySlug.TryGetValue(r.Category + "/" + r.Entry.Slug, out bool sel) && r.IsSelected != sel)
                {
                    r.SelectionChanged = null;
                    r.IsSelected = sel;
                    r.SelectionChanged = OnRowSelectionChanged;
                }
            }
            RefreshCategoryStates();
            NotifyCounts();
        }

        private void OnRowSelectionChanged()
        {
            RefreshCategoryStates();
            NotifyCounts();
        }

        private void RefreshCategoryStates()
        {
            var states = States();
            foreach (var c in Categories) c.SetFromRows(SubstanceSelectionPolicy.CategoryState(states, c.Name));
        }

        private void NotifyCounts()
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(SelectedInDocumentCount));
            OnPropertyChanged(nameof(Footer));
            OnPropertyChanged(nameof(CanRun));
        }
    }
}
```

- [ ] **Step 5: View**

`src/Views/SubstanceBatchView.xaml.cs`:
```csharp
using System;
using LECG.ViewModels;
using LECG.Views.Base;

namespace LECG.Views
{
    public partial class SubstanceBatchView : LecgWindow
    {
        public SubstanceBatchView(SubstanceBatchViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            InitializeComponent();
            DataContext = viewModel;
            BindDialogClose(viewModel, () => viewModel.ShouldRun);
        }
    }
}
```

`src/Views/SubstanceBatchView.xaml` (uses the same theme resources as `PbrMaterialCreatorView.xaml`; a chip is a small bordered TextBlock whose background switches on the bool):
```xml
<base:LecgWindow x:Class="LECG.Views.SubstanceBatchView"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:base="clr-namespace:LECG.Views.Base"
    xmlns:icons="clr-namespace:LECG.Utils"
    Title="SUBSTANCE BATCH MATERIALS"
    Width="1100" Height="760" MinWidth="900" MinHeight="600"
    UseLayoutRounding="True" SnapsToDevicePixels="True"
    WindowIcon="{x:Static icons:Icons.Material}">

    <base:LecgWindow.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="pack://application:,,,/LECG;component/src/Resources/Themes/LecgTheme.xaml"/>
            </ResourceDictionary.MergedDictionaries>
            <BooleanToVisibilityConverter x:Key="BoolToVis"/>
            <Style x:Key="ChipStyle" TargetType="Border">
                <Setter Property="CornerRadius" Value="3"/>
                <Setter Property="Padding" Value="5,1"/>
                <Setter Property="Margin" Value="0,0,4,0"/>
                <Setter Property="BorderThickness" Value="1"/>
                <Setter Property="BorderBrush" Value="{DynamicResource LecgBorderDark}"/>
                <Setter Property="Background" Value="Transparent"/>
            </Style>
            <Style x:Key="ChipText" TargetType="TextBlock">
                <Setter Property="FontSize" Value="10"/>
                <Setter Property="FontWeight" Value="Bold"/>
                <Setter Property="Foreground" Value="{DynamicResource LecgTextMuted}"/>
            </Style>
            <DataTemplate x:Key="ChipOn">
                <Border Style="{StaticResource ChipStyle}" Background="{DynamicResource LecgBorderDark}">
                    <TextBlock Style="{StaticResource ChipText}" Foreground="{DynamicResource LecgSurfaceBackground}" Text="{Binding}"/>
                </Border>
            </DataTemplate>
        </ResourceDictionary>
    </base:LecgWindow.Resources>

    <Grid Margin="{DynamicResource ViewContentMargin}">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- HEADER -->
        <StackPanel Grid.Row="0" Margin="{DynamicResource HeaderMarginSmall}">
            <TextBlock Text="Substance Batch Materials" FontWeight="{DynamicResource WeightBold}" FontSize="{DynamicResource FontSizeTitle}" Foreground="{DynamicResource LecgTextPrimary}" Margin="{DynamicResource TitleMarginSmall}"/>
            <TextBlock Text="{Binding ScanSummary}" FontSize="{DynamicResource FontSizeSmall}" Foreground="{DynamicResource LecgTextSecondary}"/>
        </StackPanel>

        <!-- SETTINGS BAR -->
        <Grid Grid.Row="1" Margin="0,0,0,10">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="12"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="12"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="12"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>

            <TextBlock Grid.Row="0" Grid.Column="0" Text="Library root" Foreground="{DynamicResource LecgTextSecondary}" FontSize="11" Margin="0,0,0,4"/>
            <TextBox Grid.Row="1" Grid.Column="0" Text="{Binding LibraryRoot, UpdateSourceTrigger=PropertyChanged}" Height="32" VerticalContentAlignment="Center"/>
            <Button Grid.Row="1" Grid.Column="1" Content="..." Command="{Binding BrowseLibraryCommand}" Width="32" Height="32" Margin="4,0,0,0" Style="{DynamicResource SecondaryButtonStyle}"/>

            <TextBlock Grid.Row="0" Grid.Column="3" Text="Baked texture output" Foreground="{DynamicResource LecgTextSecondary}" FontSize="11" Margin="0,0,0,4"/>
            <TextBox Grid.Row="1" Grid.Column="3" Text="{Binding OutputRoot, UpdateSourceTrigger=PropertyChanged}" Height="32" VerticalContentAlignment="Center"/>
            <Button Grid.Row="1" Grid.Column="4" Content="..." Command="{Binding BrowseOutputCommand}" Width="32" Height="32" Margin="4,0,0,0" Style="{DynamicResource SecondaryButtonStyle}"/>

            <TextBlock Grid.Row="0" Grid.Column="6" Text="Resolution" Foreground="{DynamicResource LecgTextSecondary}" FontSize="11" Margin="0,0,0,4"/>
            <ComboBox Grid.Row="1" Grid.Column="6" ItemsSource="{Binding ResolutionChoices}" SelectedItem="{Binding TargetSize}" Width="80" Height="32" VerticalContentAlignment="Center"/>

            <TextBlock Grid.Row="0" Grid.Column="8" Text="Size (mm)" Foreground="{DynamicResource LecgTextSecondary}" FontSize="11" Margin="0,0,0,4"/>
            <TextBox Grid.Row="1" Grid.Column="8" Text="{Binding SizeMillimeters, UpdateSourceTrigger=PropertyChanged}" Width="80" Height="32" VerticalContentAlignment="Center"/>
        </Grid>

        <!-- BODY -->
        <Grid Grid.Row="2">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="230"/>
                <ColumnDefinition Width="12"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- CATEGORIES -->
            <Border Grid.Column="0" Background="{DynamicResource LecgSurfaceBackground}" BorderBrush="{DynamicResource LecgBorderDark}" BorderThickness="1" CornerRadius="4" Padding="10">
                <DockPanel>
                    <TextBlock DockPanel.Dock="Top" Text="CATEGORIES" Style="{DynamicResource SectionHeaderStyle}" Margin="0,0,0,8"/>
                    <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="0,0,0,8">
                        <Button Content="All" Command="{Binding SelectAllCommand}" Padding="10,2" FontSize="11" Style="{DynamicResource SecondaryButtonStyle}" Margin="0,0,6,0"/>
                        <Button Content="None" Command="{Binding SelectNoneCommand}" Padding="10,2" FontSize="11" Style="{DynamicResource SecondaryButtonStyle}"/>
                    </StackPanel>
                    <ScrollViewer VerticalScrollBarVisibility="Auto">
                        <ItemsControl ItemsSource="{Binding Categories}">
                            <ItemsControl.ItemTemplate>
                                <DataTemplate>
                                    <CheckBox Content="{Binding Label}" IsChecked="{Binding IsChecked}" IsThreeState="True" Margin="0,2" Foreground="{DynamicResource LecgTextPrimary}"/>
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>
                    </ScrollViewer>
                </DockPanel>
            </Border>

            <!-- MATERIALS -->
            <Border Grid.Column="2" Background="{DynamicResource LecgSurfaceBackground}" BorderBrush="{DynamicResource LecgBorderDark}" BorderThickness="1" CornerRadius="4" Padding="10">
                <DockPanel>
                    <Grid DockPanel.Dock="Top" Margin="0,0,0,8">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="Auto"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="MATERIALS" Style="{DynamicResource SectionHeaderStyle}" VerticalAlignment="Center" Margin="0,0,12,0"/>
                        <TextBox Grid.Column="1" Text="{Binding FilterText, UpdateSourceTrigger=PropertyChanged}" Height="28" VerticalContentAlignment="Center" ToolTip="Filter by name or category"/>
                    </Grid>

                    <ListView ItemsSource="{Binding RowsView}"
                              VirtualizingPanel.IsVirtualizing="True"
                              VirtualizingPanel.VirtualizationMode="Recycling"
                              ScrollViewer.HorizontalScrollBarVisibility="Disabled"
                              BorderThickness="0" Background="Transparent">
                        <ListView.View>
                            <GridView>
                                <GridViewColumn Width="34">
                                    <GridViewColumn.CellTemplate>
                                        <DataTemplate>
                                            <CheckBox IsChecked="{Binding IsSelected, UpdateSourceTrigger=PropertyChanged}" HorizontalAlignment="Center"/>
                                        </DataTemplate>
                                    </GridViewColumn.CellTemplate>
                                </GridViewColumn>
                                <GridViewColumn Header="Name" Width="300" DisplayMemberBinding="{Binding DisplayName}"/>
                                <GridViewColumn Header="Category" Width="130" DisplayMemberBinding="{Binding Category}"/>
                                <GridViewColumn Header="Maps" Width="230">
                                    <GridViewColumn.CellTemplate>
                                        <DataTemplate>
                                            <StackPanel Orientation="Horizontal">
                                                <Border Style="{StaticResource ChipStyle}" Background="{DynamicResource LecgBorderDark}"><TextBlock Style="{StaticResource ChipText}" Text="BC" Foreground="{DynamicResource LecgSurfaceBackground}"/></Border>
                                                <Border Style="{StaticResource ChipStyle}" Background="{DynamicResource LecgBorderDark}"><TextBlock Style="{StaticResource ChipText}" Text="N" Foreground="{DynamicResource LecgSurfaceBackground}"/></Border>
                                                <Border Style="{StaticResource ChipStyle}" Background="{DynamicResource LecgBorderDark}"><TextBlock Style="{StaticResource ChipText}" Text="R" Foreground="{DynamicResource LecgSurfaceBackground}"/></Border>
                                                <Border>
                                                    <Border.Style>
                                                        <Style TargetType="Border" BasedOn="{StaticResource ChipStyle}">
                                                            <Style.Triggers><DataTrigger Binding="{Binding HasMetal}" Value="True"><Setter Property="Background" Value="{DynamicResource LecgBorderDark}"/></DataTrigger></Style.Triggers>
                                                        </Style>
                                                    </Border.Style>
                                                    <TextBlock Style="{StaticResource ChipText}" Text="M"/>
                                                </Border>
                                                <Border>
                                                    <Border.Style>
                                                        <Style TargetType="Border" BasedOn="{StaticResource ChipStyle}">
                                                            <Style.Triggers><DataTrigger Binding="{Binding HasAo}" Value="True"><Setter Property="Background" Value="{DynamicResource LecgBorderDark}"/></DataTrigger></Style.Triggers>
                                                        </Style>
                                                    </Border.Style>
                                                    <TextBlock Style="{StaticResource ChipText}" Text="AO"/>
                                                </Border>
                                                <Border>
                                                    <Border.Style>
                                                        <Style TargetType="Border" BasedOn="{StaticResource ChipStyle}">
                                                            <Style.Triggers><DataTrigger Binding="{Binding HasOpacity}" Value="True"><Setter Property="Background" Value="{DynamicResource LecgBorderDark}"/></DataTrigger></Style.Triggers>
                                                        </Style>
                                                    </Border.Style>
                                                    <TextBlock Style="{StaticResource ChipText}" Text="OP"/>
                                                </Border>
                                            </StackPanel>
                                        </DataTemplate>
                                    </GridViewColumn.CellTemplate>
                                </GridViewColumn>
                                <GridViewColumn Header="Status" Width="80" DisplayMemberBinding="{Binding Status}"/>
                            </GridView>
                        </ListView.View>
                    </ListView>
                </DockPanel>
            </Border>
        </Grid>

        <!-- OPTIONS + FOOTER -->
        <Grid Grid.Row="3" Margin="0,10,0,0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="16"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <CheckBox Grid.Column="0" Content="Overwrite appearance of existing materials" IsChecked="{Binding OverwriteExisting}" Foreground="{DynamicResource LecgTextPrimary}"/>
            <CheckBox Grid.Column="2" Content="Force re-bake textures" IsChecked="{Binding ForceRebake}" Foreground="{DynamicResource LecgTextPrimary}"/>
            <TextBlock Grid.Column="3" Text="{Binding Footer}" HorizontalAlignment="Right" VerticalAlignment="Center" Foreground="{DynamicResource LecgTextSecondary}" FontSize="11"/>
        </Grid>

        <!-- BUTTONS -->
        <Grid Grid.Row="4" Margin="{DynamicResource ButtonRowMarginSmall}">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="12"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <Button Content="Cancel" Grid.Column="0" Height="{DynamicResource ButtonHeight}" Command="{Binding CancelCommand}" Style="{DynamicResource SecondaryButtonStyle}"/>
            <Button Content="Create Materials" Grid.Column="2" Height="{DynamicResource ButtonHeight}" Command="{Binding ApplyCommand}" IsEnabled="{Binding CanRun}" Style="{DynamicResource AccentButtonStyle}"/>
        </Grid>
    </Grid>
</base:LecgWindow>
```

Chip rule: the always-on chips (BC, N, R) set `Style` and `Background` as attributes. The conditional chips (M, AO, OP) must NOT set the `Style` attribute; they carry a `Border.Style` element with a `DataTrigger`, because a Border cannot set `Style` both ways. The text colour on conditional chips stays muted when hollow; when filled, the muted text on the dark background is still legible at 10 px bold. The unused `ChipOn` DataTemplate in resources can be deleted.

- [ ] **Step 6: Register VM and View**

In `src/Core/Bootstrapper.cs` add `services.AddTransient<SubstanceBatchViewModel>();` next to `PbrMaterialCreatorViewModel` and `services.AddTransient<Views.SubstanceBatchView>();` next to `Views.PbrMaterialCreatorView`.

- [ ] **Step 7: Build**

Run: `$DOTNET build LECG.csproj -p:RevitVersion=2026 -p:SkipRevitDeploy=true -c Debug`
Expected: Build succeeded (XAML compiles). If `Icons.Material` does not exist in `src/Utils/Icons.cs`, use `Icons.Home` and note it.

- [ ] **Step 8: Commit**

```bash
git add src/Models/SubstanceBatchSettings.cs src/ViewModels/SubstanceMaterialRowViewModel.cs src/ViewModels/SubstanceCategoryViewModel.cs src/ViewModels/SubstanceBatchViewModel.cs src/Views/SubstanceBatchView.xaml src/Views/SubstanceBatchView.xaml.cs src/Core/Bootstrapper.cs
git commit -m "feat: substance batch window (categories, rows with map chips, settings)

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---
### Task 12: Batch command, log-window cancel, ribbon button (LECG)

**Files:**
- Modify: `src/ViewModels/LogViewModel.cs` (add `IsCancelRequested`, `CancelCommand`)
- Modify: `src/Views/LogView.xaml` (add Cancel button, line ~173)
- Create: `src/Commands/SubstanceBatchCommand.cs`
- Modify: `src/Configuration/UIConstants.cs` (three constants)
- Modify: `src/Core/Ribbon/RibbonService.cs` (button in the Visualization panel after Material Creator, line ~215)

**Interfaces:**
- Consumes: `SubstanceBatchViewModel.SelectedEntries/CurrentSettings/SetExistingNames/ShouldRun` (Task 11), `ISubstanceMaterialCreateService` (Task 8), `SubstanceBatchOptions/BakeOptions/TextureTransform/SubstanceBatchResult`.
- Produces: `LogViewModel.IsCancelRequested` (bool, observable), `LogViewModel.CancelCommand`.

- [ ] **Step 1: Cancel support in the log window**

In `src/ViewModels/LogViewModel.cs` add:
```csharp
        private bool _isCancelRequested;
        public bool IsCancelRequested { get => _isCancelRequested; set => SetProperty(ref _isCancelRequested, value); }

        private bool _canCancel;
        public bool CanCancel { get => _canCancel; set => SetProperty(ref _canCancel, value); }

        public ICommand CancelCommand { get; }
```
and in the constructor: `CancelCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { IsCancelRequested = true; CanCancel = false; _logger.Log("Cancel requested. Finishing current material..."); });`

In `src/Views/LogView.xaml`, the button row grid at line ~170: add a third column and a button between Copy and Close:
```xml
<Button Grid.Column="1" Command="{Binding CancelCommand}" Content="Cancel" Style="{DynamicResource SecondaryButtonStyle}" Height="32" Padding="12,0" Margin="8,0,0,0"
        Visibility="{Binding CanCancel, Converter={StaticResource BoolToVis}}"/>
```
Make sure a `BooleanToVisibilityConverter x:Key="BoolToVis"` exists in that window's resources (add one if not). Adjust the Close button's `Grid.Column` if the grid used `*`/`Auto` columns; keep Copy left, Cancel middle, Close right.

- [ ] **Step 2: UI constants**

In `src/Configuration/UIConstants.cs` after `ButtonMaterialCreator_Tooltip` add:
```csharp
        public const string ButtonSubstanceBatch_Name = "btnSubstanceBatch";
        public const string ButtonSubstanceBatch_Text = "Substance\nBatch";
        public const string ButtonSubstanceBatch_Tooltip = "Create Advanced Opaque materials in batch from a Substance bake library (basecolor, normal, roughness, metallic, AO).";
```

- [ ] **Step 3: Ribbon button**

In `src/Core/Ribbon/RibbonService.cs`, right after the `ButtonMaterialCreator` `CreateButton` call:
```csharp
            RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
                UIConstants.ButtonSubstanceBatch_Name,
                UIConstants.ButtonSubstanceBatch_Text,
                "LECG.Commands.SubstanceBatchCommand",
                UIConstants.ButtonSubstanceBatch_Tooltip,
                AppImages.Palette
            ), assemblyPath, availability);
```

- [ ] **Step 4: Command**

`src/Commands/SubstanceBatchCommand.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Windows.Threading;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Core.Substance;
using LECG.Models;
using LECG.Services.Interfaces;
using LECG.ViewModels;
using LECG.Views;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class SubstanceBatchCommand : RevitCommand
    {
        protected override string? TransactionName => null;

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            var createService = ServiceLocator.GetRequiredService<ISubstanceMaterialCreateService>();
            var viewModel = ServiceLocator.GetRequiredService<SubstanceBatchViewModel>();
            viewModel.SetExistingNames(createService.ExistingMaterialNames(doc));

            var view = ServiceLocator.CreateWith<SubstanceBatchView>(viewModel);
            view.Initialize(uiDoc);
            bool? dialogResult = view.ShowDialog();
            if (dialogResult != true || !viewModel.ShouldRun)
            {
                return;
            }

            IReadOnlyList<SubstanceMaterialEntry> entries = viewModel.SelectedEntries();
            SubstanceBatchSettings settings = viewModel.CurrentSettings();
            string outputRoot = string.IsNullOrWhiteSpace(settings.OutputRoot)
                ? BakeOutputPaths.DefaultOutputRoot(settings.LibraryRoot)
                : settings.OutputRoot;

            var options = new SubstanceBatchOptions(
                new BakeOptions(outputRoot, settings.TargetSize, settings.ForceRebake),
                TextureTransform.Uniform(settings.SizeMillimeters),
                settings.OverwriteExisting);

            ShowLogWindow($"Substance Batch: {entries.Count} material{(entries.Count == 1 ? "" : "s")}");
            if (_logViewModel != null) _logViewModel.CanCancel = true;

            foreach (string warning in viewModel.Warnings)
            {
                Log($"SCAN WARNING: {warning}");
            }

            var result = new SubstanceBatchResult();
            for (int i = 0; i < entries.Count; i++)
            {
                if (_logViewModel?.IsCancelRequested == true)
                {
                    Log($"CANCELLED after {i} of {entries.Count}.");
                    break;
                }

                SubstanceMaterialEntry entry = entries[i];
                UpdateProgress((double)i / entries.Count * 100, $"{i + 1}/{entries.Count}  {entry.DisplayName}");
                Log($"[{i + 1}/{entries.Count}] {entry.Category} / {entry.DisplayName}");

                result.Add(createService.Create(doc, entry, options, Log));
                PumpUi();
            }

            if (_logViewModel != null) _logViewModel.CanCancel = false;
            UpdateProgress(100, "Complete");
            Log($"COMPLETE: {result.Summary}");

            int shown = 0;
            foreach (SubstanceMaterialReport r in result.Reports)
            {
                if (r.Outcome != SubstanceMaterialOutcome.Failed) continue;
                Log($"  FAILED {r.DisplayName}: {r.Error}");
                if (++shown >= 10) { Log("  ... more failures omitted"); break; }
            }
        }

        /// <summary>Lets the log window repaint and receive the Cancel click while this command owns the UI thread.</summary>
        private static void PumpUi()
        {
            Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
            dispatcher?.Invoke(() => { }, DispatcherPriority.Background);
        }
    }
}
```

Note: `_logViewModel` is a `protected` field on `RevitCommand`. `SubstanceBatchViewModel` is resolved from DI (transient) so `IMetallicProbeService` is injected; `CreateWith<SubstanceBatchView>(viewModel)` matches the `PbrMaterialCreatorCommand` pattern.

- [ ] **Step 5: Build, deploy, smoke**

Run: `$DOTNET build LECG.csproj -p:RevitVersion=2026 -c Debug`. Restart Revit. Expected: LECG tab shows "Substance Batch" beside "PBR Material". Open it: header reads `534 materials in 28 categories`, category list has 28 entries, footer `534 selected · 0 already in document`. Click None, tick Ceiling, footer `2 selected`. Filter `oak` shows only wood rows. Create → log window with Cancel button; two materials created; Material Browser shows `Ceiling Perforated Tiles` and `Composite Ceiling Tiles`, class `Ceiling`, description `Ceiling / ceiling_perforated_tiles / Substance 4K bake`, appearance Advanced Opaque with Base Color, Roughness, Normal images and (perforated) Cutout. Open again: both rows show `In doc`; run again without Overwrite → 2 skipped.

- [ ] **Step 6: Commit**

```bash
git add src/ViewModels/LogViewModel.cs src/Views/LogView.xaml src/Commands/SubstanceBatchCommand.cs src/Configuration/UIConstants.cs src/Core/Ribbon/RibbonService.cs
git commit -m "feat: Substance Batch command, ribbon button, log window cancel

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 13: Appearance-asset dump diagnostic (LECG)

**Files:**
- Create: `src/Commands/DumpAppearanceAssetCommand.cs`
- Modify: `src/Configuration/UIConstants.cs`, `src/Core/Ribbon/RibbonService.cs`

**Interfaces:**
- Consumes: Revit API only. No new services.

- [ ] **Step 1: Command**

`src/Commands/DumpAppearanceAssetCommand.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using LECG.Core;

namespace LECG.Commands
{
    /// <summary>
    /// Diagnostic: logs the appearance-asset property tree of the material on a picked face,
    /// plus the names of all library appearance assets. Used to confirm schema/property names.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class DumpAppearanceAssetCommand : RevitCommand
    {
        protected override string? TransactionName => null;

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            ShowLogWindow("Appearance Asset Dump");

            Log("== Library appearance assets ==");
            IList<Asset> library = doc.Application.GetAssets(AssetType.Appearance);
            foreach (var group in library.GroupBy(a => a.Name).OrderBy(g => g.Key))
            {
                string schema = (group.First().FindByName("BaseSchema") as AssetPropertyString)?.Value ?? "?";
                Log($"  {group.Key}  x{group.Count()}  BaseSchema={schema}");
            }

            Reference? picked;
            try
            {
                picked = uiDoc.Selection.PickObject(ObjectType.Face, "Pick a face to dump its material appearance");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                Log("Pick cancelled.");
                return;
            }

            Element? element = doc.GetElement(picked);
            if (element?.GetGeometryObjectFromReference(picked) is not Face face)
            {
                Log("Not a face.");
                return;
            }

            Material? material = doc.GetElement(face.MaterialElementId) as Material;
            if (material == null)
            {
                Log("Face has no material.");
                return;
            }

            Log($"== Material '{material.Name}'  class='{material.MaterialClass}' category='{material.MaterialCategory}' ==");
            if (doc.GetElement(material.AppearanceAssetId) is not AppearanceAssetElement aae)
            {
                Log("No appearance asset.");
                return;
            }

            Asset asset = aae.GetRenderingAsset();
            Log($"Appearance asset '{aae.Name}'  Asset.Name='{asset.Name}'  BaseSchema='{(asset.FindByName("BaseSchema") as AssetPropertyString)?.Value}'");
            Dump(asset, 1);
        }

        private void Dump(Asset asset, int depth)
        {
            string pad = new string(' ', depth * 2);
            for (int i = 0; i < asset.Size; i++)
            {
                AssetProperty p = asset[i];
                string value = Describe(p);
                Log($"{pad}{p.Name}  [{p.Type}]  {value}");

                Asset? connected = p.GetSingleConnectedAsset();
                if (connected != null)
                {
                    Log($"{pad}  -> connected asset '{connected.Name}'  BaseSchema='{(connected.FindByName("BaseSchema") as AssetPropertyString)?.Value}'");
                    Dump(connected, depth + 2);
                }
            }
        }

        private static string Describe(AssetProperty p)
        {
            return p switch
            {
                AssetPropertyString s => $"\"{s.Value}\"",
                AssetPropertyBoolean b => b.Value.ToString(),
                AssetPropertyInteger n => n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                AssetPropertyEnum e => e.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                AssetPropertyDouble d => d.Value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture),
                AssetPropertyFloat f => f.Value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture),
                AssetPropertyDistance dist => $"{dist.Value:0.####} ft",
                AssetPropertyDoubleArray4d c => string.Join(",", c.GetValueAsDoubles().Select(v => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))),
                _ => string.Empty,
            };
        }
    }
}
```

- [ ] **Step 2: Button**

`UIConstants.cs`:
```csharp
        public const string ButtonDumpAsset_Name = "btnDumpAsset";
        public const string ButtonDumpAsset_Text = "Dump\nAsset";
        public const string ButtonDumpAsset_Tooltip = "Diagnostic: log the appearance asset tree of a picked face's material.";
```
`RibbonService.cs`, after the Substance Batch button:
```csharp
            RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
                UIConstants.ButtonDumpAsset_Name,
                UIConstants.ButtonDumpAsset_Text,
                "LECG.Commands.DumpAppearanceAssetCommand",
                UIConstants.ButtonDumpAsset_Tooltip,
                AppImages.SearchReplace
            ), assemblyPath, availability);
```

- [ ] **Step 3: Build, deploy, run**

Run: `$DOTNET build LECG.csproj -p:RevitVersion=2026 -c Debug`. In Revit, place a wall, assign the Autodesk library material **"Metal - Mesh"** (or any Autodesk material whose appearance shows a Normal image; search "mesh" in the Material Browser library). Run Dump Asset, pick the wall face. Record in `docs/superpowers/specs/2026-09-04-substance-batch-pbr-design.md` under a new heading `## Runtime findings (Task 13)`:
  - the library asset name and `BaseSchema` string for the Advanced Opaque template
  - the connected asset `Name`/`BaseSchema` under `surface_normal` (expected `BumpMap`) and its `bumpmap_Type` value
  If the observed schema differs from `BumpMap`, change `MaterialBitmapPropertyService.BumpMapSchema` and, if needed, `AdvancedAppearanceAssetService.IsOpaqueSchema`, then re-run Task 12's smoke.

- [ ] **Step 4: Commit**

```bash
git add src/Commands/DumpAppearanceAssetCommand.cs src/Configuration/UIConstants.cs src/Core/Ribbon/RibbonService.cs docs/superpowers/specs/2026-09-04-substance-batch-pbr-design.md
git commit -m "feat: appearance asset dump diagnostic; record runtime schema findings

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 14: End-to-end verification and memory update

**Files:**
- Modify: `CLAUDE.md` (one bullet under the Visualization panel line: "Substance Batch — batch Advanced Opaque materials from `C:\LECG\SubstanceBakes`; spec in `docs/superpowers/specs/2026-09-04-substance-batch-pbr-design.md`")

- [ ] **Step 1: Full test run**

Run: `$DOTNET test LECG.Tests/LECG.Tests.csproj -p:RevitVersion=2026 -p:SkipRevitDeploy=true`
Expected: 0 failed. Then simulate CI: `$DOTNET build LECG.Core/LECG.Core.csproj -c Release /p:TreatWarningsAsErrors=true` — 0 warnings.

- [ ] **Step 2: Render check**

In Revit, with the two Ceiling materials from Task 12 applied to a wall and a floor, Realistic view: perforations visible as cutouts on the perforated tile, relief reads correctly (holes look recessed, not embossed). If relief looks inverted, the OpenGL assumption was wrong: flip `PbrBakeMath.InvertGreen` out of `PbrTextureBakeService.Bake` (one line) and re-bake with Force. Record the outcome in the spec's `## Runtime findings` section.

- [ ] **Step 3: Full category run**

Run Substance Batch on **Metal** (28 materials, exercises the F0 path) with default settings. Expected: `28 created, 0 failed`, total wall time under 3 minutes, `_revit\Metal\*` folders contain `*_f0.png` for brushed/polished metals. Log any failures with their message into the spec's runtime findings.

- [ ] **Step 4: Update CLAUDE.md and commit**

```bash
git add CLAUDE.md docs/superpowers/specs/2026-09-04-substance-batch-pbr-design.md
git commit -m "docs: Substance Batch in CLAUDE.md; runtime findings

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

- [ ] **Step 5: Merge readiness**

`git log --oneline codex/review..HEAD` should list 14 commits. Do not merge; hand back to the user with the branch name.

---

## Self-review notes

- Spec §1 → Task 1-2. §2 → Task 3-5. §3 → Task 6-7. §4 → Task 8. §5 → Task 9. §6 → Task 10-12. §7 → Task 13. Testing section → Tasks 1-5, 8, 10 (Core, CI) and Task 5 (Windows, local). Error handling → Task 2 (warnings), Task 5/8 (per-material catch), Task 7 (template not found throws before first material because `FindTemplate` runs inside the first `Create`; the first report will be Failed with the asset-name list, and every following material fails the same way — acceptable, the summary shows N failed with the same message).
- Spec said bake runs on a background Task. Plan runs it synchronously on the Revit thread with `PumpUi()` between materials, because the command must block anyway and a blocking await adds nothing. Cancel still works via the pump.
- Type names used consistently: `SubstanceMaterialEntry`, `BakedTextureSet`, `BakeOptions`, `TextureTransform`, `SubstanceBatchOptions`, `SubstanceMaterialReport`, `SubstanceBatchResult`, `SubstanceRowState`, `SubstanceBatchSettings`.
