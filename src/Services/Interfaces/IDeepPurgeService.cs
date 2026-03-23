using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IDeepPurgeService
    {
        void Purge(Document projectDoc, int passCount, IProgressReporter reporter);
    }
}
