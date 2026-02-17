using System;

namespace LECG.Services.Interfaces
{
    public interface IPurgeSummaryService
    {
        void Report(Action<string> logCallback, Action<double, string> progressCallback, int lineStylesDeleted, int fillPatternsDeleted, int materialsDeleted, int levelsDeleted);
    }
}
