namespace LECG.Models
{
    public sealed record SexyRevitSettings(
        bool UseConsistentColors,
        bool UseSmoothLines,
        bool UseDetailFine,
        bool HideLevels,
        bool HideGrids,
        bool HideRefPoints,
        bool HideScopeBox,
        bool HideSectionBox,
        bool ConfigureSun);
}
