using System.Text.Json.Serialization;

namespace LECG.RevitCopilot.Models;

internal sealed record ChatEntry(
    [property: JsonPropertyName("timestamp_utc")] DateTimeOffset TimestampUtc,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("tool_name")] string? ToolName = null);
