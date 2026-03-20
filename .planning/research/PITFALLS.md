# Pitfalls Research

**Domain:** Revit 2026 Floor/Toposolid geometry operations — conversion, repair, split, merge, document export
**Researched:** 2026-03-19
**Confidence:** HIGH (primary sources: local Revit 2026 API export via `docs/review/revit-api/`, live codebase inspection)

---

## Critical Pitfalls

### Pitfall 1: SlabShapeVertex objects become stale after any ModifySubElement call within the same transaction

**What goes wrong:**
A `SlabShapeVertex` reference obtained from `editor.SlabShapeVertices` is a native-wrapped pointer. After calling `editor.ModifySubElement(vertex, offset)` on any vertex, all previously cached vertex references in the current iteration may be invalid or point to out-of-date positions. Iterating a captured list while modifying vertices leads to operating on stale geometry, producing mismatched elevations on the new element.

**Why it happens:**
`SlabShapeVertices` returns a live collection backed by native memory. Revit internally regenerates the slab shape after each `ModifySubElement` call. The existing C# wrapper objects do not automatically update their `Position` property to reflect post-modification state. This was encountered in the existing `SimplifyPointsService`, where each `editor.DeletePoint(v)` can silently fail because the underlying native object has already been invalidated.

**How to avoid:**
Snapshot the vertex `Position` values (`vertex.Position`) into a plain `List<XYZ>` before entering the modification loop. Use the XYZ values, not vertex object references, as the source of truth for all elevation math. For Floor-to-Toposolid conversion: collect `(XYZ position, double relativeOffset)` pairs before any modification. For Toposolid-to-Floor: use `AddPoints` on `SlabShapeEditor` in a second transaction after the Floor is created, not in the same transaction as element creation.

**Warning signs:**
- Conversion produces a flat element even though source had shape points
- `ModifySubElement` calls silently succeed but the resulting surface is wrong
- `editor.SlabShapeVertices.Cast<SlabShapeVertex>().ToList()` count changes mid-loop without explicit `DeletePoint` calls

**Phase to address:**
Floor-to-Toposolid conversion phase and Toposolid-to-Floor conversion phase. The vertex snapshot pattern must be established as a service contract before any conversion service accesses `SlabShapeEditor`.

---

### Pitfall 2: Toposolid.Create requires a CurveLoop whose first loop is counterclockwise (viewed from above); Floor.Create requires the opposite winding for outer boundary

**What goes wrong:**
`Toposolid.Create(document, profiles, points, topoTypeId, levelId)` requires the outer boundary `CurveLoop` to be counterclockwise when viewed from above (positive Z normal), matching `CurveLoop.IsCounterclockwise(XYZ.BasisZ) == true`. `Floor.Create` requires the outer boundary to be counterclockwise as well when viewed from above, but silently re-orients curves in some cases. When converting between the two, copying the `Sketch.Profile` (a `CurveArrArray`) directly without checking orientation can cause the new element to reject the profile or create it inverted.

**Why it happens:**
The `Sketch.Profile` on a Floor or Toposolid exposes `CurveArrArray` (legacy type), not `IList<CurveLoop>`. When extracting boundaries to re-create elements, developers convert `CurveArr` to `CurveLoop` without verifying the winding direction. The Revit API confirmed: `CurveLoop` has `IsCounterclockwise(XYZ normal)` and `Flip()` to correct orientation, but these are only available on `CurveLoop`, not on `CurveArr`.

**How to avoid:**
After converting `CurveArr` segments to a `CurveLoop`, always call `loop.IsCounterclockwise(XYZ.BasisZ)`. For the outer boundary: if false, call `loop.Flip()`. For inner boundaries (voids/holes): must be clockwise, so flip if `IsCounterclockwise` returns true. Apply this check in a boundary normalization method used by every creation service (Split Boundaries, Merge, both conversions). `CurveLoop.HasPlane()` should also be verified before any plane-dependent operations; non-planar loops from imported Sketches will throw on `GetPlane()`.

