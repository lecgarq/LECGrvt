# Stack Research

**Domain:** Revit 2026 addin — geometry operations commands (Floor/Toposolid conversion, repair, split, merge, type separation)
**Researched:** 2026-03-19
**Confidence:** HIGH — all API signatures verified against local Revit 2026 DLL reflection export at `docs/review/revit-api/`

---

## Validated Existing Stack (Do Not Change)

| Technology | Version | Role |
|------------|---------|------|
| net8.0-windows | .NET 8 | Target framework — Revit 2026 addin host |
| WPF | .NET 8 | UI layer |
| Revit 2026 API | 2026 | RevitAPI.dll + RevitAPIUI.dll |
| CommunityToolkit.Mvvm | 8.2.2 | ViewModel base, source generators |
| SimpleDi | internal | Lightweight DI container |

No new NuGet packages are needed for this milestone. All required capability is in the existing Revit 2026 API assemblies.

---

## Core Technologies: New API Surface

### Group 1 — Element Creation (Conversion Commands)

| Class | Namespace | Verified Signature | Purpose |
|-------|-----------|-------------------|---------|
| `Toposolid` (static) | `Autodesk.Revit.DB` | `Toposolid.Create(Document, IList<CurveLoop>, IList<XYZ>, ElementId topoTypeId, ElementId levelId)` | Floor-to-Toposolid: create with boundary + interior elevation points in one call |
| `Toposolid` (static) | `Autodesk.Revit.DB` | `Toposolid.Create(Document, IList<CurveLoop>, ElementId topoTypeId, ElementId levelId)` | Simpler overload — boundary only, no interior points |
| `Toposolid` (static) | `Autodesk.Revit.DB` | `Toposolid.Create(Document, IList<XYZ>, ElementId topoTypeId, ElementId levelId)` | Points-only overload — use when boundary is implicit |
| `Floor` (static) | `Autodesk.Revit.DB` | `Floor.Create(Document, IList<CurveLoop>, ElementId floorTypeId, ElementId levelId)` | Toposolid-to-Floor: create from extracted boundary loops |
| `Floor` (static) | `Autodesk.Revit.DB` | `Floor.Create(Document, IList<CurveLoop>, ElementId floorTypeId, ElementId levelId, bool isStructural, Line slopeArrow, double slope)` | Extended overload — use only if slope must be preserved |
| `Floor` (static) | `Autodesk.Revit.DB` | `Floor.GetDefaultFloorType(Document, bool isFoundation)` | Fallback type resolution when source has no matching Floor type |

**Why `Toposolid.Create` with points overload:** The interior `IList<XYZ>` parameter is the mechanism for transferring `SlabShapeEditor` vertex positions (internal shape points) from the source Floor into the new Toposolid. This is the only way to preserve edited elevations in the conversion without relying on `SlabShapeEditor` re-entry on the new element.

---

### Group 2 — Shape Point Read/Write (All Conversion + Fix Points Commands)

| Class | Namespace | Verified Members | Purpose |
|-------|-----------|-----------------|---------|
| `SlabShapeEditor` | `Autodesk.Revit.DB` | `Enable()`, `ResetSlabShape()`, `IsEnabled` (bool), `SlabShapeVertices` (SlabShapeVertexArray), `SlabShapeCreases` (SlabShapeCreaseArray) | Gate: must call `Enable()` before any read/write; already used in `SimplifyPointsService` |
| `SlabShapeEditor` | `Autodesk.Revit.DB` | `AddPoint(XYZ) : SlabShapeVertex`, `AddPoints(IList<XYZ>) : IList<SlabShapeVertex>` | Write elevation points to a new element — primary tool for Fix Points and conversion replay |
| `SlabShapeEditor` | `Autodesk.Revit.DB` | `ModifySubElement(SlabShapeVertex, double offset)`, `ModifySubElement(SlabShapeCrease, double offset)` | Adjust elevation of specific vertex/crease by offset in feet — used for Fix Points alignment |
| `SlabShapeEditor` | `Autodesk.Revit.DB` | `DeletePoint(SlabShapeVertex) : bool` | Remove orphaned or inconsistent vertices — Fix Points cleanup path |
| `SlabShapeEditor` | `Autodesk.Revit.DB` | `AddSplitLine(SlabShapeVertex, SlabShapeVertex) : IList<SlabShapeCrease>` | Create crease lines between vertices — needed if converted element must replicate crease geometry |
| `SlabShapeVertex` | `Autodesk.Revit.DB` | `.Position` (XYZ), `.VertexType` (SlabShapeVertexType) | Read vertex world position and type (Interior vs Boundary) — used to filter which points to transfer |
| `SlabShapeVertexType` | `Autodesk.Revit.DB` | Enum | Discriminate boundary-driven vertices (not transferable) from user-added interior points (transferable) |

