using System;

namespace LECG.Services.Interfaces
{
    public interface IPurgePassMessagingService
    {
        void LogPassStart(Action<string> logCallback, int passIndex);
        void LogCategoryCheck(Action<string> logCallback, Action<double, string> progressCallback, int passIndex, string categoryLabel, double progressValue);
    }
}
