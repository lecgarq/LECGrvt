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
internal sealed record ApiProjectPresencePack(int SchemaVersion, int RevitVersion, string InventorySha256,
    string SourceReceiptsSha256, string Scope, int OperationCount, ApiProjectPresenceModel[] Models,
    ApiProjectPresenceEntry[] Entries);
internal sealed record ApiProjectReadContext(string Status, int TargetCount, int Attempts, string? Reason);
internal sealed record ApiProjectReadEntry(string Operation, ApiProjectReadContext[] Contexts);
internal sealed record ApiProjectReadPack(int SchemaVersion, int RevitVersion, string CopilotAssemblySha256,
    string SourceReceiptsSha256, string Scope, int OperationCount, int ContextCount,
    ApiProjectPresenceModel[] Models, ApiProjectReadEntry[] Entries);
internal sealed record NativeProjectReadContext(string Status, int Attempts, string? TargetKind, string? Reason);
internal sealed record NativeProjectReadEntry(string Operation, NativeProjectReadContext[] Contexts);
internal sealed record NativeProjectReadPack(int SchemaVersion, int RevitVersion, string CopilotAssemblySha256,
    string SourceReceiptsSha256, string Scope, int OperationCount, int ContextCount,
    ApiProjectPresenceModel[] Models, NativeProjectReadEntry[] Entries);
internal sealed record NativeProjectPreviewModel(string Discipline, string Model, string ModelSha256, int ElementCount,
    bool SyntheticDeleteFixtureAvailable);
internal sealed record NativeProjectPreviewContext(string Status, int Attempts, string? TargetKind,
    bool SyntheticFixtureUsed, string? Reason);
