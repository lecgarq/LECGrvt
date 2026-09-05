using System;
using System.Collections.Generic;

namespace LECG.Batch.Models
{
    public class CloudModelCacheEntry
    {
        public string HubId { get; set; } = "";
        public string ProjectId { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public DateTime FetchedAtUtc { get; set; }
        public List<ApsVersion> Models { get; set; } = new List<ApsVersion>();
    }
}
