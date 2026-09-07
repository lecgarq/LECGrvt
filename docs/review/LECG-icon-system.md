# LECG Ribbon Icon System

## Production contract

- Revit: 2026.
- Family: 38 command pictograms, including the `Align Elements` pulldown master.
- Master format: transparent SVG, `64 × 64` viewBox.
- Runtime formats: transparent PNG at `32 × 32` and `16 × 16` px.
- Primary line/mark: LECG Blue `#1E4257`.
- Structural accent: Ink `#1E1E1C`.
- Secondary fill: Water `#CFE0EB`.
- No tile background, gradient, shadow, bevel, gloss, 3D or embedded text.
- Nominal master stroke: 4 units; rounded caps and joins.

## Locations

- SVG masters: `src/Resources/IconSources/Lecg*.svg`
- Ribbon PNGs: `src/Resources/Images/Lecg*_32.png` and `Lecg*_16.png`
- Generator: `scripts/generate_lecg_icons.py`
- Visual QA sheet: `docs/review/LECG-icon-family-contact-sheet.png`
- Runtime mapping: `src/Utilities/AppImages.cs`
- Ribbon assignments: `src/Core/Ribbon/RibbonService.cs`

Regenerate and validate all outputs:

```powershell
python scripts/generate_lecg_icons.py
dotnet build -p:SkipRevitDeploy=true
```

## Asset map

| Group | Command | Asset stem |
|---|---|---|
| Home | Home | `LecgHome` |
| Project Health | Clean Schemas | `LecgCleanSchemas` |
| Project Health | Compact Styles | `LecgCompactStyles` |
| Project Health | Purge Unused | `LecgPurgeUnused` |
| Project Health | Formula Grouping | `LecgFormulaGrouping` |
| Project Health | Warnings | `LecgWarnings` |
| Standards | CAD Blocks | `LecgCadBlocks` |
| Standards | Batch Rename | `LecgBatchRename` |
| Standards | Convert Family | `LecgConvertFamily` |
| Standards | Convert Shared | `LecgConvertShared` |
| Standards | Category Changer | `LecgCategoryChanger` |
| Standards | Shared to Family Param | `LecgSharedToFamilyParam` |
| Standards | Filter Copy | `LecgFilterCopy` |
| Toposolids | Assign Material | `LecgAssignMaterial` |
| Toposolids | Offset Elevations | `LecgOffsetElevations` |
| Toposolids | Reset Slabs | `LecgResetSlabs` |
| Toposolids | Simplify Points | `LecgSimplifyPoints` |
| Toposolids | Align Edges | `LecgAlignEdges` |
| Toposolids | Update Contours | `LecgUpdateContours` |
| Toposolids | Change Level | `LecgChangeLevel` |
| Toposolids | Floor to Toposolid | `LecgFloorToToposolid` |
| Toposolids | Toposolid to Floor | `LecgToposolidToFloor` |
| Toposolids | Fix Points | `LecgFixPoints` |
| Toposolids | Split Boundaries | `LecgSplitBoundaries` |
| Toposolids | Divide Toposolid | `LecgDivideToposolid` |
| Align | Align Elements | `LecgAlignMaster` |
| Align | Align Left | `LecgAlignLeft` |
| Align | Align Center | `LecgAlignCenter` |
| Align | Align Right | `LecgAlignRight` |
| Align | Align Top | `LecgAlignTop` |
| Align | Align Middle | `LecgAlignMiddle` |
| Align | Align Bottom | `LecgAlignBottom` |
| Align | Distribute Horizontally | `LecgDistributeH` |
| Align | Distribute Vertically | `LecgDistributeV` |
| Model Organization | Type to Linked | `LecgTypeToLinked` |
| Visualization | Render Match | `LecgRenderMatch` |
| Visualization | PBR Material | `LecgPbrMaterial` |
| Visualization | Sexy Revit | `LecgSexyRevit` |

## QA result

- 38 unique command concepts.
- 38 SVG masters.
- 38 PNGs at 32 px.
- 38 PNGs at 16 px.
- All PNGs validated as RGBA with transparent background and visible foreground.
- Every command in `RibbonService` uses its own 32 px and 16 px property; no placeholder or reused icon remains.
