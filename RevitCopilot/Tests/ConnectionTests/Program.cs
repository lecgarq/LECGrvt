using System.Text.Json;
using System.Diagnostics;
using System.Text;
using LECG.RevitCopilot.Configuration;
using LECG.RevitCopilot.Llm;

if (args.Contains("--setter-inventory"))
{
    SetterInventory.Write(args[Array.IndexOf(args, "--setter-inventory") + 1]);
    return;
}

if (args.Contains("--fake-project-server"))
{
    await ProjectSessionChecks.RunFakeServerAsync(args[Array.IndexOf(args, "--fake-project-server") + 1]);
    return;
}
if (args.Contains("--project-sessions"))
{
    await ProjectSessionChecks.RunAsync();
    return;
}

if (args.Contains("--api-library"))
{
    ApiLibraryChecks.Run();
    return;
}

if (args.Contains("--agent-library"))
{
    AgentLibraryChecks.Run();
    return;
}

if (args.Contains("--usage-only"))
{
    await UsageChecks.RunAsync();
    return;
}

if (args.Contains("--installation-only"))
{
    string temporaryRoot = Path.Combine(Path.GetTempPath(), "LECG-McpInstallationTest-" + Guid.NewGuid().ToString("N"));
    string addin = Path.Combine(temporaryRoot, "addin");
    string legacy = Path.Combine(temporaryRoot, "legacy");
    string environmentRoot = Path.Combine(temporaryRoot, "environment");
    string[] filenames = ["RevitCopilot.McpServer.dll", "RevitCopilot.McpServer.deps.json",
        "RevitCopilot.McpServer.runtimeconfig.json", "ModelContextProtocol.Core.dll"];
    Directory.CreateDirectory(temporaryRoot);
    try
    {
        bool missingRejected = false;
        try { McpServerInstallation.ResolveFrom(addin, legacy, environmentRoot); }
        catch (FileNotFoundException ex) { missingRejected = ex.Message.Contains(addin) && ex.Message.Contains(legacy); }
        Require(missingRejected, "Missing installation must report the actual searched paths.");
        string environmentMcp = Path.Combine(environmentRoot, "RevitCopilot", "mcp");
        Directory.CreateDirectory(environmentMcp);
        foreach (string name in filenames) File.WriteAllText(Path.Combine(environmentMcp, name), "fixture");
        Require(McpServerInstallation.ResolveFrom(addin, legacy, environmentRoot) == Path.Combine(environmentMcp, filenames[0]), "Environment-path fallback failed.");
        string bundledMcp = Path.Combine(addin, "mcp");
        Directory.CreateDirectory(bundledMcp);
        File.WriteAllText(Path.Combine(bundledMcp, filenames[0]), "incomplete fixture");
        Require(McpServerInstallation.ResolveFrom(addin, legacy, environmentRoot) == Path.Combine(environmentMcp, filenames[0]), "Incomplete bundle must not hide a complete fallback.");
        foreach (string name in filenames) File.WriteAllText(Path.Combine(bundledMcp, name), "fixture");
        Require(McpServerInstallation.ResolveFrom(addin, legacy, environmentRoot) == Path.Combine(bundledMcp, filenames[0]), "Bundled installation must take priority.");
    }
    finally { Directory.Delete(temporaryRoot, recursive: true); }

    string productionAssembly = Path.GetFullPath(args[Array.IndexOf(args, "--installation-only") + 1]);
    var assembly = System.Reflection.Assembly.LoadFrom(productionAssembly);
    string serverPath = (string)assembly.GetType("LECG.RevitCopilot.Configuration.McpServerInstallation")!
        .GetMethod("Resolve", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, null)!;
    Require(serverPath == Path.Combine(Path.GetDirectoryName(productionAssembly)!, "mcp", filenames[0]), "Production assembly did not resolve its own bundled server.");
    var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"))
    {
        UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
        RedirectStandardOutput = true, RedirectStandardError = true,
        StandardInputEncoding = new UTF8Encoding(false), WorkingDirectory = Path.GetDirectoryName(serverPath)!,
    };
    info.ArgumentList.Add(serverPath);
    using var serverProcess = Process.Start(info)!;
    var serverErrors = serverProcess.StandardError.ReadToEndAsync();
    try
    {
        await serverProcess.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"lecg-install-test","version":"1"}}}""");
        using var initialized = JsonDocument.Parse(await serverProcess.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)) ?? throw new Exception(await serverErrors));
        Require(initialized.RootElement.TryGetProperty("result", out _), "Bundled server handshake failed.");
        await serverProcess.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
        await serverProcess.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""");
        using var listed = JsonDocument.Parse(await serverProcess.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)) ?? throw new Exception(await serverErrors));
        var names = listed.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToArray();
        Require(names.Length == 16 && names.Contains("api_search") && names.Contains("agent_read_batch") && names.Contains("agent_preview_batch"), "Bundled server did not expose the complete 16-tool interface.");
        await serverProcess.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"api_search","arguments":{"query":"Width","element_type":"Wall","kind":"read","limit":2}}}""");
        using var searched = JsonDocument.Parse(await serverProcess.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)) ?? throw new Exception(await serverErrors));
        Require(searched.RootElement.ToString().Contains("api.get:Autodesk.Revit.DB.Wall.Width"), "Real MCP discovery did not return the bound Wall.Width getter.");
    }
    finally { if (!serverProcess.HasExited) serverProcess.Kill(entireProcessTree: true); }
    Console.WriteLine("PASS: missing/partial installation, environment fallback, bundled priority, production path resolution, initialize handshake, 16 tools and actual API discovery.");
    Console.WriteLine(serverPath);
    return;
}

// Read-only integration checks; no model inference or Revit changes unless --live is supplied.
if (args.Contains("--no-dotnet-path"))
{
    // Reproduce a desktop host that inherited PATH before the system .NET installation.
    string filteredPath = string.Join(Path.PathSeparator,
        (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .Where(path => !File.Exists(Path.Combine(path.Trim('"'), "dotnet.exe"))));
    Environment.SetEnvironmentVariable("PATH", filteredPath);
}
using var fixture = JsonDocument.Parse("""
    {"model":"future-model","displayName":"Future model","defaultReasoningEffort":"ultra",
     "supportedReasoningEfforts":[{"reasoningEffort":"low"},{"reasoningEffort":"ultra"},{"reasoningEffort":"new-effort"}],
     "hidden":true,"isDefault":false}
    """);
var parsed = CodexModelOption.Parse(fixture.RootElement);
Require(parsed.Efforts.SequenceEqual(new[] { "low", "ultra", "new-effort" }), "Server-provided effort names must not be filtered by a hardcoded list.");
Require(parsed.Hidden && parsed.Label.Contains("hidden"), "Hidden catalog entries must be labeled.");
using CodexAppServerClient client = new(new CopilotConfiguration());
using CancellationTokenSource timeout = new(TimeSpan.FromMinutes(3));
var models = await client.GetModelsAsync(timeout.Token);
if (args.Contains("--usage-read"))
{
    var usage = await client.ReadUsageAsync(timeout.Token);
    Console.WriteLine(usage.QuotaText);
    Require(usage.UpdatedAt is not null, "Usage read did not complete.");
    Console.WriteLine("PASS: live account allowance read without inference.");
    return;
}
foreach (var model in models)
{
    foreach (string effort in model.Efforts) client.SelectModel(model.Id, effort);
    Console.WriteLine($"{model.Id}: {string.Join(", ", model.Efforts)}{(model.Hidden ? " [hidden]" : "")}");
}
bool rejected = false;
try { client.SelectModel(models[0].Id, "unsupported-effort"); }
catch (ArgumentException) { rejected = true; }
Require(rejected, "Unsupported reasoning effort must be rejected before a turn.");
await client.VerifyRevitToolsAsync(timeout.Token);
Console.WriteLine("PASS: production client initialized the Revit MCP tools.");
await client.ReconnectAsync(timeout.Token);
await client.VerifyRevitToolsAsync(timeout.Token);
Console.WriteLine("PASS: reconnect initialized a fresh Revit MCP connection.");
if (args.Contains("--live"))
{
    foreach (string id in new[] { "gpt-6-astra", "gpt-5.6-sol" })
    {
        var model = models.Single(m => m.Id == id);
        client.SelectModel(id, model.Efforts.Contains("low") ? "low" : model.DefaultEffort);
        string answer = await client.RunAgentAsync("Call only lecg-revit project_info and report the active project title. Do not modify anything.", cancellationToken: timeout.Token);
        Console.WriteLine($"{id}: {answer}");
    }
}
Console.WriteLine("PASS: catalog parsing, all advertised model/effort pairs, invalid effort rejection, MCP startup and reconnect.");

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
