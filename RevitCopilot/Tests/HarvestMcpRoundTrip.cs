using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace LECG.RevitCopilot.SmokeTests;

internal static class HarvestMcpRoundTrip
{
    internal static async Task<string> CallAsync(string tool, string arguments, LECG.RevitCopilot.Models.ProjectContext? project = null)
    {
        string server = Path.Combine(Path.GetDirectoryName(typeof(RevitCopilotApp).Assembly.Location)!, "mcp", "RevitCopilot.McpServer.dll");
        if (!File.Exists(server)) throw new FileNotFoundException("Test output lacks the bundled MCP server.", server);
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true, StandardInputEncoding = new UTF8Encoding(false), WorkingDirectory = Path.GetDirectoryName(server)! };
        start.ArgumentList.Add(server);
        if (project is not null)
        {
            start.Environment["LECG_REVIT_PIPE_NAME"] = $"LECG.RevitCopilot.2026.{Environment.ProcessId}";
            start.Environment["LECG_REVIT_PROJECT_KEY"] = project.Key;
            start.Environment["LECG_REVIT_RUNTIME_KEY"] = project.RuntimeKey;
        }
        using var process = Process.Start(start) ?? throw new IOException("Test MCP server did not start.");
        var errors = process.StandardError.ReadToEndAsync();
        async Task<JsonElement> Response(int id)
        {
            for (int i = 0; i < 32; i++)
            {
                string line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20)) ?? throw new IOException("MCP server closed its output.");
                using var parsed = JsonDocument.Parse(line);
                if (parsed.RootElement.TryGetProperty("id", out var responseId) && responseId.TryGetInt32(out int number) && number == id)
                {
                    if (parsed.RootElement.TryGetProperty("error", out var error)) throw new InvalidOperationException(error.ToString());
                    return parsed.RootElement.GetProperty("result").Clone();
                }
            }
            throw new IOException("Too many unrelated MCP messages.");
        }
        try
        {
            await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"lecg-harvest-smoke","version":"1"}}}""");
            await Response(1);
            await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
            await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"agent_capabilities","arguments":{"query":"huecos muro","limit":1}}}""");
            var discovery = await Response(2);
            if (!discovery.ToString().Contains("host_inserts", StringComparison.Ordinal)) throw new InvalidOperationException("Actual MCP discovery did not find the reviewed native operation.");
            using var args = JsonDocument.Parse(arguments);
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 3, method = "tools/call", @params = new { name = tool, arguments = args.RootElement } }));
            var result = await Response(3);
            if (result.TryGetProperty("isError", out var failed) && failed.GetBoolean()) throw new InvalidOperationException(result.ToString());
            return result.GetProperty("content").EnumerateArray().First(c => c.GetProperty("type").GetString() == "text").GetProperty("text").GetString()!;
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true); // Only this test-owned child server.
            await process.WaitForExitAsync();
            await errors;
        }
    }
}