**Warning signs:**
- `Autodesk.Revit.Exceptions.ArgumentException` thrown at `Toposolid.Create` or `Floor.Create` with message referencing profile or curve loop orientation
- Element creates but geometry appears mirrored or boundary holes are filled incorrectly
- Elements with multiple boundaries (outer + inner voids) fail only when inner boundaries exist

**Phase to address:**
All phases that call `Floor.Create` or `Toposolid.Create` (conversions, Split Boundaries, Merge). Create a shared boundary normalization helper in the first conversion phase and re-use across all others.

---

### Pitfall 3: Elevation coordinate mismatch — Floor uses height offset from Level in internal feet, Toposolid uses absolute Z in XYZ points

**What goes wrong:**
A Floor's elevation is encoded as `FLOOR_HEIGHTABOVELEVEL_PARAM` (a relative offset in internal feet from the associated Level's elevation). A Toposolid's elevation is encoded as the absolute Z value of the XYZ points passed to `Toposolid.Create` — the Level is reference only, not added to the Z coordinates. Converting Floor to Toposolid without accounting for this produces a Toposolid sitting at an elevation offset by the level's absolute height. Converting Toposolid to Floor sets the height-offset parameter wrong when the Toposolid was modeled at non-zero absolute elevation.

**Why it happens:**
Developers look at `FLOOR_HEIGHTABOVELEVEL_PARAM` on the source Floor and pass that as the Z offset to the Toposolid points. The correct math is: `toposolidPointZ = levelElevation + floorHeightOffset + slabVertexRelativeOffset`. When going the other direction, `floorHeightOffset = lowestPointAbsoluteZ - levelElevation`, and then each vertex offset passed to `SlabShapeEditor.ModifySubElement` must be `absoluteZ - (levelElevation + floorHeightOffset)`.

**How to avoid:**
Use `doc.GetElement(levelId) as Level` and read `level.Elevation` (in internal feet) before any coordinate math. Define a single elevation resolution method that takes a source element plus target level and returns both the `heightAboveLevel` parameter value and the `XYZ[]` points with correct absolute Z for Toposolid creation. The existing `ToposolidBaseElevationService` has related patterns — extend it rather than reimplementing in conversion services.

**Warning signs:**
- Converted element appears at the correct XY position but is floating above or buried below the model
- Elements match visually in 3D but `FLOOR_HEIGHTABOVELEVEL_PARAM` reads differently than expected
- Elevation is correct for elements hosted on Level 0 (elevation = 0) but wrong for any other level

**Phase to address:**
Both conversion phases (Floor-to-Toposolid, Toposolid-to-Floor). Must be resolved before any conversion produces a saved element, or live documents will contain silent elevation errors.

---

### Pitfall 4: SlabShapeEditor.Enable() must be called before AddPoints in a new transaction after element creation; calling it in the creation transaction causes InvalidOperationException

**What goes wrong:**
After calling `Toposolid.Create(...)` or `Floor.Create(...)` inside `TransactionService.Run(...)`, calling `element.GetSlabShapeEditor()` immediately followed by `editor.Enable()` and `editor.AddPoints(...)` in the same transaction causes a native `InvalidOperationException`. The element's geometry has not fully resolved — Revit requires a regeneration boundary between element creation and SlabShapeEditor activation.

**Why it happens:**
`Toposolid.Create` and `Floor.Create` schedule geometry resolution for the end-of-transaction commit. `SlabShapeEditor.Enable()` requires the element's base geometry to be fully realized. Inside a single transaction, the element exists as an ID but its native geometry context is not ready for sub-element modification. The existing `SimplifyPointsService` avoids this by only ever working on already-committed elements.

**How to avoid:**
Use two separate `TransactionService.Run` calls: (1) create the element and commit, (2) obtain `GetSlabShapeEditor()`, call `Enable()`, then `AddPoints(points)` or `ModifySubElement(vertex, offset)`. The existing `TransactionService` does not provide a built-in "regenerate between transactions" pattern — call `doc.Regenerate()` only inside the second transaction scope if needed, never outside a transaction.

