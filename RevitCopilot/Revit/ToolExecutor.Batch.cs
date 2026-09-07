using System.Diagnostics;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.RevitCopilot.Agent;

namespace LECG.RevitCopilot.Revit;

internal sealed partial class ToolExecutor
{
    private object ReadBatch(UIDocument ui, Document doc, JsonElement args)
    {
        AgentBatchStep[] steps = AgentBatch.Parse(args, "read");
        Dictionary<string, JsonElement> completed = new(StringComparer.Ordinal);
        List<object> results = [];
        foreach (AgentBatchStep step in steps)
        {
            var timer = Stopwatch.StartNew();
            try
            {
                JsonElement resolved = AgentBatch.Resolve(step.Arguments, completed);
                object result = AgentRead(ui, doc, step.Operation, resolved);
                completed.Add(step.Id, JsonSerializer.SerializeToElement(result, JsonOptions));
                results.Add(new { id = step.Id, success = true, result, execution_ms = timer.Elapsed.TotalMilliseconds,
                    receipt_id = RecordReadSuccess(step.Operation, resolved, completed[step.Id]) });
            }
            catch (Exception ex)
            {
                results.Add(new { id = step.Id, success = false, error = ex.Message, execution_ms = timer.Elapsed.TotalMilliseconds });
                return new { status = "stopped_on_error", completed_count = completed.Count, results };
            }
        }
        return new { status = "completed", completed_count = completed.Count, results };
    }

    private static object PerformChangeBatch(Document doc, JsonElement args)
    {
        AgentBatchStep[] steps = AgentBatch.Parse(args, "change");
        Dictionary<string, JsonElement> completed = new(StringComparer.Ordinal);
        List<object> results = [];
        int targetCount = 0;
        int deletedCount = 0;
        foreach (AgentBatchStep step in steps)
        {
            try
            {
                JsonElement resolved = AgentBatch.Resolve(step.Arguments, completed);
                targetCount += resolved.TryGetProperty("unique_ids", out var ids) ? ids.GetArrayLength() : 1;
                if (targetCount > 200) throw new ArgumentException("A change batch may target at most 200 elements across its steps.");
                object result = PerformChange(doc, step.Operation, resolved);
                doc.Regenerate();
                JsonElement serialized = JsonSerializer.SerializeToElement(result, JsonOptions);
                if (serialized.TryGetProperty("deleted_ids_including_dependents", out var deleted)) deletedCount += deleted.GetArrayLength();
                if (deletedCount > 200) throw new ArgumentException("A batch may delete at most 200 elements including dependencies.");
                completed.Add(step.Id, serialized);
                results.Add(new { id = step.Id, operation = step.Operation, result });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Batch step '{step.Id}' ({step.Operation}) failed; the entire batch is rolled back. {ex.Message}", ex);
            }
        }
        return new { completed_count = results.Count, steps = results };
    }

    private static object FindApiElements(Document doc, JsonElement args)
    {
        using FilteredElementCollector collector = new(doc);
        if (OptionalString(args, "category") is not null) collector.OfCategory(ReadCategory(args));
        string kind = OptionalString(args, "kind") ?? "instances";
        if (kind == "instances") collector.WhereElementIsNotElementType();
        else if (kind == "types") collector.WhereElementIsElementType();
        else if (kind != "all") throw new ArgumentException("kind must be instances, types or all.");
        // Keep a native filter even when kind=all; Revit rejects an unfiltered collector.
        else collector.WherePasses(new LogicalOrFilter(new ElementIsElementTypeFilter(), new ElementIsElementTypeFilter(true)));
        string? className = OptionalString(args, "element_type");
        Type? type = className is null ? null : typeof(Element).Assembly.GetType(className.StartsWith("Autodesk.", StringComparison.Ordinal) ? className : "Autodesk.Revit.DB." + className);
        if (className is not null && (type is null || !typeof(Element).IsAssignableFrom(type))) throw new ArgumentException("Use an exact Revit Element subclass name.");
        int limit = Math.Clamp(OptionalInt(args, "limit") ?? 10, 1, 50);
        int offset = Math.Clamp(OptionalInt(args, "offset") ?? 0, 0, 100000);
        Element[] page = collector.Where(e => (type is null || type.IsInstanceOfType(e)) && Matches(SafeElementName(e), args))
            .Skip(offset).Take(limit + 1).ToArray();
        return new { offset, next_offset = page.Length > limit ? (int?)(offset + limit) : null,
            items = page.Take(limit).Select(e => new { id = e.Id.Value, unique_id = e.UniqueId, name = SafeElementName(e), element_type = e.GetType().FullName, is_type = e is ElementType }) };
    }
}
