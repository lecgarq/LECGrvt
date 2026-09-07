using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using LECG.RevitCopilot.Configuration;
using LECG.RevitCopilot.Llm;
using LECG.RevitCopilot.Models;
using LECG.RevitCopilot.Services;

internal static class ProjectSessionChecks
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
    internal static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "LECG-ProjectSessions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        // Kept as inspectable test evidence; no user's chat state or auth files are touched.
        Console.WriteLine("Evidence: " + root);
        string serverRoot = Path.Combine(root, "server");
        Directory.CreateDirectory(serverRoot);
        CodexAppServerClient Create() => new(new CopilotConfiguration(), () =>
        {
            var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"))
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                RedirectStandardOutput = true, RedirectStandardError = true, StandardInputEncoding = new UTF8Encoding(false),
            };
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("--fake-project-server"); start.ArgumentList.Add(serverRoot);
            return start;
        }, Path.Combine(root, "store"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var token = timeout.Token;
        var a = new ProjectContext("file:C:\\A\\Project.rvt", "runtime-a", "Project.rvt", "C:\\A\\Project.rvt", true);
        var b = new ProjectContext("file:C:\\B\\Project.rvt", "runtime-b", "Project.rvt", "C:\\B\\Project.rvt", true);
        string firstThread;
        using (var client = Create())
        {
            var models = await client.GetModelsAsync(token);
            Check(client.ConversationThreadId is null, "model catalog creates no empty chats");
            client.SelectModel(models[0].Id, "low");
            await client.SelectProjectAsync(a, token);
            Check(client.ConversationThreadId is null, "opening a file does not create a chat until first message");
            Check(await client.RunAgentAsync("first", cancellationToken: token) == "Reply: first", "first project turn");
            firstThread = client.ConversationThreadId!;
            await client.RunAgentAsync("second", cancellationToken: token);
            Check(client.ConversationThreadId == firstThread, "same project reuses thread");
            using (var competing = Create())
            {
                bool rejected = false;
                try { await competing.SelectProjectAsync(a, token); } catch (IOException) { rejected = true; }
                Check(rejected, "same project cannot be opened concurrently by another panel process");
            }
            await client.SelectProjectAsync(b, token);
            Check(client.ConversationHistory.Count == 0, "same filename in another folder has separate history");
            await client.RunAgentAsync("other project", cancellationToken: token);
            Check(client.ConversationThreadId != firstThread, "different project gets different thread");
            await client.SelectProjectAsync(a, token);
            Check(client.ConversationHistory.Any(m => m.Content == "first"), "project switch restores transcript");
            await client.RunAgentAsync("back", cancellationToken: token);
            Check(client.ConversationThreadId == firstThread, "project switch resumes original thread");
        }
        using (var client = Create())
        {
            await client.GetModelsAsync(token);
            await client.SelectProjectAsync(a with { RuntimeKey = "reopened-a" }, token);
            Check(client.ConversationHistory.Any(m => m.Content == "back"), "restart restores saved messages");
            await client.RunAgentAsync("restart", cancellationToken: token);
            Check(client.ConversationThreadId == firstThread, "restart uses thread/resume");
            int prompts = 0;
            client.QuestionRequested += q => { prompts++; q.Submit(q.Questions.ToDictionary(x => x.Id, _ => "Accept")); };
            Check(await client.RunAgentAsync("approval", cancellationToken: token) == "approved once", "MCP approval routes through explicit UI answer");
            Check(prompts == 1, "exactly one approval request surfaced");
            Check(await client.RunAgentAsync("foreign approval", cancellationToken: token) == "declined safely", "foreign-thread request is declined");
            Check(prompts == 1, "foreign request never reaches UI");
            Check(await client.RunAgentAsync("shell approval", cancellationToken: token) == "declined safely", "shell escalation remains denied");
            await client.ReconnectAsync(token);
            await client.RunAgentAsync("reconnect", cancellationToken: token);
            Check(client.ConversationThreadId == firstThread, "Reconnect preserves project conversation");
            await client.NewConversationAsync(token);
            Check(client.ConversationHistory.Count == 0 && client.ConversationThreadId is null, "New chat clears only current conversation");
            await client.RunAgentAsync("fresh", cancellationToken: token);
            Check(client.ConversationThreadId != firstThread, "New chat creates an independent thread");
            Check(Directory.EnumerateFiles(Path.Combine(root, "store"), "history-*.json", SearchOption.AllDirectories).Any(), "New chat archives earlier local history");
        }
        using (var client = Create())
        {
            await client.GetModelsAsync(token);
            var temporary = new ProjectContext("unsaved:u", "runtime-u", "Untitled.rvt", "Unsaved", false);
            await client.SelectProjectAsync(temporary, token);
            await client.RunAgentAsync("unsaved", cancellationToken: token);
            string id = client.ConversationThreadId!;
            await client.SelectProjectAsync(temporary with { Key = "file:C:\\Saved.rvt", FileName = "Saved.rvt", Persistent = true }, token);
            await client.RunAgentAsync("saved", cancellationToken: token);
            Check(client.ConversationThreadId == id, "first Save migrates unsaved conversation");
        }
        var store = new ProjectConversationStore(Path.Combine(root, "unit"));
        var state = new ProjectConversation("project", "account", "conversation", "thread", "Model.rvt", null, null, []);
        using (store.Acquire("account", "project")) { store.Save(state); store.Save(state with { FileName = "Renamed.rvt" }); }
        Check(store.Load("other-account", "project") is null, "account histories remain isolated");
        Check(Directory.EnumerateFiles(Path.Combine(root, "unit"), "*.previous", SearchOption.AllDirectories).Any(), "atomic history saves retain recovery copy");
        using var questionJson = JsonDocument.Parse("""{"questions":[{"id":"q","question":"Apply?","options":[{"label":"Accept"},{"label":"Decline"}]}]}""");
        var question = CopilotQuestionRequest.Parse("q", "item/tool/requestUserInput", questionJson.RootElement)!;
        Check(!question.Submit(new Dictionary<string,string>()), "blank approval does not grant permission");
        Check(!question.Submit(new Dictionary<string,string> { ["q"] = "invented" }), "unoffered approval answer rejected");
        Check(question.Submit(new Dictionary<string,string> { ["q"] = "Decline" }), "explicit decline accepted");
        Check(!question.Submit(new Dictionary<string,string> { ["q"] = "Accept" }), "approval request resolves only once");
        using (var client = Create())
        {
            await client.GetModelsAsync(token);
            await client.SelectProjectAsync(b, token);
            client.QuestionRequested += q => q.Completion.TrySetResult(q.CancelResponse);
            Check(await client.RunAgentAsync("approval", cancellationToken: token) == "declined safely", "dismissed question does not run tool");
        }
        using (var client = Create())
        {
            await client.GetModelsAsync(token);
            await client.SelectProjectAsync(b, token);
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(token);
            client.QuestionRequested += _ => cancel.Cancel();
            bool cancelled = false;
            try { await client.RunAgentAsync("approval", cancellationToken: cancel.Token); }
            catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled, "cancellation while approval is pending does not deadlock");
            await client.ReconnectAsync(token);
            Check(await client.RunAgentAsync("after cancel", cancellationToken: token) == "Reply: after cancel", "reconnect after pending approval cancellation remains usable");
        }
        string savedStatePath = Directory.EnumerateFiles(Path.Combine(root, "unit"), "current.json", SearchOption.AllDirectories).Single();
        File.WriteAllText(savedStatePath, "broken fixture");
        bool corruptRejected = false;
        try { store.Load("account", "project"); } catch (InvalidDataException) { corruptRejected = true; }
        Check(corruptRejected && File.ReadAllText(savedStatePath) == "broken fixture", "corrupt history is reported without overwriting evidence");
        var calls = File.ReadAllLines(Path.Combine(serverRoot, "calls.jsonl")).Select(l => JsonDocument.Parse(l)).ToArray();
        try
        {
            Check(calls.Any(d => d.RootElement.GetProperty("method").GetString() == "thread/resume"), "wire protocol recorded thread/resume");
            Check(calls.Where(d => d.RootElement.GetProperty("method").GetString() == "thread/name/set")
                .All(d => d.RootElement.GetProperty("params").GetProperty("name").GetString() is "Project.rvt" or "Saved.rvt" or "Untitled.rvt"), "chat names are project filenames");
            Check(calls.Where(d => d.RootElement.GetProperty("method").GetString() is "thread/start" or "thread/resume" or "turn/start")
                .All(d => d.RootElement.GetProperty("params").GetProperty("approvalPolicy").GetString() == "on-request"), "approval policy is never silently disabled");
        }
        finally { foreach (var d in calls) d.Dispose(); }
        Console.WriteLine("Project session and approval checks passed. Live AI calls: 0.");
    }

    internal static async Task RunFakeServerAsync(string root)
    {
        string? thread = null, turn = null, pendingMethod = null;
        int nextTurn = 0;
        async Task Send(object response) { await Console.Out.WriteLineAsync(JsonSerializer.Serialize(response, Options)); await Console.Out.FlushAsync(); }
        while (await Console.In.ReadLineAsync() is { } line)
        {
            using var parsed = JsonDocument.Parse(line);
            var request = parsed.RootElement;
            if (!request.TryGetProperty("method", out var methodValue))
            {
                if (pendingMethod is null) continue;
                var answer = request.GetProperty("result");
                bool accepted = answer.TryGetProperty("answers", out var answers) && answers.EnumerateObject().Any() &&
                    answers.EnumerateObject().First().Value.GetProperty("answers")[0].GetString() == "Accept";
                await Finish(accepted ? "approved once" : "declined safely");
                pendingMethod = null;
                continue;
            }
            string method = methodValue.GetString()!;
            File.AppendAllText(Path.Combine(root, "calls.jsonl"), line + "\n");
            if (!request.TryGetProperty("id", out var id)) continue;
            var p = request.GetProperty("params");
            object result = new { };
            switch (method)
            {
                case "initialize": break;
                case "account/read": result = new { account = new { type = "chatgpt", email = "fixture@example.invalid" }, requiresOpenaiAuth = false }; break;
                case "model/list": result = new { data = new[] { new { id = "fixture", model = "fixture", displayName = "Fixture", description = "Offline fixture", hidden = false, isDefault = true, defaultReasoningEffort = "low", supportedReasoningEfforts = new[] { new { reasoningEffort = "low", description = "Low" } } } }, nextCursor = (string?)null }; break;
                case "thread/start":
                    thread = "fixture-" + Guid.NewGuid().ToString("N");
                    File.WriteAllText(Path.Combine(root, thread + ".json"), "[]");
                    result = new { thread = new { id = thread } }; break;
                case "thread/resume":
                    thread = p.GetProperty("threadId").GetString();
                    result = new { thread = new { id = thread, turns = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(root, thread + ".json"))) } }; break;
                case "mcpServerStatus/list": result = new { data = new[] { new { name = "lecg-revit", tools = new { project_info = new { } } } }, nextCursor = (string?)null }; break;
                case "turn/start":
                    turn = "turn-" + ++nextTurn;
                    string prompt = p.GetProperty("input")[0].GetProperty("text").GetString()!;
                    int marker = prompt.LastIndexOf("\n\nUser message:\n", StringComparison.Ordinal);
                    if (marker >= 0) prompt = prompt[(marker + "\n\nUser message:\n".Length)..];
                    await Send(new { id = id.Clone(), result = new { turn = new { id = turn } } });
                    AppendItem(new { type = "userMessage", content = new[] { new { type = "text", text = prompt } } });
                    if (prompt.Contains("approval"))
                    {
                        pendingMethod = prompt == "shell approval" ? "item/commandExecution/requestApproval" : "item/tool/requestUserInput";
                        await Send(new { id = "approval-" + turn, method = pendingMethod, @params = new { threadId = prompt == "foreign approval" ? "foreign" : thread, turnId = turn,
                            itemId = "item", questions = new[] { new { id = "permission", header = "Approval", question = "Allow this Revit tool call?", options = new[] { new { label = "Accept", description = "Allow once" }, new { label = "Decline", description = "Do not run" } } } } } });
                    }
                    else await Finish("Reply: " + prompt);
                    continue;
                case "turn/interrupt": await Finish("interrupted"); break;
            }
            await Send(new { id = id.Clone(), result });
        }
        void AppendItem(object item)
        {
            string path = Path.Combine(root, thread + ".json");
            var history = JsonSerializer.Deserialize<List<JsonElement>>(File.ReadAllText(path))!;
            history.Add(JsonSerializer.SerializeToElement(new { items = new[] { item } }, Options));
            File.WriteAllText(path, JsonSerializer.Serialize(history, Options));
        }
        async Task Finish(string text)
        {
            AppendItem(new { type = "agentMessage", phase = "final_answer", text });
            await Send(new { method = "item/completed", @params = new { threadId = thread, turnId = turn, item = new { type = "agentMessage", phase = "final_answer", text } } });
            await Send(new { method = "turn/completed", @params = new { threadId = thread, turn = new { id = turn, status = "completed" } } });
        }
    }
}
