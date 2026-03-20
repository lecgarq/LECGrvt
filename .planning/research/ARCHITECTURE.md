# Architecture Research

**Domain:** Revit 2026 Addin — Geometry Operations Commands (v2.0 milestone)
**Researched:** 2026-03-19
**Confidence:** HIGH — based on direct source inspection of the existing codebase

---

## System Overview

The existing plugin follows a strict layered architecture. All new commands in the
v2.0 Geometry Operations milestone must slot into this same structure without
exceptions.

```
┌──────────────────────────────────────────────────────────────────┐
│                      REVIT RIBBON (UI Entry)                      │
│   Toposolids Panel (7 existing + 6 new buttons)                  │
│   Type to Linked Models → "Project Health" panel                 │
├──────────────────────────────────────────────────────────────────┤
│                        COMMANDS LAYER                            │
│  FloorToToposolidCommand   ToposolidToFloorCommand               │
│  FixPointsCommand          SplitBoundariesCommand                │
│  MergeElementsCommand      TypeToLinkedModelsCommand             │
│  (each inherits RevitCommand, decorated [Transaction(Manual)])   │
├──────────────────────────────────────────────────────────────────┤
│                       VIEWS + VIEWMODELS                         │
│  LecgWindow subclasses (XAML)                                    │
│  BaseViewModel / CommunityToolkit.Mvvm [ObservableProperty]      │
│  SelectionViewModel component (reused across all commands)       │
├──────────────────────────────────────────────────────────────────┤
│                        SERVICES LAYER                            │
│  New:  IGeometryBoundaryService  IFloorConversionService         │
│        IToposolidConversionService  IFixPointsService            │
│        ISplitBoundariesService  IMergeElementsService            │
│        ILinkedModelExportService                                 │
│  Existing (extended or reused):                                  │
│        ISlabService  IToposolidBaseElevationService              │
│        ITransactionService  IChangeLevelService  IOffsetService  │
├──────────────────────────────────────────────────────────────────┤
│                     REVIT 2026 API LAYER                         │
│  Floor  Toposolid  SlabShapeEditor  SlabShapeVertex  Sketch      │
│  Document  FilteredElementCollector  ElementTransformUtils       │
│  Application  ModelPath  RevitLinkInstance                       │
└──────────────────────────────────────────────────────────────────┘
```

---

## Component Responsibilities

| Component | Responsibility | Pattern |
|-----------|----------------|---------|
| `RevitCommand` subclass | Entry point; resolve services from `ServiceLocator`, open view, delegate all logic to services | Identical to all existing commands |
| `LecgWindow` subclass (View) | Show config UI, call `SelectionCoordinator.PickObjects` from button handler, pass refs to VM | Same as `ChangeLevelView`, `ResetSlabsView` |
| `BaseViewModel` subclass | Hold UI state, expose `Apply()` that calls service, `SelectionViewModel` as a property | Same as `ChangeLevelViewModel` |
| `SelectionViewModel` | Reusable component — tracks count, status text, filter, fires `OnRequestSelect` event | Already exists; use as-is |
| Domain service (new) | Contain all Revit API manipulation; accept `Document` and typed inputs; no UI awareness | Same shape as `SimplifyPointsService`, `ChangeLevelService` |
| `IGeometryBoundaryService` (new, shared) | Extract `Sketch` boundary loops from Floor or Toposolid as `CurveLoop` lists | Shared by Floor↔Toposolid, Split, Merge |
| `ITransactionService` | Wrap all mutations; already supports `Run`, `RunConditional`, `RunWithWarningHandler` | No changes needed |
| `Bootstrapper` | Register every new interface → concrete mapping as `AddSingleton` | Add one line per new service/VM/view |
| `RibbonService` | Add new buttons to `CreateToposolidsPanel` (and one to `CreateHealthPanel`) | Follow existing `CreateButton` calls |

---

## Recommended Project Structure

All new files follow the existing folder conventions exactly:

