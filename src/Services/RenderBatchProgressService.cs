using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class RenderBatchProgressService : IRenderBatchProgressService
    {
        public bool ShouldReport(int processedCount)
        {
            return processedCount % 10 == 0;
        }

        public double ToPercent(int processedCount, int totalCount)
        {
            return (double)processedCount / totalCount * 100;
        }
    }
}
