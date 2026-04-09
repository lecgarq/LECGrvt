using LECG.Batch.Models;

namespace LECG.Batch.Services.Interfaces
{
    public interface IBatchReportService
    {
        void WriteReport(BatchManifest manifest);
    }
}
