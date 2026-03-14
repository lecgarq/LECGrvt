using System;

namespace LECG.Services.Interfaces
{
    public interface IPurgeSummaryService
    {
        void Report(IProgressReporter reporter, int lineStylesDeleted, int linePatternsDeleted, int fillPatternsDeleted, int materialsDeleted, int levelsDeleted, int parametersDeleted);
        void Report(Action<string> logCallback, Action<double, string> progressCallback, int lineStylesDeleted, int linePatternsDeleted, int fillPatternsDeleted, int materialsDeleted, int levelsDeleted, int parametersDeleted);
    }
}
