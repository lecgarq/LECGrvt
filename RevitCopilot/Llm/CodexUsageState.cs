using System.Text.Json;

namespace LECG.RevitCopilot.Llm;

internal sealed record CodexUsageSnapshot(string QuotaText, string TokenText, DateTimeOffset? UpdatedAt);

internal sealed class CodexUsageState
{
    private readonly object _gate = new();
    private readonly Dictionary<string, JsonElement> _buckets = [];
    private string _tokens = "Turn tokens: not reported yet";
    private DateTimeOffset? _updatedAt;

    internal CodexUsageSnapshot ApplyLimits(JsonElement payload, bool fullSnapshot)
    {
        lock (_gate)
        {
            if (fullSnapshot) _buckets.Clear();
            if (payload.TryGetProperty("rateLimitsByLimitId", out var map) && map.ValueKind == JsonValueKind.Object && map.EnumerateObject().Any())
            {
                foreach (var entry in map.EnumerateObject())
                    if (entry.Value.ValueKind == JsonValueKind.Object) _buckets[entry.Name] = entry.Value.Clone();
            }
            else if (payload.TryGetProperty("rateLimits", out var bucket) && bucket.ValueKind == JsonValueKind.Object)
            {
                string id = Text(bucket, "limitId") ?? "codex";
                _buckets[id] = bucket.Clone();
            }
            _updatedAt = DateTimeOffset.Now;
            return Snapshot();
        }
    }

    internal CodexUsageSnapshot ApplyTokens(JsonElement payload)
    {
        lock (_gate)
        {
            if (payload.TryGetProperty("tokenUsage", out var usage) && usage.ValueKind == JsonValueKind.Object)
            {
                string last = Count(usage, "last", "totalTokens");
                string total = Count(usage, "total", "totalTokens");
                string cached = Count(usage, "last", "cachedInputTokens");
                _tokens = $"Last turn: {last} tokens · thread: {total}\nCached input in last turn: {cached}";
            }
            return Snapshot();
        }
    }

    internal CodexUsageSnapshot Reset()
    {
        lock (_gate)
        {
            _buckets.Clear();
            _tokens = "Turn tokens: not reported yet";
            _updatedAt = null;
            return Snapshot();
        }
    }

    internal CodexUsageSnapshot ResetThread()
    {
        lock (_gate)
        {
            _tokens = "Thread tokens: not reported yet";
            return Snapshot();
        }
    }

    private CodexUsageSnapshot Snapshot()
    {
        var lines = _buckets.OrderBy(p => p.Key == "codex" ? 0 : 1).Select(p =>
        {
            string label = Text(p.Value, "limitName") ?? (p.Key == "codex" ? "Codex" : p.Key);
            var windows = new[] { Window(p.Value, "primary"), Window(p.Value, "secondary") }.Where(w => w is not null);
            string detail = string.Join("\n", windows.Select(w => $"{label} · {w}"));
            return string.IsNullOrEmpty(detail) ? $"{label}: allowance unavailable" : detail;
        });
        string quota = string.Join("\n", lines);
        return new(string.IsNullOrEmpty(quota) ? "Account allowance unavailable" : quota, _tokens, _updatedAt);
    }

    private static string? Window(JsonElement bucket, string name)
    {
        if (!bucket.TryGetProperty(name, out var window) || window.ValueKind != JsonValueKind.Object) return null;
        string duration = "window";
        if (window.TryGetProperty("windowDurationMins", out var minutes) && minutes.ValueKind == JsonValueKind.Number && minutes.TryGetInt64(out long n) && n > 0)
            duration = n % 1440 == 0 ? $"{n / 1440}d" : n % 60 == 0 ? $"{n / 60}h" : $"{n}m";
        string remaining = window.TryGetProperty("usedPercent", out var used) && used.ValueKind == JsonValueKind.Number && used.TryGetDouble(out double value) && double.IsFinite(value)
            ? $"{Math.Clamp(100 - value, 0, 100):0.#}% left" : "remaining unavailable";
        string reset = "reset unavailable";
        if (window.TryGetProperty("resetsAt", out var timestamp) && timestamp.ValueKind == JsonValueKind.Number && timestamp.TryGetInt64(out long seconds))
        {
            try { reset = $"resets {DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime():MMM d, HH:mm zzz}"; }
            catch (ArgumentOutOfRangeException) { }
        }
        return $"{duration}: {remaining} · {reset}";
    }

    private static string Count(JsonElement usage, string group, string name) =>
        usage.TryGetProperty(group, out var part) && part.ValueKind == JsonValueKind.Object &&
        part.TryGetProperty(name, out var count) && count.ValueKind == JsonValueKind.Number && count.TryGetInt64(out long n) && n >= 0 ? n.ToString("N0") : "unavailable";

    private static string? Text(JsonElement value, string name) =>
        value.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.String ? field.GetString() : null;
}