```
src/
├── Commands/
│   ├── FloorToToposolidCommand.cs        # new
│   ├── ToposolidToFloorCommand.cs        # new
│   ├── FixPointsCommand.cs               # new
│   ├── SplitBoundariesCommand.cs         # new
│   ├── MergeElementsCommand.cs           # new
│   └── TypeToLinkedModelsCommand.cs      # new
├── Services/
│   ├── GeometryBoundaryService.cs        # new — SHARED foundation
│   ├── FloorConversionService.cs         # new
│   ├── ToposolidConversionService.cs     # new
│   ├── FixPointsService.cs               # new
│   ├── SplitBoundariesService.cs         # new
│   ├── MergeElementsService.cs           # new
│   ├── LinkedModelExportService.cs       # new
│   └── Interfaces/
│       ├── IGeometryBoundaryService.cs   # new
│       ├── IFloorConversionService.cs    # new
│       ├── IToposolidConversionService.cs # new
│       ├── IFixPointsService.cs          # new
│       ├── ISplitBoundariesService.cs    # new
│       ├── IMergeElementsService.cs      # new
│       └── ILinkedModelExportService.cs  # new
├── ViewModels/
│   ├── FloorToToposolidViewModel.cs      # new
│   ├── ToposolidToFloorViewModel.cs      # new
│   ├── FixPointsViewModel.cs             # new
│   ├── SplitBoundariesViewModel.cs       # new
│   ├── MergeElementsViewModel.cs         # new
│   └── TypeToLinkedModelsViewModel.cs    # new
└── Views/
    ├── FloorToToposolidView.xaml/.cs     # new
    ├── ToposolidToFloorView.xaml/.cs     # new
    ├── FixPointsView.xaml/.cs            # new
    ├── SplitBoundariesView.xaml/.cs      # new
    ├── MergeElementsView.xaml/.cs        # new
    └── TypeToLinkedModelsView.xaml/.cs   # new
```

---

## Architectural Patterns

### Pattern 1: Standard Command Shell

Every new command follows exactly the same four-step Execute body as the existing commands.

**What:** Open config view first (never start by asking user to pick). View handles
selection internally via `SelectionCoordinator`. On OK, delegate processing to service.

**When to use:** All six new commands — no exceptions.

**Example (matches existing ChangeLevelCommand shape):**
```csharp
[Transaction(TransactionMode.Manual)]
public class FloorToToposolidCommand : RevitCommand
{
    public override void Execute(UIDocument uiDoc, Document doc)
    {
        var service = ServiceLocator.GetRequiredService<IFloorConversionService>();
        var vm      = ServiceLocator.GetRequiredService<FloorToToposolidViewModel>();
        vm.Initialize(doc);

        var view = ServiceLocator.GetRequiredService<FloorToToposolidView>();
        view.Initialize(uiDoc);
        if (view.ShowDialog() != true) return;

        ShowLogWindow("Floor to Toposolid");
    }
}
```

---

### Pattern 2: Shared Boundary Extraction Service

**What:** `IGeometryBoundaryService` is the single place that reads `element.SketchId`,
casts to `Sketch`, and returns `IList<CurveLoop>` (one per boundary loop). All four
commands that manipulate geometry boundaries (FloorToToposolid, ToposolidToFloor,
SplitBoundaries, MergeElements) consume this service rather than each duplicating
Sketch traversal.

**Why this is critical:** `AlignEdgesBoundaryCollectionService` already shows the
pattern — it holds `IAlignEdgesBoundaryPointService` and calls `doc.GetElement(toposolid.SketchId)`.
The new service generalises this to work on both `Floor` and `Toposolid`, returns
curve loops instead of XYZ points, and is scope-neutral (no intersector dependency).