internal sealed record NativeProjectPreviewEntry(string Operation, NativeProjectPreviewContext[] Contexts);
internal sealed record NativeProjectPreviewPack(int SchemaVersion, int RevitVersion, string CopilotAssemblySha256,
    string SourceReceiptsSha256, string Scope, int OperationCount, int ContextCount,
    NativeProjectPreviewModel[] Models, NativeProjectPreviewEntry[] Entries);

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
        if (pack.SchemaVersion != 1 || pack.RevitVersion != 2026 || pack.OperationCount != 2216
            || pack.Models.Length != 4 || pack.Entries.Length != pack.OperationCount
            || !pack.Models.Select(model => model.Discipline).SequenceEqual(new[] { "architecture", "topography", "structure", "mep" })
            || pack.Models.DistinctBy(model => model.ModelSha256).Count() != pack.Models.Length
            || pack.Entries.DistinctBy(e => e.Operation).Count() != pack.Entries.Length
            || pack.Entries.Any(e => !Index.Value.ContainsKey(e.Operation)
                || e.Counts.Length != pack.Models.Length || e.Counts.Any(count => count < 0)))
            throw new InvalidDataException("Invalid API project-presence evidence.");
        return pack;
    });
    private static readonly Lazy<IReadOnlyDictionary<string, ApiProjectPresenceEntry>> Presence = new(() =>
        PresencePack.Value.Entries.ToDictionary(e => e.Operation, StringComparer.Ordinal));
    private static readonly Lazy<ApiProjectReadPack> ReadPack = new(() =>
    {
        var pack = JsonSerializer.Deserialize<ApiProjectReadPack>(ReadResource("LECG.Revit2026.ProjectReads",
            "The installed API project-read evidence is missing. Reinstall the tested Copilot bundle."),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true })!;
        string[] allowed = ["read_succeeded", "context_unsupported", "missing_fixture"];
        if (pack.SchemaVersion != 1 || pack.RevitVersion != 2026 || pack.OperationCount != 1411
            || pack.ContextCount != pack.OperationCount * 4 || pack.Models.Length != 4
            || pack.Entries.Length != pack.OperationCount
            || !pack.Models.Select(model => model.Discipline).SequenceEqual(new[] { "architecture", "topography", "structure", "mep" })
            || pack.Models.DistinctBy(model => model.ModelSha256).Count() != pack.Models.Length
            || pack.Entries.DistinctBy(e => e.Operation).Count() != pack.Entries.Length
            || pack.Entries.Any(e => !Index.Value.ContainsKey(e.Operation) || !e.Operation.StartsWith("api.get:", StringComparison.Ordinal)
                || e.Contexts.Length != pack.Models.Length || e.Contexts.Any(context => !allowed.Contains(context.Status)
                    || context.TargetCount < 0 || context.Attempts < 0
                    || context.Status == "missing_fixture" && (context.TargetCount != 0 || context.Attempts != 0)
                    || context.Status != "missing_fixture" && (context.TargetCount == 0 || context.Attempts == 0)
                    || context.Status == "context_unsupported" && string.IsNullOrWhiteSpace(context.Reason))))
            throw new InvalidDataException("Invalid API project-read evidence.");
        return pack;
    });
    private static readonly Lazy<IReadOnlyDictionary<string, ApiProjectReadEntry>> Reads = new(() =>
        ReadPack.Value.Entries.ToDictionary(e => e.Operation, StringComparer.Ordinal));
    private static readonly Lazy<NativeProjectReadPack> NativeReadPack = new(() =>
    {
        var pack = JsonSerializer.Deserialize<NativeProjectReadPack>(ReadResource("LECG.Revit2026.NativeProjectReads",
            "The installed native project-read evidence is missing. Reinstall the tested Copilot bundle."),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true })!;
        string[] allowed = ["read_succeeded", "missing_fixture"];
        var registered = CapabilityCatalog.All.Where(capability => capability.Kind == "read")
            .Select(capability => capability.Name).ToHashSet(StringComparer.Ordinal);
        if (pack.SchemaVersion != 1 || pack.RevitVersion != 2026 || pack.OperationCount != 45
            || pack.ContextCount != pack.OperationCount * 4 || pack.Models.Length != 4
            || pack.Entries.Length != pack.OperationCount || registered.Count != pack.OperationCount
            || !pack.Models.Select(model => model.Discipline).SequenceEqual(new[] { "architecture", "topography", "structure", "mep" })
            || pack.Models.DistinctBy(model => model.ModelSha256).Count() != pack.Models.Length
            || pack.Entries.DistinctBy(entry => entry.Operation).Count() != pack.Entries.Length
            || pack.Entries.Any(entry => !registered.Contains(entry.Operation) || entry.Contexts.Length != pack.Models.Length
                || entry.Contexts.Any(context => !allowed.Contains(context.Status) || context.Attempts < 0
                    || context.Status == "missing_fixture" && context.Attempts != 0
                    || context.Status == "read_succeeded" && (context.Attempts == 0 || string.IsNullOrWhiteSpace(context.TargetKind)))))
            throw new InvalidDataException("Invalid native project-read evidence.");
        return pack;
    });
    private static readonly Lazy<IReadOnlyDictionary<string, NativeProjectReadEntry>> NativeReads = new(() =>
        NativeReadPack.Value.Entries.ToDictionary(entry => entry.Operation, StringComparer.Ordinal));
    private static readonly Lazy<NativeProjectPreviewPack> NativePreviewPack = new(() =>
    {
        var pack = JsonSerializer.Deserialize<NativeProjectPreviewPack>(ReadResource("LECG.Revit2026.NativeProjectPreviews",
            "The installed native project-preview evidence is missing. Reinstall the tested Copilot bundle."),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true })!;
        string[] allowed = ["preview_succeeded", "missing_fixture"];
        var registered = CapabilityCatalog.All.Where(capability => capability.Kind == "change")
            .Select(capability => capability.Name).ToHashSet(StringComparer.Ordinal);
        const string fixture = "Autodesk.Revit.DB.DirectShape(test_fixture)";
        if (pack.SchemaVersion != 1 || pack.RevitVersion != 2026 || pack.OperationCount != 17
            || pack.ContextCount != pack.OperationCount * 4 || pack.Models.Length != 4
            || pack.Entries.Length != pack.OperationCount || registered.Count != pack.OperationCount
            || !pack.Models.Select(model => model.Discipline).SequenceEqual(new[] { "architecture", "topography", "structure", "mep" })
            || pack.Models.Any(model => !model.SyntheticDeleteFixtureAvailable)
            || pack.Models.DistinctBy(model => model.ModelSha256).Count() != pack.Models.Length
            || pack.Entries.DistinctBy(entry => entry.Operation).Count() != pack.Entries.Length
            || pack.Entries.Any(entry => !registered.Contains(entry.Operation) || entry.Contexts.Length != pack.Models.Length
                || entry.Contexts.Any(context => !allowed.Contains(context.Status) || context.Attempts < 0
                    || context.Status == "missing_fixture" && context.Attempts != 0
                    || context.Status == "preview_succeeded" && (context.Attempts == 0 || string.IsNullOrWhiteSpace(context.TargetKind))
                    || context.SyntheticFixtureUsed != (context.TargetKind == fixture)
                    || context.SyntheticFixtureUsed && entry.Operation != "delete_elements")))
            throw new InvalidDataException("Invalid native project-preview evidence.");
        return pack;
    });
    private static readonly Lazy<IReadOnlyDictionary<string, NativeProjectPreviewEntry>> NativePreviews = new(() =>
        NativePreviewPack.Value.Entries.ToDictionary(entry => entry.Operation, StringComparer.Ordinal));
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
    internal static int ReadOperationCount => Reads.Value.Count;
    internal static int NativeReadOperationCount => NativeReads.Value.Count;
    internal static int NativePreviewOperationCount => NativePreviews.Value.Count;
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
    private static object[] ProjectReadContexts(string operation)
    {
        if (!Reads.Value.TryGetValue(operation, out var entry)) return [];
        return ReadPack.Value.Models.Select((model, index) => (object)new
        {
            discipline = model.Discipline,
            model = model.Model,
            model_sha256 = model.ModelSha256,
            status = entry.Contexts[index].Status,
            target_count = entry.Contexts[index].TargetCount,
            attempts = entry.Contexts[index].Attempts,
            reason = entry.Contexts[index].Reason
        }).ToArray();
    }
    private static object[] NativeProjectReadContexts(string operation)
    {
        if (!NativeReads.Value.TryGetValue(operation, out var entry)) return [];
        return NativeReadPack.Value.Models.Select((model, index) => (object)new
        {
            discipline = model.Discipline,
            model = model.Model,
            model_sha256 = model.ModelSha256,
            status = entry.Contexts[index].Status,
            attempts = entry.Contexts[index].Attempts,
            target_kind = entry.Contexts[index].TargetKind,
            reason = entry.Contexts[index].Reason
        }).ToArray();
    }
    private static object[] NativeProjectPreviewContexts(string operation)
    {
        if (!NativePreviews.Value.TryGetValue(operation, out var entry)) return [];
        return NativePreviewPack.Value.Models.Select((model, index) => (object)new
        {
            discipline = model.Discipline,
            model = model.Model,
            model_sha256 = model.ModelSha256,
            status = entry.Contexts[index].Status,
            attempts = entry.Contexts[index].Attempts,
            target_kind = entry.Contexts[index].TargetKind,
            synthetic_fixture_used = entry.Contexts[index].SyntheticFixtureUsed,
            reason = entry.Contexts[index].Reason
        }).ToArray();
    }
    internal static object NativeFor(string operation) => new
    {
        project_read_contexts = NativeProjectReadContexts(operation),
        project_preview_contexts = NativeProjectPreviewContexts(operation),
        read_scope = NativeReadPack.Value.Scope,
        preview_scope = NativePreviewPack.Value.Scope
    };
    internal static object For(string operation) => Index.Value.TryGetValue(operation, out var entry)
        ? new { state = entry.State, passed_models = entry.PassedModels, context_failures = entry.ContextFailures,
            unsupported_models = entry.UnsupportedModels, same_value_models = entry.SameValueModels,
            project_contexts = ProjectContexts(operation), project_target_presence = ProjectTargetPresence(operation),
            project_read_contexts = ProjectReadContexts(operation),
            campaign = Pack.Value.Campaign }
        : new { state = "not_tested", passed_models = 0, context_failures = 0, unsupported_models = 0,
            same_value_models = 0, project_contexts = Array.Empty<object>(), project_target_presence = Array.Empty<object>(),
            project_read_contexts = Array.Empty<object>(),
            campaign = Pack.Value.Campaign };
}
