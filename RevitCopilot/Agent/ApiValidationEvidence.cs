using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace LECG.RevitCopilot.Agent;

internal sealed record ApiValidationEntry(string Operation, string State, int PassedModels, int ContextFailures, int UnsupportedModels, int SameValueModels);
internal sealed record ApiValidationPack(int SchemaVersion, int RevitVersion, string Campaign, string SourceSha256, string Scope, ApiValidationEntry[] Entries);
internal sealed record ApiProjectContext(string Discipline, string Model, string ModelSha256, string Status, string? Reason,
    bool CompoundWorkflowValidated, string? RequiredWorkflow);
internal sealed record ApiValidationContextEntry(string Operation, ApiProjectContext[] Contexts);
internal sealed record ApiValidationContextPack(int SchemaVersion, int RevitVersion, string Campaign,
    string ValidationLedgerSha256, string SourceReceiptsSha256, string Scope, int ReceiptCount, int WorkflowReceiptCount,
    ApiValidationContextEntry[] Entries);
internal sealed record ApiProjectPresenceModel(string Discipline, string Model, string ModelSha256, int ElementCount);
internal sealed record ApiProjectPresenceEntry(string Operation, int[] Counts);
internal sealed record ApiProjectPresencePack(int SchemaVersion, int RevitVersion, string SetterInventorySha256,
    string SourceReceiptsSha256, string Scope, int OperationCount, ApiProjectPresenceModel[] Models,
    ApiProjectPresenceEntry[] Entries);

internal static class ApiValidationEvidence
{
    private static readonly Lazy<byte[]> PackBytes = new(() => ReadResource("LECG.Revit2026.Validation",
        "The installed API validation ledger is missing. Reinstall the tested Copilot bundle."));
    private static readonly Lazy<ApiValidationPack> Pack = new(() =>
    {
        var pack = JsonSerializer.Deserialize<ApiValidationPack>(PackBytes.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true })!;
        if (pack.SchemaVersion != 1 || pack.RevitVersion != 2026 || pack.Entries.Length != 2216 || pack.Entries.DistinctBy(e => e.Operation).Count() != pack.Entries.Length)
            throw new InvalidDataException("Invalid API validation ledger.");
        return pack;
    });
    private static readonly Lazy<IReadOnlyDictionary<string, ApiValidationEntry>> Index = new(() => Pack.Value.Entries.ToDictionary(e => e.Operation, StringComparer.Ordinal));
    private static readonly Lazy<IReadOnlyDictionary<string, ApiValidationContextEntry>> Contexts = new(() =>
    {
        byte[] bytes = ReadResource("LECG.Revit2026.ValidationContexts",
            "The installed API project-context evidence is missing. Reinstall the tested Copilot bundle.");
        var pack = JsonSerializer.Deserialize<ApiValidationContextPack>(bytes, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true })!;
        string ledgerHash = Convert.ToHexString(SHA256.HashData(PackBytes.Value));
        if (pack.SchemaVersion != 1 || pack.RevitVersion != 2026 || pack.Campaign != Pack.Value.Campaign
            || pack.ValidationLedgerSha256 != ledgerHash || pack.ReceiptCount != pack.Entries.Sum(e => e.Contexts.Length)
            || pack.WorkflowReceiptCount != pack.Entries.Sum(e => e.Contexts.Count(c => c.CompoundWorkflowValidated))
            || pack.Entries.DistinctBy(e => e.Operation).Count() != pack.Entries.Length
            || pack.Entries.Any(e => !Index.Value.ContainsKey(e.Operation) || e.Contexts.Length is < 1 or > 4
                || e.Contexts.DistinctBy(c => c.ModelSha256).Count() != e.Contexts.Length
                || e.Contexts.Any(c => c.CompoundWorkflowValidated != !string.IsNullOrWhiteSpace(c.RequiredWorkflow))))
            throw new InvalidDataException("Invalid API project-context evidence.");
        return pack.Entries.ToDictionary(e => e.Operation, StringComparer.Ordinal);
    });
    private static readonly Lazy<ApiProjectPresencePack> PresencePack = new(() =>
    {
        var pack = JsonSerializer.Deserialize<ApiProjectPresencePack>(ReadResource("LECG.Revit2026.ProjectPresence",
            "The installed API project-presence evidence is missing. Reinstall the tested Copilot bundle."),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true })!;
        if (pack.SchemaVersion != 1 || pack.RevitVersion != 2026 || pack.OperationCount != 805
            || pack.Models.Length != 4 || pack.Entries.Length != pack.OperationCount
            || !pack.Models.Select(model => model.Discipline).SequenceEqual(new[] { "architecture", "topography", "structure", "mep" })
            || pack.Models.DistinctBy(model => model.ModelSha256).Count() != pack.Models.Length
            || pack.Entries.DistinctBy(e => e.Operation).Count() != pack.Entries.Length
            || pack.Entries.Any(e => !Index.Value.ContainsKey(e.Operation) || !e.Operation.StartsWith("api.set:", StringComparison.Ordinal)
                || e.Counts.Length != pack.Models.Length || e.Counts.Any(count => count < 0)))
            throw new InvalidDataException("Invalid API project-presence evidence.");
        return pack;
    });
    private static readonly Lazy<IReadOnlyDictionary<string, ApiProjectPresenceEntry>> Presence = new(() =>
        PresencePack.Value.Entries.ToDictionary(e => e.Operation, StringComparer.Ordinal));
    private static byte[] ReadResource(string name, string error)
    {
        using Stream source = typeof(ApiValidationEvidence).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidDataException(error);
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        return buffer.ToArray();
    }
    internal static int Count => Index.Value.Count;
    internal static int ContextOperationCount => Contexts.Value.Count;
    internal static int PresenceOperationCount => Presence.Value.Count;
    private static object[] ProjectContexts(string operation) =>
        (Contexts.Value.GetValueOrDefault(operation)?.Contexts ?? []).Select(context => (object)new
        {
            discipline = context.Discipline,
            model = context.Model,
            model_sha256 = context.ModelSha256,
            status = context.Status,
            reason = context.Reason,
            compound_workflow_validated = context.CompoundWorkflowValidated,
            required_workflow = context.RequiredWorkflow
        }).ToArray();
    private static object[] ProjectTargetPresence(string operation)
    {
        if (!Presence.Value.TryGetValue(operation, out var entry)) return [];
        return PresencePack.Value.Models.Select((model, index) => (object)new
        {
            discipline = model.Discipline,
            model = model.Model,
            model_sha256 = model.ModelSha256,
            element_count = model.ElementCount,
            target_count = entry.Counts[index]
        }).ToArray();
    }
    internal static object For(string operation) => Index.Value.TryGetValue(operation, out var entry)
        ? new { state = entry.State, passed_models = entry.PassedModels, context_failures = entry.ContextFailures,
            unsupported_models = entry.UnsupportedModels, same_value_models = entry.SameValueModels,
            project_contexts = ProjectContexts(operation), project_target_presence = ProjectTargetPresence(operation),
            campaign = Pack.Value.Campaign }
        : new { state = "not_tested", passed_models = 0, context_failures = 0, unsupported_models = 0,
            same_value_models = 0, project_contexts = Array.Empty<object>(), project_target_presence = Array.Empty<object>(),
            campaign = Pack.Value.Campaign };
}
