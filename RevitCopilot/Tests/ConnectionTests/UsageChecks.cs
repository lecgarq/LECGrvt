using System.Text.Json;
using LECG.RevitCopilot.Configuration;
using LECG.RevitCopilot.Llm;

internal static class UsageChecks
{
    internal static async Task RunAsync()
    {
        var state = new CodexUsageState();
        using var initial = JsonDocument.Parse("""
            {"rateLimits":{"primary":{"usedPercent":99}},"rateLimitsByLimitId":{
              "codex":{"primary":{"usedPercent":13,"windowDurationMins":10080,"resetsAt":1789173839},"secondary":null},
              "spark":{"limitName":"Spark","primary":{"usedPercent":0,"windowDurationMins":300}}}}
            """);
        var snapshot = state.ApplyLimits(initial.RootElement, true);
        Require(snapshot.QuotaText.Contains("87% left") && snapshot.QuotaText.Contains("Spark") && !snapshot.QuotaText.Contains("1% left"), "Multi-bucket data must override legacy limits.");
        using var update = JsonDocument.Parse("""{"rateLimits":{"limitId":"codex","primary":{"usedPercent":105},"secondary":{"usedPercent":null,"windowDurationMins":null,"resetsAt":null}}}""");
        snapshot = state.ApplyLimits(update.RootElement, false);
        Require(snapshot.QuotaText.Contains("0% left") && snapshot.QuotaText.Contains("remaining unavailable") && snapshot.QuotaText.Contains("Spark"), "Updates must clamp percentages, preserve unrelated buckets, and tolerate nulls.");
        using var tokenUpdate = JsonDocument.Parse("""{"tokenUsage":{"last":{"totalTokens":500,"cachedInputTokens":200},"total":{"totalTokens":900}}}""");
        snapshot = state.ApplyTokens(tokenUpdate.RootElement);
        Require(snapshot.TokenText.Contains("500") && snapshot.TokenText.Contains("900") && snapshot.TokenText.Contains("200"), "Token activity should preserve server totals without counting cached input twice.");
        using var missing = JsonDocument.Parse("""{"rateLimits":null,"rateLimitsByLimitId":null}""");
        Require(state.ApplyLimits(missing.RootElement, true).QuotaText == "Account allowance unavailable", "Missing account data must not imply 100 percent remaining.");
        Require(state.Reset().UpdatedAt is null, "Reconnect must invalidate cached usage.");

        using var client = new CodexAppServerClient(new CopilotConfiguration());
        CodexUsageSnapshot? idleUpdate = null;
        client.UsageUpdated += value => idleUpdate = value;
        using var notification = JsonDocument.Parse("""{"method":"account/rateLimits/updated","params":{"rateLimits":{"limitId":"codex","primary":{"usedPercent":25}}}}""");
        var handler = typeof(CodexAppServerClient).GetMethod("HandleNotificationAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        await (Task)handler.Invoke(client, ["account/rateLimits/updated", notification.RootElement])!;
        Require(idleUpdate?.QuotaText.Contains("75% left") == true, "Quota notifications must update the display even when no AI turn is active.");
        Console.WriteLine("PASS: multiple quota buckets, notification merging, null data, clamping, token totals, reconnect reset and idle quota updates. No inference calls.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
