using System.Text.Json;
using System.Text.Json.Nodes;

namespace LECG.RevitCopilot.Agent;

internal sealed record AgentBatchStep(string Id, string Operation, JsonElement Arguments);

internal static class AgentBatch
{
    internal const string ChangeOperation = "workflow_change_batch";
    internal static AgentBatchStep[] Parse(JsonElement args, string kind)
    {
        JsonElement steps = args.GetProperty("steps");
        if (steps.ValueKind != JsonValueKind.Array || steps.GetArrayLength() is < 1 or > 12)
            throw new ArgumentException("Supply 1 to 12 steps.");
        List<AgentBatchStep> result = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (JsonElement step in steps.EnumerateArray())
        {
            string id = step.GetProperty("id").GetString() ?? "";
            if (id.Length is < 1 or > 40 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_') || !ids.Add(id))
                throw new ArgumentException("Step IDs must be unique, 1–40 ASCII letters/digits/underscores.");
            string operation = step.GetProperty("operation").GetString() ?? "";
            if (operation == ChangeOperation) throw new ArgumentException("Nested batches are not supported.");
            CapabilityCatalog.Require(operation, kind);
            JsonElement arguments = step.GetProperty("arguments");
            if (arguments.ValueKind != JsonValueKind.Object) throw new ArgumentException("Step arguments must be an object.");
            result.Add(new(id, operation, arguments.Clone()));
        }
        return result.ToArray();
    }

    // References are resolved from earlier results separately in preview and commit.
    // No preview-created identifier is carried into committed execution.
    internal static JsonElement Resolve(JsonElement arguments, IReadOnlyDictionary<string, JsonElement> results)
    {
        JsonNode? ResolveNode(JsonNode? node, int depth)
        {
            if (depth > 12) throw new ArgumentException("Batch argument nesting exceeds 12 levels.");
            if (node is JsonValue value && value.TryGetValue<string>(out string? text) && text.StartsWith("$step:", StringComparison.Ordinal))
            {
                string[] path = text[6..].Split('.');
                if (!results.TryGetValue(path[0], out JsonElement found)) throw new ArgumentException($"Step reference '{text}' is not an earlier successful step.");
                foreach (string key in path.Skip(1))
                {
                    if (found.ValueKind == JsonValueKind.Array && int.TryParse(key, out int index) && index >= 0 && index < found.GetArrayLength()) found = found[index];
                    else if (found.ValueKind == JsonValueKind.Object && found.TryGetProperty(key, out var property)) found = property;
                    else throw new ArgumentException($"Unresolved step result '{text}'.");
                }
                return JsonNode.Parse(found.GetRawText());
            }
            if (node is JsonObject obj)
                return new JsonObject(obj.Select(p => KeyValuePair.Create(p.Key, ResolveNode(p.Value, depth + 1))));
            if (node is JsonArray array) return new JsonArray(array.Select(v => ResolveNode(v, depth + 1)).ToArray());
            return node?.DeepClone();
        }
        return JsonSerializer.SerializeToElement(ResolveNode(JsonNode.Parse(arguments.GetRawText()), 0));
    }
}
