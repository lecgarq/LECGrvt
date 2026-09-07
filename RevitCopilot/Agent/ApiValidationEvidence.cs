using System.IO;
using System.Text.Json;

namespace LECG.RevitCopilot.Agent;

internal sealed record ApiValidationEntry(string Operation, string State, int PassedModels, int ContextFailures, int UnsupportedModels, int SameValueModels);
internal sealed record ApiValidationPack(int SchemaVersion, int RevitVersion, string Campaign, string SourceSha256, string Scope, ApiValidationEntry[] Entries);

internal static class ApiValidationEvidence
{
    private static readonly Lazy<ApiValidationPack> Pack = new(() =>
    {
        using var source = typeof(ApiValidationEvidence).Assembly.GetManifestResourceStream("LECG.Revit2026.Validation")
            ?? throw new InvalidDataException("The installed API validation ledger is missing. Reinstall the tested Copilot bundle.");
        var pack = JsonSerializer.Deserialize<ApiValidationPack>(source, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true })!;
        if (pack.SchemaVersion != 1 || pack.RevitVersion != 2026 || pack.Entries.Length != 2216 || pack.Entries.DistinctBy(e => e.Operation).Count() != pack.Entries.Length)
            throw new InvalidDataException("Invalid API validation ledger.");
        return pack;
    });
    private static readonly Lazy<IReadOnlyDictionary<string, ApiValidationEntry>> Index = new(() => Pack.Value.Entries.ToDictionary(e => e.Operation, StringComparer.Ordinal));
    internal static int Count => Index.Value.Count;
    internal static object For(string operation) => Index.Value.TryGetValue(operation, out var entry)
        ? new { state = entry.State, passed_models = entry.PassedModels, context_failures = entry.ContextFailures,
            unsupported_models = entry.UnsupportedModels, same_value_models = entry.SameValueModels, campaign = Pack.Value.Campaign }
        : new { state = "not_tested", passed_models = 0, context_failures = 0, unsupported_models = 0, same_value_models = 0, campaign = Pack.Value.Campaign };
}
