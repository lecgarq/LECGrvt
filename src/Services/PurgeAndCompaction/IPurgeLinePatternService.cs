using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IPurgeLinePatternService
    {
        int PurgeUnusedLinePatterns(Document doc, Action<string>? logCallback = null);
        int PurgeUnusedLinePatterns(Document doc, PurgeContext context, Action<string>? logCallback = null);
    }
}
