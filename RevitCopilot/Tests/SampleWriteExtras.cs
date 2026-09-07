using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.RevitCopilot.Revit;

namespace LECG.RevitCopilot.SmokeTests;

internal static class SampleWriteExtras
{
    internal static void Run(UIApplication app, Document doc, ToolExecutor executor, Material material, string model, List<object> rows)
    {
        JsonElement Call(string tool, object args)
        {
            using var parsed = JsonDocument.Parse(executor.Execute(app, tool, JsonSerializer.Serialize(args)));
            if (!parsed.RootElement.GetProperty("success").GetBoolean()) throw new InvalidOperationException(parsed.RootElement.ToString());
            if (doc.IsModifiable) throw new InvalidOperationException("Operation left a transaction open.");
            return parsed.RootElement.GetProperty("data").Clone();
        }
        JsonElement Preview(string operation, object args) => Call("agent_preview", new { operation, arguments_json = JsonSerializer.Serialize(args) });
        JsonElement Apply(JsonElement preview) => Call("agent_apply", new { preview_id = preview.GetProperty("preview_id").GetString() });
        void Test(string operation, Action action)
        {
            try { action(); rows.Add(new { model, operation, status = "passed", verification = "Fresh preview/apply and direct API postcondition; outer rollback." }); }
            catch (Exception ex) { rows.Add(new { model, operation, status = "failed", error = ex.Message }); }
        }
        Test("workflow_change_batch", () =>
        {
            string name = "LECG batch " + Guid.NewGuid().ToString("N");
            int before = new FilteredElementCollector(doc).OfClass(typeof(Level)).GetElementCount();
            var preview = Preview("workflow_change_batch", new { steps = new object[] {
                new { id = "level", operation = "create_level", arguments = new { name, elevation_mm = 6500 } },
                new { id = "rename", operation = "rename_elements", arguments = new { unique_ids = new[] { "$step:level.unique_id" }, prefix = "Verified " } }
            } });
            if (new FilteredElementCollector(doc).OfClass(typeof(Level)).GetElementCount() != before) throw new InvalidOperationException("Batch preview leaked its level.");
            var applied = Apply(preview);
            string uid = applied.GetProperty("result").GetProperty("steps")[0].GetProperty("result").GetProperty("unique_id").GetString()!;
            if (doc.GetElement(uid).Name != "Verified " + name || new FilteredElementCollector(doc).OfClass(typeof(Level)).GetElementCount() != before + 1)
                throw new InvalidOperationException("Batch dependency did not use the fresh committed level.");
        });
        // Find a real unambiguous writable material parameter, then duplicate its type as a disposable fixture.
        var target = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().Take(250)
            .SelectMany(symbol => symbol.Parameters.Cast<Parameter>().Where(p => !p.IsReadOnly && p.StorageType == StorageType.ElementId &&
                p.Definition.GetDataType() == SpecTypeId.Reference.Material && symbol.GetParameters(p.Definition.Name).Count == 1)
                .Select(p => new { Symbol = symbol, Name = p.Definition.Name })).FirstOrDefault();
        if (target is null)
        {
            rows.Add(new { model, operation = "assign_material", status = "unsupported", reason = "No writable material parameter in the bounded family-type fixture search." });
            return;
        }
        Test("assign_material", () =>
        {
            ElementType fixture;
            using (var tx = new Transaction(doc, "Disposable material parameter fixture"))
            {
                tx.Start();
                try
                {
                    fixture = target.Symbol.Duplicate("LECG material fixture " + Guid.NewGuid().ToString("N"));
                    if (tx.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Material fixture did not commit.");
                }
                catch { if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack(); throw; }
            }
            Parameter parameter = fixture.LookupParameter(target.Name);
            long before = parameter.AsElementId().Value;
            var preview = Preview("assign_material", new { unique_ids = new[] { fixture.UniqueId }, parameter_name = target.Name, material_unique_id = material.UniqueId });
            if (parameter.AsElementId().Value != before) throw new InvalidOperationException("Material preview leaked a change.");
            Apply(preview);
            if (parameter.AsElementId() != material.Id) throw new InvalidOperationException("Material parameter did not match the requested material.");
        });
    }
}
