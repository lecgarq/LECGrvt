using System;
using Autodesk.Revit.DB;
using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface ITextStyleCompactionService
    {
        TextStyleCompactionResult Compact(Document doc, CompactingStylesContext? context, IProgressReporter reporter);
    }
}
