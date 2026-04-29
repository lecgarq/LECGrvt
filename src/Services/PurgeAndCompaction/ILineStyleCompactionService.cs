using System;
using Autodesk.Revit.DB;
using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface ILineStyleCompactionService
    {
        LineStyleCompactionResult Compact(Document doc, CompactingStylesContext? context, IProgressReporter reporter);
        LineStyleCompactionResult Compact(Document doc, CompactingStylesContext? context = null, Action<string>? logCallback = null, Action<double, string>? progressCallback = null);
    }
}
