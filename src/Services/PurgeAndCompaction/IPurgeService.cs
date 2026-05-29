using System;
using Autodesk.Revit.DB;
using LECG.Core.Purge;

namespace LECG.Services.Interfaces
{
    public interface IPurgeService
    {
        void PurgeAll(Document doc, int passCount, PurgeOptions options, IProgressReporter reporter);
        int PurgeUnusedLineStyles(Document doc, Action<string>? logCallback = null);
        int PurgeUnusedLinePatterns(Document doc, Action<string>? logCallback = null);
        int PurgeUnusedFillPatterns(Document doc, Action<string>? logCallback = null);
        int PurgeUnusedMaterials(Document doc, Action<string>? logCallback = null);
        int PurgeUnusedLevels(Document doc, Action<string>? logCallback = null);
        int PurgeUnusedParameters(Document doc, Action<string>? logCallback = null);
    }
}
