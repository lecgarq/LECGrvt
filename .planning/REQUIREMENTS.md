# Requirements: LECG Revit Plugin

**Defined:** 2026-03-19
**Core Value:** Preserve the exact geometric behavior, elevation data, and project reference system of modeled elements during any conversion, repair, or reorganization operation.

## v2.0 Requirements

Requirements for Geometry Operations milestone. Each maps to roadmap phases.

### Conversion

- [ ] **CONV-01**: User can convert one or more Floors to Toposolids preserving all shape points, boundary geometry, and elevation values
- [ ] **CONV-02**: User can convert one or more Toposolids to Floors preserving all edited points, boundary geometry, and elevation values
- [ ] **CONV-03**: User can select the destination level and type for converted elements
- [ ] **CONV-04**: System auto-suggests matching destination type based on source type name similarity
- [ ] **CONV-05**: User can choose whether to delete the source element after successful conversion
- [ ] **CONV-06**: User can see per-element conversion results in a log with vertex count and skip reasons

### Fix Points

- [ ] **FIXP-01**: User can repair inconsistent points on Floors or Toposolids that don't follow the surrounding surface
- [ ] **FIXP-02**: System detects vertices at edges or transition zones with abnormal triangulation
- [ ] **FIXP-03**: System adjusts incorrect points by reading surrounding surface behavior and matching reference geometry
- [ ] **FIXP-04**: User can see per-element repair results in a log

### Split Boundaries

- [ ] **SPLT-01**: User can split a Floor or Toposolid containing multiple boundaries into separate independent elements
- [ ] **SPLT-02**: Each resulting element preserves its boundary's elevations, edited points, and geometric definition
- [ ] **SPLT-03**: System detects and reports elements with only a single boundary (no split needed)
- [ ] **SPLT-04**: User can see per-element split results in a log

### Type to Linked Models

- [ ] **LINK-01**: User can separate model content by selected element types into individual Revit files
- [ ] **LINK-02**: Each exported file preserves the same internal origin, survey point, project base point, and shared coordinates as the source
- [ ] **LINK-03**: User can select which types to export via a checkbox list showing element counts per type
- [ ] **LINK-04**: User can define the export file location manually
- [ ] **LINK-05**: The active model is cleaned of exported geometry to become a coordination-only container
- [ ] **LINK-06**: Exported files are linked back into the host model as Revit references
- [ ] **LINK-07**: Empty files (types with no geometry) are not generated
- [ ] **LINK-08**: User can see export results in a log with file paths and element counts

### Shared Foundation

- [ ] **CORE-01**: Boundary extraction service converts Sketch.Profile CurveArrArray to validated IList of CurveLoop with correct winding order
- [ ] **CORE-02**: Vertex snapshot utility reads all SlabShapeVertex positions safely before any modification

## Future Requirements

Deferred to v2.x or later.

### Fix Points Enhancements

- **FIXP-05**: User can configure XY tolerance and resolution strategy (min/max/average Z)
- **FIXP-06**: User can see vertex count audit log per element (before/after counts)
- **FIXP-07**: User can preview fix results in dry-run mode before applying

### Merge Elements

- **MERG-01**: User can combine multiple Floors or Toposolids into one consolidated element
- **MERG-02**: Merged element preserves elevation logic, shape behavior, and geometric continuity
- **MERG-03**: System rejects mixed-category merge (Floor + Toposolid)
- **MERG-04**: System warns about non-abutting boundaries before proceeding

### Conversion Enhancements

- **CONV-07**: Slope-arrow Floor to Toposolid via solid face sampling
- **CONV-08**: Crease-line preservation during Floor to Toposolid conversion
- **CONV-09**: Toposolid subdivision handling during conversion

### Type to Linked Models Enhancements

- **LINK-09**: Configurable file naming pattern template
- **LINK-10**: Dry-run mode showing what would be exported without creating files

## Out of Scope

| Feature | Reason |
|---------|--------|
| Curved-boundary polygon union for Merge | High complexity, no Revit API support for 2D polygon union; defer to v2.x |
| Revit versions before 2026 | Single-target strategy, no backwards compatibility |
| Conceptual mass / adaptive component geometry | Not part of Floor/Toposolid domain |
| Cloud-based file operations | All operations are local Revit document manipulation |
| Toposolid subdivision conversion | Sub-division Toposolids (HostTopoId set) have parent dependencies; needs research |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| CORE-01 | Phase 10 | Pending |
| CORE-02 | Phase 10 | Pending |
| CONV-01 | Phase 11 | Pending |
| CONV-02 | Phase 11 | Pending |
| CONV-03 | Phase 11 | Pending |
| CONV-04 | Phase 11 | Pending |
| CONV-05 | Phase 11 | Pending |
| CONV-06 | Phase 11 | Pending |
| FIXP-01 | Phase 12 | Pending |
| FIXP-02 | Phase 12 | Pending |
| FIXP-03 | Phase 12 | Pending |
| FIXP-04 | Phase 12 | Pending |
| SPLT-01 | Phase 13 | Pending |
| SPLT-02 | Phase 13 | Pending |
| SPLT-03 | Phase 13 | Pending |
| SPLT-04 | Phase 13 | Pending |
| LINK-01 | Phase 14 | Pending |
| LINK-02 | Phase 14 | Pending |
| LINK-03 | Phase 14 | Pending |
| LINK-04 | Phase 14 | Pending |
| LINK-05 | Phase 14 | Pending |
| LINK-06 | Phase 14 | Pending |
| LINK-07 | Phase 14 | Pending |
| LINK-08 | Phase 14 | Pending |

**Coverage:**
- v2.0 requirements: 24 total
- Mapped to phases: 24
- Unmapped: 0

---
*Requirements defined: 2026-03-19*
*Last updated: 2026-03-19 after roadmap creation (v2.0 traceability complete)*