**Warning signs:**
- `Autodesk.Revit.Exceptions.InvalidOperationException` mentioning "regeneration required" or "element not resolved"
- `editor.IsEnabled` returns false immediately after creation despite calling `Enable()`
- AddPoints returns a non-empty list but vertices are not visible in the model

**Phase to address:**
Floor-to-Toposolid conversion (primary risk). Toposolid-to-Floor conversion (same pattern). The two-transaction pattern must be documented as a service contract before implementation.

---

### Pitfall 5: Toposolid.Split returns ElementIds of new elements but those IDs are invalid until after the transaction commits

**What goes wrong:**
`Toposolid.Split(splitCurveLoops)` is confirmed present in the Revit 2026 API and returns `IList<ElementId>`. Attempting to call `doc.GetElement(id)` on returned IDs inside the same transaction to immediately read properties (type name, existing shape points) returns null or an invalid object. This is relevant to Split Boundaries where the goal is to independently configure each new element after splitting.

**Why it happens:**
Revit's transaction model defers the materialization of newly created element IDs until commit. The IDs are reserved but the backing element data is not accessible via `doc.GetElement` until the transaction is complete and the change has been registered.

**How to avoid:**
Split Boundaries implementation should use the pattern: (1) call `Toposolid.Split(loops)` and store the returned IDs, (2) commit the transaction, (3) open a new transaction, (4) now access each element by the stored IDs. Parameter copying (type assignment, material, instance parameters) must happen in the second transaction, not the first. Validate each ID with `doc.GetElement(id)?.IsValidObject == true` before accessing.

**Warning signs:**
- NullReferenceException when accessing returned elements immediately after `Split`
- Elements appear in model after undo but parameter data is wrong/default
- Type or material assignment fails silently because `doc.GetElement(id)` returned null inside the split transaction

**Phase to address:**
Split Boundaries phase. Also applies to Merge if merge is implemented via create-then-delete within a single transaction scope.

---

### Pitfall 6: CurveArrArray from Sketch.Profile is a legacy type that does not directly feed Floor.Create or Toposolid.Create

**What goes wrong:**
`Sketch.Profile` returns `CurveArrArray`, which is the old Revit pre-2021 boundary type. Both `Floor.Create` and `Toposolid.Create` take `IList<CurveLoop>`, not `CurveArrArray`. Direct casting or treating `CurveArr` as `CurveLoop` fails at compile time. Developers who find the sketch via `element.SketchId` and read `sketch.Profile` then need to manually reconstruct `CurveLoop` objects from each `CurveArr`. This conversion is not provided by the API — it must be hand-coded.

**Why it happens:**
The `Sketch` class is a legacy type. Its `Profile` property predates the `CurveLoop` API. The existing `AlignEdgesBoundaryCollectionService` already accesses `toposolid.SketchId` to get the sketch, confirming this pattern is in use. The conversion from `CurveArr` to `CurveLoop` requires iterating each `Curve` in the `CurveArr` and calling `loop.Append(curve)`.

**How to avoid:**
Write a single static utility method `CurveArrToCurveLoop(CurveArr arr)` that iterates curves and appends them to a new `CurveLoop`. Call `loop.IsCounterclockwise(XYZ.BasisZ)` immediately after construction and flip if needed (per Pitfall 2). Both `Floor.SketchId` and `Toposolid.SketchId` are confirmed present in the API. Use this as the canonical boundary extraction path for both conversion operations and for the Split/Merge boundary collection.

**Warning signs:**
- Compile error: `CurveArr` cannot be used where `CurveLoop` is expected
- Runtime ArgumentException from `Floor.Create` or `Toposolid.Create` when the profile list contains zero items (empty loop from failed conversion)
- Boundary missing curves because iteration stopped early due to disconnected curve segments in the source sketch

**Phase to address:**
Both conversion phases. Establish the utility method in a shared boundary extraction service before any command-specific service is written.

---

### Pitfall 7: Document.SaveAs inside an IExternalCommand execution is blocked unless called outside any active transaction