**Acquisition pattern** (already proven in `SimplifyPointsService`):
```csharp
SlabShapeEditor editor = floor.GetSlabShapeEditor(); // or toposolid.GetSlabShapeEditor()
if (!editor.IsEnabled) editor.Enable();
var vertices = editor.SlabShapeVertices.Cast<SlabShapeVertex>().ToList();
```

Both `Floor.GetSlabShapeEditor()` and `Toposolid.GetSlabShapeEditor()` are verified present with identical signature.

---

### Group 3 — Boundary Extraction (Split, Merge, Conversion Commands)

| Class | Namespace | Verified Members | Purpose |
|-------|-----------|-----------------|---------|
| `Sketch` | `Autodesk.Revit.DB` | `.Profile` (CurveArrArray), `.SketchPlane` (SketchPlane) | Primary boundary source — both Floor and Toposolid expose `.SketchId`; cast retrieved element to `Sketch` |
| `Floor` | `Autodesk.Revit.DB` | `.SketchId` (ElementId) | Navigate from Floor to its Sketch |
| `Toposolid` | `Autodesk.Revit.DB` | `.SketchId` (ElementId) | Navigate from Toposolid to its Sketch |
| `CurveLoop` | `Autodesk.Revit.DB` | `CurveLoop.Create(IList<Curve>)`, `.Append(Curve)`, `.IsOpen()`, `.IsCounterclockwise(XYZ normal)`, `.Flip()`, `.GetCurveLoopIterator()`, `.NumberOfCurves()` | Build, inspect, and orient boundary loops for `Floor.Create` / `Toposolid.Create` |
| `CurveLoop` (static) | `Autodesk.Revit.DB` | `CurveLoop.CreateViaCopy(CurveLoop)`, `CurveLoop.CreateViaTransform(CurveLoop, Transform)` | Clone loops without mutation — required when merging or splitting boundaries across elements |

**Boundary extraction pattern** (already used in `AlignEdgesBoundaryCollectionService`):
```csharp
Sketch sketch = doc.GetElement(element.SketchId) as Sketch;
CurveArrArray profile = sketch.Profile; // outer + inner loops
// Convert CurveArray -> CurveLoop.Create(curves.Cast<Curve>().ToList())
```

`Sketch.Profile` returns a `CurveArrArray` (not `IList<CurveLoop>`). Conversion to `CurveLoop` objects is needed before passing to `Floor.Create` or `Toposolid.Create`.

**For Merge Elements:** Collect all boundary `CurveLoop` objects from each source element. The outer loop of the merged result must be a single `CurveLoop` enclosing all sources. If sources are adjacent (share an edge), manually construct the union boundary. If sources overlap, the Revit API does not provide planar polygon union — implement a simple XY convex hull or use `CurveLoop` concatenation for non-overlapping adjacent cases.

---

### Group 4 — Toposolid-Specific (Conversion + Split Commands)

| Class | Namespace | Verified Signature | Purpose |
|-------|-----------|-------------------|---------|
| `Toposolid` | `Autodesk.Revit.DB` | `.Split(IList<CurveLoop> splitCurveLoops) : IList<ElementId>` | Native split — given cutting loops returns new element IDs. Use for Split Boundaries when source is Toposolid |
| `Toposolid` | `Autodesk.Revit.DB` | `.HostTopoId` (ElementId) | Detect if Toposolid is a subdivision (HostTopoId != InvalidElementId) — affects split/merge eligibility |
| `Toposolid` | `Autodesk.Revit.DB` | `.GetSubDivisionIds() : IList<ElementId>` | Enumerate sub-divisions before merge to avoid orphaned subdivisions |
| `Toposolid` | `Autodesk.Revit.DB` | `.CreateSubDivision(Document, ElementId topoTypeId, IList<CurveLoop>) : Toposolid` | Create subdivision on a host Toposolid — not needed for main commands but available |

**Note on `Toposolid.Split`:** This is an instance method (not static). It requires an active transaction and the split loop must lie within the boundary of the host Toposolid. The returned `IList<ElementId>` contains the new fragment IDs; the original element is modified in place to become one of the fragments.

