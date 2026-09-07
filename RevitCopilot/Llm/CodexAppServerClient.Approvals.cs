using System.Collections.Concurrent;
using System.Text.Json;

namespace LECG.RevitCopilot.Llm;

internal sealed partial class CodexAppServerClient
{
    private readonly ConcurrentDictionary<string, CopilotQuestionRequest> _questions = new();
    internal event Action<CopilotQuestionRequest>? QuestionRequested;
    internal event Action<string>? QuestionClosed;

    private async Task HandleServerRequestAsync(JsonElement id, string method, JsonElement parameters, CancellationToken token)
    {
        string key = _connectionGeneration + ":" + id.GetRawText();
        ActiveTurn? requestTurn = _activeTurn;
        try
        {
            // File/shell/permission escalation remains denied. Only explicit user responses
            // can authorize a tool request; annotations and the Revit confirmation stay intact.
            CopilotQuestionRequest? request = null;
            if (_activeTurn is { } active && GetString(parameters, "threadId") == _threadId &&
                (active.TurnId is null || GetString(parameters, "turnId") is null || GetString(parameters, "turnId") == active.TurnId))
                request = CopilotQuestionRequest.Parse(key, method, parameters);
            if (request is not null && QuestionRequested is not null && _questions.TryAdd(key, request))
            {
                QuestionRequested.Invoke(request);
                object result = await request.Completion.Task.WaitAsync(token).ConfigureAwait(false);
                if (_questions.TryRemove(key, out _)) await WriteAsync(new { id, result }, token).ConfigureAwait(false);
            }
            else
            {
                object? result = method switch
                {
                    "item/commandExecution/requestApproval" or "item/fileChange/requestApproval" => new { decision = "decline" },
                    "item/permissions/requestApproval" => new { permissions = new { }, scope = "turn" },
                    "item/tool/requestUserInput" or "tool/requestUserInput" => new { answers = new Dictionary<string, object>() },
                    "mcpServer/elicitation/request" => new { action = "decline", content = (object?)null },
                    _ => null,
                };
                if (result is null) await WriteAsync(new { id, error = new { code = -32601, message = "This request is not supported by the Revit panel." } }, token).ConfigureAwait(false);
                else await WriteAsync(new { id, result }, token).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _questions.TryRemove(key, out _);
            // Never leave an unobserved background request or let a malformed approval authorize a tool.
            requestTurn?.Completion.TrySetException(new InvalidOperationException("The approval request could not be completed safely. Reconnect before retrying. " + ex.Message, ex));
        }
        finally { QuestionClosed?.Invoke(key); }
    }

    internal void CancelQuestions()
    {
        foreach (var request in _questions.Values) request.Completion.TrySetResult(request.CancelResponse);
    }

    private void ResolveQuestion(JsonElement parameters)
    {
        if (GetString(parameters, "threadId") != _threadId || !parameters.TryGetProperty("requestId", out var id)) return;
        string key = _connectionGeneration + ":" + id.GetRawText();
        if (_questions.TryRemove(key, out var request)) request.Completion.TrySetResult(request.CancelResponse);
    }
}
