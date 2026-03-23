# Roadmap: LECG Revit Plugin

## Milestones

- Completed **v1.0 UI Refresh** - Phases P1-P9 (shipped 2026-03-19)
- In progress **v2.0 Geometry Operations** - Phases P10-P14

## Phases

<details>
<summary>v1.0 UI Refresh (Phases P1-P9) - SHIPPED 2026-03-19</summary>

All 24 commands shipped covering purge, standards normalization, toposolid tools, alignment, visualization, CAD/family conversion, and material creation. Full UI modernization with LecgDialog, LecgTreeView, and theme system.

</details>

### v2.0 Geometry Operations (In Progress)

**Milestone Goal:** Add core geometry conversion, repair, and model organization commands for Floors and Toposolids. Every command preserves geometric behavior, elevation data, and project references.

**Note:** Merge Elements was descoped from v2.0 and deferred to v2.x. The five commands shipping are: Shared Foundation (services), Conversion Pair, Fix Points, Split Boundaries, and Type to Linked Models.

## Phase Details

### Phase 10: Shared Foundation
**Goal**: Shared geometry services exist that all subsequent commands can depend on safely
**Depends on**: Phase 9 (prior milestone complete)
**Requirements**: CORE-01, CORE-02
**Success Criteria** (what must be TRUE):
  1. `IGeometryBoundaryService.ExtractLoops(element)` returns a validated `IList<CurveLoop>` with correct winding order for any Floor or Toposolid
  2. Outer boundary loops are counterclockwise and void loops are clockwise — `Floor.Create` and `Toposolid.Create` accept the output without `ArgumentException`
  3. `ISlabService.GetEditorVertexPositions(element)` returns a safe `List<XYZ>` snapshot before any modification — iterating the snapshot while modifying the editor does not throw
  4. Both services are registered in `Bootstrapper` and resolvable via `SimpleDi`
**Plans**: 1 implementation pass completed (2026-03-19)

### Phase 11: Conversion Pair (Floor to Toposolid and Toposolid to Floor)
**Goal**: Users can convert Floors to Toposolids and Toposolids to Floors in batch, with all shape points, boundaries, and elevations intact
**Depends on**: Phase 10
**Requirements**: CONV-01, CONV-02, CONV-03, CONV-04, CONV-05, CONV-06
**Success Criteria** (what must be TRUE):
  1. User opens the conversion command, sees a config window with source elements listed, selects destination type and level, and starts conversion without being asked to select elements first
  2. Each converted element has the same boundary CurveLoops and the same number of interior shape points as its source — no points are lost or displaced
  3. Absolute elevation of every shape point is preserved across the Floor-level-offset vs Toposolid-absolute-Z coordinate systems
  4. User can choose whether to delete each source element after a successful conversion, and the default is to delete
  5. The log shows per-element results with vertex count, destination type, and the reason for any element that was skipped
  6. The system auto-suggests a destination type whose name most closely matches the source type name
**Plans**: 1 implementation pass completed (2026-03-19)

### Phase 12: Fix Points
**Goal**: Users can repair inconsistent edge and transition points on Floors and Toposolids without recreating elements
**Depends on**: Phase 10
**Requirements**: FIXP-01, FIXP-02, FIXP-03, FIXP-04
**Success Criteria** (what must be TRUE):
  1. User opens the Fix Points command, sees a config window listing selected Floors and Toposolids, and runs repair without being asked to select elements first
  2. The system identifies vertices at edges or transition zones whose Z values deviate from the surrounding surface and flags them — only Interior-type vertices are candidates for deletion; Edge vertices are corrected via `ModifySubElement`
  3. After repair, flagged vertices match the Z interpolation of the surrounding surface — the element renders without triangulation spikes
  4. The log shows per-element results with vertex counts before and after, and the correction method applied to each modified point
**Plans**: 1 implementation pass completed (2026-03-19)

### Phase 13: Split Boundaries
**Goal**: Users can separate a Floor or Toposolid that contains multiple boundaries into independent elements, each preserving its own geometry
**Depends on**: Phase 10, Phase 11
**Requirements**: SPLT-01, SPLT-02, SPLT-03, SPLT-04
**Success Criteria** (what must be TRUE):
  1. User opens Split Boundaries, sees a list of selected elements with boundary counts, and elements with only one boundary are reported as not needing a split — no silent skips
  2. After splitting, each resulting element has exactly one boundary and preserves the elevation, edited points, and geometric definition of that boundary from the original element
  3. The Toposolid path uses the native `Toposolid.Split` API and the Floor path recreates one element per loop — both paths preserve type, level, and height offset on each result
  4. The log shows per-element split results with input boundary count and output element IDs
**Plans**: 1 implementation pass completed (2026-03-19)

### Phase 14: Type to Linked Models
**Goal**: Users can separate model content by element type into individual Revit files that link back into the host with shared coordinates
**Depends on**: Phase 10
**Requirements**: LINK-01, LINK-02, LINK-03, LINK-04, LINK-05, LINK-06, LINK-07, LINK-08
**Success Criteria** (what must be TRUE):
  1. User opens the command, sees a checkbox list of element types with element counts, selects which types to export, defines an output folder, and starts export — no files are created before the user confirms
  2. Each exported Revit file contains only the elements of its type, shares the same internal origin, survey point, and project base point as the host model
  3. Types that contain no geometry produce no output file — empty files are not created
  4. After export, each output file is linked back into the host model as a Revit reference at the correct position using shared coordinates
  5. The host model is cleaned of all exported geometry, leaving it as a coordination-only container
  6. The log shows per-type results with file path, element count copied, and link registration status
**Plans**: 1 implementation pass completed (2026-03-19)

## Progress

**Execution Order:** P10 → P11 → P12 (can follow P11 or run after P10) → P13 → P14

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 10. Shared Foundation | v2.0 | 1/1 | Completed | 2026-03-19 |
| 11. Conversion Pair | v2.0 | 1/1 | Completed | 2026-03-19 |
| 12. Fix Points | v2.0 | 1/1 | Completed | 2026-03-19 |
| 13. Split Boundaries | v2.0 | 1/1 | Completed | 2026-03-19 |
| 14. Type to Linked Models | v2.0 | 1/1 | Completed | 2026-03-19 |
