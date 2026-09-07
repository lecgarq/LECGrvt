using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LECG.RevitCopilot.Configuration;
using LECG.RevitCopilot.Models;
using LECG.RevitCopilot.Revit;
using LECG.RevitCopilot.Services;

namespace LECG.RevitCopilot.Llm;

internal sealed class LlmClient : IAgentClient
{
    private readonly CopilotConfiguration _configuration;
    private readonly ExternalEventDispatcher _dispatcher;
    private readonly HttpClient _httpClient = new();
    private readonly List<ChatEntry> _history = [];
    private readonly SessionLogger _sessionLogger = new();
    private string? _previousResponseId;
    private string? _conversationConfiguration;
    private bool _disposed;
    private ProjectContext? _project;

    internal LlmClient(CopilotConfiguration configuration, ExternalEventDispatcher dispatcher)
    {
        _configuration = configuration;
        _dispatcher = dispatcher;
    }

    internal IReadOnlyList<ChatEntry> History => _history;
    internal void ResetProjectConversation(ProjectContext? project = null)
    {
        _project = project;
        _history.Clear();
        _previousResponseId = null;
        _conversationConfiguration = null;
    }

    public async Task<string> RunAgentAsync(
        string userPrompt,
        Func<string, string?, Task>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            throw new ArgumentException("Enter a message before sending.", nameof(userPrompt));
        }

        CopilotSettings settings = _configuration.ReadSettings();
        ValidateSettings(settings);
        string instructions = _configuration.ReadSystemPrompt();
        ResetConversationIfConfigurationChanged(settings);

        _history.Add(new ChatEntry(DateTimeOffset.UtcNow, "user", userPrompt.Trim()));
        await ReportAsync(progress, "Thinking...", null).ConfigureAwait(false);

        object[] input = [new { role = "user", content = userPrompt.Trim() }];
        try
        {
            for (int iteration = 1; iteration <= Math.Clamp(settings.MaxAgentIterations, 1, 30); iteration++)
            {
                LlmTurn turn = await CreateResponseAsync(
                    settings,
                    instructions,
                    input,
                    _previousResponseId,
                    cancellationToken).ConfigureAwait(false);
                _previousResponseId = turn.ResponseId;

                if (turn.ToolCalls.Count == 0)
                {
                    string responseText = string.IsNullOrWhiteSpace(turn.Text)
                        ? "The model returned no text response."
                        : turn.Text.Trim();
                    _history.Add(new ChatEntry(DateTimeOffset.UtcNow, "assistant", responseText));
                    await SaveSessionSafelyAsync(cancellationToken).ConfigureAwait(false);
                    await ReportAsync(progress, "Ready", null).ConfigureAwait(false);
                    return responseText;
                }

                List<object> toolOutputs = new(turn.ToolCalls.Count);
                foreach (ToolCall call in turn.ToolCalls)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ReportAsync(progress, "Executing Revit API...", call.Name).ConfigureAwait(false);
                    _history.Add(new ChatEntry(
                        DateTimeOffset.UtcNow,
                        "tool_call",
                        call.ArgumentsJson,
                        call.Name));

                    string result = await _dispatcher.ExecuteToolAsync(
                        call.Name,
                        call.ArgumentsJson,
                        cancellationToken, _project?.Key, _project?.RuntimeKey).ConfigureAwait(false);
                    _history.Add(new ChatEntry(DateTimeOffset.UtcNow, "tool_result", result, call.Name));
                    toolOutputs.Add(new
                    {
                        type = "function_call_output",
                        call_id = call.CallId,
                        output = result
                    });
                }

                await SaveSessionSafelyAsync(cancellationToken).ConfigureAwait(false);
                await ReportAsync(progress, "Thinking...", null).ConfigureAwait(false);
                input = toolOutputs.ToArray();
            }

            throw new InvalidOperationException(
                $"The agent exceeded {settings.MaxAgentIterations} tool iterations. Refine the request and try again.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _history.Add(new ChatEntry(DateTimeOffset.UtcNow, "system", ex.Message));
            await SaveSessionSafelyAsync(CancellationToken.None).ConfigureAwait(false);
            await ReportAsync(progress, "Ready", null).ConfigureAwait(false);
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
    }

