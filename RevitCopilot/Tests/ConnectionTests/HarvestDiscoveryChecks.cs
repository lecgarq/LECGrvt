using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using LECG.RevitCopilot.Agent;

internal static class HarvestDiscoveryChecks
{
    internal static void Run()
    {
        (string Query, string Name)[] native = [("huecos muro", "host_inserts"), ("fases modificables", "element_phase_status"), ("caras inferiores losa", "host_bottom_faces")];
        foreach (var test in native)
        {
            var result = JsonSerializer.SerializeToElement(CapabilityCatalog.Search(test.Query, 1));
            Require(result.GetProperty("items")[0].GetProperty("Name").GetString() == test.Name, "Native Spanish search selected wrong operation.");
            Require(CapabilityCatalog.Require(test.Name, "read").Kind == "read", "Harvest operation must remain read-only.");
            bool denied = false;
            try { CapabilityCatalog.Require(test.Name, "change"); } catch (ArgumentException) { denied = true; }
            Require(denied, "Read operation must not enter preview/apply.");
        }
        (string Query, string Name)[] properties = [("ancho de muro", "api.get:Autodesk.Revit.DB.WallType.Width"), ("escala de vista", "api.get:Autodesk.Revit.DB.View.Scale"),
            ("estado de anclaje", "api.get:Autodesk.Revit.DB.Element.Pinned"), ("fase de creación", "api.get:Autodesk.Revit.DB.Element.CreatedPhaseId")];
        int before = 0;
        foreach (var test in properties)
        {
            // Preserve the pre-change ranking algorithm as a small regression comparison, not an AI accuracy benchmark.
            var terms = Regex.Split(test.Query, @"[\s._:]+").Where(t => t.Length > 0).Take(12).ToArray();
            string? baseline = RevitApiCatalog.All.Values.Where(b => b.Kind == "read")
                .Select(b => new { b.Operation, Score = terms.Sum(t => (b.Property.Name.Contains(t, StringComparison.OrdinalIgnoreCase) ? 4 : 0) +
                    (b.Property.DeclaringType!.FullName!.Contains(t, StringComparison.OrdinalIgnoreCase) ? 2 : 0) + (b.Summary.Contains(t, StringComparison.OrdinalIgnoreCase) ? 1 : 0)) })
                .Where(b => b.Score > 0).OrderByDescending(b => b.Score).ThenBy(b => b.Operation, StringComparer.Ordinal).FirstOrDefault()?.Operation;
            if (baseline == test.Name) before++;
            var result = JsonSerializer.SerializeToElement(RevitApiCatalog.Search(test.Query, "read", limit: 1));
            Require(result.GetProperty("items")[0].GetProperty("Name").GetString() == test.Name, "Reviewed property phrase selected wrong operation.");
        }
        Require(ReviewedDiscovery.Score(ReviewedDiscovery.Terms("ancho muro"), "execute_arbitrary_code") == 0, "Vocabulary cannot add executable operations.");
        Require(ReviewedDiscovery.Score(ReviewedDiscovery.Terms("fase de creación"), "api.set:Autodesk.Revit.DB.Element.CreatedPhaseId") == 0, "Read vocabulary must not silently add write aliases.");
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 40; i++) RevitApiCatalog.Search(properties[i % properties.Length].Query, "read", limit: 1);
        Console.WriteLine(JsonSerializer.Serialize(new { test = "reviewed_discovery", passed = true, curated_property_queries = properties.Length,
            baseline_top1 = before, reviewed_top1 = properties.Length, native_queries_passed = native.Length, warm_mean_ms = watch.Elapsed.TotalMilliseconds / 40,
            scope = "Small curated retrieval regression, not general model intelligence or runtime accuracy." }));
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
