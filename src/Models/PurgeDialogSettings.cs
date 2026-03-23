namespace LECG.Models
{
    public class PurgeDialogSettings
    {
        public bool PurgeLineStyles { get; set; } = true;
        public bool PurgeLinePatterns { get; set; } = true;
        public bool PurgeFillPatterns { get; set; } = true;
        public bool PurgeMaterials { get; set; } = true;
        public bool PurgeLevels { get; set; } = false;
        public bool PurgeParameters { get; set; } = false;
        public bool PurgeGroups { get; set; } = false;
        public bool PurgeGridTypes { get; set; } = false;
        public bool PurgeLevelTypes { get; set; } = false;
        public bool PurgeConstraints { get; set; } = false;
        public bool PurgeUnplacedRooms { get; set; } = false;
        public bool PurgeViewTemplates { get; set; } = false;
        public bool PurgeViewFilters { get; set; } = false;
        public bool IsDeepPurge { get; set; } = true;
        public int PassCount => IsDeepPurge ? 3 : 1;
    }
}
