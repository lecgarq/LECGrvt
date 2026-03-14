using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IPurgeService
    {
        void PurgeAll(Document doc, int passCount, bool lineStyles, bool linePatterns, bool fillPatterns, bool materials, bool levels, bool parameters, IProgressReporter reporter);
        void PurgeAll(Document doc, int passCount, bool lineStyles, bool linePatterns, bool fillPatterns, bool materials, bool levels, bool parameters, Action<string> logCallback, Action<double, string> progressCallback);
        int PurgeUnusedLineStyles(Document doc, Action<string>? logCallback = null);
        int PurgeUnusedLinePatterns(Document doc, Action<string>? logCallback = null);
        int PurgeUnusedFillPatterns(Document doc, Action<string>? logCallback = null);
        int PurgeUnusedMaterials(Document doc, Action<string>? logCallback = null);
        int PurgeUnusedLevels(Document doc, Action<string>? logCallback = null);
        int PurgeUnusedParameters(Document doc, Action<string>? logCallback = null);
    }
}
