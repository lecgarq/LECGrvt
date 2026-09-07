using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core.Rename;
using LECG.RevitCopilot.Agent;
using LECG.Services;

namespace LECG.RevitCopilot.Revit;

internal sealed partial class ToolExecutor
{
    private readonly Func<string, string, bool> _confirm;
    private readonly Dictionary<string, ChangePlan> _plans = [];
    private static long _documentRevision;

    internal ToolExecutor(Func<string, string, bool>? confirmation = null, string? recipePath = null)
    {
        _confirm = confirmation ?? ConfirmInRevit;
        if (recipePath is not null) _recipes = new WorkflowLibrary(recipePath);
    }
    internal static void NotifyDocumentChanged() => Interlocked.Increment(ref _documentRevision);
    private static string DocumentKey(Document doc) => doc.ProjectInformation.UniqueId;

    private object PreviewChange(Document doc, string operation, JsonElement args)
    {
        CapabilityCatalog.Require(operation, "change");
        ValidateChangeDocument(doc);
        // Exercise the real operation in a transaction and roll it back, including dependent deletions.
        object result = ChangeTransaction(doc, operation, args, commit: false);
        foreach (string expired in _plans.Where(p => p.Value.Expires < DateTimeOffset.UtcNow).Select(p => p.Key).ToArray()) _plans.Remove(expired);
        if (_plans.Count >= 20) _plans.Remove(_plans.Keys.First());
        string id = Guid.NewGuid().ToString("N");
        _plans[id] = new(DocumentKey(doc), Interlocked.Read(ref _documentRevision), operation, args.Clone(),
            JsonSerializer.Serialize(result, JsonOptions), DateTimeOffset.UtcNow.AddMinutes(10));
        return new { preview_id = id, operation, status = "preview_rolled_back", result,
            next_step = "Explain this preview to the user. agent_apply requires a local Revit confirmation. Any document change or 10-minute expiry invalidates the preview." };
    }

    private object ApplyChange(Document doc, string id)
    {
        if (!_plans.Remove(id, out ChangePlan? plan)) throw new ArgumentException("Preview is missing or already used. Generate a new preview.");
        ValidateChangeDocument(doc);
        if (plan.DocumentKey != DocumentKey(doc) || plan.Revision != Interlocked.Read(ref _documentRevision) || plan.Expires < DateTimeOffset.UtcNow)
            throw new InvalidOperationException("The document changed or the preview expired. Generate a fresh preview.");
        if (!_confirm($"Apply {plan.Operation} to {doc.Title}?", plan.Preview)) return new { status = "cancelled", operation = plan.Operation };
        object result = ChangeTransaction(doc, plan.Operation, plan.Arguments, commit: true);
        return new { status = "committed", operation = plan.Operation, result, receipt_id = RecordSuccess(plan.Operation, plan.Arguments) };
    }

    private static void ValidateChangeDocument(Document doc)
    {
        if (doc.IsFamilyDocument || doc.IsLinked || doc.IsReadOnly || doc.IsModifiable)
            throw new InvalidOperationException("This operation requires an editable project document with no transaction already open.");
    }

    private static object ChangeTransaction(Document doc, string operation, JsonElement args, bool commit)
    {
        using Transaction transaction = new(doc, $"Copilot: {operation}");
        if (transaction.Start() != TransactionStatus.Started) throw new InvalidOperationException("Cannot start the Revit transaction.");
        transaction.SetFailureHandlingOptions(transaction.GetFailureHandlingOptions().SetClearAfterRollback(true).SetFailuresPreprocessor(new RejectFailures()));
        try
        {
            object result = PerformChange(doc, operation, args);
            doc.Regenerate();
            TransactionStatus status = commit ? transaction.Commit() : transaction.RollBack();
            if (status != (commit ? TransactionStatus.Committed : TransactionStatus.RolledBack)) throw new InvalidOperationException($"Revit transaction ended with {status}.");
            return result;
        }
        catch
        {
            if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
            throw;
        }
    }

