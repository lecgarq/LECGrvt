using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using LECG.RevitCopilot.Agent;
using LECG.RevitCopilot.Revit;

namespace LECG.RevitCopilot.SmokeTests;

internal sealed class SampleBenchmark : IExternalEventHandler
{
    private const string Source = @"C:\Program Files\Autodesk\Revit 2026\Samples";
    private readonly string _output = Path.Combine(Path.GetDirectoryName(typeof(SampleBenchmark).Assembly.Location)!, "benchmarks", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
    private readonly List<object> _models = [];
    private readonly List<object> _rows = [];
    private readonly Dictionary<string, string> _hashes = [];
    private readonly Queue<string> _files = new();
    private readonly ToolExecutor _executor = new((_, _) => throw new InvalidOperationException("Read sweep requested a write."));
    private Queue<ApiPropertyBinding> _getters = new();
    private Queue<ApiPropertyBinding> _setters = new();
    private readonly bool _sweepSetters = Environment.GetEnvironmentVariable("REVIT_COPILOT_SETTER_SWEEP") == "1";
    private SetterSweep? _setterSweep;
    private readonly List<JsonElement> _baselineRows = [];
    private string? _baselineSha256;
    private readonly List<object> _coverageFixtures = [];
    private Dictionary<Type, Element[]> _fixtures = [];
    private Document? _doc;
    private string? _blank;
    private string? _name;
    private Task<object>? _transport;
    private bool _finished;
    private double _openMs;
    private int _elementCount;
    private DateTime _deadline;
    private bool _openingCopy;
    private bool _runningFixtures;
    private readonly List<object> _loadWarnings = [];
    private readonly ExternalEvent _event;
    internal SampleBenchmark() { _event = ExternalEvent.Create(this); }
    internal void Request() { if (!_finished && _transport is not { IsCompleted: false }) _event.Raise(); }
    public string GetName() => "Isolated Autodesk sample benchmark";
    public void Execute(UIApplication app) => Tick(app);
    internal void OnFailuresProcessing(object? sender, Autodesk.Revit.DB.Events.FailuresProcessingEventArgs e)
    {
        if (!_openingCopy && !_runningFixtures) return; // Never handle user-model failures or production confirmations.
        var failures = e.GetFailuresAccessor();
        if (!_openingCopy && failures.GetTransactionName() is not ("Agent smoke fixtures" or "Create benchmark-only wall" or "Harvest read fixtures" or "Disposable material parameter fixture")) return;
        foreach (var warning in failures.GetFailureMessages().Where(f => f.GetSeverity() == FailureSeverity.Warning))
        {
            _loadWarnings.Add(new { model = _name, scope = _openingCopy ? "sample_load" : "fixture_setup", message = warning.GetDescriptionText() });
            failures.DeleteWarning(warning);
        }
        e.SetProcessingResult(FailureProcessingResult.Continue); // Errors are not resolved or ignored.
    }

    private void Tick(UIApplication app)
    {
        if (_finished) return;
        try
        {
            if (_blank is null)
            {
                if (app.ActiveUIDocument is null) return;
                Document blank = app.ActiveUIDocument.Document;
                Require(string.IsNullOrEmpty(blank.PathName) && !blank.IsWorkshared, "Benchmark must start on the journal's unsaved scratch, never a user project.");
                Directory.CreateDirectory(_output);
                _blank = Path.Combine(_output, "blank.rvt");
                blank.SaveAs(_blank, new SaveAsOptions { OverwriteExistingFile = false });
                _deadline = DateTime.UtcNow.AddHours(_sweepSetters ? 4 : 1);
                string copies = Path.Combine(_output, "sample-copies");
                Directory.CreateDirectory(copies);
                if (_sweepSetters) _setterSweep = new SetterSweep(copies);
                string? baseline = Environment.GetEnvironmentVariable("REVIT_COPILOT_VALIDATION_BASELINE");
                if (_sweepSetters && !string.IsNullOrWhiteSpace(baseline))
                {
                    _baselineSha256 = Hash(baseline);
                    using var prior = JsonDocument.Parse(File.ReadAllText(baseline));
                    if (prior.RootElement.GetProperty("status").GetString() != "completed" || prior.RootElement.GetProperty("error").ValueKind != JsonValueKind.Null)
                        throw new InvalidOperationException("Validation baseline must be a successfully completed campaign.");
                    _baselineRows.AddRange(prior.RootElement.GetProperty("operations").EnumerateArray()
                        .Where(row => row.GetProperty("operation").GetString()!.StartsWith("api.set:", StringComparison.Ordinal) && row.GetProperty("status").GetString() == "passed")
                        .Select(row => row.Clone()));
                    _setterSweep!.Seed(_baselineRows.Where(row => row.TryGetProperty("verification", out var v)
                        && v.GetString()!.StartsWith("Changed-value", StringComparison.Ordinal)).Select(row => row.GetProperty("operation").GetString()!));
                }
                foreach (string source in Directory.GetFiles(Source, "*.rvt").OrderBy(p => p, StringComparer.Ordinal))
                {
                    string name = Path.GetFileName(source);
                    _hashes.Add(name, Hash(source));
                    string copy = Path.Combine(copies, name);
                    File.Copy(source, copy, overwrite: false);
                    File.SetAttributes(copy, FileAttributes.Normal);
                    // Unload references in the COPY so no central/cloud/linked source is opened.
                    var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(copy);
                    using var transmission = TransmissionData.ReadTransmissionData(modelPath);
                    if (transmission is not null)
                    {
                        foreach (ElementId id in transmission.GetAllExternalFileReferenceIds())
                        {
                            var reference = transmission.GetLastSavedReferenceData(id);
                            // Material/keynote references do not support a model-link unload request.
                            if (reference.ExternalFileReferenceType is ExternalFileReferenceType.RevitLink or ExternalFileReferenceType.CADLink)
                                transmission.SetDesiredReferenceData(id, reference.GetAbsolutePath(), PathType.Absolute, false);
                        }
                        transmission.IsTransmitted = true;
                        TransmissionData.WriteTransmissionData(modelPath, transmission);
                    }
                    _files.Enqueue(copy);
                }
                Checkpoint("running");
            }
            Require(DateTime.UtcNow < _deadline, "Benchmark exceeded its configured deadline.");
            if (_transport is not null)
            {
                if (!_transport.IsCompleted) return;
                object result;
                try { result = _transport.GetAwaiter().GetResult(); }
                catch (Exception ex) { result = new { status = "failed", error = ex.Message }; }
                _models.Add(new { model = _name, open_ms = _openMs, element_count = _elementCount,
                    fixture_types = _fixtures.Keys.Select(t => t.FullName).Order().ToArray(), transport = result });
                _transport = null;
                Checkpoint("running");
                _name = null;
            }
            if (_name is null)
            {
                if (!_files.TryDequeue(out string? path)) { Finish(app, null); return; }
                Document? previous = _doc;
                _name = Path.GetFileName(path);
                var timer = Stopwatch.StartNew();
                using var options = new OpenOptions { DetachFromCentralOption = DetachFromCentralOption.DetachAndDiscardWorksets };
                _openingCopy = true;
                try { _doc = app.OpenAndActivateDocument(ModelPathUtils.ConvertUserVisiblePathToModelPath(path), options, false).Document; }
                finally { _openingCopy = false; }
                _openMs = timer.Elapsed.TotalMilliseconds;
                if (previous?.IsValidObject == true) Require(previous.Close(false), "Previous disposable model did not close.");
                Require(!_doc.IsWorkshared && !_doc.IsLinked, "Sample did not detach safely.");
                Require(!new FilteredElementCollector(_doc).OfClass(typeof(RevitLinkType)).Cast<RevitLinkType>().Any(t => RevitLinkType.IsLoaded(_doc, t.Id)), "A linked model unexpectedly loaded; stop testing.");
                var existingIds = _sweepSetters ? AllElements(_doc).ToElementIds().ToHashSet() : null;
                if (_sweepSetters) _coverageFixtures.Add(new { model = _name, fixtures = CoverageFixtures.Create(_doc) });
                _elementCount = AllElements(_doc).GetElementCount();
                // Retain up to 30 candidates per type for context selection; each setter probes at most three.
                _fixtures = AllElements(_doc).ToElements().OrderByDescending(e => existingIds is not null && !existingIds.Contains(e.Id)).GroupBy(x => x.GetType())
                    .ToDictionary(g => g.Key, g => g.Take(_sweepSetters ? 30 : 3).ToArray());
                _getters = new Queue<ApiPropertyBinding>(RevitApiCatalog.All.Values.Where(b => b.Kind == "read").OrderBy(b => b.Operation));
                if (_sweepSetters) _setters = new Queue<ApiPropertyBinding>(RevitApiCatalog.All.Values.Where(b => b.Kind == "change").OrderBy(b => b.Operation));
                Checkpoint("running");
            }
            for (int i = 0; i < 40 && _getters.TryDequeue(out var binding); i++) Read(app, binding);
            if (_getters.Count > 0) return;
            for (int i = 0; i < 3 && _setters.TryDequeue(out var setter); i++)
            {
                var targets = _fixtures.Where(pair => setter.Property.DeclaringType!.IsAssignableFrom(pair.Key)).SelectMany(pair => pair.Value);
                _rows.Add(_setterSweep!.Run(app, _doc!, _name!, setter, targets));
            }
            if (_setters.Count > 0) { if (_setters.Count % 30 < 3) Checkpoint("running"); return; }
            Require(!_doc!.IsModifiable && AllElements(_doc).GetElementCount() == _elementCount, "Read sweep changed element count or left a transaction.");
            _runningFixtures = true;
            try { SampleWriteChecks.Run(app, _doc, _name!, _rows); }
            finally { _runningFixtures = false; }
            Require(!_doc.IsModifiable && AllElements(_doc).GetElementCount() == _elementCount, "Write fixtures were not completely rolled back.");
            var project = ProjectContextReader.Read(_doc);
            _transport = Task.Run(() => BenchmarkMcpSession.RunAsync(project));
        }
        catch (Exception ex) { Finish(app, ex.ToString()); }
    }

    private void Read(UIApplication app, ApiPropertyBinding binding)
    {
        var candidates = _fixtures.Where(pair => binding.Property.DeclaringType!.IsAssignableFrom(pair.Key)).SelectMany(pair => pair.Value).Take(12).ToArray();
        if (candidates.Length == 0)
        {
            _rows.Add(new { model = _name, operation = binding.Operation, status = "unsupported", reason = "No matching element class in this sample." });
            return;
        }
        List<object> attempts = [];
        bool passed = false;
        bool allUnsupported = true;
        foreach (Element element in candidates)
        {
            var timer = Stopwatch.StartNew();
            string json = _executor.Execute(app, "agent_read", JsonSerializer.Serialize(new { operation = binding.Operation,
                arguments_json = JsonSerializer.Serialize(new { unique_ids = new[] { element.UniqueId } }) }));
            double ms = timer.Elapsed.TotalMilliseconds;
            using var parsed = JsonDocument.Parse(json);
            bool success = parsed.RootElement.GetProperty("success").GetBoolean();
            var values = success ? parsed.RootElement.GetProperty("data").GetProperty("result").GetProperty("items") : default;
            bool unsupported = success && values.EnumerateArray().All(item => item.TryGetProperty("status", out var status) && status.GetString() == "unsupported");
            passed = success && !unsupported;
            allUnsupported &= unsupported;
            attempts.Add(new { id = element.Id.Value, type = element.GetType().FullName, success = passed,
                elapsed_ms = ms, response_utf8_bytes = Encoding.UTF8.GetByteCount(json),
                error = success ? null : parsed.RootElement.GetProperty("error").GetString(),
                unsupported, reason = unsupported ? values[0].GetProperty("reason").GetString() : null });
            Require(!_doc!.IsModifiable, "Getter left a transaction open.");
            if (passed) break;
        }
        _rows.Add(new { model = _name, operation = binding.Operation, status = passed ? "passed" : allUnsupported ? "unsupported" : "failed",
            verification = "Real ToolExecutor invocation and JSON serialization; not full semantic certification.", attempts });
    }

    private void Checkpoint(string status, string? error = null)
    {
        File.WriteAllText(Path.Combine(_output, "benchmark-results.json"), JsonSerializer.Serialize(new {
            status, error, active_model = _name, remaining_models = _files.Select(Path.GetFileName).ToArray(), remaining_setters_in_model = _setters.Count,
            setter_sweep_enabled = _sweepSetters,
            live_ai_calls = 0, linked_references_loaded = false, source_hashes = _hashes, load_warnings = _loadWarnings, models = _models,
            coverage_fixtures = _coverageFixtures, baseline_path = Environment.GetEnvironmentVariable("REVIT_COPILOT_VALIDATION_BASELINE"),
            baseline_sha256 = _baselineSha256,
            tested_addin_sha256 = Hash(typeof(ToolExecutor).Assembly.Location), test_harness_sha256 = Hash(typeof(SampleBenchmark).Assembly.Location),
            operations = _rows.Concat(_baselineRows.Select(row => (object)row)),
            installed_catalog = new { native = CapabilityCatalog.All.Select(c => new { operation = c.Name, kind = c.Kind }),
                api = RevitApiCatalog.All.Values.Select(c => new { operation = c.Operation, kind = c.Kind }) }
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void Finish(UIApplication app, string? error)
    {
        _finished = true;
        try
        {
            foreach (var pair in _hashes) Require(Hash(Path.Combine(Source, pair.Key)) == pair.Value, "Original sample hash changed: " + pair.Key);
            if (_blank is not null && File.Exists(_blank))
            {
                app.OpenAndActivateDocument(_blank);
                if (_doc?.IsValidObject == true) Require(_doc.Close(false), "Cannot close sample without saving.");
            }
        }
        catch (Exception ex) { error = (error ?? "") + "\nCleanup: " + ex; }
        Checkpoint(error is null ? "completed" : "failed", error);
        app.Application.WriteJournalComment("LECG_SAMPLE_BENCHMARK " + _output, false);
        app.PostCommand(RevitCommandId.LookupPostableCommandId(PostableCommand.ExitRevit));
    }
    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    private static FilteredElementCollector AllElements(Document doc) => new FilteredElementCollector(doc)
        .WherePasses(new LogicalOrFilter(new ElementIsElementTypeFilter(), new ElementIsElementTypeFilter(true)));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
