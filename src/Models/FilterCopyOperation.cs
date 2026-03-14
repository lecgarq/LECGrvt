using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Models
{
    public sealed record FilterCopyFilterState(
        ElementId FilterId,
        OverrideGraphicSettings GraphicsSettings,
        bool IsVisible,
        bool ShouldRemove);

    public sealed record FilterCopyViewState(
        ElementId ViewId,
        IReadOnlyList<FilterCopyFilterState> Filters);
}
