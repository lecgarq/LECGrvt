using System.IO;
using System.Text.Json;
using LECG.RevitCopilot.Configuration;
using LECG.RevitCopilot.Models;

namespace LECG.RevitCopilot.Services;

internal sealed class SessionLogger
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path;

    internal SessionLogger(string? directory = null)
    {
        directory ??= CopilotPaths.Sessions;
        Directory.CreateDirectory(directory);
        _path = Path.Combine(
            directory,
            $"session_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.json");
    }

    internal async Task SaveAsync(IReadOnlyCollection<ChatEntry> entries, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using FileStream stream = new(
                _path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                4096,
                useAsync: true);

            await JsonSerializer.SerializeAsync(
                stream,
                new { session_started_utc = entries.FirstOrDefault()?.TimestampUtc, messages = entries },
                new JsonSerializerOptions { WriteIndented = true },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