**Floor has no `.Split` equivalent.** For Split Boundaries on Floor elements: extract each `CurveLoop` from `Sketch.Profile` as separate loops, call `Floor.Create` for each, then `Document.Delete` the original.

---

### Group 5 — Document Operations (Type to Linked Models Command)

| Class | Namespace | Verified Signature | Purpose |
|-------|-----------|-------------------|---------|
| `Application` | `Autodesk.Revit.ApplicationServices` | `Application.NewProjectDocument(string templateFileName) : Document` | Create blank target document from a template `.rvt` file — accessed via `doc.Application` |
| `Application` | `Autodesk.Revit.ApplicationServices` | `Application.NewProjectDocument(UnitSystem) : Document` | Alternative: blank doc with no template (metric/imperial) |
| `Document` | `Autodesk.Revit.DB` | `Document.SaveAs(string filepath, SaveAsOptions)`, `Document.SaveAs(ModelPath, SaveAsOptions)` | Save new document to disk before linking |
| `Document` | `Autodesk.Revit.DB` | `Document.Close(bool saveModified) : bool` | Close the sub-document after saving — required to avoid open document accumulation |
| `ElementTransformUtils` | `Autodesk.Revit.DB` | `CopyElements(Document source, ICollection<ElementId>, Document destination, Transform, CopyPasteOptions) : ICollection<ElementId>` | Copy elements cross-document — already used in `SlabService.DuplicateElement` for same-doc; cross-doc variant is the same static method |
| `ModelPathUtils` | `Autodesk.Revit.DB` | `ModelPathUtils.ConvertUserVisiblePathToModelPath(string) : ModelPath` | Convert file path string to `ModelPath` for `RevitLinkType.Create` |
| `RevitLinkType` | `Autodesk.Revit.DB` | `RevitLinkType.Create(Document, ModelPath, RevitLinkOptions) : LinkLoadResult` | Add the saved sub-document as a link type in the host document |
| `RevitLinkInstance` | `Autodesk.Revit.DB` | `RevitLinkInstance.Create(Document, ElementId revitLinkTypeId) : RevitLinkInstance` | Place the link instance at origin (shared coordinates apply) |
| `RevitLinkInstance` | `Autodesk.Revit.DB` | `RevitLinkInstance.Create(Document, ElementId, ImportPlacement) : RevitLinkInstance` | Place with explicit placement enum — use `ImportPlacement.Shared` to inherit project coordinates |
| `Document` | `Autodesk.Revit.DB` | `Document.AcquireCoordinates(ElementId linkInstanceId)` | Transfer shared coordinate system from host to linked doc — must be called in sub-document after linking OR call in reverse |

**Type to Linked workflow sequence:**
1. Group source elements by type name.
2. For each type group:
   a. `doc.Application.NewProjectDocument(templatePath)` — open blank sub-doc.
   b. `ElementTransformUtils.CopyElements(hostDoc, elementIds, subDoc, Transform.Identity, options)` — copy elements.
   c. `subDoc.SaveAs(outputPath, saveAsOptions)`.
   d. `subDoc.Close(false)`.
3. Back in host doc transaction:
   a. `RevitLinkType.Create(hostDoc, ModelPathUtils.ConvertUserVisiblePathToModelPath(outputPath), new RevitLinkOptions(false))` — `false` = not overlay.
   b. `RevitLinkInstance.Create(hostDoc, linkTypeId, ImportPlacement.Shared)` — places at shared origin.
   c. `hostDoc.Delete(originalElementIds)` — remove originals.

**Shared coordinates:** `ImportPlacement.Shared` on `RevitLinkInstance.Create` aligns the link using project shared coordinates. No additional `AcquireCoordinates` call needed if the source and sub-document share the same base point (which they will when elements are copied with `Transform.Identity`).

---

### Group 6 — Element Lifecycle (All Commands)

| Class | Namespace | Verified Signature | Purpose |
|-------|-----------|-------------------|---------|
| `Document` | `Autodesk.Revit.DB` | `Document.Delete(ICollection<ElementId>) : ICollection<ElementId>` | Delete source elements after conversion/merge — standard pattern |
| `Document` | `Autodesk.Revit.DB` | `Document.Delete(ElementId) : ICollection<ElementId>` | Single-element variant |
| `Element` | `Autodesk.Revit.DB` | `Element.GetTypeId() : ElementId` | Read element type for grouping (Type to Linked) and type-matching (conversions) |
| `Element` | `Autodesk.Revit.DB` | `Element.ChangeTypeId(ElementId) : ElementId` | Reassign type to converted element — use after `Floor.Create` to match source Floor type |
| `FilteredElementCollector` | `Autodesk.Revit.DB` | `.OfClass(Type).Cast<T>()` | Collect existing Floor/Toposolid types for type resolution — already used throughout codebase |

