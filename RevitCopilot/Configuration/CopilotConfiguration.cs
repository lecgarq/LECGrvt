using System.IO;
using System.Text.Json;

namespace LECG.RevitCopilot.Configuration;

internal sealed class CopilotConfiguration
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    internal void Initialize()
    {
        Directory.CreateDirectory(CopilotPaths.Root);
        Directory.CreateDirectory(CopilotPaths.Sessions);

        if (!File.Exists(CopilotPaths.Prompt))
        {
            File.WriteAllText(CopilotPaths.Prompt, DefaultPrompt);
        }

        if (!File.Exists(CopilotPaths.Settings))
        {
            File.WriteAllText(
                CopilotPaths.Settings,
                JsonSerializer.Serialize(new CopilotSettings(), JsonOptions));
        }
    }

    internal string ReadSystemPrompt()
    {
        Initialize();
        string prompt = File.ReadAllText(CopilotPaths.Prompt).Trim();
        return string.IsNullOrWhiteSpace(prompt) ? DefaultPrompt : prompt;
    }

    internal CopilotSettings ReadSettings()
    {
        Initialize();
        try
        {
            CopilotSettings? settings = JsonSerializer.Deserialize<CopilotSettings>(
                File.ReadAllText(CopilotPaths.Settings),
                JsonOptions);
            return settings ?? new CopilotSettings();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Invalid JSON in {CopilotPaths.Settings}: {ex.Message}", ex);
        }
    }

    private const string DefaultPrompt = """
        You are an Autodesk Revit 2026.5 BIM copilot operating inside the active model.
        Use the supplied tools for model facts; never invent element IDs, parameters, or project data.
        Explain intended destructive changes clearly. Only call mutation tools when the user explicitly asks
        to modify or delete model data. Report tool errors accurately and give an actionable next step.
        Keep answers compact, practical, and oriented to BIM production outcomes.
        """;
}
