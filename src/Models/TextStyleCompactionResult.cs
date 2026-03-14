using System.Collections.Generic;

namespace LECG.Models
{
    public class TextStyleCompactionResult
    {
        public int DuplicateGroups { get; set; }
        public int CanonicalTypesCreated { get; set; }
        public int ReferencesRewired { get; set; }
        public int OriginalTypesDeleted { get; set; }
        public List<string> CreatedCanonicalNames { get; } = new List<string>();
        public List<string> BlockedDeletions { get; } = new List<string>();
    }
}
