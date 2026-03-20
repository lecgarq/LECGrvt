# Project Research Summary

**Project:** LECG Revit Plugin — v2.0 Geometry Operations milestone
**Domain:** Revit 2026 addin — Floor/Toposolid conversion, repair, splitting, merging, and type-based model separation
**Researched:** 2026-03-19
**Confidence:** HIGH

## Executive Summary

The v2.0 milestone adds six geometry-manipulation commands to an existing, well-structured Revit addin. The codebase already provides a proven pattern for every layer: `RevitCommand` base class, `LecgWindow` views, `CommunityToolkit.Mvvm` view models, service interfaces with a lightweight DI container, and `TransactionService` wrapping all Revit mutations. Every new command slots into this layered architecture without exception. All required API surface — `Toposolid.Create`, `Floor.Create`, `SlabShapeEditor`, `Sketch.Profile`, `RevitLinkType.Create`, and `ElementTransformUtils.CopyElements` — has been verified against locally installed Revit 2026 assemblies. No new NuGet packages are needed.

The recommended implementation order mirrors the architectural dependency graph. A shared `IGeometryBoundaryService` (which converts the legacy `CurveArrArray` from `Sketch.Profile` into `IList<CurveLoop>`) is the single most critical first deliverable because four of six commands depend on it. The two conversion commands (Floor↔Toposolid) come next as a matched pair, followed by Fix Points (independent), Split Boundaries (depends on shared boundary service), Merge Elements (depends on Split patterns), and finally Type to Linked Models (completely independent, but highest complexity because it involves multi-document operations and file I/O).

The most dangerous technical risks are not feature complexity but transaction-boundary mistakes: `SlabShapeEditor.Enable()` crashes when called in the same transaction as element creation; `Toposolid.Split` returns IDs that cannot be accessed until after commit; and `Document.SaveAs` throws if called inside any active transaction. All three are pre-empted by a two-transaction-per-command design, which is already the architectural norm in the codebase. Curve loop winding order and elevation coordinate mismatch (Floor uses a relative parameter offset; Toposolid uses absolute Z in point coordinates) must be addressed in a shared utility in Phase 1 before any conversion is implemented.

---

## Key Findings

### Recommended Stack

The stack is already determined and requires zero changes. The plugin targets .NET 8 on WPF with `CommunityToolkit.Mvvm 8.2.2` and the Revit 2026 API assemblies. Every new class is a new file in an existing folder; no new projects, no new package dependencies, no framework changes.

**Core technologies:**
- `Toposolid.Create(doc, loops, points, typeId, levelId)` — the 5-parameter overload is the single correct call for Floor→Toposolid; it creates the element and seeds elevation points atomically
- `Floor.Create(doc, loops, typeId, levelId)` + `SlabShapeEditor.AddPoints` in a second transaction — required pattern for Toposolid→Floor because Floor has no points-at-creation overload
- `Sketch.SketchId` + `Sketch.Profile` (CurveArrArray) — primary boundary source; must be converted to `IList<CurveLoop>` via a shared utility
- `Toposolid.Split(IList<CurveLoop>)` — native API for splitting Toposolids; not available on Floor (must use recreate-per-loop pattern there)
- `Application.NewProjectDocument` + `ElementTransformUtils.CopyElements` (cross-document overload) + `RevitLinkType.Create` + `RevitLinkInstance.Create` — full Type to Linked Models pipeline, all API signatures verified
- `TransactionService.Run` — all Revit mutations must go through this; two separate calls (create, then configure) is the required pattern for any command that adds shape points post-creation

### Expected Features

All six commands are P1 (must ship in v2.0). None are optional for this milestone.

**Must have (table stakes):**
- Preserve all shape points (SlabShapeVertices) during every conversion — this is the entire value of the plugin
- Preserve boundary sketch (CurveLoops) and height offset from level on every converted element
- Config window before any selection (project interaction contract — non-negotiable)
- Batch processing of multiple elements — no per-element manual runs
- Per-element log output with result, vertex count, and skip reason
- Delete source element option (default on) for conversion commands
- Mixed-category rejection for Merge (no Floor+Toposolid mixing)
- Multi-boundary detection and messaging for Split Boundaries
- Type list with element counts before Type to Linked Models export

