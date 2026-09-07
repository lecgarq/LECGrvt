using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using LECG.RevitCopilot.Agent;

internal static class ApiLibraryChecks
{
    internal static void Run()
    {
        var cold = Stopwatch.StartNew();
        var all = RevitApiCatalog.All;
        Require(ApiValidationEvidence.Count == all.Count, "Each API binding must have explicit runtime validation evidence.");
        foreach (var binding in all.Values)
        {
            var evidence = JsonSerializer.SerializeToElement(ApiValidationEvidence.For(binding.Operation));
            Require(evidence.GetProperty("state").GetString() != "not_tested", "A completed campaign must account for every accessor.");
            Require(!string.IsNullOrWhiteSpace(evidence.GetProperty("campaign").GetString()), "Validation requires campaign provenance.");
        }
        double coldMs = cold.Elapsed.TotalMilliseconds;
        Require(all.Count >= 2000, "Expected at least 2,000 real bound accessor functions, not aliases.");
        foreach (var binding in all.Values)
        {
            PropertyInfo p = binding.Property;
            Require(p.GetIndexParameters().Length == 0 && p.GetMethod is { IsPublic: true, IsStatic: false }, "Every binding needs a public instance getter.");
            Require(binding.Kind != "change" || p.SetMethod is { IsPublic: true, IsStatic: false }, "Changes need a real public instance setter.");
            Require(p.DeclaringType!.Assembly.GetName().Name == "RevitAPI", "No foreign assembly access.");
            Require(ReferenceEquals(CapabilityCatalog.Require(binding.Operation).Name, binding.Operation), "All bindings must pass actual operation dispatch validation.");
        }
        var result = JsonSerializer.SerializeToElement(RevitApiCatalog.Search("Width", "read", "Wall", 2));
        Require(result.GetProperty("items").GetArrayLength() <= 2 && result.ToString().Contains("api.get:Autodesk.Revit.DB.Wall.Width"), "Search must be bounded, typed and useful.");
        bool denied = false;
        try { RevitApiCatalog.Require("api.set:Autodesk.Revit.DB.Document.PathName", "change"); }
        catch (ArgumentException) { denied = true; }
        Require(denied, "Document/session access must not be exposed as a property mutation.");
        var timer = Stopwatch.StartNew();
        for (int i = 0; i < 30; i++) RevitApiCatalog.Search("Scale", "change", "View", 3);
        double warmMs = timer.Elapsed.TotalMilliseconds / 30;

        using var args = JsonDocument.Parse("""{"steps":[{"id":"first","operation":"create_level","arguments":{"name":"PRIVATE NAME","elevation_mm":3000}},{"id":"second","operation":"api.set:Autodesk.Revit.DB.Element.Name","arguments":{"unique_ids":["$step:first.unique_id"],"value":"NEW NAME"}}]}""");
        AgentBatch.Parse(args.RootElement, "change");
        using var dependency = JsonDocument.Parse("""{"unique_ids":["$step:first.unique_id"]}""");
        var resolved = AgentBatch.Resolve(dependency.RootElement, new Dictionary<string, JsonElement> { ["first"] = JsonSerializer.SerializeToElement(new { unique_id = "fresh-committed-id" }) });
        Require(resolved.GetProperty("unique_ids")[0].GetString() == "fresh-committed-id", "Dependencies must resolve against current results.");
        denied = false;
        try { AgentBatch.Resolve(dependency.RootElement, new Dictionary<string, JsonElement>()); }
        catch (ArgumentException) { denied = true; }
        Require(denied, "Future or nonexistent dependency references must fail.");
        string directory = Path.Combine(Path.GetTempPath(), "LECG-ApiLibrary-" + Guid.NewGuid().ToString("N"));
        try
        {
            var library = new WorkflowLibrary(Path.Combine(directory, "recipes.json"));
            WorkflowRecipe saved = library.Save("Atomic levels", "Fresh dependent workflow", [new(AgentBatch.ChangeOperation, args.RootElement)]);
            string encoded = JsonSerializer.Serialize(saved);
            Require(encoded.Contains("$step:first.unique_id") && encoded.Contains("api.set:Autodesk.Revit.DB.Element.Name") && !encoded.Contains("PRIVATE NAME") && !encoded.Contains("NEW NAME"), "Recipe must keep operation structure and symbolic dependencies, not old literals.");
            Require(library.Get(saved.Id).Steps.Length == 1, "Batch recipe must reload.");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        Console.WriteLine(JsonSerializer.Serialize(new { passed = true, bound_functions = all.Count, reads = all.Values.Count(b => b.Kind == "read"), changes = all.Values.Count(b => b.Kind == "change"), cold_catalog_ms = coldMs, warm_search_mean_ms = warmMs,
            note = "All bindings structurally validated; actual Revit invocation is covered separately, not claimed for every member." }));
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
