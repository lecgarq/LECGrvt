using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IPurgePassExecutionService
    {
        (int lineStylesDeleted, int linePatternsDeleted, int fillPatternsDeleted, int materialsDeleted, int levelsDeleted) ExecutePass(
            Document doc,
            int passIndex,
            bool lineStyles,
            bool linePatterns,
            bool fillPatterns,
            bool materials,
            bool levels,
            IProgressReporter reporter);

        (int lineStylesDeleted, int linePatternsDeleted, int fillPatternsDeleted, int materialsDeleted, int levelsDeleted) ExecutePass(
            Document doc,
            int passIndex,
            bool lineStyles,
            bool linePatterns,
            bool fillPatterns,
            bool materials,
            bool levels,
            Action<string> logCallback,
            Action<double, string> progressCallback);
    }
}
