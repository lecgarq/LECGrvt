namespace LECG.RevitCopilot.Llm;

internal interface IAgentClient : IDisposable
{
    Task<string> RunAgentAsync(
        string userPrompt,
        Func<string, string?, Task>? progress = null,
        CancellationToken cancellationToken = default);
}
