using System;
using Autodesk.Revit.DB;
using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface ITextStyleCompactionService
    {
        TextStyleCompactionResult Compact(Document doc, CompactingStylesContext? context, IProgressReporter reporter);
        TextStyleCompactionResult Compact(Document doc, CompactingStylesContext? context = null, Action<string>? logCallback = null, Action<double, string>? progressCallback = null);
    }
}
