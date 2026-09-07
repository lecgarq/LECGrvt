using System.Diagnostics;
using System.Text.Json;
using LECG.RevitCopilot.Agent;

internal static class KnowledgeLibraryChecks
{
    internal static void Run()
    {
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        JsonElement Search(string query, string kind = "all") => JsonSerializer.SerializeToElement(KnowledgeLibrary.Search(query, 2, kind));
        Check(KnowledgeLibrary.Count == 3000, "The entire source reference bank must be embedded.");
        var lookup = Search("WallType.Width", "read").GetProperty("items")[0];
        Check(lookup.GetProperty("operation").GetString() == "api.get:Autodesk.Revit.DB.WallType.Width", "Exact getter discovery failed.");
        Check(Search("WallType.Width", "change").GetProperty("items").EnumerateArray()
            .All(item => item.GetProperty("kind").GetString() == "change" && item.GetProperty("operation").GetString() != "api.set:Autodesk.Revit.DB.WallType.Width"), "Read-only width must not invent a setter.");
        var native = Search("HostObject.FindInserts").GetProperty("items")[0];
        Check(native.GetProperty("operation").GetString() == "host_inserts", "Reviewed method must map to its existing adapter.");
        var reference = Search("AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance", "reference").GetProperty("items")[0];
        Check(!reference.GetProperty("executable").GetBoolean(), "Documentation must not grant execution.");
        string id = reference.GetProperty("id").GetString()!;
        var first = JsonSerializer.SerializeToElement(KnowledgeLibrary.Get(id, limit: 30));
        Check(first.GetProperty("source").GetString()!.Length == 30 && first.GetProperty("next_offset").GetInt32() == 30, "Reference output must be paginated.");
        var detail = JsonSerializer.SerializeToElement(KnowledgeLibrary.Get(id));
        Check(!detail.GetProperty("source").GetString()!.Contains("public static class Candidate"), "Runtime pack must exclude generated code.");
        Check(detail.GetProperty("research_status").GetString() is not null, "Retain research qualification separately from execution status.");
        var timer = Stopwatch.StartNew();
        for (int i = 0; i < 30; i++) Search("WallType.Width", "read");
        Console.WriteLine($"PASS: 3,000 shared references, read/change distinction, explicit adapter mapping, documentation-only boundary and pagination; warm search mean {timer.Elapsed.TotalMilliseconds / 30:F2} ms.");
    }
}
