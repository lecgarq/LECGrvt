using Autodesk.Revit.DB;
using LECG.Core.Purge;

namespace LECG.Services.Interfaces
{
    public interface IPurgeExecutionCoordinatorService
    {
        PurgeResult Execute(Document doc, int passCount, PurgeOptions options, IProgressReporter reporter);
    }
}
