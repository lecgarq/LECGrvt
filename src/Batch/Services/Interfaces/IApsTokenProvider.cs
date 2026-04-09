using LECG.Batch.Models;

namespace LECG.Batch.Services.Interfaces
{
    public interface IApsTokenProvider
    {
        Task<string> GetValidTokenAsync(CancellationToken cancellationToken = default);
    }
}
