using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IPurgePassExecutionService
    {
        (int lineStylesDeleted, int fillPatternsDeleted, int materialsDeleted, int levelsDeleted) ExecutePass(
            Document doc,
            int passIndex,
            bool lineStyles,
            bool fillPatterns,
            bool materials,
            bool levels,
            Action<string> logCallback,
            Action<double, string> progressCallback);
    }
}
