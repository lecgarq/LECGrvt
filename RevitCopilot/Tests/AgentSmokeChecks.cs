using System.IO;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.RevitCopilot.Agent;
using LECG.RevitCopilot.Revit;

namespace LECG.RevitCopilot.SmokeTests;

internal static class AgentSmokeChecks
{
    internal static void Run(UIApplication app, Document doc, Wall wall, List<object> results)
    {
        string path = Path.Combine(Path.GetTempPath(), "LECG-AgentSmoke-" + Guid.NewGuid().ToString("N"), "recipes.json");
        // This executor is test-owned; production always uses a local Revit confirmation dialog.
        var executor = new ToolExecutor((_, _) => true, path);
        Level level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().First();
        Material material;
        ElementType type;
        ViewDrafting view;
        ViewSheet sheet;
        ViewSchedule schedule;
        SelectionFilterElement filter;
        Floor floor;
        Wall disposable;
        XYZ origin = FindFixtureOrigin(doc);
        using (Transaction fixture = new(doc, "Agent smoke fixtures"))
        {
            fixture.Start();
            try
            {
                material = (Material)doc.GetElement(Material.Create(doc, "Agent material"));
                type = ((ElementType)doc.GetElement(wall.GetTypeId())).Duplicate("Agent wall type");
                var draftType = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().First(v => v.ViewFamily == ViewFamily.Drafting);
                view = ViewDrafting.Create(doc, draftType.Id);
                sheet = ViewSheet.Create(doc, ElementId.InvalidElementId);
                Grid.Create(doc, Line.CreateBound(origin + new XYZ(0, 20, 0), origin + new XYZ(10, 20, 0)));
                schedule = ViewSchedule.CreateSchedule(doc, new ElementId(BuiltInCategory.OST_Walls));
                schedule.RowHeightOverride = RowHeightOverrideOptions.None;
                var field = schedule.Definition.AddField(ScheduleFieldType.Instance, new ElementId(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS));
                field.ColumnHeading = "Verified comments column";
                filter = SelectionFilterElement.Create(doc, "Agent selection filter");
                filter.SetElementIds(new[] { wall.Id });
                view.AddFilter(filter.Id);
                view.SetFilterVisibility(filter.Id, false);
                var floorType = new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>().First();
                XYZ[] corners = [origin, origin + new XYZ(10, 0, 0), origin + new XYZ(10, 10, 0), origin + new XYZ(0, 10, 0)];
                CurveLoop loop = new();
                for (int i = 0; i < 4; i++) loop.Append(Line.CreateBound(corners[i], corners[(i + 1) % 4]));
                floor = Floor.Create(doc, [loop], floorType.Id, level.Id);
                disposable = Wall.Create(doc, Line.CreateBound(origin + new XYZ(0, 15, 0), origin + new XYZ(10, 15, 0)), wall.GetTypeId(), level.Id, 10, 0, false, false);
                Require(fixture.Commit() == TransactionStatus.Committed, "Agent fixture creation failed.");
            }
            catch { if (fixture.GetStatus() == TransactionStatus.Started) fixture.RollBack(); throw; }
        }
        app.ActiveUIDocument.Selection.SetElementIds([wall.Id]);
        string comments = wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Definition.Name;
        JsonElement Call(string tool, object arguments, bool success = true)
        {
            using var parsed = JsonDocument.Parse(executor.Execute(app, tool, JsonSerializer.Serialize(arguments)));
            var root = parsed.RootElement;
            Require(root.GetProperty("success").GetBoolean() == success, root.ToString());
            Require(!doc.IsModifiable, "Agent operation left a transaction open.");
            return success ? root.GetProperty("data").Clone() : root.Clone();
        }
        JsonElement Preview(string operation, object arguments) => Call("agent_preview", new { operation, arguments_json = JsonSerializer.Serialize(arguments) });
        JsonElement Apply(JsonElement preview) => Call("agent_apply", new { preview_id = preview.GetProperty("preview_id").GetString() });
        object Uids(Element e) => new { unique_ids = new[] { e.UniqueId } };
        try
        {
            HarvestReadSmokeChecks.Run(app, doc, wall, floor, level, results);
            SharedKnowledgeReadChecks.Run(app, doc, wall, schedule, view, filter, results);
            ReviewedMethodSmokeChecks.Run(app, doc, wall, floor, results);
            foreach (Capability capability in CapabilityCatalog.All.Where(c => c.Kind == "read" && !ReviewedMethodSmokeChecks.Operations.Contains(c.Name)))
            {
                object arguments = capability.Name switch
                {
                    "types_list" or "elements_count" => new { category = "OST_Walls" },
                    "parameters_get" => new { unique_ids = new[] { wall.UniqueId }, parameter_names = new[] { comments } },
                    "sheet_views" => new { unique_id = sheet.UniqueId },
                    "element_materials" or "element_bounds" => new { unique_id = wall.UniqueId },
                    "host_inserts" => new { unique_id = wall.UniqueId },
                    "element_dependents" or "element_valid_types" => new { unique_id = wall.UniqueId },
                    "schedule_fields" => new { unique_id = schedule.UniqueId },
                    "view_filters" => new { unique_id = view.UniqueId },
                    "knowledge_get" => new { reference_id = "742369128674aae8a9b74352" },
                    "host_bottom_faces" => new { unique_id = floor.UniqueId },
                    "element_phase_status" => new { unique_ids = new[] { wall.UniqueId } },
                    _ => new { limit = 2 }
                };
                Call("agent_read", new { operation = capability.Name, arguments_json = JsonSerializer.Serialize(arguments) });
                results.Add(new { test = "agent_read:" + capability.Name, passed = true });
            }

            (string Name, object Args)[] changes =
            [
                ("rename_elements", new { unique_ids = new[] { type.UniqueId }, prefix = "Verified " }),
                ("set_parameters", new { unique_ids = new[] { wall.UniqueId }, parameter_name = comments, new_value = "Agent verified" }),
                ("set_pinned", new { unique_ids = new[] { wall.UniqueId }, pinned = false }),
                ("set_type", new { unique_ids = new[] { wall.UniqueId }, type_unique_id = type.UniqueId }),
                ("move_elements", new { unique_ids = new[] { wall.UniqueId }, x_mm = 10, y_mm = 0, z_mm = 0 }),
                ("rotate_elements", new { unique_ids = new[] { wall.UniqueId }, origin_x_mm = 0, origin_y_mm = 0, angle_degrees = 2 }),
                ("delete_elements", Uids(disposable)),
                ("create_level", new { name = "Agent new level", elevation_mm = 3000 }),
                ("duplicate_type", new { unique_id = type.UniqueId, name = "Agent duplicated type" }),
                ("duplicate_view", new { unique_id = view.UniqueId, name = "Agent duplicated view", mode = "Duplicate" }),
                ("material_color", new { unique_ids = new[] { material.UniqueId }, red = 200, green = 100, blue = 50, transparency = 20 }),
                ("material_graphics_solid", new { unique_ids = new[] { material.UniqueId }, red = 30, green = 60, blue = 90 }),
                ("view_scale", new { unique_ids = new[] { view.UniqueId }, scale = 50 }),
                ("slab_offset", new { unique_ids = new[] { floor.UniqueId }, offset_mm = 100 }),
                ("slab_reset", Uids(floor)),
            ];
            string? recipeReceipt = null;
            foreach (var change in changes)
            {
                string oldName = type.Name;
                int beforeCount = CountElements(doc);
                JsonElement preview = Preview(change.Name, change.Args);
                Require(preview.GetProperty("status").GetString() == "preview_rolled_back", "Preview did not roll back.");
                Require(beforeCount == CountElements(doc) && type.Name == oldName, "Preview persisted a change.");
                JsonElement applied = Apply(preview);
                Require(applied.GetProperty("status").GetString() == "committed", "Change failed to commit.");
                recipeReceipt = applied.GetProperty("receipt_id").GetString();
                Call("agent_apply", new { preview_id = preview.GetProperty("preview_id").GetString() }, false);
                results.Add(new { test = "agent_change:" + change.Name, passed = true });
            }
            Require(wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString() == "Agent verified", "Parameter batch was not applied.");
            Require(Math.Abs(floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).AsDouble() - UnitUtils.ConvertToInternalUnits(100, UnitTypeId.Millimeters)) < 1e-8, "Shared offset service changed units incorrectly.");
            Require(view.Scale == 50 && material.Color.Red == 30, "View or shared material service result incorrect.");
            string typeName = type.Name;
            Call("agent_preview", new { operation = "rename_elements", arguments_json = JsonSerializer.Serialize(new { unique_ids = new[] { type.UniqueId, wall.UniqueId }, prefix = "Must rollback " }) }, false);
            Require(type.Name == typeName, "Failed batch did not roll back its earlier edits.");
            Call("agent_preview", new { operation = "assign_material", arguments_json = JsonSerializer.Serialize(new { unique_ids = new[] { wall.UniqueId }, parameter_name = comments, material_unique_id = material.UniqueId }) }, false);
            results.Add(new { test = "agent_failure_rollback_and_material_parameter_guard", passed = true });

            JsonElement stale = Preview("rename_elements", new { unique_ids = new[] { type.UniqueId }, prefix = "Stale " });
            using (Transaction edit = new(doc, "Invalidate agent preview")) { edit.Start(); material.Transparency = 21; edit.Commit(); }
            Call("agent_apply", new { preview_id = stale.GetProperty("preview_id").GetString() }, false);
            Require(type.Name == typeName, "Stale preview modified the model.");
            results.Add(new { test = "agent_stale_preview_rejected", passed = true });

            JsonElement saved = Call("agent_recipe_save", new { name = "Test recipe", description = "Verified workflow", receipt_ids = new[] { recipeReceipt } });
            Call("agent_recipe_get", new { recipe_id = saved.GetProperty("id").GetString() });
            Call("agent_recipe_search", new { query = "Test recipe" });
            Require(!File.ReadAllText(path).Contains(floor.UniqueId), "Saved recipe retained an old element identifier.");
            results.Add(new { test = "agent_recipe_save_get_search_fresh_inputs", passed = true });
        }
        finally { if (Directory.Exists(Path.GetDirectoryName(path))) Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static XYZ FindFixtureOrigin(Document doc)
    {
        double maxX = 0;
        double maxY = 0;
        foreach (Element element in new FilteredElementCollector(doc).WhereElementIsNotElementType())
        {
            BoundingBoxXYZ? box = element.get_BoundingBox(null);
            if (box is null) continue;
            maxX = Math.Max(maxX, box.Max.X);
            maxY = Math.Max(maxY, box.Max.Y);
        }
        return new XYZ(Math.Ceiling(maxX / 100) * 100 + 100, Math.Ceiling(maxY / 100) * 100 + 100, 0);
    }
    private static int CountElements(Document doc) => new FilteredElementCollector(doc).WhereElementIsNotElementType().GetElementCount()
        + new FilteredElementCollector(doc).WhereElementIsElementType().GetElementCount();
}