    private static object PerformChange(Document doc, string operation, JsonElement args)
    {
        if (operation == AgentBatch.ChangeOperation) return PerformChangeBatch(doc, args);
        if (RevitApiCatalog.IsApiOperation(operation)) return ExecuteApiProperty(doc, operation, args, change: true);
        Element[] targets = args.TryGetProperty("unique_ids", out _) ? ReadElements(doc, args) :
            args.TryGetProperty("unique_id", out var single) ? [RequireElement(doc, single.GetString()!)] : [];
        if (operation != "create_level" && targets.Length == 0) throw new ArgumentException("Supply explicit target unique IDs.");
        foreach (Element element in targets) ValidateMutable(doc, element);
        long[] ids = targets.Select(e => e.Id.Value).ToArray();
        switch (operation)
        {
            case "rename_elements":
                return RenameElements(targets, args);
            case "set_parameters":
            {
                string name = RequireString(args, "parameter_name");
                JsonElement value = RequireProperty(args, "new_value");
                var changes = new List<object>();
                foreach (Element element in targets)
                {
                    Parameter p = ExactParameter(element, name);
                    object before = SerializeParameter(doc, p);
                    if (p.IsReadOnly || !SetParameterValue(doc, p, value)) throw new InvalidOperationException($"Parameter update rejected on {element.Id.Value}.");
                    changes.Add(new { id = element.Id.Value, before, after = SerializeParameter(doc, p) });
                }
                return new { changes };
            }
            case "set_pinned":
                foreach (Element e in targets) e.Pinned = RequireProperty(args, "pinned").GetBoolean();
                return new { ids, pinned = RequireProperty(args, "pinned").GetBoolean() };
            case "set_type":
            {
                Element type = RequireElement(doc, RequireString(args, "type_unique_id"));
                if (type is not ElementType) throw new ArgumentException("The target must be an element type.");
                List<long> replacements = [];
                foreach (Element e in targets)
                {
                    if (!e.GetValidTypes().Contains(type.Id)) throw new ArgumentException($"Type is not compatible with {e.Id.Value}.");
                    ElementId replacement = e.ChangeTypeId(type.Id);
                    replacements.Add(replacement == ElementId.InvalidElementId ? e.Id.Value : replacement.Value);
                }
                return new { ids, resulting_ids = replacements, type_id = type.Id.Value };
            }
            case "move_elements":
                ElementTransformUtils.MoveElements(doc, targets.Select(e => e.Id).ToList(), new XYZ(Feet(Number(args, "x_mm")), Feet(Number(args, "y_mm")), Feet(Number(args, "z_mm"))));
                return new { ids, x_mm = Number(args, "x_mm"), y_mm = Number(args, "y_mm"), z_mm = Number(args, "z_mm") };
            case "rotate_elements":
            {
                XYZ origin = new(Feet(Number(args, "origin_x_mm")), Feet(Number(args, "origin_y_mm")), 0);
                ElementTransformUtils.RotateElements(doc, targets.Select(e => e.Id).ToList(), Line.CreateBound(origin, origin + XYZ.BasisZ), Number(args, "angle_degrees") * Math.PI / 180);
                return new { ids, angle_degrees = Number(args, "angle_degrees") };
            }
            case "delete_elements":
            {
                var deleted = doc.Delete(targets.Select(e => e.Id).ToList());
                if (deleted.Count > 200) throw new InvalidOperationException("Deletion would affect more than 200 elements. Narrow the request.");
                return new { requested_ids = ids, deleted_ids_including_dependents = deleted.Select(i => i.Value).Order().ToArray() };
            }
            case "create_level":
            {
                Level level = Level.Create(doc, Feet(Number(args, "elevation_mm")));
                level.Name = ValidName(args);
                return new { name = level.Name, elevation_mm = Mm(level.Elevation), created_id = level.Id.Value, unique_id = level.UniqueId };
            }
            case "duplicate_type":
            {
                ElementType type = targets.Single() as ElementType ?? throw new ArgumentException("Select an element type.");
                ElementType created = type.Duplicate(ValidName(args));
                return new { name = created.Name, created_id = created.Id.Value, unique_id = created.UniqueId };
            }
            case "duplicate_view":
            {
                View view = targets.Single() as View ?? throw new ArgumentException("Select a view.");
                if (!Enum.TryParse(RequireString(args, "mode"), out ViewDuplicateOption option) || !Enum.IsDefined(option) || !view.CanViewBeDuplicated(option))
                    throw new ArgumentException("This view cannot be duplicated with the requested mode.");
                Element created = doc.GetElement(view.Duplicate(option));
                created.Name = ValidName(args);
                return new { name = created.Name, created_id = created.Id.Value, unique_id = created.UniqueId };
            }
            case "material_color":
            case "material_graphics_solid":
            {
                Color color = new(Byte(args, "red"), Byte(args, "green"), Byte(args, "blue"));
                foreach (Element e in targets)
                {
                    Material material = e as Material ?? throw new ArgumentException("Select materials only.");
                    if (operation == "material_graphics_solid")
                        new RenderMaterialGraphicsApplyService().Apply(material, color, new RenderSolidFillPatternService().GetSolidFillPatternId(doc));
                    else
                    {
                        int transparency = RequireProperty(args, "transparency").GetInt32();
                        if (transparency is < 0 or > 100) throw new ArgumentException("Transparency must be 0 to 100.");
                        material.UseRenderAppearanceForShading = false;
                        material.Color = color;
                        material.Transparency = transparency;
                    }
                }
                return new { ids, rgb = new[] { color.Red, color.Green, color.Blue }, operation };
            }
            case "assign_material":
            {
                Material material = RequireElement(doc, RequireString(args, "material_unique_id")) as Material ?? throw new ArgumentException("Select a material.");
                string parameterName = RequireString(args, "parameter_name");
                foreach (Element element in targets)
                {
                    Parameter parameter = ExactParameter(element, parameterName);
                    if (parameter.IsReadOnly || parameter.StorageType != StorageType.ElementId || parameter.Definition.GetDataType() != SpecTypeId.Reference.Material || !parameter.Set(material.Id))
                        throw new ArgumentException($"'{parameterName}' is not a writable material parameter on {element.Id.Value}.");
                }
                return new { ids, material = material.Name, material_id = material.Id.Value, parameter_name = parameterName };
            }
            case "view_scale":
            {
                int scale = RequireProperty(args, "scale").GetInt32();
                if (scale is < 1 or > 2000) throw new ArgumentException("Scale denominator must be 1 to 2000.");
                foreach (Element element in targets) (element as View ?? throw new ArgumentException("Select views only.")).Scale = scale;
                return new { ids, scale };
            }
            case "slab_offset":
            {
                double offset = Feet(Number(args, "offset_mm"));
                double projectOffset = UnitUtils.ConvertFromInternalUnits(offset, doc.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId());
                foreach (Element e in targets)
                    if (e is not Floor || !new OffsetService().TryOffsetElement(doc, e, projectOffset)) throw new ArgumentException($"Slab offset is unavailable for {e.Id.Value}; select floors with writable offsets.");
                return new { ids, offset_mm = Number(args, "offset_mm") };
            }
            case "slab_reset":
            {
                var results = new List<object>();
                foreach (Element e in targets)
                {
                    if (e is not Floor and not Toposolid) throw new ArgumentException("Select floors or toposolids only.");
                    bool changed = new SlabService().TryResetSlabShape(e, out string message);
                    results.Add(new { id = e.Id.Value, changed, message });
                }
                return new { results };
            }
            default: throw new ArgumentException($"Unsupported change '{operation}'.");
        }
    }

