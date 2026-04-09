using LECG.Batch.Models;

namespace LECG.Batch.Services.Interfaces
{
    public interface IApsAuthService
    {
        Task<ApsSession> SignInAsync(CancellationToken cancellationToken = default);
        Task<ApsSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
        void SignOut();
    }
}
