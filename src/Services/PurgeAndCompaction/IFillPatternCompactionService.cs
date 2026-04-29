using System;
using Autodesk.Revit.DB;
using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface IFillPatternCompactionService
    {
        FillPatternCompactionResult Compact(Document doc, CompactingStylesContext? context, IProgressReporter reporter);
        FillPatternCompactionResult Compact(Document doc, CompactingStylesContext? context = null, Action<string>? logCallback = null, Action<double, string>? progressCallback = null);
    }
}
