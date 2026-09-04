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
