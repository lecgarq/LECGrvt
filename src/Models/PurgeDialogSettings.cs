namespace LECG.Models
{
    public class PurgeDialogSettings
    {
        public bool PurgeLineStyles { get; set; } = true;
        public bool PurgeFillPatterns { get; set; } = true;
        public bool PurgeMaterials { get; set; } = true;
        public bool PurgeLevels { get; set; } = false;
        public bool PurgeParameters { get; set; } = false;
        public bool IsDeepPurge { get; set; } = true;
    }
}
