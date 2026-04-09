using LECG.Batch.Models;

namespace LECG.Batch.Services.Interfaces
{
    public interface IApsDataManagementService
    {
        Task<IReadOnlyList<ApsHub>> GetHubsAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ApsProject>> GetProjectsAsync(string hubId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ApsFolderItem>> GetTopFoldersAsync(string hubId, string projectId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ApsFolderItem>> GetFolderContentsAsync(string projectId, string folderId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ApsVersion>> GetVersionsAsync(string projectId, string itemId, CancellationToken cancellationToken = default);
        Task<ApsVersion?> GetVersionAsync(string projectId, string versionId, CancellationToken cancellationToken = default);
        bool IsRevitCloudModel(ApsVersion version);
    }
}
