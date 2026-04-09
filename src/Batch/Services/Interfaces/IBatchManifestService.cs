using LECG.Batch.Models;

namespace LECG.Batch.Services.Interfaces
{
    public interface IBatchManifestService
    {
        BatchManifest Load();
        void Save(BatchManifest manifest);
        void AddJobs(BatchManifest manifest, IEnumerable<ApsVersion> versions, bool publishAfterSync);
    }
}
