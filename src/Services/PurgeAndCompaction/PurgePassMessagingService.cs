using System;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgePassMessagingService : IPurgePassMessagingService
    {
        public void LogPassStart(IProgressReporter reporter, int passIndex, int totalPasses = 3)
        {
            ArgumentNullException.ThrowIfNull(reporter);
            reporter.Log($"--- PASS {passIndex}/{totalPasses} ---");
        }

        public void LogCategoryCheck(IProgressReporter reporter, int passIndex, string categoryLabel, double progressValue)
        {
            ArgumentNullException.ThrowIfNull(reporter);
            ArgumentNullException.ThrowIfNull(categoryLabel);

            reporter.Log($"Checking {categoryLabel}...");
            reporter.Report($"Pass {passIndex}: Purging {categoryLabel.ToLower()}...", progressValue);
        }

        public void LogPassStart(Action<string> logCallback, int passIndex, int totalPasses = 3)
        {
            logCallback?.Invoke($"--- PASS {passIndex}/{totalPasses} ---");
        }

        public void LogCategoryCheck(Action<string> logCallback, Action<double, string> progressCallback, int passIndex, string categoryLabel, double progressValue)
        {
            ArgumentNullException.ThrowIfNull(categoryLabel);

            logCallback?.Invoke($"Checking {categoryLabel}...");
            progressCallback?.Invoke(progressValue, $"Pass {passIndex}: Purging {categoryLabel.ToLower()}...");
        }
    }
}
