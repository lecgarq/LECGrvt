using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using TextRange = System.Windows.Documents.TextRange;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;

namespace LECG.RevitCopilot.SmokeTests;

// Loaded only by the adjacent journal manifest, never by the production manifest.
public sealed class SmokeApplication : IExternalApplication
{
    private readonly List<object> _results = [];
    private readonly Queue<(string Name, object Args, Action<JsonElement> Check)> _checks = new();
    private Task<string>? _pending;
    private (string Name, object Args, Action<JsonElement> Check) _current;
    private Document? _scratch;
    private Wall? _wall;
    private string? _outputDirectory;
    private DateTime _deadline;
    private bool _started;
    private bool _finished;
    private object? _dispatcher;
    private MethodInfo? _execute;
    private FrameworkElement? _panel;
    private SampleBenchmark? _benchmark;

    public Result OnStartup(UIControlledApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (!Environment.GetCommandLineArgs().Any(arg => string.Equals(
            Path.GetFileName(arg), "journal.RevitCopilotSmokeTest.txt", StringComparison.OrdinalIgnoreCase))) return Result.Succeeded;
        bool liveCodex = Environment.GetEnvironmentVariable("REVIT_COPILOT_LIVE_CODEX_TEST") == "1";
        _deadline = DateTime.UtcNow.AddSeconds(liveCodex ? 240 : 90);
        application.Idling += OnIdling;
        if (Environment.GetEnvironmentVariable("REVIT_COPILOT_SAMPLE_BENCHMARK") == "1")
        {
            _benchmark = new SampleBenchmark();
            application.ControlledApplication.FailuresProcessing += _benchmark.OnFailuresProcessing;
        }
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.Idling -= OnIdling;
        if (_benchmark is not null) application.ControlledApplication.FailuresProcessing -= _benchmark.OnFailuresProcessing;
        return Result.Succeeded;
    }

