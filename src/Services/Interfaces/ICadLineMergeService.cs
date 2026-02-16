using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ICadLineMergeService
    {
        List<Line> MergeCollinearLines(List<Line> sourceLines);
    }
}
