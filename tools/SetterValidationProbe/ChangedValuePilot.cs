using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using NUnit.Framework;

namespace LECG.SetterValidationProbe;

[NonParallelizable]
public sealed class ChangedValuePilot
{
    private const string Root = @"C:\LECG\RevitAddins\LECG";
    private const string Evidence = Root + @"\docs\review\setter-validation-gate";
    private UIApplication _ui = null!;
    private JsonDocument _manifest = null!;
    private string _run = "", _source = "", _modelHash = "";
    private bool _abort;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    internal static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    [OneTimeSetUp]
    public void Setup(UIApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _ui = application;
        Require(Process.GetCurrentProcess().ProcessName == "Revit" && application.Application.VersionNumber == "2026"
            && Environment.Version.Major == 10, "Runner/runtime gate failed.");
        Require(application.Application.Documents.Size == 0, "Pilot refuses to run alongside any open model.");
        _manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Evidence, "pilot-manifest.json")));
        var m = _manifest.RootElement;
        Require(Hash(typeof(Element).Assembly.Location) == m.GetProperty("api_sha256").GetString(), "API binary changed.");
        Require(Hash(Path.Combine(Evidence, "pilot-preregistration.md")) == m.GetProperty("preregistration_sha256").GetString(), "Preregistration changed.");
        Require(Hash(Path.Combine(Evidence, "elementid-classification.csv")) == m.GetProperty("classification_sha256").GetString(), "Classification changed.");
        var model = m.GetProperty("models").EnumerateArray().Single(x => x.GetProperty("name").GetString() == m.GetProperty("pilot_model").GetString());
        _source = model.GetProperty("path").GetString()!;
        _modelHash = model.GetProperty("sha256").GetString()!;
        Require(Path.GetFullPath(_source) == @"C:\Program Files\Autodesk\Revit 2026\Samples\Snowdon Towers Sample Electrical.rvt", "Unapproved source path.");
        Require(Hash(_source) == _modelHash, "Sample hash changed.");
        _run = Path.Combine(Evidence, "pilot-runs", DateTime.UtcNow.ToString("yyyyMMddTHHmmss") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_run);
        var binaries = new[] { typeof(Element).Assembly.Location, typeof(UIApplication).Assembly.Location,
            Assembly.GetExecutingAssembly().Location, typeof(Assert).Assembly.Location, Process.GetCurrentProcess().MainModule!.FileName };
        File.WriteAllText(Path.Combine(_run, "provenance.json"), JsonSerializer.Serialize(new {
            started_at = DateTime.UtcNow, revision = m.GetProperty("revision").GetString(),
            runtime = Environment.Version.ToString(), revit_build = application.Application.VersionBuild,
            manifest_sha256 = Hash(Path.Combine(Evidence, "pilot-manifest.json")),
            protocol_amendment_sha256 = Hash(Path.Combine(Evidence, "pilot-protocol-amendment.md")),
            model = Path.GetFileName(_source), model_sha256 = _modelHash,
            binaries = binaries.Select(p => new { path = p, sha256 = Hash(p) }),
            sources = Directory.GetFiles(Path.Combine(Root, "tools/SetterValidationProbe"), "*.cs")
                .Select(p => new { path = p, sha256 = Hash(p) }),
            isolation = "Fresh detached disposable copy per operation; never saved; observable rollback checks are not a proof of all internal state."
        }, JsonOptions));
    }

    [TestCase("Electrical.CableType.ConductorMaterial")]
    [TestCase("Material.CutBackgroundPatternId")]
    [TestCase("TextElement.Text")]
    [TestCase("Plumbing.PipingSystemType.FluidTemperature")]
    [TestCase("Structure.LoadCase.Number")]
    [TestCase("ReferencePlane.BubbleEnd")]
    [TestCase("ViewSheetSet.IsAutomatic")]
    [TestCase("Electrical.ElectricalSystem.CircuitConnectionType")]
    public void RecordsChangedValueAndRestoration(string property)
    {
        ArgumentNullException.ThrowIfNull(property);
        Require(!_abort, "Earlier isolation failure aborted the pilot; no further document opens.");
        Require(_ui.Application.Documents.Size == 0, "Unexpected document open; refusing pilot.");
        string operation = "api.set:Autodesk.Revit.DB." + property;
        string directory = Path.Combine(_run, property);
        Directory.CreateDirectory(directory);
        string copy = Path.Combine(directory, "disposable.rvt");
        var result = new Dictionary<string, object?> {
            ["operation"] = operation, ["model"] = Path.GetFileName(_source), ["model_sha256"] = _modelHash,
            ["status"] = "rejected-with-reason", ["stage"] = "opening", ["setter_attempted"] = false,
            ["copy"] = copy, ["rollback_verified"] = false, ["cleanup_verified"] = false };
        void Checkpoint() => File.WriteAllText(Path.Combine(directory, "checkpoint.json"), JsonSerializer.Serialize(result, JsonOptions));
        Checkpoint();
        Document? doc = null;
        var openingWarnings = new List<string>();
        bool opening = false;
        void OpeningFailures(object? sender, FailuresProcessingEventArgs args)
        {
            if (!opening) return;
            var accessor = args.GetFailuresAccessor();
            foreach (var failure in accessor.GetFailureMessages())
            {
                openingWarnings.Add(failure.GetSeverity() + ": " + failure.GetDescriptionText());
                if (failure.GetSeverity() == FailureSeverity.Warning) accessor.DeleteWarning(failure);
                else { args.SetProcessingResult(FailureProcessingResult.ProceedWithRollBack); return; }
            }
            args.SetProcessingResult(FailureProcessingResult.Continue);
        }
        _ui.Application.FailuresProcessing += OpeningFailures;
        var timer = Stopwatch.StartNew();
        try
        {
            Require(Hash(_source) == _modelHash, "Sample changed since setup.");
            File.Copy(_source, copy, false);
            File.SetAttributes(copy, FileAttributes.Normal);
            var path = ModelPathUtils.ConvertUserVisiblePathToModelPath(copy);
            using (var transmission = TransmissionData.ReadTransmissionData(path))
            {
                if (transmission is not null)
                {
                    foreach (var id in transmission.GetAllExternalFileReferenceIds())
                    {
                        var reference = transmission.GetLastSavedReferenceData(id);
                        if (reference.ExternalFileReferenceType is ExternalFileReferenceType.RevitLink or ExternalFileReferenceType.CADLink)
                            transmission.SetDesiredReferenceData(id, reference.GetAbsolutePath(), PathType.Absolute, false);
                    }
                    transmission.IsTransmitted = true;
                    TransmissionData.WriteTransmissionData(path, transmission);
                }
            }
            result["copy_open_sha256"] = Hash(copy);
            using var options = new OpenOptions { DetachFromCentralOption = DetachFromCentralOption.DetachAndDiscardWorksets };
            opening = true;
            try { doc = _ui.Application.OpenDocumentFile(path, options); }
            finally { opening = false; }
            Require(!doc.IsFamilyDocument && !doc.IsLinked && !doc.IsReadOnly && !doc.IsWorkshared, "Unsafe document context.");
            Require(!new FilteredElementCollector(doc).OfClass(typeof(RevitLinkType)).Cast<RevitLinkType>()
                .Any(t => RevitLinkType.IsLoaded(doc, t.Id)), "Unexpected loaded Revit link.");
            result["stage"] = "generator";
            int split = property.LastIndexOf('.');
            Type type = typeof(Element).Assembly.GetType("Autodesk.Revit.DB." + property[..split], true)!;
            PropertyInfo info = type.GetProperty(property[(split + 1)..])!;
            var targets = PilotValues.Elements(doc).Where(type.IsInstanceOfType).ToArray();
            result["target_count"] = targets.Length;
            var refusals = new List<object>();
            foreach (Element target in targets)
            {
                object before;
                object? candidate;
                try
                {
                    before = info.GetValue(target)!;
                    candidate = PilotValues.Candidate(target, property, before);
                    if (candidate is null || PilotValues.Equal(before, candidate)) continue;
                }
                catch (Exception ex) { refusals.Add(new { target = target.UniqueId, reason = Error(ex) }); continue; }
                result["generator_errors"] = refusals;
                result["stage"] = "pre_write_snapshot";
                Checkpoint();
                Probe(doc, target, info, before, candidate, result);
                return;
            }
            result["generator_errors"] = refusals;
            result["reason"] = targets.Length == 0 ? "no_existing_target_in_unmodified_sample" : "no_proven_valid_alternative";
        }
        catch (Exception ex)
        {
            result["reason"] = Error(ex);
            result["infrastructure_failure"] = true;
            _abort = true;
            throw;
        }
        finally
        {
            _ui.Application.FailuresProcessing -= OpeningFailures;
            result["opening_warnings"] = openingWarnings;
            try
            {
                if (doc is not null && doc.IsValidObject) Require(doc.Close(false), "Disposable document failed to close.");
                Require(_ui.Application.Documents.Size == 0, "A document remains open.");
                Require(Hash(_source) == _modelHash, "Source sample changed.");
                if (result.TryGetValue("copy_open_sha256", out object? expected))
                    Require(Hash(copy) == (string)expected!, "Opened copy was unexpectedly saved.");
                result["cleanup_verified"] = true;
            }
            catch (Exception ex) { result["cleanup_error"] = Error(ex); _abort = true; }
            result["elapsed_ms"] = timer.Elapsed.TotalMilliseconds;
            File.WriteAllText(Path.Combine(directory, "receipt.json"), JsonSerializer.Serialize(result, JsonOptions));
            if (_abort) Assert.Fail("Pilot isolation/infrastructure failed; inspect receipt. No further writes permitted.");
        }
    }

    private void Probe(Document doc, Element target, PropertyInfo property, object before, object desired, Dictionary<string, object?> result)
    {
        string uid = target.UniqueId;
        result["target"] = uid;
        result["before"] = PilotValues.Snapshot(before);
        result["desired"] = PilotValues.Snapshot(desired);
        var snapshot = State(doc, out var unobservedBefore);
        var warnings = Warnings(doc);
        var events = new List<object>();
        void Changed(object? sender, DocumentChangedEventArgs args)
        {
            // Revit can return another managed wrapper for the same document. Compare its
            // project identity, not wrapper reference identity; only one project is open.
            if (args.GetDocument().ProjectInformation.UniqueId != doc.ProjectInformation.UniqueId) return;
            events.Add(new { operation = args.Operation.ToString(), added = args.GetAddedElementIds().Select(i => i.Value).ToArray(),
                modified = args.GetModifiedElementIds().Select(i => i.Value).ToArray(), deleted = args.GetDeletedElementIds().Select(i => i.Value).ToArray() });
        }
        _ui.Application.DocumentChanged += Changed;
        using var group = new TransactionGroup(doc, "LECG pilot - always rollback");
        Require(group.Start() == TransactionStatus.Started, "Transaction group did not start.");
        try
        {
            result["stage"] = "setter";
            using var transaction = new Transaction(doc, "LECG changed-value pilot");
            Require(transaction.Start() == TransactionStatus.Started, "Transaction did not start.");
            var failures = new RejectFailures();
            transaction.SetFailureHandlingOptions(transaction.GetFailureHandlingOptions().SetFailuresPreprocessor(failures).SetClearAfterRollback(true));
            result["setter_attempted"] = true;
            property.SetValue(target, desired);
            doc.Regenerate();
            result["after_regenerate"] = PilotValues.Snapshot(property.GetValue(target));
            result["stage"] = "commit";
            var status = transaction.Commit();
            result["commit_status"] = status.ToString();
            result["failures"] = failures.Messages;
            if (status != TransactionStatus.Committed)
            {
                result["reason"] = "inner_transaction_not_committed";
                return;
            }
            object after = property.GetValue(doc.GetElement(uid))!;
            result["after_commit"] = PilotValues.Snapshot(after);
            result["stage"] = "comparison";
            result["status"] = PilotValues.Equal(before, after) ? "still-same-value"
                : PilotValues.Equal(desired, after) ? "validated" : "rejected-with-reason";
            result["reason"] = PilotValues.Equal(before, after) ? "committed_but_unchanged"
                : PilotValues.Equal(desired, after) ? null : "committed_value_does_not_match_requested_value";
        }
        catch (Exception ex) { result["reason"] = Error(ex); }
        finally
        {
            try
            {
                var rollback = group.RollBack();
                result["rollback_status"] = rollback.ToString();
                var restored = State(doc, out var unobservedAfter);
                long[] changedIds = snapshot.Keys.Union(restored.Keys).Where(id => snapshot.GetValueOrDefault(id) != restored.GetValueOrDefault(id)).ToArray();
                result["restoration"] = new { before_elements = snapshot.Count, after_elements = restored.Count,
                    different_elements = changedIds, warning_set_restored = warnings == Warnings(doc),
                    unobservable_parameter_slots_before = unobservedBefore, unobservable_parameter_slots_after = unobservedAfter,
                    complete_parameter_snapshot = unobservedBefore.Count == 0 && unobservedAfter.Count == 0,
                    target_restored = PilotValues.Equal(before, property.GetValue(doc.GetElement(uid))),
                    scope = "All element identities and readable parameters; target property; warning identities. Unavailable parameter slots and internal/geometry state are NOT verified. Fresh-copy isolation required." };
                result["document_changes"] = events;
                Require(rollback == TransactionStatus.RolledBack && !doc.IsModifiable && changedIds.Length == 0
                    && warnings == Warnings(doc) && PilotValues.Equal(before, property.GetValue(doc.GetElement(uid))), "Rollback observable restoration failed.");
                result["rollback_verified"] = true;
            }
            finally { _ui.Application.DocumentChanged -= Changed; }
        }
    }

    private static Dictionary<long, string> State(Document doc, out List<string> unobserved)
    {
        var gaps = new List<string>();
        unobserved = gaps;
        return PilotValues.Elements(doc).ToDictionary(e => e.Id.Value, e =>
        {
        // Read native wrappers immediately while their owning ParameterSet is alive. Never
        // buffer/sort Parameter wrappers or hand a deferred API enumeration to the serializer.
        var parameters = new List<string>();
        using var parameterSet = e.Parameters;
        int index = 0;
        foreach (Parameter p in parameterSet)
        {
            index++;
            if (p.Definition is null)
            {
                gaps.Add(e.UniqueId + ":parameter-slot:" + index);
                parameters.Add("unobservable-slot:" + index);
                continue; // Explicitly disclosed API gap; never read StorageType without a definition.
            }
            StorageType storage = p.StorageType;
            string? value = storage switch {
                StorageType.String => p.AsString(), StorageType.Integer => p.AsInteger().ToString(CultureInfo.InvariantCulture),
                StorageType.Double => p.AsDouble().ToString("R", CultureInfo.InvariantCulture),
                StorageType.ElementId => p.AsElementId().Value.ToString(CultureInfo.InvariantCulture), _ => null };
            parameters.Add(JsonSerializer.Serialize(new { id = p.Id.Value, storage = storage.ToString(), has_value = p.HasValue, value }));
        }
        parameters.Sort(StringComparer.Ordinal);
        string text = JsonSerializer.Serialize(new { e.UniqueId, type = e.GetTypeId().Value, group = e.GroupId.Value,
            category = e.Category?.Id.Value, e.Pinned, parameters });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        });
    }

    private static string Warnings(Document doc) => JsonSerializer.Serialize(doc.GetWarnings().Select(w => new {
        definition = w.GetFailureDefinitionId().Guid, elements = w.GetFailingElements().Select(i => i.Value).Order().ToArray(),
        description = w.GetDescriptionText() }).OrderBy(w => JsonSerializer.Serialize(w), StringComparer.Ordinal));
    private static string Error(Exception ex) => ex is TargetInvocationException { InnerException: { } inner } ? Error(inner) : ex.GetType().Name + ": " + ex.Message;

    private sealed class RejectFailures : IFailuresPreprocessor
    {
        internal List<string> Messages { get; } = [];
        public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
        {
            Messages.AddRange(accessor.GetFailureMessages().Select(m => m.GetSeverity() + ": " + m.GetDescriptionText()));
            return Messages.Count > 0 ? FailureProcessingResult.ProceedWithRollBack : FailureProcessingResult.Continue;
        }
    }

    [OneTimeTearDown]
    public void Teardown() => _manifest?.Dispose();
}
