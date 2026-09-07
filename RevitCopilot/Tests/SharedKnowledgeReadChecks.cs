using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.RevitCopilot.Revit;

namespace LECG.RevitCopilot.SmokeTests;

internal static class SharedKnowledgeReadChecks
{
    internal static void Run(UIApplication app, Document doc, Wall wall, ViewSchedule schedule, View view, FilterElement filter, List<object> results)
    {
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        var executor = new ToolExecutor((_, _) => throw new InvalidOperationException("Read requested confirmation."));
        int before = new FilteredElementCollector(doc).GetElementCount();
        JsonElement Read(string operation, object args, bool expected = true)
        {
            using var json = JsonDocument.Parse(executor.Execute(app, "agent_read", JsonSerializer.Serialize(new { operation, arguments_json = JsonSerializer.Serialize(args) })));
            Check(json.RootElement.GetProperty("success").GetBoolean() == expected, json.RootElement.ToString());
            Check(!doc.IsModifiable, "Read left a transaction open.");
            return expected ? json.RootElement.GetProperty("data").Clone() : json.RootElement.Clone();
        }
        var dependents = Read("element_dependents", new { unique_id = wall.UniqueId, limit = 1 }).GetProperty("result");
        Check(dependents.GetProperty("total_count").GetInt32() == wall.GetDependentElements(null).Count, "Dependent count differs from API.");
        Check(dependents.GetProperty("items").GetArrayLength() == 1, "Dependent output is not paginated.");
        var valid = Read("element_valid_types", new { unique_id = wall.UniqueId, limit = 50 }).GetProperty("result");
        Check(valid.GetProperty("total_count").GetInt32() == wall.GetValidTypes().Count, "Valid type count differs from API.");
        Check(valid.GetProperty("items").EnumerateArray().All(e => wall.IsValidType(new ElementId(e.GetProperty("id").GetInt64()))), "Query returned an invalid type.");
        var phases = Read("phases_list", new { limit = 50 }).GetProperty("result");
        Check(phases.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("id").GetInt64())
            .SequenceEqual(doc.Phases.Cast<Phase>().Take(50).Select(p => p.Id.Value)), "Phase order changed.");
        var options = Read("design_options_list", new { limit = 1 }).GetProperty("result");
        Check(options.GetProperty("total_count").GetInt32() == new FilteredElementCollector(doc).OfClass(typeof(DesignOption)).GetElementCount(), "Design option count differs.");
        var worksets = Read("worksets_list", new { limit = 1 }).GetProperty("result");
        Check(worksets.GetProperty("is_workshared").GetBoolean() == doc.IsWorkshared, "Worksharing context differs.");
        var fields = Read("schedule_fields", new { unique_id = schedule.UniqueId }).GetProperty("result").GetProperty("fields").GetProperty("items");
        Check(fields.GetArrayLength() == 1 && fields[0].GetProperty("heading").GetString() == "Verified comments column" &&
            fields[0].GetProperty("parameter_id").GetInt64() == (long)BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS, "Schedule field identity or heading differs.");
        var filters = Read("view_filters", new { unique_id = view.UniqueId }).GetProperty("result").GetProperty("filters").GetProperty("items");
        Check(filters.GetArrayLength() == 1 && filters[0].GetProperty("unique_id").GetString() == filter.UniqueId && !filters[0].GetProperty("is_visible").GetBoolean(), "Applied filter result differs.");
        var knowledge = Read("knowledge_search", new { query = "HostObject.FindInserts", limit = 1 }).GetProperty("result");
        Check(knowledge.GetProperty("total_references").GetInt32() == 3000, "Runtime index is incomplete.");
        var reference = Read("knowledge_get", new { reference_id = knowledge.GetProperty("items")[0].GetProperty("id").GetString() }).GetProperty("result");
        Check(reference.GetProperty("operation").GetString() == "host_inserts", "Knowledge does not resolve its native adapter.");
        Read("schedule_fields", new { unique_id = wall.UniqueId }, false);
        Read("view_filters", new { unique_id = wall.UniqueId }, false);
        Read("element_dependents", new { unique_id = Guid.NewGuid().ToString("D") + "-00000001" }, false);
        Read("knowledge_get", new { reference_id = "not-a-reference" }, false);
        var unsupported = Read("api.get:Autodesk.Revit.DB.ViewSchedule.RowHeight", new { unique_ids = new[] { schedule.UniqueId } });
        Check(unsupported.GetProperty("result").GetProperty("items")[0].GetProperty("status").GetString() == "unsupported" &&
            (!unsupported.TryGetProperty("receipt_id", out var receipt) || receipt.ValueKind == JsonValueKind.Null), "Unsupported read must not create a reusable success receipt.");
        Check(new FilteredElementCollector(doc).GetElementCount() == before, "Read changed element count.");
        results.Add(new { test = "shared_knowledge_native_postconditions", passed = true, checks = "API comparisons, phase order, pagination, schedule/filter identity, knowledge adapter mapping, invalid targets, unsupported receipts and no open transaction" });
    }
}
