using System.Security.Cryptography;
using System.Text.Json;

namespace KnowledgeLab;

internal static class RuntimeValidationExport
{
    internal static void Write(string sourcePath, string destination)
    {
        using var source = JsonDocument.Parse(File.ReadAllText(sourcePath));
        JsonElement root = source.RootElement;
        if (root.GetProperty("status").GetString() != "completed" || root.GetProperty("error").ValueKind != JsonValueKind.Null)
            throw new InvalidDataException("Only a completed campaign with successful cleanup may supply runtime evidence.");
        var rows = root.GetProperty("operations").EnumerateArray().GroupBy(r => r.GetProperty("operation").GetString()!)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        var entries = root.GetProperty("installed_catalog").GetProperty("api").EnumerateArray().Select(binding =>
        {
            string operation = binding.GetProperty("operation").GetString()!;
            string kind = binding.GetProperty("kind").GetString()!;
            JsonElement[] attempts = rows.GetValueOrDefault(operation, []);
            int Count(string status) => attempts.Where(a => a.GetProperty("status").GetString() == status)
                .Select(a => a.GetProperty("model").GetString()).Distinct().Count();
            int changed = attempts.Where(a => a.GetProperty("status").GetString() == "passed" &&
                a.TryGetProperty("verification", out var v) && v.GetString()!.StartsWith("Changed-value", StringComparison.Ordinal)).Count();
            string state = kind == "read" ? Count("passed") > 0 ? "read_invoked" : Count("failed") > 0 ? "read_failed" : "unsupported_context"
                : changed > 0 ? "changed_value_tested" : Count("passed") > 0 ? "reviewed_fixture_passed"
                : Count("roundtrip_only") > 0 ? "same_value_only" : Count("context_rejected") > 0 ? "context_rejected"
                : Count("missing_fixture") > 0 ? "missing_fixture" : "not_tested";
            return new { operation, state, passed_models = Count("passed"), context_failures = Count("failed") + Count("context_rejected"),
                unsupported_models = Count("unsupported") + Count("missing_fixture"), same_value_models = Count("roundtrip_only") };
        }).OrderBy(e => e.operation, StringComparer.Ordinal).ToArray();
        if (entries.Length != 2216 || entries.Select(e => e.operation).Distinct().Count() != entries.Length)
            throw new InvalidDataException("The runtime evidence must account for all 2,216 API bindings.");
        var pack = new { schema_version = 1, revit_version = 2026, campaign = Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(sourcePath))),
            source_sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath))),
            scope = "Disposable Autodesk project samples; links unloaded. Evidence describes tested contexts, not universal correctness or permission to modify.", entries };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        File.WriteAllText(destination, JsonSerializer.Serialize(pack, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(entries.GroupBy(e => e.state).Select(g => new { state = g.Key, count = g.Count() })));
    }
}
