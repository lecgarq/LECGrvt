using System.IO;

namespace LECG.RevitCopilot.Configuration;

internal static class CopilotPaths
{
    internal static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RevitCopilot");

    internal static readonly string Prompt = Path.Combine(Root, "system_prompt.txt");
    internal static readonly string Settings = Path.Combine(Root, "config.json");
    internal static readonly string Sessions = Path.Combine(Root, "sessions");
}
