using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using LECG.RevitCopilot.Models;

namespace LECG.RevitCopilot.SmokeTests;

// No model or authentication is involved: this talks JSON-RPC directly to our test server.
internal static class BenchmarkMcpSession
{
    internal static async Task<object> RunAsync(ProjectContext project)
    {
        string server = Path.Combine(Path.GetDirectoryName(typeof(RevitCopilotApp).Assembly.Location)!, "mcp", "RevitCopilot.McpServer.dll");
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false), WorkingDirectory = Path.GetDirectoryName(server)! };
        start.ArgumentList.Add(server);
        start.Environment["LECG_REVIT_PIPE_NAME"] = $"LECG.RevitCopilot.2026.{Environment.ProcessId}";
        start.Environment["LECG_REVIT_PROJECT_KEY"] = project.Key;
        start.Environment["LECG_REVIT_RUNTIME_KEY"] = project.RuntimeKey;
        var cold = Stopwatch.StartNew();
        using var process = Process.Start(start) ?? throw new IOException("Cannot start test MCP server.");
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        int id = 0;
        async Task<(JsonElement Result, int Bytes)> Request(string method, object parameters)
        {
            int expected = ++id;
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { jsonrpc = "2.0", id = expected, method, @params = parameters }));
            await process.StandardInput.FlushAsync();
            for (int i = 0; i < 32; i++)
            {
                string line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)) ?? throw new IOException("MCP output closed.");
                using var json = JsonDocument.Parse(line);
                if (!json.RootElement.TryGetProperty("id", out var responseId) || !responseId.TryGetInt32(out int actual) || actual != expected) continue;
                if (json.RootElement.TryGetProperty("error", out var error)) throw new IOException(error.ToString());
                return (json.RootElement.GetProperty("result").Clone(), Encoding.UTF8.GetByteCount(line));
            }
            throw new IOException("Unmatched MCP response.");
        }
        try
        {
            await Request("initialize", new { protocolVersion = "2025-03-26", capabilities = new { }, clientInfo = new { name = "sample-benchmark", version = "1" } });
            double startupMs = cold.Elapsed.TotalMilliseconds;
            await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
            async Task<JsonElement> SharedRead(string operation, object arguments)
            {
                var response = await Request("tools/call", new { name = "agent_read", arguments = new { operation, arguments_json = JsonSerializer.Serialize(arguments) } });
                string text = response.Result.GetProperty("content")[0].GetProperty("text").GetString()!;
                using var parsed = JsonDocument.Parse(text);
                if (!parsed.RootElement.GetProperty("success").GetBoolean()) throw new IOException(text);
                return parsed.RootElement.GetProperty("data").GetProperty("result").Clone();
            }
            var knowledge = await SharedRead("knowledge_search", new { query = "HostObject.FindInserts", limit = 1 });
            if (knowledge.GetProperty("total_references").GetInt32() != 3000) throw new IOException("Incomplete MCP knowledge pack.");
            var detail = await SharedRead("knowledge_get", new { reference_id = knowledge.GetProperty("items")[0].GetProperty("id").GetString(), limit = 300 });
            if (detail.GetProperty("operation").GetString() != "host_inserts") throw new IOException("MCP reference does not map to native adapter.");
            await SharedRead("phases_list", new { limit = 2 });
            List<object> samples = [];
            object[] steps = [new { id = "levels", operation = "levels_list", arguments = new { limit = 2 } },
                new { id = "materials", operation = "materials_list", arguments = new { limit = 2 } },
                new { id = "views", operation = "views_list", arguments = new { limit = 2 } }];
            // Compare the same three reads separately versus one batch, alternating order.
            for (int repeat = 0; repeat < 11; repeat++)
            foreach (string mode in repeat % 2 == 0 ? new[] { "separate", "batch" } : new[] { "batch", "separate" })
            {
                var elapsed = Stopwatch.StartNew();
                int bytes = 0;
                double execution = 0, queue = 0;
                async Task Check(string name, object arguments)
                {
                    var response = await Request("tools/call", new { name, arguments });
                    bytes += response.Bytes;
                    if (response.Result.TryGetProperty("isError", out var isError) && isError.GetBoolean()) throw new IOException(response.Result.ToString());
                    string value = response.Result.GetProperty("content").EnumerateArray().First(c => c.GetProperty("type").GetString() == "text").GetProperty("text").GetString()!;
                    using var parsed = JsonDocument.Parse(value);
                    var root = parsed.RootElement;
                    if (!root.GetProperty("success").GetBoolean()) throw new IOException(value);
                    if (name == "agent_read_batch" && root.GetProperty("data").GetProperty("completed_count").GetInt32() != 3) throw new IOException("Incomplete read batch.");
                    if (root.TryGetProperty("execution_ms", out var exec)) execution += exec.GetDouble();
                    if (root.TryGetProperty("queue_wait_ms", out var wait)) queue += wait.GetDouble();
                }
                if (mode == "batch") await Check("agent_read_batch", new { steps_json = JsonSerializer.Serialize(steps) });
                else foreach (string operation in new[] { "levels_list", "materials_list", "views_list" })
                    await Check("agent_read", new { operation, arguments_json = "{\"limit\":2}" });
                samples.Add(new { mode, warmup = repeat == 0, round_trip_ms = elapsed.Elapsed.TotalMilliseconds,
                    response_utf8_bytes = bytes, execution_ms = execution, queue_wait_ms = queue });
            }
            return new { status = "passed", startup_ms = startupMs, samples, shared_knowledge_checks = "search, get, native phases through actual MCP", additional_tool_calls = 3, live_ai_calls = 0 };
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true); // Only the child owned by this method.
            await process.WaitForExitAsync();
            await stderr;
        }
    }
}