**Should have (competitive differentiators):**
- Fix Points configurable XY tolerance and resolution strategy (min/max/average Z)
- Native `Toposolid.Split` API path for Split Boundaries (more accurate mesh than manual recreate)
- Selective type export checkbox list for Type to Linked Models
- Auto-match type by name during conversion (reduces config clicks)
- Non-abutting boundary warning before Merge proceeds
- Vertex count audit log per element for BIM manager workflows

**Defer (v2.x or later):**
- Fix Points dry-run/preview mode
- Slope-arrow Floor→Toposolid via solid face sampling
- Curved-boundary polygon union for Merge (straight-boundary only in v2.0)
- Crease-line preservation during Floor→Toposolid (API support uncertain)
- Type to Linked Models naming pattern template UI
- Toposolid subdivision handling during conversion

### Architecture Approach

The architecture is a strict six-layer stack: Ribbon → Command → View/ViewModel → Service → (shared services) → Revit API. All new components follow exactly the shapes of existing ones. The single most important architectural decision is the new shared `IGeometryBoundaryService`, which centralizes the `CurveArrArray`→`CurveLoop` conversion and winding-order normalization that four commands all require. Without it, the same bug would need to be fixed in four places.

**Major components:**
1. `IGeometryBoundaryService` / `GeometryBoundaryService` — shared foundation; extracts `IList<CurveLoop>` from any Floor or Toposolid; normalizes winding order; validates planarity; consumed by FloorConversion, ToposolidConversion, SplitBoundaries, MergeElements
2. `IFloorConversionService` / `IToposolidConversionService` — conversion pair; depend on boundary service + extended `ISlabService`; read source, create target in transaction 1, apply shape points in transaction 2
3. `IFixPointsService` — standalone; iterates `SlabShapeEditor` vertices by type, applies Z corrections via `ModifySubElement`; no element creation/deletion
4. `ISplitBoundariesService` — uses boundary service; uses native `Toposolid.Split` for Toposolid path; recreates per-loop for Floor path; two-transaction pattern (split, then copy parameters)
5. `IMergeElementsService` — uses boundary service; projects all source loops to a common plane; creates merged element; transfers all shape points; depends on patterns established by Split phase
6. `ILinkedModelExportService` — fully independent; multi-document transaction pattern from `DeepPurgeService`; explicit phase boundaries (collect → SaveAs outside transaction → link in host transaction → delete in host transaction)
7. Extended `ISlabService` — two new methods: `GetEditorVertexPositions(element)` and `ApplyVertices(element, positions)`; shared by conversion and fix commands

### Critical Pitfalls

1. **SlabShapeVertex references become stale after any `ModifySubElement` call in the same transaction** — snapshot all `vertex.Position` values into `List<XYZ>` before entering any modification loop; never iterate the live `SlabShapeVertices` collection while modifying it

2. **`SlabShapeEditor.Enable()` called in the same transaction as element creation throws `InvalidOperationException`** — mandatory two-transaction pattern: transaction 1 creates the element and commits; transaction 2 calls `Enable()`, `AddPoints()`, `ModifySubElement()`; this is non-negotiable for Floor→Toposolid and Toposolid→Floor

3. **CurveLoop winding order mismatch causes `ArgumentException` at `Floor.Create` / `Toposolid.Create`** — the shared boundary utility must call `loop.IsCounterclockwise(XYZ.BasisZ)` on every extracted loop and `loop.Flip()` if incorrect; outer loops counterclockwise, inner void loops clockwise

4. **Elevation coordinate mismatch: Floor stores a relative `FLOOR_HEIGHTABOVELEVEL_PARAM`; Toposolid stores absolute Z in point coordinates** — the conversion math is: `topoZ = level.Elevation + floorHeightOffset + vertexRelativeOffset`; wrong math silently produces elements floating above or buried below the model, visible only after commit

5. **`Document.SaveAs` throws if any transaction is open in any document** — Type to Linked Models must be fully outside `TransactionService.Run` when calling `SaveAs`; use the `DeepPurgeService` multi-document transaction pattern; call `File.Exists(path)` before `RevitLinkType.Create` because the link creation is not lazy

6. **`Toposolid.Split` returns IDs that are not accessible via `doc.GetElement` until after the transaction commits** — always use two transactions: split in transaction 1, copy parameters to new elements in transaction 2

