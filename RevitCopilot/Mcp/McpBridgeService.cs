using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using LECG.RevitCopilot.Revit;

namespace LECG.RevitCopilot.Mcp;

internal sealed class McpBridgeService : IDisposable
{
    internal const string PipeName = "LECG.RevitCopilot.2026";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly ExternalEventDispatcher _dispatcher;
    private Task? _serverTask;
    private Task? _panelServerTask;
    private bool _disposed;

    internal McpBridgeService(ExternalEventDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    internal void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _serverTask ??= RunServerAsync(PipeName, _shutdown.Token);
        _panelServerTask ??= RunServerAsync($"{PipeName}.{Environment.ProcessId}", _shutdown.Token);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _shutdown.Cancel();
        _shutdown.Dispose();
    }

    private async Task RunServerAsync(string pipeName, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using NamedPipeServerStream pipe = new(
                    pipeName,
                    PipeDirection.InOut,
                    4,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                await HandleConnectionAsync(pipe, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleConnectionAsync(Stream stream, CancellationToken cancellationToken)
    {
        using StreamReader reader = new(stream, new UTF8Encoding(false), leaveOpen: true);
        using StreamWriter writer = new(stream, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true,
        };

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null) return;

            BridgeResponse response;
            string? requestId = null;
            try
            {
                BridgeRequest request = JsonSerializer.Deserialize<BridgeRequest>(line, JsonOptions)
                    ?? throw new InvalidDataException("The MCP bridge request is empty.");
                requestId = request.Id;
                if (string.IsNullOrWhiteSpace(request.Tool))
                {
                    throw new InvalidDataException("The MCP bridge request has no tool name.");
                }

                string arguments = request.Arguments.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                    ? "{}"
                    : request.Arguments.GetRawText();
                string result = await _dispatcher.ExecuteToolAsync(
                    request.Tool,
                    arguments,
                    cancellationToken, request.ExpectedProjectKey, request.ExpectedRuntimeKey).ConfigureAwait(false);
                response = new BridgeResponse(request.Id, result, null);
            }
            catch (Exception ex)
            {
                response = new BridgeResponse(requestId, null, ex.Message);
            }

            await writer.WriteLineAsync(
                JsonSerializer.Serialize(response, JsonOptions).AsMemory(),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record BridgeRequest(string? Id, string? Tool, JsonElement Arguments, string? ExpectedProjectKey = null, string? ExpectedRuntimeKey = null);
    private sealed record BridgeResponse(string? Id, string? Result, string? Error);
}
