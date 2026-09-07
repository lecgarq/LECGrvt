using System.IO;

namespace LECG.RevitCopilot.Configuration;

internal static class McpServerInstallation
{
    private static readonly string[] RequiredFiles =
    [
        "RevitCopilot.McpServer.dll",
        "RevitCopilot.McpServer.deps.json",
        "RevitCopilot.McpServer.runtimeconfig.json",
        "ModelContextProtocol.Core.dll",
    ];

    internal static string Resolve() => ResolveFrom(
        Path.GetDirectoryName(typeof(McpServerInstallation).Assembly.Location)!,
        CopilotPaths.Root,
        Environment.GetEnvironmentVariable("LOCALAPPDATA"));

    internal static string ResolveFrom(string addinDirectory, string legacyRoot, string? localAppData)
    {
        List<string> candidates = [Path.Combine(addinDirectory, "mcp"), Path.Combine(legacyRoot, "mcp")];
        if (!string.IsNullOrWhiteSpace(localAppData) && Path.IsPathFullyQualified(localAppData))
            candidates.Add(Path.Combine(localAppData, "RevitCopilot", "mcp"));

        List<string> failures = [];
        foreach (string directory in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string[] missing = RequiredFiles.Where(name => !File.Exists(Path.Combine(directory, name))).ToArray();
            if (missing.Length == 0) return Path.Combine(directory, RequiredFiles[0]);
            failures.Add($"{directory}: missing or inaccessible {string.Join(", ", missing)}");
        }
        throw new FileNotFoundException(
            "The Revit MCP server installation is incomplete or inaccessible. Repair LECG Copilot, then restart Revit.\n\nChecked:\n" +
            string.Join("\n", failures));
    }
}