7. **`DeletePoint` on Corner or Edge vertices throws; only Interior vertices can be deleted** — Fix Points must filter by `vertex.VertexType == SlabShapeVertexType.Interior` before any deletion attempt; use `ModifySubElement` for Edge corrections, never `DeletePoint`

---

## Implications for Roadmap

Based on combined research, the architecture file's suggested build order is the correct roadmap structure. It is dependency-driven and matches how pitfall prevention layers in.

### Phase 1: Shared Foundation

**Rationale:** Four of six commands depend on `IGeometryBoundaryService`. The CurveArrArray→CurveLoop conversion and winding-order normalization (Pitfall 2, Pitfall 6) must exist and be tested before any command-specific service is written. Fixing this bug once in one place is architecturally superior to fixing it four times. The `ISlabService` extension methods (`GetEditorVertexPositions`, `ApplyVertices`) standardize the vertex snapshot pattern that prevents Pitfall 1 (stale SlabShapeVertex references).

**Delivers:** `GeometryBoundaryService` with winding-order normalization; two new methods on `ISlabService`; both registered in `Bootstrapper`; covered by unit-testable boundary extraction

**Addresses:** Boundary extraction for Split Boundaries, Merge, and both conversions; vertex snapshot contract for Fix Points and conversions

**Avoids:** Pitfall 1 (stale vertex refs), Pitfall 2 (winding order), Pitfall 6 (CurveArrArray conversion)

**Research flag:** Standard patterns — skip phase research. All API signatures verified; `AlignEdgesBoundaryCollectionService` is existing precedent.

---

### Phase 2: Conversion Pair (Floor↔Toposolid)

**Rationale:** The two conversion commands are mirror implementations and share the same edge cases (multi-loop boundaries, elevation coordinate math, two-transaction point application). Building them together prevents solving the same problems with different approaches. These are the highest user-value features (P1) and establish the elevation math patterns that Split and Merge will reuse.

**Delivers:** `FloorConversionService`, `ToposolidConversionService`, `FloorToToposolidCommand`, `ToposolidToFloorCommand`, both view models and views, ribbon buttons in Toposolids panel

**Addresses:** Floor→Toposolid (table stakes: preserve points, boundary, level, offset, delete source); Toposolid→Floor (same guarantees); batch processing; type dropdown; log per element

**Avoids:** Pitfall 1 (vertex snapshot via ISlabService), Pitfall 3 (elevation math: `level.Elevation + heightOffset + vertexOffset`), Pitfall 4 (two-transaction pattern for AddPoints)

**Research flag:** Standard patterns — skip phase research. All API signatures verified from local Revit 2026 assemblies.

---

### Phase 3: Fix Points

**Rationale:** Fix Points only depends on the extended `ISlabService` from Phase 1 and `TransactionService`. It is the simplest of the six commands and does not create or delete elements. It can be developed in parallel with Phase 2 by a second developer if needed. It also serves as the natural post-conversion cleanup tool, making it useful immediately after Phase 2 ships.

**Delivers:** `FixPointsService`, `FixPointsCommand`, view model and view, ribbon button

**Addresses:** Detect vertices within configurable XY tolerance; apply Z correction with resolution strategy (min Z default); filter by `SlabShapeVertexType.Interior` for deletions; `ModifySubElement` for Edge corrections; log per element

**Avoids:** Pitfall 11 (VertexType filter — Corner/Edge vertices cannot be deleted); Pitfall 1 (vertex snapshot before modification loop)

**Research flag:** Standard patterns — skip phase research. `SimplifyPointsService` is full precedent.

---

### Phase 4: Split Boundaries

**Rationale:** Split Boundaries uses `IGeometryBoundaryService` (Phase 1) and the same element-creation patterns established in Phase 2. By Phase 4, the winding-order edge cases and multi-loop boundaries are already understood. The `Toposolid.Split` API path is straightforward; the Floor recreate-per-loop path mirrors the conversion create pattern. Pitfall 5 (Split IDs not accessible until commit) requires the two-transaction pattern already established in Phase 2.

**Delivers:** `SplitBoundariesService`, `SplitBoundariesCommand`, view model and view, ribbon button

**Addresses:** Single-boundary detection and early-out messaging; native `Toposolid.Split` for Toposolid path; `Floor.Create` per loop for Floor path; shape point redistribution via XY containment; copy type/level/offset to each result element