**What goes wrong:**
For Type to Linked Models, the command must call `Document.SaveAs(filepath, saveAsOptions)` to write the linked file. This call throws `Autodesk.Revit.Exceptions.InvalidOperationException` if any transaction is currently open in any document. The `TransactionService` wraps all operations — if the save is naively placed inside a `_transactionService.Run(...)` call, it will fail immediately.

**Why it happens:**
`Document.SaveAs` modifies the document's backing storage and requires the document to be in a stable, committed state. Revit enforces that no transaction scope is active at the time of save. The constraint applies to all documents in the application session, not just the document being saved.

**How to avoid:**
Structure Type to Linked Models as a sequence with explicit phase boundaries: (1) collect element IDs and parameters in a read-only phase (no transaction), (2) create the destination document copy via `Document.SaveAs` outside any transaction, (3) open the saved document via `Application.OpenDocumentFile`, (4) open a transaction in the destination document to delete non-relevant elements and configure shared coordinates, (5) call `destinationDoc.Save()` inside a transaction if needed, (6) close the destination document, (7) open a transaction in the source document to delete the exported elements and create the RevitLinkType. The `TransactionService` does not currently provide a mode for this sequencing — the Type-to-Linked-Models service must be implemented without wrapping all operations in a single `_transactionService.Run`.

**Warning signs:**
- `InvalidOperationException` with message "The document cannot be saved while a transaction is open"
- `SaveAs` call silently does nothing when wrapped in a delegate
- Revit hangs or crashes when `SaveAs` is called while the document has uncommitted changes

**Phase to address:**
Type to Linked Models phase only. This is the sole command requiring document-level save operations.

---

### Pitfall 8: RevitLinkType.Create requires the target file to already exist on disk before the host transaction commits

**What goes wrong:**
`RevitLinkType.Create(document, path, options)` must be called inside a transaction in the host document. The `path` (a `ModelPath`) must point to a file that already exists on disk at the time of the call. If the destination `.rvt` file was created by a `Document.SaveAs` call that has not yet flushed to disk (or if the save failed), `RevitLinkType.Create` throws with a file-not-found error or loads an empty link.

**Why it happens:**
`RevitLinkType.Create` immediately attempts to resolve the file. There is no lazy evaluation. The save from the previous phase must be complete and the file handle must be released before this call. On Windows, `Document.SaveAs` followed by `Document.Close(false)` should guarantee the file is flushed, but race conditions are possible if the close is not awaited.

**How to avoid:**
After calling `destinationDoc.Close(false)`, verify `File.Exists(filepath)` before calling `RevitLinkType.Create`. Use `ModelPathUtils.ConvertUserVisiblePathToModelPath(filepath)` to construct the `ModelPath` argument. Pass `new RevitLinkOptions(false)` for non-workset-linked files. The `LinkLoadResult` returned by `Create` should be checked: a `LoadResult` of `Success` confirms the link is active.

**Warning signs:**
- `RevitLinkType.Create` throws file-not-found despite the SaveAs appearing to succeed
- Link is created but shows "Not Found" status immediately
- Link loads but shows empty geometry (destination file was partially written)

**Phase to address:**
Type to Linked Models phase. The file existence check must be the bridge between the save phase and the link creation phase.

---

### Pitfall 9: Shared coordinates are not automatically transferred when using Document.SaveAs from a host document

**What goes wrong:**
The destination document created by `Document.SaveAs` inherits the host document's internal coordinate system but the shared coordinate (survey point, project base point) relationship is not automatically established. When the linked file is placed back into the host via `RevitLinkType.Create`, it loads at `By Linked File` origin by default. If the user's workflow expects `Shared Coordinates` positioning, the linked model will appear offset from the correct location.

**Why it happens:**
Shared coordinates in Revit require explicit publishing or acquisition. `Document.SaveAs` is a file copy operation — it does not publish coordinates to the new file or establish a shared coordinate relationship. After the linked file is loaded, `Document.AcquireCoordinates(linkInstanceId)` can be called in the destination document, but only after the link is created and loaded. Alternatively, `RevitLinkType.SavePositions(callback)` handles saving back to the linked file after positioning.

