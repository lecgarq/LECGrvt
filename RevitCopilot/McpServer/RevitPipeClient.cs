using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace LECG.RevitCopilot.McpServer;

internal static class RevitPipeClient
{
    private const string PipeName = "LECG.RevitCopilot.2026";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal static async Task<string> CallAsync(
        string tool,
        object arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            return await CallCoreAsync(tool, arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, JsonOptions);
        }
    }

    private static async Task<string> CallCoreAsync(
        string tool,
        object arguments,
        CancellationToken cancellationToken)
    {
        await using NamedPipeClientStream pipe = new(
            ".",
            Environment.GetEnvironmentVariable("LECG_REVIT_PIPE_NAME") ?? PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous,
            System.Security.Principal.TokenImpersonationLevel.Identification);

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                "Revit 2026 is not connected. Start Revit and confirm the LECG Revit Copilot add-in loaded.");
        }

        using StreamReader reader = new(pipe, new UTF8Encoding(false), leaveOpen: true);
        using StreamWriter writer = new(pipe, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true,
        };

        string id = Guid.NewGuid().ToString("N");
        await writer.WriteLineAsync(
            JsonSerializer.Serialize(new { id, tool, arguments,
                expectedProjectKey = Environment.GetEnvironmentVariable("LECG_REVIT_PROJECT_KEY"),
                expectedRuntimeKey = Environment.GetEnvironmentVariable("LECG_REVIT_RUNTIME_KEY") }, JsonOptions).AsMemory(),
            cancellationToken).ConfigureAwait(false);

        string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (line is null)
        {
            throw new IOException("Revit closed the MCP connection before returning a result.");
        }

        BridgeResponse response = JsonSerializer.Deserialize<BridgeResponse>(line, JsonOptions)
            ?? throw new InvalidDataException("Revit returned an empty MCP response.");
        if (!string.Equals(response.Id, id, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Revit returned an MCP response with an unexpected request ID.");
        }

        if (!string.IsNullOrWhiteSpace(response.Error))
        {
            throw new InvalidOperationException(response.Error);
        }

        return response.Result ?? throw new InvalidDataException("Revit returned no tool result.");
    }

    private sealed record BridgeResponse(string? Id, string? Result, string? Error);
}