**Interface shape:**
```csharp
public interface IGeometryBoundaryService
{
    // Returns one CurveLoop per boundary loop in the element's Sketch.
    // Works for both Floor and Toposolid.
    IList<CurveLoop> GetBoundaryLoops(Document doc, Element element);

    // True if the element has more than one independent boundary loop.
    bool HasMultipleBoundaries(Document doc, Element element);
}
```

---

### Pattern 3: Elevation Preservation via ToposolidBaseElevationService

**What:** Both conversion commands must preserve absolute elevation. The existing
`IToposolidBaseElevationService.Resolve(doc, toposolid)` already resolves
`(BaseElevation, DebugMessage)` by reading the level + height offset. For
Floor-to-Toposolid, the equivalent is reading `FLOOR_HEIGHTABOVELEVEL_PARAM`
(same parameter that `OffsetService` already uses). New conversion services should
depend on `IToposolidBaseElevationService` (Toposolid→Floor direction) and read
`FLOOR_HEIGHTABOVELEVEL_PARAM` directly for the Floor→Toposolid direction.

**Do not** recalculate elevation from geometry — always use the parameter path.

---

### Pattern 4: SlabShapeEditor Point Copying

**What:** For conversion commands that must preserve edited surface points
(SlabShapeVertex positions), the pattern is: enable `SlabShapeEditor` on the source,
iterate `SlabShapeVertices`, capture `XYZ` positions, create the target element, then
call `editor.AddPoint(xyz)` on the target's editor. `SimplifyPointsService` already
demonstrates the iteration; `SlabService.TryResetSlabShape` demonstrates editor
access. Conversion services will compose these.

**Key constraint:** `SlabShapeEditor` operations must happen inside the same
`TransactionService.Run` call that creates the target element, because the editor
context is bound to the active transaction.

---

### Pattern 5: Type to Linked Models — Multi-Document Transaction Boundary

**What:** This command uniquely requires creating new Revit project files, which
cannot happen inside a normal `TransactionService.Run` against the host document.
The pattern is:
1. Inside a host-document transaction: collect elements by type, delete from host
   (or hide/workset), record shared coordinate data.
2. Outside any host transaction: use `Application.NewProjectDocument(template)` to
   create each satellite file, then open a new `TransactionService.Run` scoped to
   the satellite document to place elements and set shared coordinates.
3. Save satellite with `SaveAsOptions`, then link back into host with
   `RevitLinkInstance`.

This is the only command that takes a dependency on `UIApplication` (for
`NewProjectDocument`) in addition to `Document`. The service interface should accept
`UIApplication` as a parameter on its primary method rather than storing it as state.

---

## Data Flow

### Conversion Commands (Floor↔Toposolid)

```
User clicks button
    → FloorToToposolidCommand.Execute(uiDoc, doc)
        → FloorToToposolidView.ShowDialog()
            → User picks elements via SelectionCoordinator.PickObjects (hides window)
            → User confirms settings
        → FloorConversionService.Convert(doc, refs, options)
            → IGeometryBoundaryService.GetBoundaryLoops(doc, floor)   [read]
            → Read FLOOR_HEIGHTABOVELEVEL_PARAM                        [read]
            → Read SlabShapeEditor vertices                            [read]
            → ITransactionService.Run(doc, "Floor to Toposolid", d => {
                  Toposolid.Create(d, curveLoops, level, topoType)   [write]
                  Set height offset param                              [write]
                  editor.Enable(); editor.AddPoint(xyz) * N           [write]
                  d.Delete(original.Id)                               [write]
              })
```

### Split Boundaries

```
User picks multi-boundary element
    → SplitBoundariesService.Split(doc, element)
        → IGeometryBoundaryService.GetBoundaryLoops(doc, element)    [read — shared]
        → Read elevation params                                        [read]
        → ITransactionService.Run(doc, "Split Boundaries", d => {
              foreach loop in loops:
                  Create new element with single loop                 [write]
                  Copy elevation params                               [write]
                  Copy type                                           [write]
              d.Delete(original.Id)                                   [write]
          })
```

### Merge Elements

