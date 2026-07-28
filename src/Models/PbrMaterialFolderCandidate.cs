namespace LECG.Models
{
    public sealed record PbrMaterialFolderCandidate(
        string FolderPath,
        string MaterialName,
        string? DiffusePath,
        string? RoughnessPath,
        string? NormalPath,
        int DetectedCount,
        string? SkipReason)
    {
        public bool IsValid =>
            string.IsNullOrWhiteSpace(SkipReason) &&
            !string.IsNullOrWhiteSpace(MaterialName) &&
            !string.IsNullOrWhiteSpace(DiffusePath);
    }
}
