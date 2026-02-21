# RESEARCH Phase 13: Bulk Aesthetic Conversion

## 1. Goal
Convert all remaining 17+ Revit command views to the "Premium Liquid Glass" aesthetic established in Phase 11 & 12.

## 2. Conversion Checklist (per file)
- Change `<Window>` to `<base:LecgWindow>`.
- Add `xmlns:base="clr-namespace:LECG.Views.Base"` if missing.
- Add `xmlns:icons="clr-namespace:LECG.Utils"` if missing.
- Set `WindowIcon="{x:Static icons:Icons.SpecificIcon}"`.
- Remove:
    - `WindowStyle="None"`
    - `AllowsTransparency="True"`
    - `Background="Transparent"`
    - `ResizeMode="..."`
    - `SizeToContent="..."` (unless specific reason to override)
- Refactor Content:
    - Remove manual TitleBar/Header borders.
    - Adjust `Grid.RowDefinitions` if a header row was removed.
    - Use `{StaticResource PanelBackgroundBrush}` for main containers.
    - Use `{StaticResource AccentButtonStyle}` for primary actions.
    - Ensure `Margin="16"` for standard outer padding.

## 3. Batches
- **Plan 13.1**: `HomeView`, `LogView`, `SelectionControl` (Components)
- **Plan 13.2**: `AlignDashboard`, `AlignEdges`, `AlignElements`, `ChangeLevel`, `OffsetElevations`, `ResetSlabs`
- **Plan 13.3**: `AssignMaterial`, `RenderAppearance`, `SexyRevit`, `SimplifyPoints`, `UpdateContours`
- **Plan 13.4**: `ConvertCad`, `ConvertFamily`, `FilterCopy`, `Purge`
