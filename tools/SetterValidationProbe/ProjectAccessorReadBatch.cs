using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Autodesk.Revit.DB;
using NUnit.Framework;

namespace LECG.SetterValidationProbe;

public sealed class ProjectAccessorReadBatch : SetterHarness
{
    protected override string ManifestName => "project-accessor-read-manifest.json";
    protected override string PreregistrationName => "project-accessor-read-preregistration.md";
    protected override string RunKind => "project-accessor-read-runs";
    protected override bool IsReadOnlyProbe => true;

    protected override bool RunReadOnlyProbe(Document doc, Dictionary<string, object?> result)
    {
        Element[] elements = PilotValues.Elements(doc);
        string root = Environment.GetEnvironmentVariable("LECG_SETTER_REPO_ROOT")
            ?? throw new InvalidOperationException("LECG_SETTER_REPO_ROOT is required.");
        string inventoryPath = Path.Combine(root, "outputs", "validation-expansion", "accessor-inventory.json");
        using var inventory = JsonDocument.Parse(File.ReadAllText(inventoryPath));
        var bindings = inventory.RootElement.EnumerateArray().Where(item => item.GetProperty("operation").GetString()!.StartsWith("api.get:", StringComparison.Ordinal))
            .Select(item => new { Operation = item.GetProperty("operation").GetString()!,
                DeclaringType = typeof(Element).Assembly.GetType(item.GetProperty("declaring_type").GetString()!, throwOnError: true)! })
            .OrderBy(binding => binding.Operation, StringComparer.Ordinal).ToArray();
        string assemblyDirectory = Path.GetDirectoryName(typeof(ProjectAccessorReadBatch).Assembly.Location)!;
        string copilotPath = Path.Combine(assemblyDirectory, "RevitCopilot.dll");
        if (SetterHarness.Hash(copilotPath) != Manifest.RootElement.GetProperty("copilot_assembly_sha256").GetString())
            throw new InvalidOperationException("Tested Copilot assembly differs from the frozen manifest.");
        Assembly freshCopilot = Assembly.LoadFile(copilotPath);
        MethodInfo probe = freshCopilot.GetType("LECG.RevitCopilot.Revit.ToolExecutor", throwOnError: true)!
            .GetMethod("ProbeApiRead", BindingFlags.Static | BindingFlags.NonPublic)!;
        var targets = bindings.Select(binding => binding.DeclaringType).Distinct()
            .ToDictionary(type => type, type => elements.Where(type.IsInstanceOfType).ToArray());
        var rows = new List<object>(bindings.Length);
        foreach (var binding in bindings)
        {
            Element[] eligible = targets[binding.DeclaringType];
            if (eligible.Length == 0)
            {
                rows.Add(new { operation = binding.Operation, status = "missing_fixture", target_count = 0, attempts = 0 });
                continue;
            }
            bool passed = false, allUnsupported = true;
            long? representativeId = null;
            string? representativeType = null;
            var reasons = new List<string>();
            var timer = Stopwatch.StartNew();
            int attempted = 0;
            foreach (Element target in eligible.Take(12))
            {
                attempted++;
                try
                {
                    var response = (JsonElement)probe.Invoke(null, new object[] { doc, binding.Operation, target.UniqueId })!;
                    bool unsupported = response.GetProperty("items").EnumerateArray().All(item =>
                        item.TryGetProperty("status", out var status) && status.GetString() == "unsupported");
                    allUnsupported &= unsupported;
                    if (unsupported)
                        reasons.Add(response.GetProperty("items")[0].GetProperty("reason").GetString()!);
                    else
                    {
                        passed = true;
                        representativeId = target.Id.Value;
                        representativeType = target.GetType().FullName;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    allUnsupported = false;
                    reasons.Add((ex.InnerException?.Message ?? ex.Message).ReplaceLineEndings(" "));
                }
                if (doc.IsModifiable) throw new InvalidOperationException("Getter opened a document transaction.");
            }
            rows.Add(new { operation = binding.Operation,
                status = passed ? "read_succeeded" : allUnsupported ? "context_unsupported" : "read_failed",
                target_count = eligible.Length, attempts = attempted, representative_id = representativeId,
                representative_type = representativeType, elapsed_ms = timer.Elapsed.TotalMilliseconds,
                reasons = reasons.Distinct(StringComparer.Ordinal).Take(3).Select(reason => reason[..Math.Min(reason.Length, 400)]).ToArray() });
        }
        if (doc.IsModifiable || PilotValues.Elements(doc).Length != elements.Length)
            throw new InvalidOperationException("Read campaign changed the document or left a transaction open.");
        result["element_count"] = elements.Length;
        result["read_operation_count"] = bindings.Length;
        result["reads"] = rows;
        result["status"] = "read-only-accessor-validation";
        result["reason"] = "production_api_read_path_no_values_recorded";
        return true;
    }

    [Test]
    public void ValidateAccessorsReadOnly()
    {
        foreach (var model in Manifest.RootElement.GetProperty("models").EnumerateArray())
        {
            UseModel(model.GetProperty("name").GetString()!);
            Record("ProjectAccessorRead");
        }
    }
}
