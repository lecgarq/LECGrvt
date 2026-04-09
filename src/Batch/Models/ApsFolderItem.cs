namespace LECG.Batch.Models
{
    public enum FolderItemType { Folder, Item }

    public class ApsFolderItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public FolderItemType ItemType { get; set; }
        public string ProjectId { get; set; } = "";
        public string? VersionId { get; set; }
        public string? ModelGuid { get; set; }
        public string? Region { get; set; }
        public bool IsRevitModel { get; set; }
    }
}