```
User picks 2+ elements of same category
    → MergeElementsService.Merge(doc, elements)
        → IGeometryBoundaryService.GetBoundaryLoops per element       [read — shared]
        → Collect all loops into one list                              [combine]
        → ITransactionService.Run(doc, "Merge Elements", d => {
              Create one new element with all loops                   [write]
              Set elevation from first/dominant element               [write]
              d.Delete(original.Ids)                                  [write]
          })
```

### Fix Points

```
User picks elements
    → FixPointsService.Fix(doc, elements, reporter)
        → ITransactionService.Run(doc, "Fix Points", d => {
              foreach element:
                  editor = element.GetSlabShapeEditor()
                  Identify inconsistent edge/transition vertices       [read]
                  Snap or remove problem vertices                      [write]
          })
```

---

## Integration Points with Existing Services

### Services That New Commands Consume Directly

| Existing Service | Used By | How |
|-----------------|---------|-----|
| `ITransactionService` | All 6 new services | Wrap every Revit mutation; use `RunWithWarningHandler` for point operations that may generate warnings |
| `ISlabService.DuplicateElement` | FloorConversion, ToposolidConversion | Create working copy before delete if "keep original" option is enabled |
| `ISlabService.TryResetSlabShape` | FixPointsService | Call before re-inserting corrected points |
| `IToposolidBaseElevationService` | ToposolidConversion (Toposolid→Floor direction) | Read base elevation to transfer to Floor height offset |
| `IChangeLevelService.GetLevels` | FloorConversion, ToposolidConversion, SplitBoundaries | Populate level dropdown in config view |
| `IOffsetService.TryOffsetElement` | Post-conversion elevation correction | Apply after element creation if elevation drift detected |

### Services That Need No Changes

`IAlignEdgesBoundaryCollectionService` and `IAlignEdgesBoundaryPointService` are
point-collection services specific to the Align Edges workflow (they depend on
`ReferenceIntersector`). They are NOT reused for the new commands — the new shared
`IGeometryBoundaryService` takes a different approach (curve loop extraction from
Sketch, no intersector).

`ISimplifyPointsService` does not need extension. `FixPointsService` will directly
use `SlabShapeEditor` via the same `editor.DeletePoint` / `editor.AddPoint` API that
`SimplifyPointsService` already demonstrates.

### Services That Get Extended

**`ISlabService`** should gain two new methods to avoid duplicating Revit API code
across the new services:

```csharp
// Add to ISlabService:
IReadOnlyList<XYZ> GetEditorVertexPositions(Element element);
void ApplyVertices(Element element, IReadOnlyList<XYZ> positions);
```

These wrap `SlabShapeEditor` enable + vertex iteration + `AddPoint` in one call,
keeping the pattern reusable across conversion and fix-points commands.

---

## New Components Summary

### New Services (all registered as `AddSingleton` in Bootstrapper)

| Interface | Concrete | Primary Dependency | Shared By |
|-----------|----------|-------------------|-----------|
| `IGeometryBoundaryService` | `GeometryBoundaryService` | — (pure Revit API) | FloorConversion, ToposolidConversion, SplitBoundaries, MergeElements |
| `IFloorConversionService` | `FloorConversionService` | `IGeometryBoundaryService`, `ISlabService`, `ITransactionService` | FloorToToposolidCommand |
| `IToposolidConversionService` | `ToposolidConversionService` | `IGeometryBoundaryService`, `ISlabService`, `IToposolidBaseElevationService`, `ITransactionService` | ToposolidToFloorCommand |
| `IFixPointsService` | `FixPointsService` | `ISlabService`, `ITransactionService` | FixPointsCommand |
| `ISplitBoundariesService` | `SplitBoundariesService` | `IGeometryBoundaryService`, `ITransactionService` | SplitBoundariesCommand |
| `IMergeElementsService` | `MergeElementsService` | `IGeometryBoundaryService`, `ITransactionService` | MergeElementsCommand |
| `ILinkedModelExportService` | `LinkedModelExportService` | `ITransactionService` | TypeToLinkedModelsCommand |

