using System.Collections.Generic;
using LECG.Batch.Models;

namespace LECG.Batch.Services.Interfaces
{
    public interface IApsCloudModelIndexStore
    {
        CloudModelCacheEntry? Get(string hubId, string projectId);
        void Save(CloudModelCacheEntry entry);
        void Clear();
    }
}
