using System.Diagnostics;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.RevitCopilot.Revit;

namespace LECG.RevitCopilot.SmokeTests;

internal static class HarvestReadSmokeChecks
{
    internal static void Run(UIApplication app, Document doc, Wall wall, Floor floor, Level level, List<object> results)
    {
        Opening opening;
        using (var transaction = new Transaction(doc, "Harvest read fixtures"))
        {
            transaction.Start();
            try
            {
                opening = doc.Create.NewOpening(wall, new XYZ(2, 0, 1), new XYZ(4, 0, 4));
                doc.Create.NewOpening(wall, new XYZ(6, 0, 1), new XYZ(8, 0, 4));
                Require(transaction.Commit() == TransactionStatus.Committed, "Opening fixtures did not commit.");
            }
            catch { if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack(); throw; }
        }
        var executor = new ToolExecutor((_, _) => throw new Exception("Read unexpectedly requested change confirmation."));
        int countBefore = new FilteredElementCollector(doc).GetElementCount();
        long phaseBefore = wall.CreatedPhaseId.Value;
        JsonElement Read(string operation, object arguments, bool success = true)
        {
            using var result = JsonDocument.Parse(executor.Execute(app, "agent_read", JsonSerializer.Serialize(new { operation, arguments_json = JsonSerializer.Serialize(arguments) })));
            Require(result.RootElement.GetProperty("success").GetBoolean() == success, result.RootElement.ToString());
            Require(!doc.IsModifiable, "Read left an open transaction.");
            return success ? result.RootElement.GetProperty("data").GetProperty("result").Clone() : result.RootElement.Clone();
        }
        var inserts = Read("host_inserts", new { unique_id = wall.UniqueId, limit = 1 }).GetProperty("inserts");
        Require(inserts.GetProperty("total_count").GetInt32() == 2 && inserts.GetProperty("items").GetArrayLength() == 1 && inserts.GetProperty("next_offset").GetInt32() == 1, "Insert paging failed.");
        Require(inserts.GetProperty("items")[0].GetProperty("unique_id").GetString() == opening.UniqueId, "Unexpected opening ID.");
        Require(Read("host_inserts", new { unique_id = wall.UniqueId, include_rectangular_openings = false }).GetProperty("inserts").GetProperty("total_count").GetInt32() == 0, "Opening filter ignored.");
        Require(Read("host_inserts", new { unique_id = wall.UniqueId, offset = 50 }).GetProperty("inserts").GetProperty("items").GetArrayLength() == 0, "Out-of-range page should be empty.");
        var phase = Read("element_phase_status", new { unique_ids = new[] { wall.UniqueId } }).GetProperty("items")[0];
        Require(phase.GetProperty("phases_modifiable").GetBoolean() == wall.ArePhasesModifiable() && phase.GetProperty("created_phase_id").GetInt64() == phaseBefore, "Phase result differs from direct API.");
        var faces = Read("host_bottom_faces", new { unique_id = floor.UniqueId }).GetProperty("faces").GetProperty("items");
        double area = faces.EnumerateArray().Sum(face => face.GetProperty("area_m2").GetDouble());
        Require(Math.Abs(area - UnitUtils.ConvertFromInternalUnits(100, UnitTypeId.SquareMeters)) < 1e-6, "Bottom-face area or unit conversion is wrong.");
        Require(Reference.ParseFromStableRepresentation(doc, faces[0].GetProperty("stable_reference").GetString()!).ElementId == floor.Id, "Face reference does not resolve to fixture.");
        Read("host_inserts", new { unique_id = level.UniqueId }, false);
        Read("host_inserts", new { unique_id = wall.UniqueId, include_shadows = "yes" }, false);
        Read("host_bottom_faces", new { unique_id = wall.UniqueId }, false);
        Read("element_phase_status", new { unique_ids = Array.Empty<string>() }, false);
        Read("element_phase_status", new { unique_ids = Enumerable.Range(0, 51).Select(i => "invalid-" + i).ToArray() }, false);
        Read("host_inserts", new { unique_id = Guid.NewGuid().ToString("D") + "-00000001" }, false);
        var timer = Stopwatch.StartNew();
        for (int i = 0; i < 20; i++) Read("host_inserts", new { unique_id = wall.UniqueId, limit = 2 });
        Require(new FilteredElementCollector(doc).GetElementCount() == countBefore && wall.CreatedPhaseId.Value == phaseBefore, "Read operations mutated the document.");
        results.Add(new { test = "harvested_native_reads", passed = true, operations = new[] { "host_inserts", "element_phase_status", "host_bottom_faces" },
            checks = "positive results, pagination, filters, square-meter units, stable reference, six error cases, no transaction or element-count/phase mutation",
            host_inserts_warm_mean_ms = timer.Elapsed.TotalMilliseconds / 20 });
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