**Avoids:** Pitfall 5 (Split returns stale IDs — access in second transaction); Pitfall 2 (winding via shared service); anti-feature: inner void loops must not become separate elements

**Research flag:** Standard patterns — skip phase research. Native API confirmed; Floor recreate pattern established in Phase 2.

---

### Phase 5: Merge Elements

**Rationale:** Merge is the inverse of Split. Phase 4 proves how Revit handles multi-loop creation, what winding failures look like, and how to redistribute shape points. Merge adds boundary union geometry (the hardest new algorithm), point deduplication, and the coplanarity constraint (Pitfall 10). Building after Phase 4 means these risks surface with full context. MVP scope: straight-boundary abutting merges only; reject curved-boundary union with a clear error.

**Delivers:** `MergeElementsService`, `MergeElementsCommand`, view model and view, ribbon button

**Addresses:** Union of straight-edge abutting CurveLoops; project all source loops to common plane (Pitfall 10); collect all shape points from all source editors; deduplication by XY proximity; mixed-category rejection; non-abutting boundary warning; delete all sources after successful merge

**Avoids:** Pitfall 10 (non-coplanar merge boundary — use `CurveLoop.CreateViaTransform` to project); Pitfall 2 (winding via shared service); anti-feature: curved-boundary union deferred to v2.x

**Research flag:** Needs attention during planning. The polygon union algorithm (shared edge detection, outer hull construction) has no Revit API support. The exact approach for adjacent straight-edge boundary combination should be designed before implementation begins.

---

### Phase 6: Type to Linked Models

**Rationale:** This command has no dependencies on the other five commands and no shared services with them. It is architecturally isolated but has the highest implementation complexity (multi-document transactions, file I/O, shared coordinates, link registration). Building last means it does not block any other feature and can be planned more carefully once the rest of the milestone is stable. Three critical pitfalls apply only to this command (P7, P8, P9, P12).

**Delivers:** `LinkedModelExportService`, `TypeToLinkedModelsCommand`, view model and view, ribbon button in Health panel

**Addresses:** Enumerate types with element counts; selective export checkbox; cross-document `ElementTransformUtils.CopyElements`; `Application.NewProjectDocument` + `Document.SaveAs`; `RevitLinkType.Create` + `RevitLinkInstance.Create` with shared coordinates; delete host elements after confirmed link; dry-run mode as v2.x add-on

**Avoids:** Pitfall 7 (SaveAs outside transaction — explicit phase boundary in service); Pitfall 8 (`File.Exists` check before `RevitLinkType.Create`); Pitfall 9 (shared coordinates — document as MVP-deferred, call `AcquireCoordinates` in v2.x); Pitfall 12 (multi-doc transactions — follow `DeepPurgeService` pattern, separate `Run` per document)

**Research flag:** Needs attention during planning. Multi-document transaction sequencing and the exact shared coordinate publishing workflow should be designed on paper before any code is written. The `DeepPurgeService` pattern is the correct structural reference but does not cover file save or link registration.

---

### Phase Ordering Rationale

- Phases 1→2→4→5 form a strict dependency chain: boundary service → conversion patterns → split patterns → merge patterns. Each phase de-risks the next.
- Phase 3 (Fix Points) is dependency-free after Phase 1 and can run in parallel with Phase 2 if bandwidth allows.
- Phase 6 (Type to Linked Models) is fully independent and benefits from being last: it is the only command with file system side effects and multi-document transactions, which are harder to undo than Revit model mutations.
- Pitfalls 1, 2, 3, 4, and 6 are all addressed in Phase 1 or Phase 2. By the time Phase 3 begins, the most dangerous transaction-boundary and coordinate-math traps are already resolved and codified in shared utilities.
- The merge boundary union algorithm (Phase 5) is the single piece of logic with no existing codebase precedent. Scoping it to straight-line abutting boundaries for v2.0 reduces the unknowns to a manageable level.

### Research Flags

Phases likely needing deeper design work before implementation:
- **Phase 5 (Merge Elements):** The polygon union algorithm for adjacent CurveLoops is not provided by the Revit API. Before coding, design the shared-edge detection and outer-hull construction approach; validate against at least two adjacent element configurations (L-shape, T-shape).
- **Phase 6 (Type to Linked Models):** Multi-document transaction sequencing (SaveAs outside transaction → open destination → transaction → close → link) must be designed as a flow diagram and reviewed against existing `DeepPurgeService` before implementation.