---

## Supporting API (Already Available, Referenced Here for Clarity)

| Class | Namespace | Purpose for New Commands |
|-------|-----------|--------------------------|
| `TransactionService` | `LECG.Services` | All Revit modifications go through this — wrap each conversion in `transactionService.Run(...)` |
| `ElementTransformUtils.CopyElements(doc, ids, doc, Transform.Identity, options)` | `Autodesk.Revit.DB` | Same-doc duplication — already in `SlabService.DuplicateElement`; reuse for pre-conversion safety copy |
| `Parameter` / `BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM` | `Autodesk.Revit.DB` | Elevation offset read/write — already in `OffsetService`; reuse for elevation transfer in conversions |
| `UnitUtils.ConvertFromInternalUnits` / `ConvertToInternalUnits` | `Autodesk.Revit.DB` | Feet ↔ display unit conversion — already used throughout |
| `SelectionCoordinator` | `LECG.Core` | Hide window during element pick — all new commands that take user selection use this |
| `SafeFailureHandler` | `LECG.Core` | Suppress non-critical failures in transactions — attach via `TransactionService.Run` default |

---

## Alternatives Considered

| Recommended | Alternative | Why Not |
|-------------|-------------|---------|
| `Toposolid.Create(doc, loops, points, typeId, levelId)` | Post-create `SlabShapeEditor.AddPoints` | Create-with-points is atomic; post-create AddPoints requires a second transaction and risks partial failure if editor is unavailable |
| Extract boundary from `Sketch.Profile` via `element.SketchId` | Use geometry API (`element.get_Geometry()`) to extract bottom face edges | Sketch gives exact model curves (lines, arcs) matching the original; geometry API returns tessellated approximations in some cases |
| `Toposolid.Split(loops)` for Toposolid split | Duplicate + boundary reclip | `.Split` is native and preserves sub-element data correctly; manual approach risks inconsistent vertex state |
| `Floor.Create` per-loop for Floor split | Modify sketch in edit mode | Edit mode is not API-accessible; per-loop `Floor.Create` from extracted sketch loops is the correct pattern |
| `Application.NewProjectDocument(templatePath)` for sub-documents | Copy current document then strip elements | NewProjectDocument + CopyElements gives a clean doc with only what's needed; copy-then-strip is fragile for large models |
| `ImportPlacement.Shared` in `RevitLinkInstance.Create` | Manually set link transform after placement | Shared placement is correct by definition when coordinates are shared; manual transform calculation is error-prone |

---

## What NOT to Add

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| Any new NuGet package for geometry | All needed geometry operations (boundary extraction, boolean on solids) are in RevitAPI.dll | `BooleanOperationsUtils`, `CurveLoop`, `Sketch.Profile` from existing API |
| `BooleanOperationsUtils.ExecuteBooleanOperation` for Merge | Operates on `Solid` 3D geometry, not on 2D boundary loops; result is not re-assignable back to a Floor/Toposolid boundary | Manually construct merged `CurveLoop` from adjacent element boundaries |
| `Toposolid.Simplify(percentage)` | Destroys precision — removes interior points by percentage, not by geometric equivalence | Use `SimplifyPointsService` (already exists) for point cleanup |
| Direct `Sketch` editing (adding/removing curves in sketch) | Sketch modification API is not publicly exposed for Floor/Toposolid in Revit 2026 | Always extract + `Document.Delete` original + `Floor.Create` / `Toposolid.Create` with new boundary |
| `ExternalResource` overload of `RevitLinkType.Create` | Requires a server registration for external resources; local file linking uses `ModelPath` overload | `RevitLinkType.Create(Document, ModelPath, RevitLinkOptions)` |

---

## Stack Patterns by Feature

