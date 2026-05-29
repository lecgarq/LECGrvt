using System;
using Autodesk.Revit.DB;
using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface ILineStyleCompactionService
    {
        LineStyleCompactionResult Compact(Document doc, CompactingStylesContext? context, IProgressReporter reporter);
    }
}