    private static object RenameElements(Element[] elements, JsonElement args)
    {
        List<object> changes = [];
        foreach (Element element in elements)
        {
            if (element is not ElementType and not View and not Level and not Grid and not Material)
                throw new ArgumentException("Rename supports types, views/sheets, levels, grids, and materials. Use parameters for other names.");
            string before = element.Name;
            string after = RenameRuleEngine.ApplyReplace(before, new(true, OptionalString(args, "find") ?? "", OptionalString(args, "replace") ?? "", false, false));
            string? casing = OptionalString(args, "case");
            if (casing is not null)
            {
                if (!Enum.TryParse(casing, true, out CoreCaseMode mode) || !Enum.IsDefined(mode)) throw new ArgumentException("Invalid case rule.");
                after = RenameRuleEngine.ApplyCase(after, new(true, mode));
            }
            after = RenameRuleEngine.ApplyAdd(after, new(true, OptionalString(args, "prefix") ?? "", OptionalString(args, "suffix") ?? "", "", 0));
            if (string.IsNullOrWhiteSpace(after) || !NamingUtils.IsValidName(after)) throw new ArgumentException($"Invalid resulting name '{after}'.");
            element.Name = after;
            changes.Add(new { id = element.Id.Value, before, after });
        }
        return new { changes };
    }

    private static double Number(JsonElement args, string key)
    {
        double n = RequireProperty(args, key).GetDouble();
        return double.IsFinite(n) ? n : throw new ArgumentException($"{key} must be finite.");
    }
    private static byte Byte(JsonElement args, string key) => checked((byte)RequireProperty(args, key).GetInt32());
    private static string ValidName(JsonElement args)
    {
        string name = RequireString(args, "name");
        return NamingUtils.IsValidName(name) ? name : throw new ArgumentException("Invalid Revit name.");
    }
    private static bool ConfirmInRevit(string question, string details)
    {
        using TaskDialog dialog = new("LECG Copilot confirmation") { MainInstruction = question, MainContent = "Review the preview details before applying this operation.",
            ExpandedContent = details, CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No, DefaultButton = TaskDialogResult.No };
        return dialog.Show() == TaskDialogResult.Yes;
    }
    private sealed record ChangePlan(string DocumentKey, long Revision, string Operation, JsonElement Arguments, string Preview, DateTimeOffset Expires);
    private sealed class RejectFailures : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor) => accessor.GetFailureMessages().Count > 0
            ? FailureProcessingResult.ProceedWithRollBack : FailureProcessingResult.Continue;
    }
}
