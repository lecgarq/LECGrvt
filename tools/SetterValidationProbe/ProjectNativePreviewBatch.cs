using System.IO;
using System.Reflection;
using System.Text.Json;
using Autodesk.Revit.DB;
using NUnit.Framework;

namespace LECG.SetterValidationProbe;

public sealed class ProjectNativePreviewBatch : SetterHarness
{
    protected override string ManifestName => "project-native-preview-manifest.json";
    protected override string PreregistrationName => "project-native-preview-preregistration.md";
    protected override string RunKind => "project-native-preview-runs";
    protected override bool IsReadOnlyProbe => true;

    protected override bool RunReadOnlyProbe(Document doc, Dictionary<string, object?> result)
    {
        string root = Environment.GetEnvironmentVariable("LECG_SETTER_REPO_ROOT")
            ?? throw new InvalidOperationException("LECG_SETTER_REPO_ROOT is required.");
        string assemblyDirectory = Path.GetDirectoryName(typeof(ProjectNativePreviewBatch).Assembly.Location)!;
        string copilotPath = Path.Combine(assemblyDirectory, "RevitCopilot.dll");
        if (Hash(copilotPath) != Manifest.RootElement.GetProperty("copilot_assembly_sha256").GetString())
            throw new InvalidOperationException("Tested Copilot assembly differs from the frozen manifest.");
        Assembly copilot = Assembly.LoadFile(copilotPath);
        Type catalog = copilot.GetType("LECG.RevitCopilot.Agent.CapabilityCatalog", throwOnError: true)!;
        Array capabilities = (Array)catalog.GetField("All", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        string[] operations = capabilities.Cast<object>().Where(capability =>
                (string)capability.GetType().GetProperty("Kind")!.GetValue(capability)! == "change")
            .Select(capability => (string)capability.GetType().GetProperty("Name")!.GetValue(capability)!)
            .Order(StringComparer.Ordinal).ToArray();
        if (operations.Length != 17) throw new InvalidOperationException("Native preview denominator changed.");
        MethodInfo probe = copilot.GetType("LECG.RevitCopilot.Revit.ToolExecutor", throwOnError: true)!
            .GetMethod("ProbeNativePreview", BindingFlags.Static | BindingFlags.NonPublic)!;
        DirectShape deleteFixture = CreateDeleteFixture(doc);
        Element[] allElements = PilotValues.Elements(doc);
        Element[] elements = allElements.Where(element => element.Id != deleteFixture.Id).ToArray();
        int initialCount = allElements.Length;
        RollbackControl(doc);
        Dictionary<long, string> controlState = State(doc, -1, out var controlGaps, out int controlExcluded);
        string controlWarnings = Warnings(doc);
        RollbackControl(doc);
        Dictionary<long, string> initialState = State(doc, -1, out var initialGaps, out int initialExcluded);
        if (!SameState(controlState, initialState) || !controlGaps.SequenceEqual(initialGaps)
            || controlExcluded != initialExcluded || controlWarnings != Warnings(doc))
            throw new InvalidOperationException("Empty rollback control did not reach a stable writable baseline.");
        bool initiallyModified = doc.IsModified;
        string initialWarnings = Warnings(doc);
        var rows = new List<object>(operations.Length);
        result["classification_only"] = false;
        result["setter_attempted"] = true;
        result["element_count"] = initialCount;
        result["preview_operation_count"] = operations.Length;
        result["writable_state_entries"] = initialState.Count;
        result["excluded_read_only_parameters"] = initialExcluded;
        result["unobserved_parameter_slots"] = initialGaps.Count;
        result["rollback_control_verified"] = true;
        result["synthetic_delete_fixture"] = true;
        result["previews"] = rows;
        foreach (string operation in operations)
        {
            // ponytail: 12 candidates bounds fixture probing; raise only after a measured false-negative.
            var candidates = Arguments(doc, elements, operation, deleteFixture).Take(12).ToArray();
            if (candidates.Length == 0)
            {
                rows.Add(new { operation, status = "missing_fixture", attempts = 0,
                    target_kind = (string?)null, reasons = Array.Empty<string>() });
                continue;
            }
            bool passed = false;
            string? targetKind = null;
            var reasons = new List<string>();
            int attempts = 0;
            foreach (var candidate in candidates)
            {
                attempts++;
                try
                {
                    var response = (JsonElement)probe.Invoke(null,
                        new object[] { doc, operation, JsonSerializer.Serialize(candidate.Args) })!;
                    if (response.GetProperty("status").GetString() != "preview_rolled_back")
                        throw new InvalidOperationException("Production preview did not report rollback.");
                    passed = true;
                    targetKind = candidate.Kind;
                }
                catch (Exception ex)
                {
                    Exception reason = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
                    reasons.Add(reason.Message.ReplaceLineEndings(" "));
                }
                if (doc.IsModifiable) throw new InvalidOperationException("Native preview left the document modifiable.");
                if (PilotValues.Elements(doc).Length != initialCount) throw new InvalidOperationException("Native preview changed the element count.");
                if (doc.IsModified != initiallyModified) throw new InvalidOperationException("Native preview changed the document modified state.");
                if (passed) break;
            }
            rows.Add(new { operation, status = passed ? "preview_succeeded" : "preview_failed", attempts,
                target_kind = targetKind, reasons = reasons.Distinct(StringComparer.Ordinal).Take(3).ToArray() });
            Dictionary<long, string> operationState = State(doc, -1, out var operationGaps, out int operationExcluded);
            if (!SameState(initialState, operationState) || !initialGaps.SequenceEqual(operationGaps)
                || initialExcluded != operationExcluded || initialWarnings != Warnings(doc))
                throw new InvalidOperationException($"Native preview '{operation}' did not restore the writable project state; {DriftTypes(doc, initialState, operationState)}.");
        }
        Dictionary<long, string> finalState = State(doc, -1, out var finalGaps, out int finalExcluded);
        if (!SameState(initialState, finalState) || !initialGaps.SequenceEqual(finalGaps) || initialExcluded != finalExcluded
            || initialWarnings != Warnings(doc))
            throw new InvalidOperationException("Native preview did not restore the writable project state.");
        result["status"] = "preview-only-native-validation";
        result["reason"] = "production_native_preview_path_no_apply_no_values_recorded";
        return true;
    }

    private static IEnumerable<(object Args, string Kind)> Arguments(Document doc, Element[] elements, string operation,
        DirectShape deleteFixture)
    {
        string Token(string label) => $"LECG preview {label} {Guid.NewGuid():N}";
        IEnumerable<(object, string)> Targets<T>(Func<T, object> map) where T : Element =>
            elements.OfType<T>().Select(target => (map(target), target.GetType().FullName!));
        Material[] materials = elements.OfType<Material>().ToArray();
        switch (operation)
        {
            case "workflow_change_batch":
                string levelName = Token("batch level");
                yield return (new { steps = new object[] {
                    new { id = "level", operation = "create_level", arguments = new { name = levelName, elevation_mm = 1234 } },
                    new { id = "rename", operation = "rename_elements", arguments = new { unique_ids = new[] { "$step:level.unique_id" }, prefix = "Verified " } }
                } }, "project");
                yield break;
            case "rename_elements":
                foreach (var item in elements.Where(element => element is ElementType or View or Level or Grid or Material)
                    .Select(element => ((object)new { unique_ids = new[] { element.UniqueId }, prefix = "LECG preview " }, element.GetType().FullName!))) yield return item;
                yield break;
            case "set_parameters":
                foreach (Element element in elements)
                foreach (string name in UniqueWritableParameters(element, StorageType.String))
                    yield return (new { unique_ids = new[] { element.UniqueId }, parameter_name = name, new_value = Token("value") }, element.GetType().FullName!);
                yield break;
            case "set_pinned":
                foreach (Element element in elements.Where(element => element is not ElementType))
                    yield return (new { unique_ids = new[] { element.UniqueId }, pinned = !element.Pinned }, element.GetType().FullName!);
                yield break;
            case "set_type":
                foreach (Element element in elements.Where(element => element is not ElementType))
                foreach (ElementId typeId in element.GetValidTypes().Where(id => id != element.GetTypeId()).Take(1))
                    yield return (new { unique_ids = new[] { element.UniqueId }, type_unique_id = doc.GetElement(typeId).UniqueId }, element.GetType().FullName!);
                yield break;
            case "move_elements":
                foreach (Element element in elements.Where(element => element is not ElementType && element.Location is not null))
                    yield return (new { unique_ids = new[] { element.UniqueId }, x_mm = 1, y_mm = 0, z_mm = 0 }, element.GetType().FullName!);
                yield break;
            case "rotate_elements":
                foreach (Element element in elements.Where(element => element is not ElementType && element.Location is not null))
                    yield return (new { unique_ids = new[] { element.UniqueId }, origin_x_mm = 0, origin_y_mm = 0, angle_degrees = 1 }, element.GetType().FullName!);
                yield break;
            case "delete_elements":
                foreach (Element element in elements.Where(element => element is FamilyInstance or Wall or Floor or Toposolid or CurveElement)
                    .OrderBy(element => element.GetDependentElements(null).Count).Take(11))
                    yield return (new { unique_ids = new[] { element.UniqueId } }, element.GetType().FullName!);
                yield return (new { unique_ids = new[] { deleteFixture.UniqueId } }, "Autodesk.Revit.DB.DirectShape(test_fixture)");
                yield break;
            case "create_level":
                yield return (new { name = Token("level"), elevation_mm = 1234 }, "project");
                yield break;
            case "duplicate_type":
                foreach (ElementType type in elements.OfType<ElementType>().OrderBy(type => type switch
                    { FamilySymbol => 0, WallType => 1, FloorType => 2, CeilingType => 3, RoofType => 4, _ => 5 }))
                    yield return (new { unique_id = type.UniqueId, name = Token("type") }, type.GetType().FullName!);
                yield break;
            case "duplicate_view":
                foreach (View view in elements.OfType<View>().Where(view => view.CanViewBeDuplicated(ViewDuplicateOption.Duplicate)))
                    yield return (new { unique_id = view.UniqueId, name = Token("view"), mode = "Duplicate" }, view.GetType().FullName!);
                yield break;
            case "material_color":
                foreach (var item in Targets<Material>(material => new { unique_ids = new[] { material.UniqueId }, red = 31, green = 63, blue = 95, transparency = 17 })) yield return item;
                yield break;
            case "assign_material":
                foreach (Element element in elements)
                foreach (string name in UniqueWritableParameters(element, StorageType.ElementId, SpecTypeId.Reference.Material))
                foreach (Material material in materials.Take(1))
                    yield return (new { unique_ids = new[] { element.UniqueId }, parameter_name = name, material_unique_id = material.UniqueId }, element.GetType().FullName!);
                yield break;
            case "view_scale":
                foreach (View view in elements.OfType<View>().Where(view => !view.IsTemplate))
                    yield return (new { unique_ids = new[] { view.UniqueId }, scale = view.Scale == 50 ? 100 : 50 }, view.GetType().FullName!);
                yield break;
            case "material_graphics_solid":
                foreach (var item in Targets<Material>(material => new { unique_ids = new[] { material.UniqueId }, red = 47, green = 79, blue = 111 })) yield return item;
                yield break;
            case "slab_offset":
                foreach (var item in Targets<Floor>(floor => new { unique_ids = new[] { floor.UniqueId }, offset_mm = 1 })) yield return item;
                yield break;
            case "slab_reset":
                foreach (Element element in elements.Where(element => element is Floor or Toposolid))
                    yield return (new { unique_ids = new[] { element.UniqueId } }, element.GetType().FullName!);
                yield break;
            default: throw new InvalidOperationException("No native-preview argument policy: " + operation);
        }
    }

    private static IEnumerable<string> UniqueWritableParameters(Element element, StorageType storage,
        ForgeTypeId? dataType = null)
    {
        using ParameterSet parameters = element.Parameters;
        return parameters.Cast<Parameter>().Where(parameter => !parameter.IsReadOnly && parameter.Definition is not null
                && parameter.StorageType == storage && (dataType is null || parameter.Definition.GetDataType() == dataType))
            .Select(parameter => parameter.Definition.Name).GroupBy(name => name, StringComparer.Ordinal)
            .Where(group => group.Count() == 1).Select(group => group.Key).Take(1).ToArray();
    }

    private static void RollbackControl(Document doc)
    {
        using var transaction = new Transaction(doc, "LECG preview rollback control");
        if (transaction.Start() != TransactionStatus.Started) throw new InvalidOperationException("Rollback control did not start.");
        doc.Regenerate();
        if (transaction.RollBack() != TransactionStatus.RolledBack) throw new InvalidOperationException("Rollback control did not roll back.");
    }

    private static DirectShape CreateDeleteFixture(Document doc)
    {
        using var transaction = new Transaction(doc, "LECG disposable delete fixture");
        if (transaction.Start() != TransactionStatus.Started) throw new InvalidOperationException("Delete fixture transaction did not start.");
        DirectShape fixture = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
        fixture.Name = "LECG disposable delete fixture";
        if (transaction.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Delete fixture transaction did not commit.");
        return fixture;
    }

    private static bool SameState(IReadOnlyDictionary<long, string> left, IReadOnlyDictionary<long, string> right) =>
        left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out string? value) && value == pair.Value);

    private static string DriftTypes(Document doc, IReadOnlyDictionary<long, string> before,
        IReadOnlyDictionary<long, string> after) => string.Join(",", before.Keys.Union(after.Keys)
        .Where(id => !before.TryGetValue(id, out string? left) || !after.TryGetValue(id, out string? right) || left != right)
        .Select(id => doc.GetElement(new ElementId(id))?.GetType().FullName ?? "missing")
        .GroupBy(type => type, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
        .Select(group => group.Key + ":" + group.Count()));

    [Test]
    public void ValidateNativePreviews()
    {
        foreach (var model in Manifest.RootElement.GetProperty("models").EnumerateArray())
        {
            UseModel(model.GetProperty("name").GetString()!);
            Record("ProjectNativePreview");
        }
    }
}
