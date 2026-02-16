using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IPurgeFillPatternService
    {
        int PurgeUnusedFillPatterns(Document doc, Action<string>? logCallback = null);
    }
}