    private async Task<LlmTurn> CreateResponseAsync(
        CopilotSettings settings,
        string instructions,
        object[] input,
        string? previousResponseId,
        CancellationToken cancellationToken)
    {
        Dictionary<string, object?> body = new()
        {
            ["model"] = settings.Model,
            ["instructions"] = instructions,
            ["input"] = input,
            ["tools"] = ToolSchemas.Definitions,
            ["tool_choice"] = "auto",
            ["parallel_tool_calls"] = true,
            ["store"] = true,
            ["max_output_tokens"] = 4096
        };
        if (!string.IsNullOrWhiteSpace(previousResponseId))
        {
            body["previous_response_id"] = previousResponseId;
        }

        using HttpRequestMessage request = new(HttpMethod.Post, settings.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ResolveApiKey());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.RequestTimeoutSeconds, 10, 600)));

        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token).ConfigureAwait(false);
        string responseJson = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"LLM request failed ({(int)response.StatusCode} {response.ReasonPhrase}): {ReadApiError(responseJson)}");
        }

        return ParseResponse(responseJson);
    }

    private static LlmTurn ParseResponse(string responseJson)
    {
        using JsonDocument document = JsonDocument.Parse(responseJson);
        JsonElement root = document.RootElement;
        string responseId = root.TryGetProperty("id", out JsonElement idElement)
            ? idElement.GetString() ?? string.Empty
            : string.Empty;
        if (string.IsNullOrWhiteSpace(responseId))
        {
            throw new InvalidOperationException("The LLM response did not contain an id.");
        }

        List<ToolCall> calls = [];
        StringBuilder text = new();
        if (root.TryGetProperty("output", out JsonElement output) && output.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in output.EnumerateArray())
            {
                string? type = item.TryGetProperty("type", out JsonElement typeElement)
                    ? typeElement.GetString()
                    : null;
                if (type == "function_call")
                {
                    string callId = RequireResponseString(item, "call_id");
                    string name = RequireResponseString(item, "name");
                    string arguments = RequireResponseString(item, "arguments");
                    calls.Add(new ToolCall(callId, name, arguments));
                }
                else if (type == "message"
                         && item.TryGetProperty("content", out JsonElement content)
                         && content.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement part in content.EnumerateArray())
                    {
                        if (part.TryGetProperty("type", out JsonElement partType)
                            && partType.GetString() == "output_text"
                            && part.TryGetProperty("text", out JsonElement partText))
                        {
                            if (text.Length > 0) text.AppendLine();
                            text.Append(partText.GetString());
                        }
                    }
                }
            }
        }

        if (calls.Count == 0 && text.Length == 0
            && root.TryGetProperty("error", out JsonElement error)
            && error.ValueKind == JsonValueKind.Object)
        {
            throw new InvalidOperationException(ReadApiError(responseJson));
        }

        return new LlmTurn(responseId, text.ToString(), calls);
    }

    private static string RequireResponseString(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? string.Empty;
        }
        throw new InvalidOperationException($"The LLM function call omitted '{property}'.");
    }

    private static string ReadApiError(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("error", out JsonElement error))
            {
                if (error.ValueKind == JsonValueKind.Object
                    && error.TryGetProperty("message", out JsonElement message))
                {
                    return message.GetString() ?? json;
                }
                if (error.ValueKind == JsonValueKind.String) return error.GetString() ?? json;
            }
        }
        catch (JsonException)
        {
            // Preserve the provider response below.
        }
        return json.Length <= 1000 ? json : json[..1000] + "...";
    }

    private static void ValidateSettings(CopilotSettings settings)
    {
        if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out Uri? endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttps && endpoint.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("config.json must contain an absolute HTTP or HTTPS endpoint.");
        }
        if (string.IsNullOrWhiteSpace(settings.Model))
        {
            throw new InvalidOperationException("config.json must contain a model name.");
        }
        if (string.IsNullOrWhiteSpace(settings.ResolveApiKey()))
        {
            throw new InvalidOperationException(
                "No API key is configured. Set REVIT_COPILOT_API_KEY (preferred), OPENAI_API_KEY, or api_key in config.json.");
        }
    }

    private void ResetConversationIfConfigurationChanged(CopilotSettings settings)
    {
        string key = settings.Endpoint.TrimEnd('/') + "|" + settings.Model;
        if (_conversationConfiguration is not null
            && !string.Equals(_conversationConfiguration, key, StringComparison.Ordinal))
        {
            _previousResponseId = null;
        }
        _conversationConfiguration = key;
    }

    private async Task SaveSessionSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _sessionLogger.SaveAsync(_history, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // Chat execution should remain usable if logging is temporarily unavailable.
        }
        catch (UnauthorizedAccessException)
        {
            // Chat execution should remain usable if the session folder is locked down.
        }
    }

    private static Task ReportAsync(
        Func<string, string?, Task>? progress,
        string status,
        string? detail)
    {
        return progress?.Invoke(status, detail) ?? Task.CompletedTask;
    }
}
