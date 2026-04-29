using System;

namespace LECG.Services.Interfaces
{
    public interface IPurgePassMessagingService
    {
        void LogPassStart(IProgressReporter reporter, int passIndex, int totalPasses = 3);
        void LogCategoryCheck(IProgressReporter reporter, int passIndex, string categoryLabel, double progressValue);
        void LogPassStart(Action<string> logCallback, int passIndex, int totalPasses = 3);
        void LogCategoryCheck(Action<string> logCallback, Action<double, string> progressCallback, int passIndex, string categoryLabel, double progressValue);
    }
}
