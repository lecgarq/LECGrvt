namespace LECG.Batch.Models
{
    public class BatchJob
    {
        public string JobId { get; set; } = "";
        public string CorrelationId { get; set; } = "";
        public JobStatus Status { get; set; }
        public string HubId { get; set; } = "";
        public string ProjectId { get; set; } = "";
        public string ProjectGuid { get; set; } = "";
        public string FolderId { get; set; } = "";
        public string ItemId { get; set; } = "";
        public string VersionId { get; set; } = "";
        public int VersionNumber { get; set; }
        public string ModelGuid { get; set; } = "";
        public string Region { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string FileName { get; set; } = "";
        public string FolderPath { get; set; } = "";
        public ModelType ModelType { get; set; }
        public bool PublishAfterSync { get; set; }
        public int RetryCount { get; set; }
        public int MaxRetries { get; set; }
        public DateTime AddedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