**How to avoid:**
For the MVP implementation: document to users that elements will be positioned at project origin in the linked model, and that shared coordinates must be manually published after the command completes. For a complete implementation: after `RevitLinkType.Create`, place the `RevitLinkInstance` at `Transform.Identity`, then call `doc.AcquireCoordinates(linkInstance.Id)` in a transaction to publish the host's shared coordinate system to the linked file. The `AcquireCoordinates` method is confirmed present in the Revit 2026 API (`Document.AcquireCoordinates(ElementId linkInstanceId)`).

**Warning signs:**
- Linked model appears at correct position in host but is offset when opened standalone
- Exported elements appear at wrong location when the link is placed by shared coordinates
- Survey point and project base point in the linked file do not match the host

**Phase to address:**
Type to Linked Models phase. Shared coordinate handling should be explicitly scoped as either MVP-deferred or MVP-required before implementation begins.

---

### Pitfall 10: Merging elements by boundary union requires all source CurveLoops to be coplanar; non-coplanar boundaries silently fail Floor.Create

**What goes wrong:**
When implementing Merge Elements, the naive approach is to collect the outer boundaries of each source element and combine them into a single outer boundary. If source elements have been given different `FLOOR_HEIGHTABOVELEVEL_PARAM` values or have SlabShape modifications at different absolute elevations, the boundaries extracted from their Sketches are not coplanar. `Floor.Create` and `Toposolid.Create` require all curves in a profile loop to be on a common plane. Passing non-coplanar boundaries throws `ArgumentException`.

**Why it happens:**
Floor boundaries from `Sketch.Profile` are always drawn at the floor's own sketch plane (which is typically the level plane, not accounting for height offset). However, if the boundary curves were modified via sketch editing, they may have Z values baked in. When two floors on different height offsets are merged, their extracted boundaries have different Z values even if visually adjacent.

