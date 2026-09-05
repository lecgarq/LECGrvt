namespace LECG.Batch.Models
{
    public class ApsVersion
    {
        public string Id { get; set; } = "";
        public string ItemId { get; set; } = "";
        public string ProjectId { get; set; } = "";
        public string HubId { get; set; } = "";
        public string FolderId { get; set; } = "";
        public int VersionNumber { get; set; }
        public string DisplayName { get; set; } = "";
        public string FileName { get; set; } = "";
        public string FileType { get; set; } = "";
        public string ExtensionType { get; set; } = "";
        public string? ModelGuid { get; set; }
        public string? Region { get; set; }
        public string ProjectGuid { get; set; } = "";
        public bool IsWorkshared { get; set; }

        // Enriched by search API
        public long? FileSizeBytes { get; set; }
        public DateTime? LastModified { get; set; }
        public string FolderPath { get; set; } = "";

        [System.Text.Json.Serialization.JsonIgnore]
        public string FileSizeDisplay => FileSizeBytes.HasValue
            ? $"{FileSizeBytes.Value / (1024.0 * 1024.0):F1} MB"
            : "";

        [System.Text.Json.Serialization.JsonIgnore]
        public string LastModifiedDisplay => LastModified.HasValue
            ? LastModified.Value.ToString("MMM d, yyyy")
            : "";

        [System.Text.Json.Serialization.JsonIgnore]
        public string FolderPathDisplay => string.IsNullOrWhiteSpace(FolderPath)
            ? "Project root"
            : FolderPath;

        [System.Text.Json.Serialization.JsonIgnore]
        public string ModelTypeDisplay => IsWorkshared ? "Workshared" : "Single-user";
    }
}