**Floor to Toposolid conversion:**
- Read: `floor.SketchId` → `Sketch.Profile` (boundary loops) + `floor.GetSlabShapeEditor().SlabShapeVertices` (interior XYZ points where `VertexType == Interior`)
- Write: `Toposolid.Create(doc, curveLoops, interiorPoints, matchedTopoTypeId, floor.LevelId)`
- Cleanup: `doc.Delete(floor.Id)`, optionally transfer `FLOOR_HEIGHTABOVELEVEL_PARAM`

**Toposolid to Floor conversion:**
- Read: `toposolid.SketchId` → `Sketch.Profile` + `toposolid.GetSlabShapeEditor().SlabShapeVertices`
- Write: `Floor.Create(doc, curveLoops, matchedFloorTypeId, toposolid.LevelId)` then `editor.AddPoints(interiorPoints)` with `ModifySubElement` for elevation offsets
- Cleanup: `doc.Delete(toposolid.Id)`

**Fix Points:**
- Read: `element.GetSlabShapeEditor().SlabShapeVertices` filtered by `VertexType`
- Write: `editor.ModifySubElement(vertex, correctedOffset)` or `editor.DeletePoint(vertex)` for orphans
- No element creation/deletion

**Split Boundaries (Toposolid):**
- Each boundary `CurveLoop` from `Sketch.Profile` treated as an independent split region
- Call `toposolid.Split(new List<CurveLoop> { singleLoop })` per loop — returns new element IDs

**Split Boundaries (Floor):**
- Collect all `CurveLoop` objects from `Sketch.Profile`
- `foreach loop: Floor.Create(doc, [loop], typeId, levelId)` then `editor.AddPoints` with matching interior points
- `doc.Delete(originalFloor.Id)`

**Merge Elements:**
- Collect boundary loops from all sources; construct outer enclosing `CurveLoop`
- Collect all interior XYZ shape points from all source editors
- `Toposolid.Create` or `Floor.Create` with merged boundary + all interior points
- `doc.Delete(allSourceIds)`

**Type to Linked Models:**
- Group by `element.GetTypeId()` → type name
- Per group: `doc.Application.NewProjectDocument(templatePath)` → `ElementTransformUtils.CopyElements(cross-doc)` → `subDoc.SaveAs(path)` → `subDoc.Close(false)`
- Per saved file: `RevitLinkType.Create(hostDoc, ModelPathUtils.ConvertUserVisiblePathToModelPath(path), new RevitLinkOptions(false))` → `RevitLinkInstance.Create(hostDoc, linkTypeId, ImportPlacement.Shared)`
- `doc.Delete(originalIds)` in host

---

## Version Compatibility

| API | Revit Version | Notes |
|-----|--------------|-------|
| `Toposolid.Create(doc, loops, points, typeId, levelId)` | 2024+ | Toposolid is a 2024 API addition; verified present in 2026 export |
| `Floor.Create(doc, loops, typeId, levelId)` | 2022+ | Replaced deprecated `Document.NewFloor` |
| `Toposolid.Split(loops)` | 2024+ | Instance method — verified in 2026 export |
| `RevitLinkInstance.Create(doc, typeId, ImportPlacement)` | 2019+ | Stable across versions; `ImportPlacement.Shared` confirmed in 2026 |
| `Application.NewProjectDocument(templatePath)` | All versions | Confirmed in revitapi.public.members.jsonl |
| `SlabShapeEditor.AddPoints(IList<XYZ>)` | 2018+ | Batch overload verified in 2026 export |

All signatures above were verified against `docs/review/revit-api/revit.autodesk.revit.db.public.members.jsonl` and `revitapi.public.members.jsonl` — local DLL reflection exports from `C:\Program Files\Autodesk\Revit 2026`.

---

## Sources

- `docs/review/revit-api/revit.autodesk.revit.db.public.members.jsonl` — Autodesk.Revit.DB namespace, all public member signatures (verified 2026-03-19)
- `docs/review/revit-api/revit.autodesk.revit.db.public.types.jsonl` — type hierarchy verification
- `docs/review/revit-api/revitapi.public.members.jsonl` — cross-namespace lookup (ApplicationServices.Application)
- `docs/review/revit-api/revit.autodesk.revit.ui.public.members.jsonl` — UIApplication.OpenAndActivateDocument
- Existing codebase: `src/Services/SlabService.cs`, `SimplifyPointsService.cs`, `AlignEdgesBoundaryCollectionService.cs`, `OffsetService.cs`, `TransactionService.cs` — integration reference

---

*Stack research for: Revit 2026 Geometry Operations Commands (v2.0 milestone)*
*Researched: 2026-03-19*
