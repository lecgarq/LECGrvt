using LECG.Core.Purge;

namespace LECG.Services.Interfaces
{
    public interface IPurgeSummaryService
    {
        void Report(IProgressReporter reporter, PurgeResult result);
    }
}