    private void OnIdling(object? sender, IdlingEventArgs e)
    {
        if (_finished || sender is not UIApplication app) return;
        if (_benchmark is not null) { _benchmark.Request(); e.SetRaiseWithoutDelay(); return; }
        try
        {
            if (DateTime.UtcNow > _deadline) throw new TimeoutException("Smoke test exceeded its configured deadline.");
            if (!_started)
            {
                if (app.ActiveUIDocument is null) return;
                if (!app.GetDockablePane(RevitCopilotApp.PaneId).IsShown()) return;
                _panel = (FrameworkElement)typeof(RevitCopilotApp).GetProperty("Panel", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                bool usageTest = Environment.GetEnvironmentVariable("REVIT_COPILOT_USAGE_TEST") == "1";
                if (usageTest && !((TextBlock)_panel.FindName("QuotaText")).Text.Contains("% left", StringComparison.Ordinal))
                { e.SetRaiseWithoutDelay(); return; }
                if (Environment.GetEnvironmentVariable("REVIT_COPILOT_LIVE_CODEX_TEST") == "1" &&
                    !((Button)_panel.FindName("SendButton")).IsEnabled) { e.SetRaiseWithoutDelay(); return; }
                _scratch = app.ActiveUIDocument.Document;
                Require(string.IsNullOrEmpty(_scratch.PathName), "Test requires a new, unsaved document.");
                Require(!_scratch.IsWorkshared, "Test requires a non-workshared scratch document.");
                _started = true;
                _results.Add(new { test = "ribbon_opens_pane", passed = true });
                if (usageTest)
                {
                    _results.Add(new { test = "account_usage_in_panel", passed = true, text = ((TextBlock)_panel.FindName("QuotaText")).Text });
                    Require((string?)((ComboBox)_panel.FindName("EffortSelector")).SelectedItem == "low", "Initial reasoning effort must conserve quota.");
                    _results.Add(new { test = "low_reasoning_default", passed = true });
                }
                CreateFixture(_scratch);
                PanelPresentationChecks.Run((LECG.RevitCopilot.UI.DockablePanel)_panel, _results);
                if (Environment.GetEnvironmentVariable("REVIT_COPILOT_AGENT_TEST") == "1")
                    AgentSmokeChecks.Run(app, _scratch, _wall!, _results);
                if (Environment.GetEnvironmentVariable("REVIT_COPILOT_API_TEST") == "1")
                    ApiSmokeChecks.Run(app, _scratch, _wall!, _results);
                PrepareChecks(app, _scratch);
                _dispatcher = typeof(RevitCopilotApp).GetProperty("Dispatcher", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                _execute = _dispatcher.GetType().GetMethod("ExecuteToolAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            }

            if (_pending is not null)
            {
                if (!_pending.IsCompleted) { e.SetRaiseWithoutDelay(); return; }
                using JsonDocument result = JsonDocument.Parse(_pending.GetAwaiter().GetResult());
                _current.Check(result.RootElement);
                _results.Add(new { test = _current.Name, passed = true, response = result.RootElement.Clone() });
                _pending = null;
            }

            if (_checks.TryDequeue(out _current))
            {
                string arguments = JsonSerializer.Serialize(_current.Args);
                if (_current.Name.StartsWith("mcp_bridge:", StringComparison.Ordinal))
                {
                    string bridgeTool = _current.Name.Split(':')[1];
                    _pending = Task.Run(() => CallMcpBridgeAsync(bridgeTool, arguments));
                }
                else if (_current.Name.StartsWith("mcp_server:", StringComparison.Ordinal))
                {
                    var project = LECG.RevitCopilot.Revit.ProjectContextReader.Read(_scratch!);
                    _pending = Task.Run(() => HarvestMcpRoundTrip.CallAsync(_current.Name.Split(':')[1], arguments, project));
                }
                else if (_current.Name == "mcp_guard:wrong")
                {
                    var project = LECG.RevitCopilot.Revit.ProjectContextReader.Read(_scratch!) with { RuntimeKey = "stale-runtime" };
                    _pending = Task.Run(() => HarvestMcpRoundTrip.CallAsync("project_info", "{}", project));
                }
                else if (_current.Name.StartsWith("codex_panel:", StringComparison.Ordinal))
                {
                    _pending = CallCodexPanelAsync(_current.Name.Split(':')[1]);
                }
                else if (_current.Name.StartsWith("session_guard:", StringComparison.Ordinal))
                {
                    var context = LECG.RevitCopilot.Revit.ProjectContextReader.Read(_scratch!);
                    bool wrong = _current.Name.EndsWith("wrong", StringComparison.Ordinal);
                    _pending = Task.Run(async () =>
                    {
                        try { return await (Task<string>)_execute!.Invoke(_dispatcher, ["project_info", "{}", CancellationToken.None, wrong ? "other-project" : context.Key, context.RuntimeKey])!; }
                        catch (Exception ex) { return JsonSerializer.Serialize(new { success = false, error = ex.Message }); }
                    });
                }
                else
                {
                    string name = _current.Name.Split(':')[0];
                    // Deliberately queue from a pool thread: Revit calls must execute through ExternalEvent.
                    _pending = Task.Run(async () => await (Task<string>)_execute!.Invoke(
                        _dispatcher, [name, arguments, CancellationToken.None, null, null])!);
                }
                e.SetRaiseWithoutDelay();
            }
            else
            {
                Require(app.CanPostCommand(RevitCommandId.LookupPostableCommandId(PostableCommand.Undo)), "No Undo operation available after tool mutations.");
                _results.Add(new { test = "undo_available_after_mutations", passed = true });
                Finish(app, null);
            }
        }
        catch (Exception ex)
        {
            Finish(app, ex.ToString());
        }
    }

    private void CreateFixture(Document doc)
    {
        using Transaction transaction = new(doc, "Smoke test: create disposable walls");
        transaction.Start();
        try
        {
            Level level = Level.Create(doc, 0);
            level.Name = "Copilot smoke level";
            WallType type = new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>().First(t => t.Kind == WallKind.Basic);
            _wall = Wall.Create(doc, Line.CreateBound(XYZ.Zero, new XYZ(10, 0, 0)), type.Id, level.Id, 10, 0, false, false);
            Require(transaction.Commit() == TransactionStatus.Committed, "Fixture transaction did not commit.");
        }
        catch
        {
            if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
            throw;
        }
    }

    private void PrepareChecks(UIApplication app, Document doc)
    {
        _checks.Enqueue(("session_guard:correct", new { }, Success));
        _checks.Enqueue(("session_guard:wrong", new { }, r =>
        {
            Require(!r.GetProperty("success").GetBoolean() && r.GetProperty("error").GetString()!.Contains("active project changed"), "Wrong-project MCP call must be blocked by the Revit dispatcher.");
        }));
        _checks.Enqueue(("mcp_guard:wrong", new { }, r =>
        {
            Require(!r.GetProperty("success").GetBoolean() && r.GetProperty("error").GetString()!.Contains("active project changed"), "Actual MCP process must forward and enforce runtime/project guards.");
        }));
        string id = _wall!.UniqueId;
        string comments = _wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Definition.Name;
        string height = _wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).Definition.Name;
        _checks.Enqueue(("mcp_bridge:project_info", new { }, r =>
        {
            Success(r);
            Require(!doc.IsModifiable, "MCP bridge query left a transaction open.");
        }));
        if (Environment.GetEnvironmentVariable("REVIT_COPILOT_API_TEST") == "1")
        {
            Floor floor = new FilteredElementCollector(doc).OfClass(typeof(Floor)).Cast<Floor>().First();
            _checks.Enqueue(("mcp_server:agent_read_batch:harvested", new { steps_json = JsonSerializer.Serialize(new object[]
            {
                new { id = "inserts", operation = "host_inserts", arguments = new { unique_id = id, limit = 2 } },
                new { id = "phases", operation = "element_phase_status", arguments = new { unique_ids = new[] { id } } },
                new { id = "faces", operation = "host_bottom_faces", arguments = new { unique_id = floor.UniqueId, limit = 2 } }
            }) }, r =>
            {
                Success(r);
                Require(r.GetProperty("data").GetProperty("completed_count").GetInt32() == 3, "Actual MCP server did not execute all harvested reads.");
                Require(!doc.IsModifiable, "Actual MCP reads left a transaction open.");
            }));
            _checks.Enqueue(("mcp_bridge:agent_read_batch", new { steps = new object[]
            {
                new { id = "width", operation = "api.get:Autodesk.Revit.DB.Wall.Width", arguments = new { unique_ids = new[] { id } } },
                new { id = "levels", operation = "levels_list", arguments = new { limit = 2 } },
                new { id = "sheets", operation = "sheets_list", arguments = new { limit = 2 } }
            } }, r =>
            {
                Success(r);
                Require(r.GetProperty("data").GetProperty("completed_count").GetInt32() == 3, "Bridge batch did not run all three reads.");
                Require(r.GetProperty("queue_wait_ms").GetDouble() >= 0 && r.GetProperty("execution_ms").GetDouble() >= 0, "Queue/execution timings are missing.");
            }));
        }
        if (Environment.GetEnvironmentVariable("REVIT_COPILOT_LIVE_CODEX_TEST") == "1")
        {
            foreach (string model in new[] { "gpt-6-astra", "gpt-5.6-sol" })
            {
                _checks.Enqueue(($"codex_panel:{model}", new { }, r =>
                {
                    Success(r);
                    string response = r.GetProperty("data").GetProperty("response").GetString() ?? string.Empty;
                    Require(response.Contains("Project1", StringComparison.OrdinalIgnoreCase), "Codex response did not identify the active scratch project.");
                }));
            }
        }
        _checks.Enqueue(("project_info", new { }, r => { Success(r); Require(!doc.IsModifiable, "Query left a transaction open."); }));
        _checks.Enqueue(("elements_query", new { category = "OST_Walls", limit = 1 }, r =>
        {
            Success(r);
            Require(r.GetProperty("data").GetProperty("returned_count").GetInt32() == 1, "Wall query did not return fixture.");
        }));
        _checks.Enqueue(("element_get", new { unique_id = id }, Success));
        _checks.Enqueue(("element_set_parameter:comments", new { unique_id = id, parameter_name = comments, new_value = "Copilot verified" }, r =>
        {
            Success(r);
            Require(_wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString() == "Copilot verified", "Comment not committed.");
            Require(!doc.IsModifiable, "Write transaction left open.");
        }));
        _checks.Enqueue(("element_set_parameter:units", new { unique_id = id, parameter_name = height, new_value = "2500 mm" }, r =>
        {
            Success(r);
            double expected = UnitUtils.ConvertToInternalUnits(2500, UnitTypeId.Millimeters);
            Require(Math.Abs(_wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble() - expected) < 1e-8, "Unit conversion mismatch.");
        }));
        _checks.Enqueue(("element_set_parameter:invalid", new { unique_id = id, parameter_name = height, new_value = "not a length" }, r =>
        {
            Require(!r.GetProperty("success").GetBoolean(), "Invalid value unexpectedly succeeded.");
            Require(!doc.IsModifiable, "Failed transaction did not close.");
            double expected = UnitUtils.ConvertToInternalUnits(2500, UnitTypeId.Millimeters);
            Require(Math.Abs(_wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble() - expected) < 1e-8, "Failed write changed the value.");
        }));
        long elementId = _wall.Id.Value;
        _checks.Enqueue(("view_isolate_or_select", new { element_ids = new[] { elementId } }, r =>
        {
            Success(r);
            Require(app.ActiveUIDocument.Selection.GetElementIds().Any(i => i.Value == elementId), "Fixture was not selected.");
        }));
        _checks.Enqueue(("elements_delete", new { unique_ids = new[] { id } }, r =>
        {
            Success(r);
            Require(doc.GetElement(new ElementId(elementId)) is null, "Wall was not deleted.");
            Require(!doc.IsModifiable, "Delete transaction left open.");
        }));
    }

    private static async Task<string> CallMcpBridgeAsync(string tool, string argumentsJson)
    {
        await using NamedPipeClientStream pipe = new(
            ".",
            "LECG.RevitCopilot.2026",
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
        await pipe.ConnectAsync(timeout.Token);

        using StreamReader reader = new(pipe, new UTF8Encoding(false), leaveOpen: true);
        using StreamWriter writer = new(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        using JsonDocument arguments = JsonDocument.Parse(argumentsJson);
        string id = Guid.NewGuid().ToString("N");
        await writer.WriteLineAsync(JsonSerializer.Serialize(new
        {
            id,
            tool,
            arguments = arguments.RootElement.Clone(),
        }).AsMemory(), timeout.Token);

        string line = await reader.ReadLineAsync(timeout.Token)
            ?? throw new IOException("MCP bridge closed without a response.");
        using JsonDocument response = JsonDocument.Parse(line);
        string? error = response.RootElement.GetProperty("error").GetString();
        if (!string.IsNullOrWhiteSpace(error)) throw new InvalidOperationException(error);
        return response.RootElement.GetProperty("result").GetString()
            ?? throw new InvalidDataException("MCP bridge returned no result.");
    }

    private async Task<string> CallCodexPanelAsync(string modelId)
    {
        Task<string> operation = await _panel!.Dispatcher.InvokeAsync(async () =>
        {
            var models = (ComboBox)_panel.FindName("ModelSelector");
            object model = models.Items.Cast<object>().Single(item =>
                (string)item.GetType().GetProperty("Id")!.GetValue(item)! == modelId);
            models.SelectedItem = model;
            var efforts = (ComboBox)_panel.FindName("EffortSelector");
            Require(efforts.Items.Cast<string>().Contains("low"), "Low effort missing from live model selector.");
            efforts.SelectedItem = "low";
            ((TextBox)_panel.FindName("PromptInput")).Text = "Use only lecg-revit project_info. Tell me the exact active Revit project title in one short sentence. Do not modify anything.";
            await (Task)_panel.GetType().GetMethod("SendCurrentPromptAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_panel, null)!;
            var host = (StackPanel)_panel.FindName("MessageHost");
            var card = (Border)host.Children[host.Children.Count - 1];
            var body = (StackPanel)card.Child;
            var text = body.Children.OfType<RichTextBox>().Single();
            string response = new TextRange(text.Document.ContentStart, text.Document.ContentEnd).Text;
            return JsonSerializer.Serialize(new { success = true, data = new { model = modelId, effort = "low", response } });
        });
        return await operation;
    }

    private void Finish(UIApplication app, string? error)
    {
        _finished = true;
        _outputDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "results", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(_outputDirectory);
        if (_scratch is not null && _scratch.IsValidObject)
        {
            try { _scratch.SaveAs(Path.Combine(_outputDirectory, "scratch.rvt"), new SaveAsOptions { OverwriteExistingFile = false }); }
            catch (Exception ex) { error = $"{error}\nScratch save failed: {ex.Message}"; }
        }
        File.WriteAllText(Path.Combine(_outputDirectory, "smoke-results.json"), JsonSerializer.Serialize(
            new
            {
                passed = error is null,
                error,
                tests = _results,
                live_llm_tested = Environment.GetEnvironmentVariable("REVIT_COPILOT_LIVE_CODEX_TEST") == "1",
            },
            new JsonSerializerOptions { WriteIndented = true }));
        app.Application.WriteJournalComment($"LECG_COPILOT_SMOKE {(error is null ? "PASS" : "FAIL")}: {_outputDirectory}", false);
        app.PostCommand(RevitCommandId.LookupPostableCommandId(PostableCommand.ExitRevit));
    }

    private static void Success(JsonElement result) => Require(result.GetProperty("success").GetBoolean(), result.ToString());
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
