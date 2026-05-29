using System;
using LECG.Core.Purge;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgeSummaryService : IPurgeSummaryService
    {
        public void Report(IProgressReporter reporter, PurgeResult result)
        {
            ArgumentNullException.ThrowIfNull(reporter);
            ArgumentNullException.ThrowIfNull(result);

            reporter.Report("Complete!", 100);
            reporter.Log("");
            reporter.Log("=== SUMMARY ===");
            LogIfActive(reporter, "Line Styles", result.LineStyles);
            LogIfActive(reporter, "Line Patterns", result.LinePatterns);
            LogIfActive(reporter, "Fill Patterns", result.FillPatterns);
            LogIfActive(reporter, "Materials", result.Materials);
            LogIfActive(reporter, "Levels", result.Levels);
            LogIfActive(reporter, "Groups", result.Groups);
            LogIfActive(reporter, "Grid Types", result.GridTypes);
            LogIfActive(reporter, "Level Types", result.LevelTypes);
            LogIfActive(reporter, "Constraints", result.Constraints);
            LogIfActive(reporter, "Unplaced Rooms", result.UnplacedRooms);
            LogIfActive(reporter, "View Templates", result.ViewTemplates);
            LogIfActive(reporter, "View Filters", result.ViewFilters);
            LogIfActive(reporter, "Family Parameters", result.Parameters);
            reporter.Log("");

            reporter.Log($"Total items purged: {result.Total}");
        }

        private static void LogIfActive(IProgressReporter reporter, string category, int count)
        {
            if (count > 0)
                reporter.Log($"{category} deleted: {count}");
        }
    }
}
