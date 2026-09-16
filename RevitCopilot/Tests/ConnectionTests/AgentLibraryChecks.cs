using System.Text.Json;
using LECG.RevitCopilot.Agent;

internal static class AgentLibraryChecks
{
    internal static void Run()
    {
        Require(CapabilityCatalog.All.Length == 60 && CapabilityCatalog.All.DistinctBy(c => c.Name).Count() == 60, "Expected 60 distinct operations.");
        Require(ApiValidationEvidence.NativeReadOperationCount == 43, "Every native read needs four-discipline project evidence.");
        Require(ApiValidationEvidence.NativePreviewOperationCount == 17, "Every native change needs four-discipline preview evidence.");
        foreach (var capability in CapabilityCatalog.All)
        {
            var evidence = JsonSerializer.SerializeToElement(ApiValidationEvidence.NativeFor(capability.Name));
            Require(evidence.GetProperty("project_read_contexts").GetArrayLength() == (capability.Kind == "read" ? 4 : 0),
                "Only native reads must expose four-discipline project evidence.");
            Require(evidence.GetProperty("project_preview_contexts").GetArrayLength() == (capability.Kind == "change" ? 4 : 0),
                "Only native changes must expose four-discipline preview evidence.");
        }
        var measuredLevels = JsonSerializer.SerializeToElement(CapabilityCatalog.Search("levels_list", 1))
            .GetProperty("items")[0].GetProperty("validation").GetProperty("project_read_contexts");
        Require(measuredLevels.GetArrayLength() == 4 && measuredLevels.EnumerateArray().All(context =>
            context.GetProperty("status").GetString() == "read_succeeded"),
            "Native capability search must expose successful named-fixture evidence.");
        var scheduleEvidence = JsonSerializer.SerializeToElement(ApiValidationEvidence.NativeFor("schedule_fields"))
            .GetProperty("project_read_contexts").EnumerateArray().Single(context =>
                context.GetProperty("discipline").GetString() == "topography");
        Require(scheduleEvidence.GetProperty("status").GetString() == "missing_fixture",
            "Native capability evidence must preserve explicit missing fixtures.");
        foreach (string operation in new[] { "curve_join_neighbors", "assigned_electrical_systems" })
        {
            var contexts = JsonSerializer.SerializeToElement(ApiValidationEvidence.NativeFor(operation))
                .GetProperty("project_read_contexts");
            Require(contexts.GetArrayLength() == 4 && contexts.EnumerateArray().All(context =>
                context.GetProperty("status").GetString() == "read_succeeded"),
                $"{operation} must retain four successful named-model contexts.");
        }
        foreach (string operation in new[] { "spatial_contains_point", "mep_connectors" })
        {
            var contexts = JsonSerializer.SerializeToElement(ApiValidationEvidence.NativeFor(operation))
                .GetProperty("project_read_contexts");
            Require(contexts.GetArrayLength() == 4 && contexts.EnumerateArray().Any(context =>
                context.GetProperty("status").GetString() == "read_succeeded"),
                $"{operation} needs at least one successful named-model context.");
        }
        foreach (string operation in new[] { "external_files_list", "panel_host" })
        {
            var contexts = JsonSerializer.SerializeToElement(ApiValidationEvidence.NativeFor(operation))
                .GetProperty("project_read_contexts");
            Require(contexts.GetArrayLength() == 4 && contexts.EnumerateArray().Any(context =>
                context.GetProperty("status").GetString() == "read_succeeded"),
                $"{operation} needs at least one successful named-model context.");
        }
        foreach (string operation in new[] { "stairs_associated_railings", "group_attached_detail_types" })
        {
            var contexts = JsonSerializer.SerializeToElement(ApiValidationEvidence.NativeFor(operation))
                .GetProperty("project_read_contexts");
            Require(contexts.GetArrayLength() == 4 && contexts.EnumerateArray().Any(context =>
                context.GetProperty("status").GetString() == "read_succeeded"),
                $"{operation} needs at least one successful named-model context.");
        }
        var createLevelEvidence = JsonSerializer.SerializeToElement(ApiValidationEvidence.NativeFor("create_level"))
            .GetProperty("project_preview_contexts");
        Require(createLevelEvidence.EnumerateArray().All(context => context.GetProperty("status").GetString() == "preview_succeeded"),
            "Create-level preview evidence must preserve four successful rollback contexts.");
        var slabOffsetEvidence = JsonSerializer.SerializeToElement(ApiValidationEvidence.NativeFor("slab_offset"))
            .GetProperty("project_preview_contexts").EnumerateArray().Single(context =>
                context.GetProperty("discipline").GetString() == "topography");
        Require(slabOffsetEvidence.GetProperty("status").GetString() == "missing_fixture",
            "Native preview evidence must preserve explicit missing fixtures.");
        var deleteEvidence = JsonSerializer.SerializeToElement(ApiValidationEvidence.NativeFor("delete_elements"))
            .GetProperty("project_preview_contexts");
        Require(deleteEvidence.EnumerateArray().Where(context =>
                context.GetProperty("synthetic_fixture_used").GetBoolean())
            .Select(context => context.GetProperty("discipline").GetString())
            .SequenceEqual(new[] { "architecture", "topography" }),
            "Delete preview evidence must identify only the two synthetic-fixture contexts.");
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
            Console.WriteLine("PASS: 60 operations, bounded discovery, recipe provenance, fresh inputs, cross-session reuse, deduplication, backup and concurrent-save protection.");
        }
        finally { Directory.Delete(directory, true); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
