using System.Text.Json.Serialization;

namespace LECG.RevitCopilot.Configuration;

internal sealed class CopilotSettings
{
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = "https://api.openai.com/v1/responses";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "gpt-5.6";

    [JsonPropertyName("api_key")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("request_timeout_seconds")]
    public int RequestTimeoutSeconds { get; set; } = 120;

    [JsonPropertyName("max_agent_iterations")]
    public int MaxAgentIterations { get; set; } = 12;

    public string ResolveApiKey()
    {
        return Environment.GetEnvironmentVariable("REVIT_COPILOT_API_KEY")
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? ApiKey;
    }
}
