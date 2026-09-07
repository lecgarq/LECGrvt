namespace LECG.RevitCopilot.Models;

internal sealed record ToolCall(string CallId, string Name, string ArgumentsJson);

internal sealed record LlmTurn(string ResponseId, string Text, IReadOnlyList<ToolCall> ToolCalls);
