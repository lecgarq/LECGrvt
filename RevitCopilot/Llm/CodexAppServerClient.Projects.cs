using System.IO;
using System.Text.Json;
using LECG.RevitCopilot.Configuration;
using LECG.RevitCopilot.Models;
using LECG.RevitCopilot.Services;

namespace LECG.RevitCopilot.Llm;

internal sealed partial class CodexAppServerClient
{
    private readonly ProjectConversationStore _projectStore;
    private readonly Dictionary<string, ProjectConversation> _temporaryConversations = [];
    private ProjectContext? _project;
    private ProjectConversation? _conversation;
    private IDisposable? _projectLease;
    private string? _accountKey;
    private bool _refreshProjectContext;
    private const string ResumeInstructions = "Session resumed in the active Revit project. Historical IDs, selections, previews and approvals are not current. Re-query facts and obtain a fresh preview/confirmation before changing anything.";
    internal IReadOnlyList<ChatEntry> ConversationHistory => _history.ToArray();
    internal string? ConversationThreadId => _threadId ?? _conversation?.ThreadId;
    internal string? SavedModel => _conversation?.Model;
    internal string? SavedEffort => _conversation?.Effort;
    internal event Action? ConversationChanged;
    internal event Action<string>? SessionWarning;

    internal async Task SelectProjectAsync(ProjectContext? project, CancellationToken token)
    {
        await _turnGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (project is not null) await EnsureConnectedAsync(token).ConfigureAwait(false);
            if (_project == project && _conversation?.AccountKey == _accountKey) return;
            SaveProject();
            if (_threadId is not null && _process is { HasExited: false })
                await RequestAsync("thread/unsubscribe", new { threadId = _threadId }, token).ConfigureAwait(false);
            _threadId = null;
            _sendInstructions = true;
            PublishUsage(_usageState.ResetThread());
            await LoadProjectAsync(project, token).ConfigureAwait(false);
        }
        finally { _turnGate.Release(); }
    }

    private Task LoadProjectAsync(ProjectContext? project, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var previousProject = _project;
        var previousConversation = _conversation;
        _projectLease?.Dispose();
        _projectLease = null;
        _project = null;
        _conversation = null;
        _history.Clear();
        if (project is null) return Task.CompletedTask;
        IDisposable? lease = null;
        try
        {
            ProjectConversation? stored;
            if (project.Persistent)
            {
                lease = _projectStore.Acquire(_accountKey!, project.Key);
                stored = _projectStore.Load(_accountKey!, project.Key);
                // First Save carries an unsaved conversation forward; Save As stays separate.
                if (stored is null && previousProject is { Persistent: false } &&
                    previousProject.Key.StartsWith("unsaved:", StringComparison.Ordinal) &&
                    previousProject.RuntimeKey == project.RuntimeKey && previousConversation?.AccountKey == _accountKey)
                    stored = previousConversation! with { ProjectKey = project.Key, FileName = project.FileName };
            }
            else _temporaryConversations.TryGetValue(_accountKey + ":" + project.Key, out stored);
            _conversation = stored ?? new(project.Key, _accountKey!, Guid.NewGuid().ToString("N"), null, project.FileName, null, null, []);
            _project = project;
            _projectLease = lease;
            _history.AddRange(_conversation.Messages);
            return Task.CompletedTask;
        }
        catch { lease?.Dispose(); throw; }
    }

    internal async Task NewConversationAsync(CancellationToken token)
    {
        await _turnGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_project is null || _conversation is null) throw new InvalidOperationException("Open a project first.");
            SaveProject();
            if (_project.Persistent) _projectStore.Archive(_conversation);
            if (_threadId is not null)
                await RequestAsync("thread/unsubscribe", new { threadId = _threadId }, token).ConfigureAwait(false);
            _threadId = null;
            _history.Clear();
            PublishUsage(_usageState.ResetThread());
            _conversation = _conversation with { ConversationId = Guid.NewGuid().ToString("N"), ThreadId = null, Messages = [] };
            _sendInstructions = true;
            SaveProject();
        }
        finally { _turnGate.Release(); }
    }

    private async Task EnsureStartedAsync(CancellationToken token)
    {
        await EnsureConnectedAsync(token).ConfigureAwait(false);
        if (_accountChanged) throw new InvalidOperationException("Your Codex account changed. Reconnect to load this account's project conversation.");
        if (_project is null || _conversation is null) throw new InvalidOperationException("Open a Revit project before starting a conversation.");
        if (_conversation.AccountKey != _accountKey) throw new InvalidOperationException("Your Codex account changed. Reconnect to load this account's project conversation.");
        if (_threadId is not null) return;
        bool resume = _conversation.ThreadId is not null;
        var settings = new Dictionary<string, object?>
        {
            ["cwd"] = CopilotPaths.Root, ["approvalPolicy"] = "on-request", ["approvalsReviewer"] = "user",
            ["sandbox"] = "read-only", ["personality"] = "friendly", ["config"] = RevitServerConfiguration(),
        };
        if (resume) settings["threadId"] = _conversation.ThreadId;
        else settings["serviceName"] = "lecg_revit_panel";
        JsonElement result;
        try { result = await RequestAsync(resume ? "thread/resume" : "thread/start", settings, token).ConfigureAwait(false); }
        catch (Exception ex) when (resume && ex is not OperationCanceledException)
        {
            throw new InvalidOperationException("Could not resume this project's conversation. Your history is preserved. Retry Reconnect, or choose New conversation to start separately. " + ex.Message, ex);
        }
        var thread = result.GetProperty("thread");
        _threadId = thread.GetProperty("id").GetString() ?? throw new InvalidDataException("Codex returned no thread ID.");
        _conversation = _conversation with { ThreadId = _threadId };
        // Save the ID immediately: a crash after creation must not generate another chat.
        SaveProject();
        if (resume && thread.TryGetProperty("turns", out var turns) && turns.GetArrayLength() > 0)
        {
            var restored = ReadTranscript(turns);
            if (restored.Count > 0) { _history.Clear(); _history.AddRange(restored); }
        }
        _sendInstructions = !resume;
        _refreshProjectContext = resume;
        await RequestAsync("thread/name/set", new { threadId = _threadId, name = _project.FileName }, token).ConfigureAwait(false);
        SaveProject();
        ConversationChanged?.Invoke();
    }

    internal static List<ChatEntry> ReadTranscript(JsonElement turns)
    {
        List<ChatEntry> entries = [];
        foreach (var turn in turns.EnumerateArray())
        {
            if (!turn.TryGetProperty("items", out var items)) continue;
            foreach (var item in items.EnumerateArray())
            {
                string? type = GetString(item, "type");
                if (type == "userMessage" && item.TryGetProperty("content", out var content))
                {
                    string text = string.Join("\n", content.EnumerateArray().Where(c => GetString(c, "type") == "text").Select(c => GetString(c, "text")));
                    const string marker = "\n\nUser message:\n";
                    int index = text.IndexOf(marker, StringComparison.Ordinal);
                    if ((text.StartsWith(PanelInstructions, StringComparison.Ordinal) || text.StartsWith(ResumeInstructions, StringComparison.Ordinal)) && index >= 0) text = text[(index + marker.Length)..];
                    entries.Add(new(DateTimeOffset.UtcNow, "user", text));
                }
                else if (type == "agentMessage" && GetString(item, "phase") is null or "final_answer")
                    entries.Add(new(DateTimeOffset.UtcNow, "assistant", GetString(item, "text") ?? ""));
            }
        }
        return entries;
    }

    private void SaveProject()
    {
        if (_conversation is null || _project is null) return;
        _conversation = _conversation with { Messages = _history.ToList(), FileName = _project.FileName,
            Model = _selectedModel?.Id ?? _conversation.Model, Effort = _selectedEffort ?? _conversation.Effort };
        if (_project.Persistent) _projectStore.Save(_conversation);
        else _temporaryConversations[_conversation.AccountKey + ":" + _project.Key] = _conversation;
    }
}
