using System.Collections.Generic;

namespace LECG.Models
{
    public class LineStyleCompactionResult
    {
        public int DuplicateGroups { get; set; }
        public int ReferencesRewired { get; set; }
        public int OriginalStylesDeleted { get; set; }
        public List<string> SurvivorNames { get; } = new List<string>();
        public List<string> BlockedDeletions { get; } = new List<string>();
    }
}
