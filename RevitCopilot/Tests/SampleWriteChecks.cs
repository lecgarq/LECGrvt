using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.RevitCopilot.Revit;

namespace LECG.RevitCopilot.SmokeTests;

internal static class SampleWriteChecks
{
    // Reviewed fixture-only operations. Never run generated candidate code or arbitrary setters.
    internal static void Run(UIApplication app, Document doc, string model, List<object> rows)
    {
        using var group = new TransactionGroup(doc, "Disposable benchmark fixtures - always roll back");
        if (group.Start() != TransactionStatus.Started) throw new InvalidOperationException("Cannot start benchmark rollback group.");
        List<object> checks = [];
        var originalViewIds = new FilteredElementCollector(doc).OfClass(typeof(ViewDrafting)).ToElementIds().ToHashSet();
        try
        {
            Wall wall;
            using (var tx = new Transaction(doc, "Create benchmark-only wall"))
            {
                tx.Start();
                try
                {
                    Level level = Level.Create(doc, 0);
                    level.Name = "LECG benchmark " + Guid.NewGuid().ToString("N");
                    var type = new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>().First(t => t.Kind == WallKind.Basic);
                    wall = Wall.Create(doc, Line.CreateBound(XYZ.Zero, new XYZ(10, 0, 0)), type.Id, level.Id, 10, 0, false, false);
                    if (tx.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Fixture commit failed.");
                }
                catch { if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack(); throw; }
            }
            AgentSmokeChecks.Run(app, doc, wall, checks);
            foreach (var check in checks)
            {
                var json = JsonSerializer.SerializeToElement(check);
                string name = json.GetProperty("test").GetString()!;
                if (name.StartsWith("agent_read:") || name.StartsWith("agent_change:"))
                    rows.Add(new { model, operation = name[(name.IndexOf(':') + 1)..], status = "passed", verification = "Reviewed fixture smoke checks inside an outer rollback group." });
            }
            rows.Add(new { model, operation = "reviewed_fixture_suite", status = "passed", checks });
            var executor = new ToolExecutor((_, _) => true);
            var levelFixture = (Level)doc.GetElement(wall.LevelId);
            var viewFixture = new FilteredElementCollector(doc).OfClass(typeof(ViewDrafting)).Cast<ViewDrafting>().First(v => !originalViewIds.Contains(v.Id));
            var materialFixture = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>().First(m => m.Name == "Agent material");
            TestSetter(executor, app, doc, model, rows, levelFixture, "Element.Name", "LECG benchmark setter", () => levelFixture.Name == "LECG benchmark setter");
            TestSetter(executor, app, doc, model, rows, wall, "Element.Pinned", true, () => wall.Pinned);
            TestSetter(executor, app, doc, model, rows, viewFixture, "View.Scale", 75, () => viewFixture.Scale == 75);
            TestSetter(executor, app, doc, model, rows, materialFixture, "Material.Color", new { red = 12, green = 34, blue = 56 }, () => materialFixture.Color.Red == 12 && materialFixture.Color.Green == 34 && materialFixture.Color.Blue == 56);
            TestSetter(executor, app, doc, model, rows, materialFixture, "Material.Transparency", 35, () => materialFixture.Transparency == 35);
            TestSetter(executor, app, doc, model, rows, levelFixture, "Level.Elevation", 1.0, () => Math.Abs(levelFixture.Elevation - 1) < 1e-8, "revit_internal");
            TestSetter(executor, app, doc, model, rows, wall.WallType, "WallType.Function", "Interior", () => wall.WallType.Function == WallFunction.Interior);
            SampleWriteExtras.Run(app, doc, executor, materialFixture, model, rows);
        }
        catch (Exception ex)
        {
            // Preserve partial evidence, but never label unexecuted cases passed.
            rows.Add(new { model, operation = "reviewed_fixture_suite", status = "failed", error = ex.ToString(), completed_checks = checks });
        }
        finally
        {
            if (group.GetStatus() == TransactionStatus.Started && group.RollBack() != TransactionStatus.RolledBack)
                throw new InvalidOperationException("Benchmark fixture rollback failed.");
        }
    }

    private static void TestSetter(ToolExecutor executor, UIApplication app, Document doc, string model, List<object> rows,
        Element element, string member, object value, Func<bool> assert, string? units = null)
    {
        string operation = "api.set:Autodesk.Revit.DB." + member;
        try
        {
            string ReadValue()
            {
                using var read = JsonDocument.Parse(executor.Execute(app, "agent_read", JsonSerializer.Serialize(new {
                    operation = "api.get:Autodesk.Revit.DB." + member, arguments_json = JsonSerializer.Serialize(new { unique_ids = new[] { element.UniqueId } }) })));
                if (!read.RootElement.GetProperty("success").GetBoolean()) throw new InvalidOperationException("Cannot verify setter's original value.");
                return read.RootElement.GetProperty("data").GetProperty("result").GetProperty("items")[0].GetProperty("value").GetRawText();
            }
            string before = ReadValue();
            using var preview = JsonDocument.Parse(executor.Execute(app, "agent_preview", JsonSerializer.Serialize(new { operation,
                arguments_json = JsonSerializer.Serialize(new { unique_ids = new[] { element.UniqueId }, value, units }) })));
            if (!preview.RootElement.GetProperty("success").GetBoolean()) throw new InvalidOperationException(preview.RootElement.ToString());
            if (ReadValue() != before) throw new InvalidOperationException("Preview did not restore the original value.");
            using var apply = JsonDocument.Parse(executor.Execute(app, "agent_apply", JsonSerializer.Serialize(new { preview_id = preview.RootElement.GetProperty("data").GetProperty("preview_id").GetString() })));
            if (!apply.RootElement.GetProperty("success").GetBoolean() || !assert() || doc.IsModifiable) throw new InvalidOperationException("Setter postcondition failed: " + apply.RootElement);
            rows.Add(new { model, operation, status = "passed", verification = "Preview, fresh apply, direct API postcondition; outer rollback." });
        }
        catch (Exception ex) { rows.Add(new { model, operation, status = "failed", error = ex.Message }); }
    }
}
