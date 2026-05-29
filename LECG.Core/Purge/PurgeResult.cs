namespace LECG.Core.Purge
{
    /// <summary>
    /// Per-category count of elements deleted by a purge run. Replaces the 13-int tuple that
    /// used to be returned and threaded through the purge service stack.
    /// </summary>
    public sealed record PurgeResult(
        int LineStyles,
        int LinePatterns,
        int FillPatterns,
        int Materials,
        int Levels,
        int Parameters,
        int Groups,
        int GridTypes,
        int LevelTypes,
        int Constraints,
        int UnplacedRooms,
        int ViewTemplates,
        int ViewFilters)
    {
        public static PurgeResult Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        public int Total =>
            LineStyles + LinePatterns + FillPatterns + Materials + Levels + Parameters
            + Groups + GridTypes + LevelTypes + Constraints + UnplacedRooms
            + ViewTemplates + ViewFilters;

        /// <summary>Component-wise sum, used to accumulate per-pass results.</summary>
        public PurgeResult Add(PurgeResult other)
        {
            System.ArgumentNullException.ThrowIfNull(other);
            return new(
            LineStyles + other.LineStyles,
            LinePatterns + other.LinePatterns,
            FillPatterns + other.FillPatterns,
            Materials + other.Materials,
            Levels + other.Levels,
            Parameters + other.Parameters,
            Groups + other.Groups,
            GridTypes + other.GridTypes,
            LevelTypes + other.LevelTypes,
            Constraints + other.Constraints,
            UnplacedRooms + other.UnplacedRooms,
            ViewTemplates + other.ViewTemplates,
            ViewFilters + other.ViewFilters);
        }
    }
}
