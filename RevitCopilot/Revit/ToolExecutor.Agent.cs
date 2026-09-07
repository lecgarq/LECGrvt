using System.IO;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.RevitCopilot.Agent;
using LECG.RevitCopilot.Configuration;

namespace LECG.RevitCopilot.Revit;

internal sealed partial class ToolExecutor
{
    private readonly Dictionary<string, ExecutionReceipt> _receipts = [];
    private readonly WorkflowLibrary _recipes = new(Path.Combine(CopilotPaths.Root, "recipes", "workflows.json"));

    private object RunLibraryTool(string tool, JsonElement args) => tool switch
    {
        "agent_recipe_search" => _recipes.Search(OptionalString(args, "query") ?? "", OptionalInt(args, "limit") ?? 3),
        "agent_recipe_get" => _recipes.Get(RequireString(args, "recipe_id")),
        "agent_recipe_save" => SaveRecipe(args),
        _ => throw new ArgumentException("Unknown recipe operation.")
    };

    private string RecordSuccess(string operation, JsonElement args)
    {
        if (_receipts.Count >= 100) _receipts.Remove(_receipts.Keys.First());
        string id = Guid.NewGuid().ToString("N");
        _receipts[id] = new(operation, args.Clone());
        return id;
    }

    private object RunAgentRead(UIDocument ui, Document doc, JsonElement args)
    {
        string operation = RequireString(args, "operation");
        using JsonDocument inputs = AgentArguments(args);
        object result = AgentRead(ui, doc, operation, inputs.RootElement);
        return new { operation, result, receipt_id = RevitApiCatalog.IsApiOperation(operation)
            ? RecordReadSuccess(operation, inputs.RootElement, JsonSerializer.SerializeToElement(result, JsonOptions))
            : RecordSuccess(operation, inputs.RootElement) };
    }

    private string? RecordReadSuccess(string operation, JsonElement args, JsonElement result) =>
        result.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array &&
        items.EnumerateArray().Any(item => item.TryGetProperty("status", out var status) && status.GetString() == "unsupported")
            ? null : RecordSuccess(operation, args);

    private object RunAgentPreview(Document doc, JsonElement args)
    {
        using JsonDocument inputs = AgentArguments(args);
        return PreviewChange(doc, RequireString(args, "operation"), inputs.RootElement);
    }

    private object SaveRecipe(JsonElement args)
    {
        string name = RequireString(args, "name");
        string description = RequireString(args, "description");
        string[] ids = RequireProperty(args, "receipt_ids").EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
        if (ids.Length is < 1 or > 8) throw new ArgumentException("Supply 1 to 8 successful execution receipt IDs in workflow order.");
        ExecutionReceipt[] receipts = ids.Select(id => _receipts.TryGetValue(id, out var receipt) ? receipt
            : throw new ArgumentException("A receipt expired or is not from this Revit session. Run and verify the workflow again.")).ToArray();
        if (!_confirm($"Save reusable workflow '{name}'?", "Confirm that the results were correct. All arguments will be replaced with fresh input placeholders. Steps:\n" + string.Join("\n", receipts.Select(r => r.Operation))))
            return new { status = "cancelled" };
        return _recipes.Save(name, description, receipts);
    }

    private static JsonDocument AgentArguments(JsonElement args)
    {
        string json = OptionalString(args, "arguments_json") ?? "{}";
        if (json.Length > 16000) throw new ArgumentException("Arguments exceed 16,000 characters. Narrow the operation.");
        JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object) { document.Dispose(); throw new ArgumentException("arguments_json must contain a JSON object."); }
        return document;
    }
}
