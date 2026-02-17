using System;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgePassMessagingService : IPurgePassMessagingService
    {
        public void LogPassStart(Action<string> logCallback, int passIndex)
        {
            logCallback?.Invoke($"--- PASS {passIndex}/3 ---");
        }

        public void LogCategoryCheck(Action<string> logCallback, Action<double, string> progressCallback, int passIndex, string categoryLabel, double progressValue)
        {
            logCallback?.Invoke($"Checking {categoryLabel}...");
            progressCallback?.Invoke(progressValue, $"Pass {passIndex}: Purging {categoryLabel.ToLower()}...");
        }
    }
}
