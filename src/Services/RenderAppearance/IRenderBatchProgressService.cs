namespace LECG.Services.Interfaces
{
    public interface IRenderBatchProgressService
    {
        bool ShouldReport(int processedCount);
        double ToPercent(int processedCount, int totalCount);
    }
}
