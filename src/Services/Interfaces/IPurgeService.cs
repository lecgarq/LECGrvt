using System;
using Autodesk.Revit.DB;

namespace LECG.Services
{
    public interface IPurgeService
    {
        void PurgeAll(Document doc, bool lineStyles, bool fillPatterns, bool materials, bool levels, Action<string> logCallback, Action<double, string> progressCallback);
        int PurgeUnusedLineStyles(Document doc, Action<string>? logCallback = null);
        int PurgeUnusedFillPatterns(Document doc, Action<string>? logCallback = null);
        int PurgeUnusedMaterials(Document doc, Action<string>? logCallback = null);
        int PurgeUnusedLevels(Document doc, Action<string>? logCallback = null);
    }
}
