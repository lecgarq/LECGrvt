using System.Text.Json;
using LECG.RevitCopilot.Agent;

internal static class AgentLibraryChecks
{
    internal static void Run()
    {
        Require(CapabilityCatalog.All.Length == 52 && CapabilityCatalog.All.DistinctBy(c => c.Name).Count() == 52, "Expected 52 distinct operations.");
        KnowledgeLibraryChecks.Run();
        HarvestDiscoveryChecks.Run();
        var discovery = JsonSerializer.SerializeToElement(CapabilityCatalog.Search("material", 2));
        Require(discovery.GetProperty("items").GetArrayLength() == 2, "Discovery should be bounded.");
        string directory = Path.Combine(Path.GetTempPath(), "LECG-AgentLibrary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "recipes.json");
            var library = new WorkflowLibrary(path);
            Require(JsonSerializer.SerializeToElement(library.Search("", 1)).GetProperty("built_in_count").GetInt32() == 29, "Expected 29 shared curated templates.");
            foreach (string id in new[] { "element-feasibility", "compound-layer-audit", "geometry-join-audit", "instance-coordinate-audit" })
                Require(library.Get(id).Provenance == "curated_template" && library.Get(id).Steps.All(step => CapabilityCatalog.Require(step.Operation).Kind == "read"), "New audit recipes must remain read-only templates.");
            var starter = library.Get("wall-audit");
            Require(starter.Provenance == "curated_template", "Templates must not claim previous execution.");
            using var args = JsonDocument.Parse("""{"unique_ids":["OLD-DOCUMENT-ID"],"parameter_name":"Comments","new_value":"private project text"}""");
            WorkflowRecipe saved = library.Save("Verified comments", "Apply reviewed comments", [new("set_parameters", args.RootElement)]);
            string stored = File.ReadAllText(path);
            Require(!stored.Contains("OLD-DOCUMENT-ID") && !stored.Contains("private project text"), "Saved recipes must not persist old inputs.");
            Require(new WorkflowLibrary(path).Get(saved.Id).Steps[0].Arguments.GetProperty("unique_ids").GetString() == "$input:step1.unique_ids", "Recipe bindings must survive restart.");
            Require(new WorkflowLibrary(path).Save("Verified comments", "Apply reviewed comments", [new("set_parameters", args.RootElement)]).Id == saved.Id, "Identical recipes must not accumulate across sessions.");
            library.Save("Second workflow", "Backup check", [new("set_parameters", args.RootElement)]);
            Require(File.Exists(path + ".previous") && File.ReadAllText(path + ".previous").Contains(saved.Id), "Updating shared recipes must preserve the prior library.");
            using (var held = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                bool locked = false;
                try { new WorkflowLibrary(path).Save("Concurrent", "Must not overwrite", [new("set_parameters", args.RootElement)]); }
                catch (IOException) { locked = true; }
                Require(locked, "Concurrent Revit sessions must not overwrite shared recipes.");
            }
            bool rejected = false;
            try { library.Save("bad", "unknown function", [new("execute_arbitrary_code", args.RootElement)]); }
            catch (ArgumentException) { rejected = true; }
            Require(rejected, "Unknown executable capabilities must be rejected.");
            Require(JsonSerializer.Serialize(library.Search("comments")).Contains(saved.Id), "Saved recipe should be discoverable.");
            Console.WriteLine("PASS: 52 operations, bounded discovery, recipe provenance, fresh inputs, cross-session reuse, deduplication, backup and concurrent-save protection.");
        }
        finally { Directory.Delete(directory, true); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