### New ViewModels (all registered as `AddTransient` in Bootstrapper)

| ViewModel | Key State |
|-----------|-----------|
| `FloorToToposolidViewModel` | `SelectionViewModel`, target `ToposolidType`, `Level`, `KeepOriginal` flag |
| `ToposolidToFloorViewModel` | `SelectionViewModel`, target `FloorType`, `Level` |
| `FixPointsViewModel` | `SelectionViewModel`, tolerance slider |
| `SplitBoundariesViewModel` | `SelectionViewModel`, preview count of resulting elements |
| `MergeElementsViewModel` | `SelectionViewModel`, dominant element selector (for elevation source) |
| `TypeToLinkedModelsViewModel` | `SelectionViewModel`, output folder path, template path, dry-run toggle |

### New Views (all registered as `AddTransient` in Bootstrapper)

All inherit `LecgWindow`. All use `SelectionViewModel.OnRequestSelect` event pattern
as seen in `ChangeLevelView` to trigger `SelectionCoordinator.PickObjects` from the
view's code-behind.

### Ribbon Changes

Add to `CreateToposolidsPanel` in `RibbonService`:
- `FloorToToposolidCommand` button
- `ToposolidToFloorCommand` button
- `FixPointsCommand` button
- `SplitBoundariesCommand` button
- `MergeElementsCommand` button

Add to `CreateHealthPanel` in `RibbonService`:
- `TypeToLinkedModelsCommand` button

Add corresponding constants to `UIConstants` and `AppImages` for each button.

---

## Suggested Build Order (dependency-aware)

Build in this order so each phase compiles and can be tested independently before
the next depends on it.

### Phase 1 — Shared Foundation (blocks everything else)

Build `IGeometryBoundaryService` / `GeometryBoundaryService` first. Extend `ISlabService`
with `GetEditorVertexPositions` and `ApplyVertices`. These two components are
dependencies for four of the six commands.

**Deliverables:** 1 new service + 2 new methods on existing interface. Register in Bootstrapper.

### Phase 2 — Conversion Pair (depends on Phase 1)

Build Floor→Toposolid and Toposolid→Floor together because they are mirrors of each
other and share the same discovery of edge cases (slab points, elevation parameter
names, multi-loop boundaries). Developing them side by side prevents solving the same
problem twice with different approaches.

**Deliverables:** `IFloorConversionService`, `IToposolidConversionService`, both
commands, VMs, views. Add to ribbon.

### Phase 3 — Fix Points (depends on Phase 1, can run parallel with Phase 2)

Fix Points only needs `ISlabService` (extended) and `ITransactionService`. It is
self-contained and the simplest of the six commands. Build after Phase 1 is stable.

**Deliverables:** `IFixPointsService`, command, VM, view. Add to ribbon.

### Phase 4 — Split Boundaries (depends on Phase 1 + Phase 2 patterns)

Split Boundaries uses `IGeometryBoundaryService` and the same element-creation
pattern established in Phase 2. Building after Phase 2 means the edge cases around
Sketch loop extraction are already understood.

**Deliverables:** `ISplitBoundariesService`, command, VM, view. Add to ribbon.

### Phase 5 — Merge Elements (depends on Phase 4)

Merge is the inverse of Split. The multi-loop creation path is proven by Phase 4.
Merge adds the extra step of combining loops from different source elements, which
requires understanding how Revit handles overlapping or adjacent CurveLoops (a risk
that Phase 4 will have already surfaced).

**Deliverables:** `IMergeElementsService`, command, VM, view. Add to ribbon.

### Phase 6 — Type to Linked Models (independent, build last)

Completely separate from the surface manipulation commands. Has the highest complexity
(multi-document operations, file I/O, shared coordinates, link insertion). Build last
so it does not block any other feature and can be spec'd more carefully once the
rest of the milestone is stable.

