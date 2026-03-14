using System.Collections.Generic;

namespace LECG.Models
{
    public class LinePatternCompactionResult
    {
        public int DuplicateGroups { get; set; }
        public int CanonicalPatternsCreated { get; set; }
        public int ReferencesRewired { get; set; }
        public int OriginalPatternsDeleted { get; set; }
        public int PatternsRenamed { get; set; }
        public List<string> CreatedCanonicalNames { get; } = new List<string>();
        public List<string> BlockedDeletions { get; } = new List<string>();
    }
}