**How to avoid:**
Before merging, project all boundary curves to a common Z plane (the target element's intended level + height offset). Use `CurveLoop.CreateViaTransform(loop, Transform.CreateTranslation(new XYZ(0, 0, targetZ - sourceZ)))` to shift loops to the common plane. Validate with `loop.HasPlane()` after projection. The merged element's SlabShape then needs to carry the elevation variation as `ModifySubElement` offsets, not as boundary curve Z values.

**Warning signs:**
- `ArgumentException` at `Floor.Create` mentioning "non-planar profile"
- Merge works for elements on the same level but fails when sources are on different levels or have different height offsets
- Merge produces a flat element that ignores the elevation difference between sources

**Phase to address:**
Merge Elements phase. Boundary projection must be addressed at the start of merge implementation, before any merge test is run on real models.

---

### Pitfall 11: Fix Points (point repair) must distinguish SlabShapeVertexType.Corner from Edge and Interior before attempting DeletePoint

**What goes wrong:**
`SlabShapeVertex.VertexType` returns one of `Corner`, `Edge`, or `Interior` (confirmed from Revit 2026 API export). `Corner` vertices are the auto-generated corners of the boundary polygon — they cannot be deleted via `DeletePoint`. Attempting to call `editor.DeletePoint(vertex)` on a `Corner` vertex throws `ArgumentException` or `InvalidOperationException`. The existing `SimplifyPointsService` catches these but does not distinguish the type, meaning Fix Points cannot determine which vertices are corrupted interior points versus legitimate corner vertices.

**Why it happens:**
The `SlabShapeEditor.DeletePoint` API only allows deletion of `Interior` type vertices (user-added points that provide sub-element elevation control). `Edge` vertices are at boundary midpoints and have constrained deletion behavior. `Corner` vertices are the boundary polygon vertices and are managed by the boundary, not by the SlabShapeEditor directly. Code that iterates `SlabShapeVertices` without filtering by `VertexType` will attempt to delete everything and silently skip Corners via exception handling.

**How to avoid:**
In Fix Points, only operate on vertices where `vertex.VertexType == SlabShapeVertexType.Interior`. For edge-case repair (detecting points near boundary edges at inconsistent elevations), filter to `SlabShapeVertexType.Edge` and use `ModifySubElement(vertex, correctedOffset)` rather than delete. The definition of "inconsistent" for Fix Points should be: Interior points whose Z position differs by more than a tolerance (e.g. 1mm = 0.00328 feet) from the interpolated surface at that XY location.

**Warning signs:**
- All `DeletePoint` calls succeed (return true) but no points are actually removed from the surface
- Element has the same shape after Fix Points as before
- Fix Points removes too many points (including legitimate corner elevation adjustments)

**Phase to address:**
Fix Points phase. The vertex type filter must be the first thing implemented, before any point-removal logic.

---

### Pitfall 12: TransactionService wraps the entire operation in one transaction, but multi-document operations for Type to Linked Models cannot use this pattern

**What goes wrong:**
Every existing service uses `_transactionService.Run(doc, name, action)` which wraps everything in a single transaction on a single document. Type to Linked Models must operate on two documents: the host (source, delete elements after export) and the destination (created from SaveAs, delete non-relevant elements). `TransactionService.Run` only accepts one `Document`. Attempting to call `doc.GetElement(id)` for destination elements inside a host-document transaction will silently fail or crash Revit.

**Why it happens:**
The `TransactionService` was designed for single-document operations. There is no mechanism to open a transaction on a different document than the one passed to `Run`. The `DeepPurgeService` precedent shows how to handle secondary document transactions: it opens the family document separately, manages transactions on it directly (through `_transactionService.Run(familyDoc, ...)` with the alternate document), and closes it explicitly.

**How to avoid:**
Follow the `DeepPurgeService` pattern: inject `ITransactionService` and call `_transactionService.Run(destinationDoc, ...)` explicitly for destination-document operations, completely separate from host-document operations. Never mix operations on two documents in the same `TransactionService.Run` call. The destination document must be opened with `Application.OpenDocumentFile` and closed with `destinationDoc.Close(false)` in a finally block.

**Warning signs:**
- Operations on the destination document silently do nothing when called inside a host-document transaction
- Revit throws "Document argument does not match transaction document" errors
- Elements are deleted from host but not from destination (or vice versa) due to transaction scope confusion

**Phase to address:**
Type to Linked Models phase. The multi-document transaction pattern must be architected before any implementation of this command begins.

---

## Technical Debt Patterns

Shortcuts that seem reasonable but create long-term problems.

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Single giant transaction for create+configure+cleanup | Simpler rollback | `SlabShapeEditor.Enable()` fails; split element IDs not accessible | Never — always use two transactions |
| Copy boundary CurveArr directly without winding check | Fewer lines of code | Silent creation failures or inverted elements in production | Never — always normalize |
| Skip `File.Exists` check before `RevitLinkType.Create` | Faster path | Cryptic link-not-found error with no meaningful message | Never |
| Broad `catch (Exception)` around `SaveAs`/`Close` | Prevents crashes | Masks real save failures, leaves orphaned temp files | Only at outermost command boundary with explicit logging |
| Deferred shared coordinate setup | Faster MVP | Users cannot use Shared Coordinates link positioning | Acceptable for v2.0 if documented prominently |
| Reuse Sketch.Profile CurveArrArray reference after source deletion | Avoids extra copy | Accessing `CurveArr` after `doc.Delete(sourceId)` throws because Sketch is deleted with the element | Never — always extract before deletion |

---

## Integration Gotchas

Common mistakes when connecting to existing services.

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| `SlabService.DuplicateElement` | Used as a merge primitive — assumes the copy retains SlabShape | `ElementTransformUtils.CopyElements` copies the element but not all SlabShape sub-element state reliably; use `Create` + point transfer instead |
| `SimplifyPointsService` | Called on conversion result to clean up points | Must be called in a separate transaction after the conversion transaction commits, not inline |
| `AlignEdgesBoundaryCollectionService` | Used to read source boundaries for split/merge | Returns `List<XYZ>` hit points, not `CurveLoop` — cannot be used directly as `Floor.Create` profile input |
| `TransactionService.Run` | Passed both source deletion and destination creation in one delegate | Any failure in deletion rolls back creation; keep delete and create in separate transactions |
| `OffsetService.TryOffsetElement` | Called after Floor-to-Toposolid conversion to adjust height | The parameter `FLOOR_HEIGHTABOVELEVEL_PARAM` is Floor-only; Toposolid elevation is encoded in point Z values, not in this parameter |
| `ToposolidService` | Extended to handle Toposolid type manipulation for conversion | `ContourSetting` operations on a type affect all instances — do not apply contour changes during conversion without explicit user intent |

---

## Performance Traps

Patterns that work at small scale but fail as usage grows.

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Calling `editor.AddPoints` with all points in one call vs. batching | Works for small point counts, freezes Revit for large Toposolids | Use `AddPoints(IList<XYZ>)` batch overload (confirmed in API), not one-at-a-time `AddPoint(XYZ)` | > 500 shape points |
| Collecting all elements then processing synchronously without progress reporting | UI appears frozen for large selections | Use `IProgressReporter` on inner loop, same pattern as `SimplifyPointsService` | > 50 selected elements |
| Opening and closing destination document multiple times during Type to Linked Models for multiple types | Each `OpenDocumentFile` loads full Revit file | Open once, process all types, close once | > 3 types in one run |
| Using `FilteredElementCollector` with LINQ `.Where()` inside a transaction delegate | Works but is slow — collector is not lazy | Use `OfClass(typeof(Floor))` or `OfCategory` before LINQ | Models with > 10,000 elements |

---

## UX Pitfalls

Common user experience mistakes in this domain.

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Deleting source element before user confirms conversion result | Irreversible data loss with no undo of source | Keep both source and result until user explicitly confirms, or rely on Revit's undo stack (single transaction delete) |
| Not reporting which elements failed Fix Points and why | User has no idea what was repaired vs. skipped | Report per-element: element ID, points before/after, skip reason (e.g. "no interior points") using `IProgressReporter` |
| Not warning that Type to Linked Models will delete elements from host | Unexpected data loss | Show explicit confirmation dialog listing element count and type names before proceeding |
| Converting Floor to Toposolid changes material assignment behavior | User loses material control without knowing it | Log the source material/type and attempt to find or create matching Toposolid type |
| Merge creating element on wrong level when sources span multiple levels | Element hovers at wrong height | Require user to explicitly select target level in VM before merge proceeds |

---

## "Looks Done But Isn't" Checklist

Things that appear complete but are missing critical pieces.

- [ ] **Floor-to-Toposolid conversion:** Often missing edge vertex transfer — verify that boundary-edge `SlabShapeVertex` offsets are recreated via `ModifySubElement`, not just interior points
- [ ] **Toposolid-to-Floor conversion:** Often missing height offset calculation from level — verify `FLOOR_HEIGHTABOVELEVEL_PARAM` is set correctly relative to the target level, not just to absolute Z = 0
- [ ] **Fix Points:** Often marks completion after `DeletePoint` loop — verify that the element's surface visually matches expectation by checking remaining vertex count and positions after the transaction commits
- [ ] **Split Boundaries:** Often only splits the element — verify that instance parameters (material assignment, comments, mark) are copied to each new element in the second transaction
- [ ] **Merge Elements:** Often only creates the merged boundary — verify that shape points from all source elements are transferred and source elements are deleted in a subsequent transaction
- [ ] **Type to Linked Models:** Often saves the file but does not create the link — verify that `RevitLinkType.Create` succeeds, `LinkLoadResult` is `Success`, and a `RevitLinkInstance` is placed at the correct position
- [ ] **Type to Linked Models:** Often forgets to delete the exported elements from host — verify deletion transaction runs after link creation and that the correct element IDs are targeted

---

## Recovery Strategies

When pitfalls occur despite prevention, how to recover.

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Stale SlabShapeVertex reference crash | LOW | Roll back the failed transaction; re-snapshot vertex positions; retry with XYZ list |
| Wrong winding order produces inverted element | LOW | Delete the failed element; call `loop.Flip()` on the boundary; recreate |
| Elevation mismatch in converted element | MEDIUM | Delete the converted element; fix the level elevation math; retry conversion |
| SaveAs failed, destination file corrupt | LOW | Delete partial file; fix transaction sequencing; retry |
| RevitLinkType.Create failed | LOW | Verify file exists; verify no active transaction; retry `Create` call |
| Wrong elements deleted from host after export | HIGH | Undo (Ctrl+Z); fix element ID collection logic; re-run command |
| Merged element has wrong boundaries | MEDIUM | Undo; fix curve projection to common plane; retry merge |

---

## Pitfall-to-Phase Mapping

How roadmap phases should address these pitfalls.

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Stale SlabShapeVertex references (P1) | Floor-to-Toposolid conversion | Test: verify all source shape points appear in converted element |
| CurveLoop winding order (P2) | First conversion phase; shared utility | Test: conversion of element with inner void boundary succeeds |
| Elevation coordinate mismatch (P3) | Both conversion phases | Test: convert element on Level 2 (elev 3m), verify converted element is at same absolute height |
| SlabShapeEditor.Enable() timing (P4) | Both conversion phases | Test: element created in transaction 1, shape points added in transaction 2, no exception |
| Split returned IDs not accessible (P5) | Split Boundaries | Test: split a Toposolid, verify each result element has correct type after second transaction |
| CurveArrArray conversion (P6) | First conversion phase; shared boundary utility | Test: boundary extraction from Sketch matches original boundary count and shape |
| Document.SaveAs inside transaction (P7) | Type to Linked Models | Test: SaveAs call succeeds, no InvalidOperationException |
| RevitLinkType.Create file requirement (P8) | Type to Linked Models | Test: File.Exists check passes before Create; Load result is Success |
| Shared coordinates not transferred (P9) | Type to Linked Models | Document scope decision at phase start; verify or defer |
| Non-coplanar merge boundary (P10) | Merge Elements | Test: merge two floors on different height offsets, verify no ArgumentException |
| Fix Points VertexType filter (P11) | Fix Points | Test: Fix Points on element with only Corner/Edge vertices produces zero deletions, no exceptions |
| Multi-document TransactionService (P12) | Type to Linked Models | Test: host and destination document transactions are independent; failure in one does not corrupt the other |

---

## Sources

- Revit 2026 API reflection export: `docs/review/revit-api/revit.autodesk.revit.db.public.members.jsonl` (HIGH confidence — authoritative local DLL metadata)
- Existing service implementations: `SlabService.cs`, `SimplifyPointsService.cs`, `AlignEdgesBoundaryCollectionService.cs`, `TransactionService.cs`, `DeepPurgeService.cs`, `OffsetService.cs` (HIGH confidence — live codebase patterns)
- Revit API usage patterns: `docs/review/05-revit-api.md` (HIGH confidence — project-specific documentation)
- Technical debt audit: `docs/review/14-current-debt-audit.md` (HIGH confidence — current codebase state)
- Known failure in existing code: `BuiltInFailures.SlabShapeFailures` namespace contains `SlabShapeWarnVerticesDeleted`, `SlabShapeWarnVerticesCoincident`, `SlabShapeEditFailed`, `SlabShapeEditFailedError` — these are Revit-defined failure IDs that can surface during `SafeFailureHandler.PreprocessFailures` and should be handled explicitly rather than generically swallowed (MEDIUM confidence — inferred from failure name semantics, not tested)

---
*Pitfalls research for: Revit 2026 geometry operations — Floor/Toposolid conversion, Fix Points, Split Boundaries, Merge Elements, Type to Linked Models*
*Researched: 2026-03-19*
