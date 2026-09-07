using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.RevitCopilot.Agent;
using LECG.RevitCopilot.Revit;

namespace LECG.RevitCopilot.SmokeTests;

internal sealed class SetterSweep(string copiesRoot)
{
    private readonly HashSet<string> _verified = new(StringComparer.Ordinal);
    internal void Seed(IEnumerable<string> operations) { foreach (string operation in operations) _verified.Add(operation); }
    internal object Run(UIApplication app, Document doc, string model, ApiPropertyBinding binding, IEnumerable<Element> candidates)
    {
        string prefix = Path.GetFullPath(copiesRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(doc.PathName).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || doc.IsWorkshared || doc.IsLinked || doc.IsFamilyDocument)
            throw new InvalidOperationException("Setter sweeps are restricted to detached disposable project copies.");
        if (_verified.Contains(binding.Operation)) return new { model, operation = binding.Operation, status = "covered_in_previous_sample" };
        Element[] targets = candidates.Where(e => e.IsValidObject)
            .Where(e => binding.Property.Name is not ("CreatedPhaseId" or "DemolishedPhaseId") || e.ArePhasesModifiable())
            .OrderBy(e => e is Dimension d && d.NumberOfSegments > 1 ? 1 : 0).Take(3).ToArray();
        if (targets.Length == 0) return new { model, operation = binding.Operation, status = "missing_fixture", reason = "No matching element class in this sample." };
        List<object> attempts = [];
        bool roundtrip = false;
        foreach (Element target in targets)
        {
            object? before;
            try { before = binding.Property.GetValue(target); }
            catch (Exception ex) { attempts.Add(new { unique_id = target.UniqueId, stage = "initial_getter", error = Error(ex) }); continue; }
            if (before is null && binding.Property.PropertyType != typeof(string)) { attempts.Add(new { stage = "initial_getter", error = "Null non-string value requires a purpose-built fixture." }); continue; }
            object[] changed = SetterProbeValues.Alternatives(doc, target, binding, before).Where(v => !SetterProbeValues.Equal(before, v)).Take(3).ToArray();
            foreach (object desired in changed.Concat(before is null ? Array.Empty<object>() : new[] { before }))
            {
                bool changing = !SetterProbeValues.Equal(before, desired);
                var timer = Stopwatch.StartNew();
                bool success = Probe(app, doc, target, binding, before, desired, out string? error);
                attempts.Add(new { unique_id = target.UniqueId, changed_value = changing, success, error, elapsed_ms = timer.Elapsed.TotalMilliseconds });
                if (success && changing)
                {
                    _verified.Add(binding.Operation);
                    return new { model, operation = binding.Operation, status = "passed", verification = "Changed-value preview rollback, commit, direct API equality and outer rollback; bounded test-only perturbation, not general workflow certification.", attempts };
                }
                roundtrip |= success;
            }
        }
        return new { model, operation = binding.Operation, status = roundtrip ? "roundtrip_only" : "context_rejected",
            verification = roundtrip ? "Original-value write only; not counted as a validated edit." : "Available targets did not accept the bounded probe; inspect attempts before classifying the limitation.", attempts };
    }

    private static bool Probe(UIApplication app, Document doc, Element target, ApiPropertyBinding binding, object? before, object desired, out string? error)
    {
        error = null;
        string uid = target.UniqueId;
        int count = new FilteredElementCollector(doc).GetElementCount();
        using var group = new TransactionGroup(doc, "Isolated API setter probe - always roll back");
        if (group.Start() != TransactionStatus.Started) throw new InvalidOperationException("Cannot start setter rollback group.");
        bool success = false;
        try
        {
            var executor = new ToolExecutor((_, _) => true); // Test-owned, guarded by the disposable path above.
            JsonElement Call(string tool, object args)
            {
                using var json = JsonDocument.Parse(executor.Execute(app, tool, JsonSerializer.Serialize(args)));
                if (!json.RootElement.GetProperty("success").GetBoolean()) throw new InvalidOperationException(json.RootElement.GetProperty("error").GetString());
                return json.RootElement.GetProperty("data").Clone();
            }
            var preview = Call("agent_preview", new { operation = binding.Operation,
                arguments_json = JsonSerializer.Serialize(new { unique_ids = new[] { uid }, value = SetterProbeValues.Wire(doc, desired), units = "revit_internal" }) });
            if (!SetterProbeValues.Equal(before, binding.Property.GetValue(doc.GetElement(uid)))) throw new InvalidOperationException("Preview did not restore the initial property value.");
            var applied = Call("agent_apply", new { preview_id = preview.GetProperty("preview_id").GetString() });
            if (applied.GetProperty("status").GetString() != "committed" || doc.IsModifiable || !SetterProbeValues.Equal(desired, binding.Property.GetValue(doc.GetElement(uid))))
                throw new InvalidOperationException("Committed setter does not equal the requested test value.");
            success = true;
        }
        catch (Exception ex) { error = Error(ex); }
        finally
        {
            if (group.GetStatus() != TransactionStatus.Started || group.RollBack() != TransactionStatus.RolledBack)
                throw new InvalidOperationException("Setter rollback failed; campaign must stop.");
            if (doc.IsModifiable || new FilteredElementCollector(doc).GetElementCount() != count || !SetterProbeValues.Equal(before, binding.Property.GetValue(doc.GetElement(uid))))
                throw new InvalidOperationException("Setter rollback did not restore document/property state; campaign must stop.");
        }
        return success;
    }
    private static string Error(Exception ex) => ex is TargetInvocationException { InnerException: { } inner } ? inner.Message : ex.Message;
}
