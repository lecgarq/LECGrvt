# LECG Revit Plugin

## What This Is

A Revit 2026 addin providing tools for converting, repairing, separating, merging, and reorganizing modeled elements while preserving geometric behavior, elevation data, and project references. Built for BIM professionals who need to manipulate Floors, Toposolids, multi-boundary geometry, and type-based model organization without losing modeled intent.

## Core Value

Preserve the exact geometric behavior, elevation data, and project reference system of modeled elements during any conversion, repair, or reorganization operation.

## Requirements

### Validated

<!-- Shipped and confirmed valuable. Phases P1-P9 completed. -->

- Purge unused line styles, fill patterns, materials (PurgeCommand)
- Clean third-party plugin schemas (CleanSchemasCommand)
- Normalize duplicated line/fill/text styles (CompactingStylesCommand)
- Batch find/replace in element names (SearchReplaceCommand)
- Convert imported CAD to Detail Item families (ConvertCadCommand)
- Convert hosted family to work plane-based generic model (ConvertFamilyCommand)
- Convert shared family to non-shared (ConvertSharedCommand)
- Change instance categories by modifying family definition (CategoryChangerCommand)
- Copy view filters between views/templates (FilterCopyCommand)
- Assign materials based on Floor/Toposolid type names (AssignMaterialCommand)
- Offset height from level for Toposolids/Floors (OffsetElevationsCommand)
- Reset slab shapes for Floor and Toposolid elements (ResetSlabsCommand)
- Remove redundant sub-element points from Toposolids (SimplifyPointsCommand)
- Align Toposolid points to another Toposolid surface (AlignEdgesCommand)
- Apply/remove contour display on Toposolid types (UpdateContoursCommand)
- Move Toposolids to new level maintaining elevation (ChangeLevelCommand)
- Align/distribute elements (8 alignment commands)
- Beautify current view (SexyRevitCommand)
- Sync graphics/identity with render appearance (RenderAppearanceMatchCommand)
- Create PBR materials from textures (PbrMaterialCreatorCommand)
- Move formula parameters to Other group (FormulaAutoGroupingCommand)

### Active

<!-- Current scope: v2.0 Geometry Operations milestone -->

- [ ] Floor to Toposolid conversion preserving shape points and elevations
- [ ] Toposolid to Floor conversion preserving edited points and elevations
- [ ] Fix Points — repair inconsistent points at edges/transitions
- [ ] Split Boundaries — separate multi-boundary elements into independent instances
- [ ] Type to Linked Models — separate by type into individual Revit files with shared coordinates

**Deferred:** Merge Elements is descoped to v2.x pending a defensible boundary-union strategy.

### Out of Scope

- Revit versions before 2026 — single-target strategy, no backwards compatibility
- Conceptual mass or adaptive component geometry — not part of Floor/Toposolid domain
- Cloud-based file operations — all operations are local Revit document manipulation

## Current Milestone: v2.0 Geometry Operations

**Goal:** Add core geometry conversion, repair, and model organization commands for Floors and Toposolids.

**Target features:**
- Floor to Toposolid conversion
- Toposolid to Floor conversion
- Fix Points (repair inconsistent surface points)
- Split Boundaries (multi-boundary to independent elements)
- Type to Linked Models (type-based model separation with shared coordinates)

**Deferred from this milestone:** Merge Elements (multiple elements to one consolidated)

## Context

- **Framework:** net8.0-windows, WPF, Revit 2026 API
- **Architecture:** Strict MVVM + DI (SimpleDi container). Commands inherit RevitCommand, services have interfaces, VMs inherit BaseViewModel with CommunityToolkit.Mvvm
- **Patterns:** All commands open config window first (never start by asking user to select). TransactionService wraps Revit transactions. SelectionCoordinator handles UI hide/show during selection.
- **Existing relevant services:** SlabService (SlabShapeEditor access, DuplicateElement), ToposolidService (ToposolidType manipulation), SimplifyPointsService (SlabShapeVertex iteration), AlignEdges services (boundary points, vertex alignment), ToposolidBaseElevationService (level + height offset resolution)
- **Ribbon:** "Toposolids" panel exists and will host most new commands. Type to Linked Models needs its own panel or goes in "Project Health".
- **Previous work:** Phases P1-P9 completed covering all validated requirements above.

## Constraints

- **Tech stack**: Revit 2026 API only, net8.0-windows, no external NuGet beyond existing
- **Architecture**: Must follow existing MVVM + DI patterns (RevitCommand, interface-backed services, LecgWindow views, Bootstrapper registration)
- **Interaction**: Commands must open config window first, never start by asking user to select elements
- **Transactions**: All Revit modifications must go through TransactionService

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Single DI container (SimpleDi) | Lightweight, no external DI framework dependency | Good |
| RevitCommand base class | Centralizes ExternalCommandData access and error handling | Good |
| LecgWindow base for all views | Consistent theming and window behavior | Good |
| TransactionService for all modifications | Centralized transaction management with SafeFailureHandler | Good |
| Toposolids panel grouping | All terrain/surface tools in one ribbon location | Good |

---
*Last updated: 2026-03-19 after milestone v2.0 initialization*
