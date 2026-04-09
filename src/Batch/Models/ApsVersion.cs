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
    }
}
