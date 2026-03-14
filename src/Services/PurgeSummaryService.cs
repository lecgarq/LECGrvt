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
            int parametersDeleted)
        {
            reporter.Report("Complete!", 100);
            reporter.Log("");
            reporter.Log("=== SUMMARY ===");
            reporter.Log($"Line Styles deleted: {lineStylesDeleted}");
            reporter.Log($"Line Patterns deleted: {linePatternsDeleted}");
            reporter.Log($"Fill Patterns deleted: {fillPatternsDeleted}");
            reporter.Log($"Materials deleted: {materialsDeleted}");
            reporter.Log($"Levels deleted: {levelsDeleted}");
            reporter.Log($"Family Parameters deleted: {parametersDeleted}");
            reporter.Log("");

            int total = lineStylesDeleted + linePatternsDeleted + fillPatternsDeleted + materialsDeleted + levelsDeleted + parametersDeleted;
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
            int parametersDeleted)
        {
            Report(new LegacyProgressReporter(progressCallback, logCallback), lineStylesDeleted, linePatternsDeleted, fillPatternsDeleted, materialsDeleted, levelsDeleted, parametersDeleted);
        }
    }
}
