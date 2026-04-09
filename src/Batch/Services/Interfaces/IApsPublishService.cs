using LECG.Batch.Models;

namespace LECG.Batch.Services.Interfaces
{
    public interface IApsPublishService
    {
        Task PublishAsync(BatchJob job, CancellationToken cancellationToken = default);
    }
}
