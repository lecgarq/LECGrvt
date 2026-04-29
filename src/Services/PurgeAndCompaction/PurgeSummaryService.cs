using System;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgeSummaryService : IPurgeSummaryService
    {
        public void Report(
            IProgressReporter reporter,
            int lineStylesDeleted,
            int linePatternsDeleted,
            int fillPatternsDeleted,
            int materialsDeleted,
            int levelsDeleted,
            int parametersDeleted,
            int groupsDeleted,
            int gridTypesDeleted,
            int levelTypesDeleted,
            int constraintsDeleted,
            int unplacedRoomsDeleted,
            int viewTemplatesDeleted,
            int viewFiltersDeleted)
        {
            ArgumentNullException.ThrowIfNull(reporter);
            reporter.Report("Complete!", 100);
            reporter.Log("");
            reporter.Log("=== SUMMARY ===");
            LogIfActive(reporter, "Line Styles", lineStylesDeleted);
            LogIfActive(reporter, "Line Patterns", linePatternsDeleted);
            LogIfActive(reporter, "Fill Patterns", fillPatternsDeleted);
            LogIfActive(reporter, "Materials", materialsDeleted);
            LogIfActive(reporter, "Levels", levelsDeleted);
            LogIfActive(reporter, "Groups", groupsDeleted);
            LogIfActive(reporter, "Grid Types", gridTypesDeleted);
            LogIfActive(reporter, "Level Types", levelTypesDeleted);
            LogIfActive(reporter, "Constraints", constraintsDeleted);
            LogIfActive(reporter, "Unplaced Rooms", unplacedRoomsDeleted);
            LogIfActive(reporter, "View Templates", viewTemplatesDeleted);
            LogIfActive(reporter, "View Filters", viewFiltersDeleted);
            LogIfActive(reporter, "Family Parameters", parametersDeleted);
            reporter.Log("");

            int total = lineStylesDeleted + linePatternsDeleted + fillPatternsDeleted + materialsDeleted + levelsDeleted + parametersDeleted + groupsDeleted + gridTypesDeleted + levelTypesDeleted + constraintsDeleted + unplacedRoomsDeleted + viewTemplatesDeleted + viewFiltersDeleted;
            reporter.Log($"Total items purged: {total}");
        }

        public void Report(
            Action<string> logCallback,
            Action<double, string> progressCallback,
            int lineStylesDeleted,
            int linePatternsDeleted,
            int fillPatternsDeleted,
            int materialsDeleted,
            int levelsDeleted,
            int parametersDeleted,
            int groupsDeleted,
            int gridTypesDeleted,
            int levelTypesDeleted,
            int constraintsDeleted,
            int unplacedRoomsDeleted,
            int viewTemplatesDeleted,
            int viewFiltersDeleted)
        {
            Report(new LegacyProgressReporter(progressCallback, logCallback), lineStylesDeleted, linePatternsDeleted, fillPatternsDeleted, materialsDeleted, levelsDeleted, parametersDeleted, groupsDeleted, gridTypesDeleted, levelTypesDeleted, constraintsDeleted, unplacedRoomsDeleted, viewTemplatesDeleted, viewFiltersDeleted);
        }

        private static void LogIfActive(IProgressReporter reporter, string category, int count)
        {
            if (count > 0)
                reporter.Log($"{category} deleted: {count}");
        }
    }
}