Phases with standard patterns (skip research-phase):
- **Phase 1 (Shared Foundation):** API surface confirmed; precedent in `AlignEdgesBoundaryCollectionService`.
- **Phase 2 (Conversion Pair):** API signatures verified from local assemblies; precedent in `SimplifyPointsService` and `OffsetService`.
- **Phase 3 (Fix Points):** `SimplifyPointsService` is full precedent; no new API surface required.
- **Phase 4 (Split Boundaries):** `Toposolid.Split` confirmed; Floor recreate pattern established in Phase 2.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All API signatures verified against locally installed Revit 2026 DLL reflection exports at `docs/review/revit-api/`; no external docs needed |
| Features | HIGH | Six commands fully specified from direct codebase analysis and API verification; user workflow derived from project context, not assumption |
| Architecture | HIGH | Derived from direct source inspection of the existing codebase; every new component has an existing precedent to mirror |
| Pitfalls | HIGH | Primary source is local API export + live codebase; transaction-boundary pitfalls verified from `SimplifyPointsService` and `DeepPurgeService` behavior patterns |

**Overall confidence:** HIGH

### Gaps to Address

- **Merge boundary union algorithm:** The exact algorithm for combining adjacent straight-line CurveLoops has no codebase precedent. Validate the approach on paper before Phase 5 begins. The adjacency test (shared edge detection within tolerance) needs a concrete tolerance value — suggest using the same XY tolerance as Fix Points (configurable, default 1mm = 0.00328 feet).
- **Shared coordinate publishing for Type to Linked Models:** Research confirmed `Document.AcquireCoordinates(linkInstanceId)` is present in the API but the exact sequencing (which document calls it, in which transaction, relative to `RevitLinkInstance.Create`) is not battle-tested in the codebase. Explicitly decide before Phase 6: either implement in v2.0 (requires design) or document as deferred to v2.x (document prominently in command UI).
- **Toposolid.Split exact behavior with non-enclosing cut loops:** The API returns new element IDs but the behavior when a cut CurveLoop only partially intersects the Toposolid boundary is not verified by test. Phase 4 implementation should include a single real-model smoke test before any edge-case handling is written.
- **`BuiltInFailures.SlabShapeFailures` handling:** Failure IDs (`SlabShapeWarnVerticesDeleted`, `SlabShapeEditFailed`) should be added to `SafeFailureHandler.PreprocessFailures` explicitly rather than caught generically. Confirm the exact failure IDs against the API export during Phase 1 or Phase 2 implementation.

---

## Sources

### Primary (HIGH confidence)

- `docs/review/revit-api/revit.autodesk.revit.db.public.members.jsonl` — Autodesk.Revit.DB all public member signatures, extracted from Revit 2026 installed assemblies (verified 2026-03-19)
- `docs/review/revit-api/revit.autodesk.revit.db.public.types.jsonl` — type hierarchy verification
- `docs/review/revit-api/revitapi.public.members.jsonl` — ApplicationServices.Application cross-namespace lookup
- `src/Services/SimplifyPointsService.cs` — SlabShapeEditor vertex iteration pattern (live codebase)
- `src/Services/AlignEdgesBoundaryCollectionService.cs` — SketchId + Sketch.Profile access pattern (live codebase)
- `src/Services/SlabService.cs` — DuplicateElement and TryResetSlabShape patterns (live codebase)
- `src/Services/TransactionService.cs` — transaction wrapping contract (live codebase)
- `src/Services/DeepPurgeService.cs` — multi-document transaction pattern (live codebase)
- `src/Services/OffsetService.cs` — FLOOR_HEIGHTABOVELEVEL_PARAM usage (live codebase)
- `src/Core/Bootstrapper.cs` — service registration inventory (live codebase)
- `src/Core/Ribbon/RibbonService.cs` — button registration conventions (live codebase)

### Secondary (MEDIUM confidence)

- `docs/review/05-revit-api.md` — project-specific Revit API usage notes
- `docs/review/14-current-debt-audit.md` — current codebase technical debt context
- `src/Commands/ChangeLevelCommand.cs`, `src/ViewModels/ChangeLevelViewModel.cs`, `src/Views/ChangeLevelView.xaml.cs` — UI/command pattern models

---

*Research completed: 2026-03-19*
*Ready for roadmap: yes*
