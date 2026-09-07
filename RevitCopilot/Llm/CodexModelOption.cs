using System.IO;
using System.Text.Json;

namespace LECG.RevitCopilot.Llm;

internal sealed record CodexModelOption(
    string Id, string DisplayName, string DefaultEffort,
    IReadOnlyList<string> Efforts, bool IsDefault, bool Hidden)
{
    public string Label => Hidden ? $"{DisplayName} (hidden in default catalog)" : DisplayName;

    internal static CodexModelOption Parse(JsonElement item)
    {
        string id = item.GetProperty("model").GetString()
            ?? throw new InvalidDataException("Codex returned a model without an identifier.");
        string name = item.TryGetProperty("displayName", out var display) ? display.GetString() ?? id : id;
        string effort = item.TryGetProperty("defaultReasoningEffort", out var level) ? level.GetString() ?? "" : "";
        string[] efforts = item.TryGetProperty("supportedReasoningEfforts", out var supported)
            ? supported.EnumerateArray().Select(e => e.GetProperty("reasoningEffort").GetString()!)
                .Where(e => !string.IsNullOrWhiteSpace(e)).Distinct().ToArray()
            : [];
        return new(id, name, effort, efforts,
            item.TryGetProperty("isDefault", out var primary) && primary.GetBoolean(),
            item.TryGetProperty("hidden", out var hidden) && hidden.GetBoolean());
    }
}
