using System;
using Autodesk.Revit.DB;
using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface ILinePatternCompactionService
    {
        LinePatternCompactionResult Compact(Document doc, CompactingStylesContext? context, IProgressReporter reporter);
    }
}
