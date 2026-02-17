using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IPurgeExecutionCoordinatorService
    {
        (int lineStylesDeleted, int fillPatternsDeleted, int materialsDeleted, int levelsDeleted) Execute(
            Document doc,
            bool lineStyles,
            bool fillPatterns,
            bool materials,
            bool levels,
            Action<string> logCallback,
            Action<double, string> progressCallback);
    }
}
