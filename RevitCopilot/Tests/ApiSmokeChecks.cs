using System.Diagnostics;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.RevitCopilot.Agent;
using LECG.RevitCopilot.Revit;

namespace LECG.RevitCopilot.SmokeTests;

internal static class ApiSmokeChecks
{
    internal static void Run(UIApplication app, Document doc, Wall wall, List<object> results)
    {
        var executor = new ToolExecutor((_, _) => true);
        JsonElement Call(string tool, object args, bool success = true)
        {
            using var data = JsonDocument.Parse(executor.Execute(app, tool, JsonSerializer.Serialize(args)));
            Require(data.RootElement.GetProperty("success").GetBoolean() == success, data.RootElement.ToString());
            Require(!doc.IsModifiable, "An API tool left a transaction open.");
            return (success ? data.RootElement.GetProperty("data") : data.RootElement).Clone();
        }
        object Uids(Element element) => new { unique_ids = new[] { element.UniqueId } };
        JsonElement Preview(string operation, object args) => Call("agent_preview", new { operation, arguments_json = JsonSerializer.Serialize(args) });
        JsonElement Apply(JsonElement preview) => Call("agent_apply", new { preview_id = preview.GetProperty("preview_id").GetString() });
        var catalog = RevitApiCatalog.All;
        Require(catalog.Count >= 2000, "Runtime binding count is below 2,000.");
        Element[] fixtures = new FilteredElementCollector(doc).WhereElementIsNotElementType().ToElements()
            .Concat(new FilteredElementCollector(doc).WhereElementIsElementType().ToElements())
            .GroupBy(e => e.GetType()).Select(g => g.First()).ToArray();
        HashSet<string> invoked = [];
        List<object> applicabilityErrors = [];
        var timer = Stopwatch.StartNew();
        foreach (var binding in catalog.Values.Where(b => b.Kind == "read"))
        {
            Element? fixture = fixtures.FirstOrDefault(e => binding.Property.DeclaringType!.IsInstanceOfType(e));
            if (fixture is null) continue;
            using var response = JsonDocument.Parse(executor.Execute(app, "agent_read", JsonSerializer.Serialize(new { operation = binding.Operation, arguments_json = JsonSerializer.Serialize(Uids(fixture)) })));
            if (response.RootElement.GetProperty("success").GetBoolean()) invoked.Add(binding.Operation);
            else applicabilityErrors.Add(new { operation = binding.Operation, error = response.RootElement.GetProperty("error").GetString() });
            Require(!doc.IsModifiable, "Getter changed document transaction state.");
        }
        Require(invoked.Count >= 100, $"Only {invoked.Count} distinct getter functions executed successfully.");
        results.Add(new { test = "api_bound_catalog_and_real_getter_sweep", passed = true, catalog_count = catalog.Count,
            distinct_getters_succeeded = invoked.Count, functions = invoked.Order().ToArray(), fixture_types = fixtures.Select(e => e.GetType().FullName).ToArray(),
            applicability_errors = applicabilityErrors, elapsed_ms = timer.Elapsed.TotalMilliseconds });

        Level level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().First();
        ViewDrafting view = new FilteredElementCollector(doc).OfClass(typeof(ViewDrafting)).Cast<ViewDrafting>().First();
        Material material = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>().First();
        string nameBefore = level.Name;
        var p = Preview("api.set:Autodesk.Revit.DB.Element.Name", new { unique_ids = new[] { level.UniqueId }, value = "API bound level" });
        Require(level.Name == nameBefore, "API preview did not roll back the name.");
        Apply(p);
        Require(level.Name == "API bound level", "API setter did not commit.");
        Call("agent_apply", new { preview_id = p.GetProperty("preview_id").GetString() }, false);
        Apply(Preview("api.set:Autodesk.Revit.DB.View.Scale", new { unique_ids = new[] { view.UniqueId }, value = 75 }));
        Require(view.Scale == 75, "Integer API setter failed.");
        Apply(Preview("api.set:Autodesk.Revit.DB.Material.Color", new { unique_ids = new[] { material.UniqueId }, value = new { red = 12, green = 34, blue = 56 } }));
        Require(material.Color.Red == 12 && material.Color.Green == 34 && material.Color.Blue == 56, "Color setter failed.");
        double elevationBefore = level.Elevation;
        Call("agent_preview", new { operation = "api.set:Autodesk.Revit.DB.Level.Elevation", arguments_json = JsonSerializer.Serialize(new { unique_ids = new[] { level.UniqueId }, value = elevationBefore + 1 }) }, false);
        Require(level.Elevation == elevationBefore, "Unit guard failed.");
        Apply(Preview("api.set:Autodesk.Revit.DB.Level.Elevation", new { unique_ids = new[] { level.UniqueId }, value = elevationBefore + 1, units = "revit_internal" }));
        Require(Math.Abs(level.Elevation - elevationBefore - 1) < 1e-8, "Native-unit setter failed.");
        Call("agent_read", new { operation = "api.get:Autodesk.Revit.DB.Wall.Width", arguments_json = JsonSerializer.Serialize(Uids(level)) }, false);
        Call("agent_preview", new { operation = "api.set:Autodesk.Revit.DB.Document.PathName", arguments_json = "{}" }, false);
        Apply(Preview("api.set:Autodesk.Revit.DB.Element.Pinned", new { unique_ids = new[] { wall.UniqueId }, value = true }));
        Require(wall.Pinned, "Boolean setter failed.");
        Apply(Preview("api.set:Autodesk.Revit.DB.Element.Pinned", new { unique_ids = new[] { wall.UniqueId }, value = false }));
        WallType wallType = wall.WallType;
        Apply(Preview("api.set:Autodesk.Revit.DB.WallType.Function", new { unique_ids = new[] { wallType.UniqueId }, value = "Interior" }));
        Require(wallType.Function == WallFunction.Interior, "Named enum setter failed.");
        Call("agent_preview", new { operation = "api.set:Autodesk.Revit.DB.WallType.Function", arguments_json = JsonSerializer.Serialize(new { unique_ids = new[] { wallType.UniqueId }, value = "999" }) }, false);
        FillPatternElement pattern = new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>().First(f => f.GetFillPattern().IsSolidFill);
        Apply(Preview("api.set:Autodesk.Revit.DB.Material.SurfaceForegroundPatternId", new { unique_ids = new[] { material.UniqueId }, value = new { unique_id = pattern.UniqueId } }));
        Require(material.SurfaceForegroundPatternId == pattern.Id, "Current-document ElementId conversion failed.");
        results.Add(new { test = "api_setters_preview_commit_reuse_units_types_scope", passed = true });

        var readSteps = new object[]
        {
            new { id = "walls", operation = "elements_find", arguments = new { element_type = "Wall", limit = 1 } },
            new { id = "width", operation = "api.get:Autodesk.Revit.DB.Wall.Width", arguments = new { unique_ids = new[] { "$step:walls.items.0.unique_id" } } },
            new { id = "levels", operation = "levels_list", arguments = new { limit = 2 } }
        };
        var readBatch = Call("agent_read_batch", new { steps = readSteps });
        Require(readBatch.GetProperty("completed_count").GetInt32() == 3 && readBatch.GetProperty("status").GetString() == "completed", "Read batch failed.");
        var badBatch = Call("agent_read_batch", new { steps = new object[]
        {
            new { id = "invalid", operation = "api.get:Autodesk.Revit.DB.Wall.Width", arguments = Uids(level) },
            new { id = "never", operation = "levels_list", arguments = new { limit = 1 } }
        } });
        Require(badBatch.GetProperty("completed_count").GetInt32() == 0 && badBatch.GetProperty("results").GetArrayLength() == 1, "Read batch should stop explicitly on error.");
        results.Add(new { test = "read_batch_dependencies_and_stop_on_error", passed = true });

        var changeSteps = new object[]
        {
            new { id = "level", operation = "create_level", arguments = new { name = "API batch provisional", elevation_mm = 6500 } },
            new { id = "rename", operation = "api.set:Autodesk.Revit.DB.Element.Name", arguments = new { unique_ids = new[] { "$step:level.unique_id" }, value = "API batch committed" } },
            new { id = "scale", operation = "view_scale", arguments = new { unique_ids = new[] { view.UniqueId }, scale = 100 } }
        };
        int levelsBefore = new FilteredElementCollector(doc).OfClass(typeof(Level)).GetElementCount();
        var batchPreview = Call("agent_preview_batch", new { steps = changeSteps });
        Require(new FilteredElementCollector(doc).OfClass(typeof(Level)).GetElementCount() == levelsBefore && view.Scale == 75, "Batch preview left changes behind.");
        var committed = Apply(batchPreview);
        Require(new FilteredElementCollector(doc).OfClass(typeof(Level)).GetElementCount() == levelsBefore + 1 && view.Scale == 100, "Batch did not commit atomically.");
        string freshId = committed.GetProperty("result").GetProperty("steps")[0].GetProperty("result").GetProperty("unique_id").GetString()!;
        Require(doc.GetElement(freshId).Name == "API batch committed", "Dependent step did not use committed result.");
        Call("agent_preview_batch", new { steps = new object[]
        {
            new { id = "name", operation = "api.set:Autodesk.Revit.DB.Element.Name", arguments = new { unique_ids = new[] { level.UniqueId }, value = "MUST ROLLBACK" } },
            new { id = "wrong_type", operation = "api.set:Autodesk.Revit.DB.View.Scale", arguments = new { unique_ids = new[] { wall.UniqueId }, value = 50 } }
        } }, false);
        Require(level.Name == "API bound level" && view.Scale == 100, "Failed batch did not roll back all changes.");
        results.Add(new { test = "atomic_change_batch_fresh_dependencies_and_full_rollback", passed = true });

        int asks = 0;
        var denyingExecutor = new ToolExecutor((_, _) => { asks++; return false; });
        using var deniedPreview = JsonDocument.Parse(denyingExecutor.Execute(app, "agent_preview", JsonSerializer.Serialize(new { operation = "api.set:Autodesk.Revit.DB.View.Scale", arguments_json = JsonSerializer.Serialize(new { unique_ids = new[] { view.UniqueId }, value = 500 }) })));
        using var deniedApply = JsonDocument.Parse(denyingExecutor.Execute(app, "agent_apply", JsonSerializer.Serialize(new { preview_id = deniedPreview.RootElement.GetProperty("data").GetProperty("preview_id").GetString() })));
        Require(asks == 1 && deniedApply.RootElement.GetProperty("data").GetProperty("status").GetString() == "cancelled" && view.Scale == 100, "Denied confirmation must not commit.");
        results.Add(new { test = "api_local_confirmation_denial", passed = true });
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