**Deliverables:** `ILinkedModelExportService`, command, VM, view. Add to Health panel.

---

## Anti-Patterns

### Anti-Pattern 1: Calling Revit API Outside TransactionService

**What people do:** Call `element.GetSlabShapeEditor().AddPoint(xyz)` directly in
the command or viewmodel because "it's just one line."

**Why it's wrong:** Any Revit API write outside a transaction crashes with
`InvalidOperationException` or silently fails without rollback on error. The existing
`ResetSlabsCommand` and `SimplifyPointsService` consistently push all writes into
`_transactionService.Run`.

**Do this instead:** All SlabShapeEditor modifications, element creation/deletion,
and parameter writes go inside `TransactionService.Run` callbacks.

---

### Anti-Pattern 2: Duplicating Sketch Boundary Extraction

**What people do:** Each conversion/split/merge service copies its own `doc.GetElement(element.SketchId)` block.

**Why it's wrong:** The Revit Sketch API has subtle failure modes (SketchId can be
invalid for certain Floor subtypes, loop ordering is not guaranteed). Duplicating the
code means the same bug must be fixed in four places.

**Do this instead:** `IGeometryBoundaryService` is the one place this logic lives.
All four services that need boundary loops inject it as a constructor dependency.

---

### Anti-Pattern 3: Starting With Element Selection Before Config Window

**What people do:** Execute → immediately call `uiDoc.Selection.PickObjects` before
showing any UI.

**Why it's wrong:** Violates the project's core interaction contract. All existing
commands show the config view first. Selection happens via the "Select" button inside
the view.

**Do this instead:** `Execute` opens the view via `ShowDialog`. The view's "Select"
button fires `SelectionViewModel.OnRequestSelect`, which triggers the view's
code-behind to call `SelectionCoordinator.PickObjects` (which hides the window during
pick and restores it after).

---

### Anti-Pattern 4: Service State That Holds Revit API Objects

**What people do:** Store `Document`, `Element`, or `SlabShapeEditor` as service
fields so they don't need to pass them around.

**Why it's wrong:** Services are registered as `AddSingleton`. Revit documents are
per-command-invocation objects. Holding a document reference across invocations causes
stale-document exceptions and is the source of the hardest-to-diagnose crashes in
Revit addins.

**Do this instead:** All services receive `Document` as a method parameter, never as
a constructor or field. This is the pattern every existing service follows without
exception.

---

### Anti-Pattern 5: Creating New Revit Documents Inside a Host Transaction

**What people do:** Call `Application.NewProjectDocument()` inside a
`TransactionService.Run(hostDoc, ...)` block.

**Why it's wrong:** Revit does not allow opening a new document while a transaction
is active on another document. It will throw or corrupt both documents.

**Do this instead:** In `TypeToLinkedModelsCommand`, collect data inside the host
transaction, commit it, then create satellite documents outside any transaction.
Satellite-document writes get their own `TransactionService.Run(satelliteDoc, ...)`.

---

## Sources

- Direct source inspection of `C:/LECG/RevitAddins/LECG/src/` (HIGH confidence)
- Existing patterns: `ChangeLevelService`, `SimplifyPointsService`, `AlignEdgesBoundaryCollectionService`, `SlabService`, `TransactionService`, `ResetSlabsCommand`
- `Bootstrapper.cs` — full service registration inventory
- `RibbonService.cs` — existing panel layout and button registration conventions
- `SelectionCoordinator.cs` and `SelectionViewModel.cs` — selection coordination pattern
- Revit 2026 API: `Toposolid.Create`, `Floor.Create`, `SlabShapeEditor`, `Sketch.SketchId`, `Application.NewProjectDocument` — training data, confidence MEDIUM for exact method signatures (verify against Revit 2026 API docs during implementation)

---

*Architecture research for: LECG v2.0 Geometry Operations milestone*
*Researched: 2026-03-19*
