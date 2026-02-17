using System;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgeSummaryService : IPurgeSummaryService
    {
        public void Report(Action<string> logCallback, Action<double, string> progressCallback, int lineStylesDeleted, int fillPatternsDeleted, int materialsDeleted, int levelsDeleted)
        {
            progressCallback?.Invoke(100, "Complete!");
            logCallback?.Invoke("");
            logCallback?.Invoke("=== SUMMARY ===");
            logCallback?.Invoke($"Line Styles deleted: {lineStylesDeleted}");
            logCallback?.Invoke($"Fill Patterns deleted: {fillPatternsDeleted}");
            logCallback?.Invoke($"Materials deleted: {materialsDeleted}");
            logCallback?.Invoke($"Levels deleted: {levelsDeleted}");
            logCallback?.Invoke("");

            int total = lineStylesDeleted + fillPatternsDeleted + materialsDeleted + levelsDeleted;
            logCallback?.Invoke($"✓ Total items purged: {total}");
        }
    }
}
