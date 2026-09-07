using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.RevitCopilot.Revit;

namespace LECG.RevitCopilot.SmokeTests;

internal static class ReviewedMethodSmokeChecks
{
    internal static readonly HashSet<string> Operations = ["element_action_checks", "elements_joined", "type_compound_layers", "instance_transform"];
    internal static void Run(UIApplication app, Document doc, Wall wall, Floor floor, List<object> results)
    {
        void Check(bool pass, string message) { if (!pass) throw new InvalidOperationException(message); }
        var executor = new ToolExecutor((_, _) => throw new InvalidOperationException("Read unexpectedly requested a change."));
        int before = new FilteredElementCollector(doc).WherePasses(new ElementIsElementTypeFilter(true)).GetElementCount() + new FilteredElementCollector(doc).WhereElementIsElementType().GetElementCount();
        JsonElement Read(string operation, object args, bool expected = true)
        {
            using var json = JsonDocument.Parse(executor.Execute(app, "agent_read", JsonSerializer.Serialize(new { operation, arguments_json = JsonSerializer.Serialize(args) })));
            Check(json.RootElement.GetProperty("success").GetBoolean() == expected, json.RootElement.ToString());
            return expected ? json.RootElement.GetProperty("data").GetProperty("result").Clone() : json.RootElement.Clone();
        }
        var actions = Read("element_action_checks", new { unique_ids = new[] { wall.UniqueId } }).GetProperty("items")[0];
        Check(actions.GetProperty("can_delete").GetBoolean() == DocumentValidation.CanDeleteElement(doc, wall.Id) &&
            actions.GetProperty("can_mirror").GetBoolean() == ElementTransformUtils.CanMirrorElement(doc, wall.Id) &&
            actions.GetProperty("can_create_parts").GetBoolean() == PartUtils.AreElementsValidForCreateParts(doc, new[] { wall.Id }), "Action feasibility differs from the API.");
        results.Add(new { test = "agent_read:element_action_checks", passed = true });
        var joined = Read("elements_joined", new { first_unique_id = wall.UniqueId, second_unique_id = floor.UniqueId });
        Check(joined.GetProperty("joined").GetBoolean() == JoinGeometryUtils.AreElementsJoined(doc, wall, floor), "Join state differs from API.");
        Read("elements_joined", new { first_unique_id = wall.UniqueId, second_unique_id = wall.UniqueId }, false);
        using (var group = new TransactionGroup(doc, "Join-state disposable fixture"))
        {
            group.Start();
            try
            {
                Wall first, second;
                using (var tx = new Transaction(doc, "Create joined walls"))
                {
                    tx.Start();
                    try
                    {
                        first = Wall.Create(doc, Line.CreateBound(new XYZ(2000, 2000, 0), new XYZ(2010, 2000, 0)), wall.GetTypeId(), wall.LevelId, 10, 0, false, false);
                        second = Wall.Create(doc, Line.CreateBound(new XYZ(2005, 1995, 0), new XYZ(2005, 2005, 0)), wall.GetTypeId(), wall.LevelId, 10, 0, false, false);
                        doc.Regenerate();
                        if (!JoinGeometryUtils.AreElementsJoined(doc, first, second)) JoinGeometryUtils.JoinGeometry(doc, first, second);
                        Check(tx.Commit() == TransactionStatus.Committed, "Joined fixture did not commit.");
                    }
                    catch { if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack(); throw; }
                }
                Check(Read("elements_joined", new { first_unique_id = first.UniqueId, second_unique_id = second.UniqueId }).GetProperty("joined").GetBoolean(), "Joined fixture was reported unjoined.");
                using (var tx = new Transaction(doc, "Unjoin fixture"))
                {
                    tx.Start();
                    try { JoinGeometryUtils.UnjoinGeometry(doc, first, second); Check(tx.Commit() == TransactionStatus.Committed, "Unjoin did not commit."); }
                    catch { if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack(); throw; }
                }
                Check(!Read("elements_joined", new { first_unique_id = first.UniqueId, second_unique_id = second.UniqueId }).GetProperty("joined").GetBoolean(), "Unjoined fixture was reported joined.");
            }
            finally { Check(group.RollBack() == TransactionStatus.RolledBack, "Join fixture rollback failed."); }
        }
        results.Add(new { test = "agent_read:elements_joined", passed = true });
        var layers = Read("type_compound_layers", new { unique_id = wall.WallType.UniqueId, limit = 50 });
        using var structure = wall.WallType.GetCompoundStructure();
        var items = layers.GetProperty("layers").GetProperty("items");
        Check(items.GetArrayLength() == structure.GetLayers().Count, "Compound layer count differs.");
        for (int i = 0; i < items.GetArrayLength(); i++)
            Check(Math.Abs(items[i].GetProperty("width_mm").GetDouble() - UnitUtils.ConvertFromInternalUnits(structure.GetLayers()[i].Width, UnitTypeId.Millimeters)) < 1e-8
                && items[i].GetProperty("material_id").GetInt64() == structure.GetLayers()[i].MaterialId.Value, "Layer width, order or material differs.");
        Read("type_compound_layers", new { unique_id = wall.UniqueId }, false);
        results.Add(new { test = "agent_read:type_compound_layers", passed = true });
        Instance? instance = new FilteredElementCollector(doc).WhereElementIsNotElementType().OfType<Instance>().FirstOrDefault();
        if (instance is not null)
        {
            var data = Read("instance_transform", new { unique_id = instance.UniqueId });
            using var transform = instance.GetTotalTransform();
            void CheckVector(string field, XYZ expected, bool millimeters = false)
            {
                double[] values = [expected.X, expected.Y, expected.Z];
                string[] axes = ["x", "y", "z"];
                for (int i = 0; i < axes.Length; i++)
                    Check(Math.Abs(data.GetProperty(field).GetProperty(axes[i]).GetDouble()
                        - (millimeters ? UnitUtils.ConvertFromInternalUnits(values[i], UnitTypeId.Millimeters) : values[i])) < 1e-8, "Transform component differs: " + field + "." + axes[i]);
            }
            CheckVector("origin_mm", transform.Origin, true);
            CheckVector("origin_feet", transform.Origin);
            CheckVector("basis_x", transform.BasisX);
            CheckVector("basis_y", transform.BasisY);
            CheckVector("basis_z", transform.BasisZ);
            Check(data.GetProperty("has_reflection").GetBoolean() == transform.HasReflection
                && data.GetProperty("is_conformal").GetBoolean() == transform.IsConformal, "Transform classification differs.");
            results.Add(new { test = "agent_read:instance_transform", passed = true });
        }
        Read("instance_transform", new { unique_id = wall.UniqueId }, false);
        Check(!doc.IsModifiable && new FilteredElementCollector(doc).WherePasses(new ElementIsElementTypeFilter(true)).GetElementCount() + new FilteredElementCollector(doc).WhereElementIsElementType().GetElementCount() == before, "Reviewed methods changed the model or left a transaction.");
        results.Add(new { test = "reviewed_methods_no_mutation", passed = true, instance_fixture_available = instance is not null });
    }
}
