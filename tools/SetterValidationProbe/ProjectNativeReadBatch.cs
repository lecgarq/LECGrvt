using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using NUnit.Framework;

namespace LECG.SetterValidationProbe;

public sealed class ProjectNativeReadBatch : SetterHarness
{
    protected override string ManifestName => "project-native-read-manifest.json";
    protected override string PreregistrationName => "project-native-read-preregistration.md";
    protected override string RunKind => "project-native-read-runs";
    protected override bool IsReadOnlyProbe => true;

    protected override bool RunReadOnlyProbe(Document doc, Dictionary<string, object?> result)
    {
        string root = Environment.GetEnvironmentVariable("LECG_SETTER_REPO_ROOT")
            ?? throw new InvalidOperationException("LECG_SETTER_REPO_ROOT is required.");
        string assemblyDirectory = Path.GetDirectoryName(typeof(ProjectNativeReadBatch).Assembly.Location)!;
        string copilotPath = Path.Combine(assemblyDirectory, "RevitCopilot.dll");
        if (SetterHarness.Hash(copilotPath) != Manifest.RootElement.GetProperty("copilot_assembly_sha256").GetString())
            throw new InvalidOperationException("Tested Copilot assembly differs from the frozen manifest.");
        Assembly copilot = Assembly.LoadFile(copilotPath);
        Type catalog = copilot.GetType("LECG.RevitCopilot.Agent.CapabilityCatalog", throwOnError: true)!;
        Array capabilities = (Array)catalog.GetField("All", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        string[] operations = capabilities.Cast<object>().Where(capability =>
                (string)capability.GetType().GetProperty("Kind")!.GetValue(capability)! == "read")
            .Select(capability => (string)capability.GetType().GetProperty("Name")!.GetValue(capability)!)
            .Order(StringComparer.Ordinal).ToArray();
        if (operations.Length != 35) throw new InvalidOperationException("Native read denominator changed.");
        MethodInfo probe = copilot.GetType("LECG.RevitCopilot.Revit.ToolExecutor", throwOnError: true)!
            .GetMethod("ProbeNativeRead", BindingFlags.Static | BindingFlags.NonPublic)!;
        Element[] elements = PilotValues.Elements(doc);
        using JsonDocument referencePack = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "RevitCopilot", "Agent", "Knowledge", "revit2026-reference.json")));
        string referenceId = referencePack.RootElement
            .GetProperty("entries")[0].GetProperty("id").GetString()!;
        var rows = new List<object>(operations.Length);
        foreach (string operation in operations)
        {
            // ponytail: 12 candidates bounds fixture probing; raise only after a measured false-negative.
            var candidates = Arguments(doc, elements, operation, referenceId).Take(12).ToArray();
            if (candidates.Length == 0)
            {
                rows.Add(new { operation, status = "missing_fixture", attempts = 0, target_kind = (string?)null, reasons = Array.Empty<string>() });
                continue;
            }
            bool passed = false;
            string? targetKind = null;
            var reasons = new List<string>();
            var timer = Stopwatch.StartNew();
            int attempted = 0;
            foreach (var candidate in candidates)
            {
                attempted++;
                try
                {
                    _ = (JsonElement)probe.Invoke(null, new object[] { doc, operation, JsonSerializer.Serialize(candidate.Args) })!;
                    passed = true;
                    targetKind = candidate.Kind;
                    break;
                }
                catch (Exception ex)
                {
                    Exception reason = ex is TargetInvocationException tie && tie.InnerException is Exception inner ? inner : ex;
                    reasons.Add(reason.Message.ReplaceLineEndings(" "));
                }
                if (doc.IsModifiable) throw new InvalidOperationException("Native read opened a document transaction.");
            }
            rows.Add(new { operation, status = passed ? "read_succeeded" : "read_failed", attempts = attempted,
                target_kind = targetKind, elapsed_ms = timer.Elapsed.TotalMilliseconds,
                reasons = reasons.Distinct(StringComparer.Ordinal).Take(3).Select(reason => reason[..Math.Min(reason.Length, 400)]).ToArray() });
        }
        if (doc.IsModifiable || PilotValues.Elements(doc).Length != elements.Length)
            throw new InvalidOperationException("Native read campaign changed the document or left a transaction open.");
        result["element_count"] = elements.Length;
        result["read_operation_count"] = operations.Length;
        result["reads"] = rows;
        result["status"] = "read-only-native-validation";
        result["reason"] = "production_native_read_path_no_values_recorded";
        return true;
    }

    private static IEnumerable<(object Args, string Kind)> Arguments(Document doc, Element[] elements,
        string operation, string referenceId)
    {
        object Empty() => new { };
        IEnumerable<(object, string)> Targets<T>(Func<T, object> map) where T : Element =>
            elements.OfType<T>().Select(target => (map(target), target.GetType().FullName!));
        switch (operation)
        {
            case "knowledge_search": yield return (new { query = "wall", kind = "all", limit = 1 }, "bundled_knowledge"); yield break;
            case "knowledge_get": yield return (new { reference_id = referenceId, offset = 0, limit = 200 }, "bundled_knowledge"); yield break;
            case "schedule_fields": foreach (var item in Targets<ViewSchedule>(e => new { unique_id = e.UniqueId, limit = 2 })) yield return item; yield break;
            case "view_filters": foreach (var item in Targets<View>(e => new { unique_id = e.UniqueId, limit = 2 })) yield return item; yield break;
            case "elements_joined":
                foreach (Element first in elements.Where(e => e.Location is not null).Take(6))
                foreach (Element second in elements.Where(e => e.Location is not null && e.Id != first.Id).Take(2))
                    yield return (new { first_unique_id = first.UniqueId, second_unique_id = second.UniqueId }, first.GetType().FullName! + "+" + second.GetType().FullName!);
                yield break;
            case "type_compound_layers": foreach (var item in Targets<HostObjAttributes>(e => new { unique_id = e.UniqueId, limit = 3 })) yield return item; yield break;
            case "instance_transform": foreach (var item in Targets<Instance>(e => new { unique_id = e.UniqueId })) yield return item; yield break;
            case "types_list": yield return (new { category = "OST_Walls", limit = 2 }, "BuiltInCategory.OST_Walls"); yield break;
            case "elements_count": yield return (new { category = "OST_Walls" }, "BuiltInCategory.OST_Walls"); yield break;
            case "sheet_views": foreach (var item in Targets<ViewSheet>(e => new { unique_id = e.UniqueId, limit = 2 })) yield return item; yield break;
            case "host_inserts": foreach (var item in Targets<HostObject>(e => new { unique_id = e.UniqueId, limit = 2 })) yield return item; yield break;
            case "host_bottom_faces":
                foreach (HostObject host in elements.OfType<HostObject>().Where(HasBottomFaces))
                    yield return (new { unique_id = host.UniqueId, limit = 2 }, host.GetType().FullName!);
                yield break;
            case "parameters_get":
                foreach (Element element in elements)
                foreach (string name in element.Parameters.Cast<Parameter>().Select(p => p.Definition?.Name).OfType<string>()
                    .GroupBy(name => name, StringComparer.Ordinal).Where(group => group.Count() == 1).Select(group => group.Key).Take(1))
                    yield return (new { unique_ids = new[] { element.UniqueId }, parameter_names = new[] { name } }, element.GetType().FullName!);
                yield break;
            case "element_dependents":
            case "element_valid_types":
            case "element_action_checks":
            case "element_materials":
            case "element_phase_status":
                foreach (Element element in elements.Take(12))
                {
                    object args = operation is "element_action_checks" or "element_phase_status"
                        ? new { unique_ids = new[] { element.UniqueId } } : new { unique_id = element.UniqueId, limit = 2 };
                    yield return (args, element.GetType().FullName!);
                }
                yield break;
            case "element_bounds":
                foreach (Element element in elements.Where(element => element.get_BoundingBox(null) is not null))
                    yield return (new { unique_id = element.UniqueId, limit = 2 }, element.GetType().FullName!);
                yield break;
            case "phases_list": case "design_options_list": case "worksets_list": case "elements_find":
            case "categories_list": case "levels_list": case "grids_list": case "views_list": case "sheets_list":
            case "schedules_list": case "materials_list": case "families_list": case "rooms_list": case "warnings_list":
            case "links_list": case "selection_get":
                yield return (Empty(), "project"); yield break;
            default: throw new InvalidOperationException("No native-read argument policy: " + operation);
        }
    }

    private static bool HasBottomFaces(HostObject host)
    {
        try { return HostObjectUtils.GetBottomFaces(host).Count > 0; }
        catch (Autodesk.Revit.Exceptions.ArgumentException) { return false; }
    }

    [Test]
    public void ValidateNativeReads()
    {
        foreach (var model in Manifest.RootElement.GetProperty("models").EnumerateArray())
        {
            UseModel(model.GetProperty("name").GetString()!);
            Record("ProjectNativeRead");
        }
    }
}
