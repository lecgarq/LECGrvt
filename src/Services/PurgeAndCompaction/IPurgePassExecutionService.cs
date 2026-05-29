using Autodesk.Revit.DB;
using LECG.Core.Purge;

namespace LECG.Services.Interfaces
{
    public interface IPurgePassExecutionService
    {
        PurgeResult ExecutePass(Document doc, int passIndex, PurgeOptions options, IProgressReporter reporter);
    }
}
