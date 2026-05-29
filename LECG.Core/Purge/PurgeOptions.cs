namespace LECG.Core.Purge
{
    /// <summary>
    /// Which element categories the Safe-mode purge should process — one flag per UI checkbox.
    /// Replaces the 13 positional bool parameters that used to be threaded through the purge
    /// service stack, removing the risk of transposing two arguments at a call site.
    /// </summary>
    public sealed record PurgeOptions(
        bool LineStyles,
        bool LinePatterns,
        bool FillPatterns,
        bool Materials,
        bool Levels,
        bool Parameters,
        bool Groups,
        bool GridTypes,
        bool LevelTypes,
        bool Constraints,
        bool UnplacedRooms,
        bool ViewTemplates,
        bool ViewFilters);
}
