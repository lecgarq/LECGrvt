# Feature Research

**Domain:** Revit geometry operations — Floor/Toposolid conversion, repair, splitting, merging, and type-based model separation
**Researched:** 2026-03-19
**Confidence:** HIGH (Revit API verified from offline extract; workflows from direct codebase analysis)

---

## Command-by-Command Feature Analysis

### Command 1: Floor to Toposolid Conversion

**What users expect:**
A user modeled terrain as a Floor (common legacy practice before Toposolid existed in Revit 2024+). They want to convert it to a Toposolid while keeping all the elevation work already done via SlabShapeEditor — corner vertices, interior points, edge points. They also expect the new Toposolid to land on the same level, with the same height offset, and ideally the same boundary sketch.

**Typical workflow:**
1. Select one or more Floor elements.
2. Choose a target ToposolidType from a dropdown populated from the document.
3. Command reads the source Floor: gets boundary sketch (`Floor.SketchId` → `Sketch.Profile`), gets height offset (`FLOOR_HEIGHTABOVELEVEL_PARAM`), gets all SlabShapeVertices (Corner, Edge, Interior) with their XYZ positions.
4. Creates a new `Toposolid` using `Toposolid.Create(doc, profiles, points, topoTypeId, levelId)` — the `points` parameter is the interior/edge vertices as XYZ list; the `profiles` parameter is the boundary CurveLoops extracted from the sketch.
5. Applies height offset via `TOPOSOLID_HEIGHTABOVELEVEL_PARAM`.
6. Optionally deletes the original Floor.

**API facts (verified):**
- `Toposolid.Create` has a 5-parameter overload: `(Document, IList<CurveLoop>, IList<XYZ>, ElementId topoTypeId, ElementId levelId)` — this is the right overload when you have both boundary and interior points.
- `Toposolid.Create` has a 4-parameter overload without points for when the surface is flat.
- `Floor.GetSlabShapeEditor()` returns `SlabShapeEditor` with `SlabShapeVertices` (typed as `SlabShapeVertexType.Corner`, `.Edge`, `.Interior`).
- `Floor.SketchId` → `Sketch.Profile` returns `CurveArrArray` (old API); must be converted to `IList<CurveLoop>`.
- `Floor` and `Toposolid` share `FLOOR_HEIGHTABOVELEVEL_PARAM` for offset — confirmed by existing `OffsetService` usage.

**Table stakes:**
- Preserve all interior/edge shape points (XYZ coordinates from SlabShapeVertices).
- Preserve boundary sketch (same CurveLoops for the profile boundary).
- Preserve height offset from level.
- Preserve level assignment.
- Delete source Floor option (on/off toggle — default on).

**Differentiators:**
- Batch conversion of multiple Floors in one operation.
- Auto-match ToposolidType by name when Floor type name partially matches a ToposolidType name.
- Log per-element success/skip with vertex count transferred.

**Anti-features:**
- Attempting to preserve Floor-specific material layer stack in the Toposolid type — ToposolidType has a completely different layer model; conversion must accept a different type, not try to copy layers.
- Converting Floors that have slope arrows (slab slope) — slope arrows are Floor-specific; the elevation is better captured by converting the shape points. Flag and skip or warn.

**Edge cases:**
- Floor with `SlabShapeEditor.IsEnabled = false` (flat): use `Toposolid.Create` without points overload or pass empty list.
- Multi-boundary Floor (outer boundary + inner void loop): `Sketch.Profile` returns a `CurveArrArray` where each `CurveArray` is one loop. First loop is outer, subsequent loops are inner voids. Pass all as separate `CurveLoop` entries in the profile list.
- Floor with slab shape crease lines (`SlabShapeCreases`): these define split lines. They cannot be directly created on Toposolid via `SlabShapeEditor` — the Toposolid API does not expose crease creation. Warn user that crease lines are lost.
- Source Floor has no sketch (unusual, can happen on linked/modified Floors): fall back to extracting boundary from solid geometry, or skip with warning.
- Level mismatch between Floor level and available levels: require user to confirm level in config UI.

**Complexity:** MEDIUM
- Core create call is straightforward once points/loops are extracted.
- CurveArrArray-to-CurveLoop conversion needs care (winding direction, curve continuity).
- SlabShapeVertex extraction is already proven by `SimplifyPointsService`.

**Existing service dependencies:**
- `SlabService.TryResetSlabShape` — model for reading `SlabShapeEditor`.
- `SimplifyPointsService` — model for iterating `SlabShapeVertices` with `Cast<SlabShapeVertex>()`.
- `AlignEdgesBoundaryCollectionService` — model for accessing `toposolid.SketchId` and reading `Sketch`.
- `OffsetService.TryOffsetElement` — reuse or adapt for setting `TOPOSOLID_HEIGHTABOVELEVEL_PARAM` on created Toposolid.
- `TransactionService.Run` — mandatory for all creates/deletes.

---

### Command 2: Toposolid to Floor Conversion

**What users expect:**
The reverse path: a Toposolid that was modeled as terrain needs to be used as a structural floor slab. Common when civil-to-BIM coordination results in Toposolids that need Floor category for structural analysis tools. Users expect the surface elevation data (all the manually edited points) to transfer to the Floor's SlabShapeEditor.

**Typical workflow:**
1. Select one or more Toposolids.
2. Choose a target FloorType from a dropdown.
3. Command reads the Toposolid: boundary sketch via `Toposolid.SketchId`, height offset via `TOPOSOLID_HEIGHTABOVELEVEL_PARAM`, and all SlabShapeVertices.
4. Creates a new `Floor` using `Floor.Create(doc, profile, floorTypeId, levelId)`.
5. Applies height offset to the Floor.
6. Enables `Floor.GetSlabShapeEditor()`, adds interior points via `SlabShapeEditor.AddPoints(IList<XYZ>)`, modifies edge/corner vertices via `SlabShapeEditor.ModifySubElement(SlabShapeVertex, offset)`.
7. Optionally deletes the source Toposolid.

**API facts (verified):**
- `Floor.Create(Document, IList<CurveLoop>, ElementId floorTypeId, ElementId levelId)` — 4-parameter create; no points parameter.
- Points must be added post-creation via `SlabShapeEditor.AddPoints(IList<XYZ>)` (adds interior/edge points) and then `ModifySubElement(vertex, offset)` for elevation.
- `SlabShapeEditor.AddPoint(XYZ)` adds a single interior point at absolute XYZ including Z elevation.

**Table stakes:**
- Preserve all interior SlabShapeVertices as new shape points.
- Preserve boundary sketch profile.
- Preserve height offset from level.
- Delete source Toposolid option.

**Differentiators:**
- Batch conversion.
- Auto-match FloorType by type name.

**Anti-features:**
- Trying to preserve Toposolid-specific data (contour settings, subdivision data) in the Floor — impossible by definition.
- Converting subdivision Toposolids (those with `HostTopoId` set, i.e., sub-divisions) — these are not stand-alone surfaces; they would need their host to be converted first. Detect via `Toposolid.HostTopoId != ElementId.InvalidElementId` and warn/skip.

**Edge cases:**
- Toposolid with `SlabShapeEditor` not enabled (unusual for Toposolids, but handle): create flat Floor.
- Toposolid with `Split` children (split by curve): the split is modeled inside the parent Toposolid's geometry but doesn't create separate elements; the resulting Floor will need to be inspected. Warn in log.
- Toposolid sub-divisions (`GetSubDivisionIds()` returns non-empty): the sub-divisions are separate Toposolid elements with `HostTopoId` pointing back. Skip if selected element is a subdivision.

**Complexity:** MEDIUM — same difficulty as Floor→Toposolid but the point-add flow is two-step (create then add via editor).

**Existing service dependencies:** Same as Command 1.

---

### Command 3: Fix Points

**What users expect:**
When two adjacent Toposolids or Floors share an edge, the SlabShapeVertices at the boundary can become inconsistent — one element has an edge vertex at elevation A, the adjacent element has its corner vertex at the same XY but at a slightly different Z, creating a visible crack or gap. Users want to select a set of elements and have the command normalize all boundary vertices to consistent elevations.

**Typical workflow:**
1. Select a set of Toposolids/Floors (or the command selects all in document).
2. Command analyzes shared boundary edges: for each pair of elements whose sketch boundaries share or nearly share an edge, finds vertices at similar XY positions.
3. Resolves inconsistencies: either snaps Z values of nearby vertices to the lowest, highest, or average elevation.
4. Applies corrections via `SlabShapeEditor.ModifySubElement(vertex, offset)` where offset adjusts the vertex Z.

**API facts (verified):**
- `SlabShapeVertex.Position` — existing codebase uses this for XYZ.
- `SlabShapeEditor.ModifySubElement(SlabShapeVertex, Double)` — modifies vertex elevation by offset (not absolute Z).
- `SlabShapeVertex.VertexType` values: `Corner` (1), `Edge` (2), `Interior` (3).
- Vertices at boundaries are typed `Corner` or `Edge`; interior points are `Interior`.

**Table stakes:**
- Detect vertices within a configurable XY tolerance that have different Z values.
- Apply elevation correction using a chosen resolution strategy (snap to lower, higher, or average).
- Process both Floors and Toposolids uniformly.
- Report what was changed in log.

**Differentiators:**
- Preview mode: show what would change before committing (requires rollback transaction or dry-run flag).
- Per-element report showing how many points were adjusted and by how much.
- Configurable tolerance (XY snap radius) exposed in config UI.

**Anti-features:**
- Automatically merging vertices from different elements (modifying the sketch boundary profile itself) — editing a Floor/Toposolid sketch requires the sketch editor to be open, which is not accessible via API. Only SlabShapeEditor vertex elevation can be modified, not the boundary curve geometry.
- Attempting to fix points on elements with non-enabled SlabShapeEditors — call `Enable()` first, but only if already partially shaped (not flat) elements.

**Edge cases:**
- Two elements with exactly matching XY but different Z on multiple vertices: need to decide resolution strategy per vertex-pair, not per element.
- Vertex on element A has no counterpart on element B within tolerance: skip that vertex — no fix needed.
- Element that is flat (SlabShapeEditor not enabled): has no vertices to fix; skip with info log.
- Elements that don't share boundaries (isolated): no-op, log as "no shared boundary found."
- Vertex offset computed as 0 (already matching): no API call needed, skip.

**Complexity:** MEDIUM-HIGH
- Requires spatial indexing (group vertices by XY proximity) across multiple elements.
- Elevation fix requires computing offset from current to target Z and calling `ModifySubElement`.
- Resolution strategy (which Z wins) needs user configuration.

**Existing service dependencies:**
- `SimplifyPointsService` — model for vertex iteration pattern.
- `AlignEdgesService` / `AlignEdgesBoundaryCollectionService` — model for boundary-point collection and intersection logic. The `AlignEdges` approach of casting rays can inform how to detect shared boundary zones.
- `TransactionService.Run` — mandatory.

---

### Command 4: Split Boundaries

**What users expect:**
A single Floor or Toposolid element was modeled with multiple boundary loops: one outer loop and one or more inner void loops, or multiple disconnected outer loops (rare but possible on Toposolids). Users want each boundary loop to become an independent element of the same type and type assignment, preserving the relevant shape points that fall within each loop's territory.

**Typical workflow:**
1. Select a Floor or Toposolid with a multi-boundary sketch.
2. Command inspects `Sketch.Profile` (CurveArrArray) to count the loops.
3. If only 1 loop: inform user that the element has a single boundary, nothing to split.
4. For each loop (or each outer loop treating inner loops as voids): create a new element using only that loop as the boundary profile.
5. Redistribute shape points: assign each SlabShapeVertex to the element whose boundary contains that vertex's XY position.
6. Apply those points to the new element via `SlabShapeEditor.AddPoints`.
7. Copy type, level, height offset from source.
8. Delete source element.

**API facts (verified):**
- `Sketch.Profile` returns `CurveArrArray` — each `CurveArray` is one closed loop.
- `Toposolid.Split(IList<CurveLoop>)` exists on the API — returns `IList<ElementId>` of resulting elements. This is the API-native split operation for Toposolids.
- For Floors, no native `Split` method exists; must create new Floor per loop.
- Point-in-polygon test needed to assign shape points to the correct resulting element.

**Table stakes:**
- Detect multi-boundary elements and skip single-boundary elements with a clear message.
- Create one independent element per outer boundary loop.
- Distribute shape points to the correct element based on XY containment.
- Preserve type, level, and height offset on each resulting element.
- Delete source element after successful split.

**Differentiators:**
- For Toposolids, prefer the native `Toposolid.Split(IList<CurveLoop>)` API over manual recreation — this preserves internal mesh topology better.
- Log per-element count of points assigned to each split result.
- Preview showing resulting element count before committing.

**Anti-features:**
- Splitting inner void loops into separate elements — inner void loops define holes, not separate surfaces. They must be treated as openings, not new elements. If a multi-boundary element has inner voids, those voids should be preserved on the element that contains them.
- Attempting to split elements that are Toposolid sub-divisions — sub-divisions have a host relationship; they cannot be independently split without modifying the parent.

**Edge cases:**
- Toposolid with `GetSubDivisionIds()` returning values: these sub-divisions share the boundary geometry; splitting the host affects them. Warn user.
- Floor with inner void loops: Treat as outer boundary + void; the void is part of the geometry, not a separate region. If user wants the void converted to a separate element, that's a different operation (not this command).
- Shape points exactly on the boundary line of a loop: use a tolerance-based containment test; assign to the loop that most closely contains the point.
- CurveArrArray with only 1 CurveArray: single boundary element — inform user, do nothing.

**Complexity:** MEDIUM-HIGH
- For Toposolids: `Toposolid.Split` simplifies the core operation significantly (HIGH confidence from API extract).
- For Floors: manual recreation per loop + point redistribution adds complexity.
- Point-in-polygon spatial containment logic required.

**Existing service dependencies:**
- `AlignEdgesBoundaryCollectionService` — model for reading sketch profile/loops.
- `SimplifyPointsService` — model for vertex iteration.
- `SlabService.DuplicateElement` — may be used to create a base copy before modification.
- `TransactionService.Run` — mandatory.

---

### Command 5: Merge Elements

**What users expect:**
Multiple adjacent Floors or Toposolids of the same type need to become one element. This typically happens when a terrain was modeled in sections or a floor was split and needs to be reunified. Users expect the merged element to preserve all the original shape points from all source elements.

**Typical workflow:**
1. Select 2 or more Floors (or 2 or more Toposolids — same category only, not mixed).
2. Command validates that elements share or abut boundaries (optional validation — merge can proceed without this check).
3. Collects all boundary loops from all source element sketches.
4. Combines loops into a single boundary: the outer hull of all combined loops. This requires a polygon union operation.
5. Collects all SlabShapeVertices from all source elements.
6. Creates one new element of the same type/level/offset using the combined boundary.
7. Adds all collected shape points to the new element.
8. Deletes all source elements.

**API facts (verified):**
- No native merge/union API exists for `Floor` or `Toposolid` in the Revit 2026 API extract. Polygon union must be computed in code.
- `Toposolid.Create` and `Floor.Create` both accept `IList<CurveLoop>` profiles.
- `SlabShapeEditor.AddPoints(IList<XYZ>)` adds all points in one call.
- Polygon union from CurveLoops requires geometric computation (no built-in Revit API for this).

**Table stakes:**
- Merge 2+ elements into one element of the same type.
- Preserve all shape points from all source elements.
- Preserve the type and level of the first (or user-selected primary) element.
- Delete all source elements after successful merge.
- Reject mixed-category selections (cannot merge a Floor with a Toposolid).

**Differentiators:**
- Union-of-profiles computation: for abutting elements where boundaries share edges, the shared edges are removed from the union. For overlapping elements, the outer boundary is the union hull.
- Log total points transferred per source element.
- Warn when elements are not adjacent (non-abutting merge creates an element with gaps).

**Anti-features:**
- Attempting a "smart" overlap merge that clips or subtracts overlapping geometry — this requires a full polygon boolean library and is far beyond the scope of this command. If boundaries overlap, use the outer hull or report an error.
- Merging elements across different levels without user confirmation — level mismatch means the height offset semantics differ; the user must explicitly choose which level to use.
- Automatic type reconciliation across mismatched types — if types differ, require user to select one target type.

**Edge cases:**
- Two elements with identical boundary loops (exact overlap): the merge result is one element with the same boundary; all points from both source elements are added (duplicates are filtered by proximity).
- Elements with non-abutting boundaries (gap between them): the created single element will have a boundary that connects the two shapes; this may be unexpected. Warn in log.
- Elements with different levels: require user to choose target level explicitly in config UI.
- Elements with different height offsets: use the offset of the primary element; log discrepancies.
- One element in the selection has a flat slab (no shape editor points): its boundary is still added to the union; just no points contributed.

**Complexity:** HIGH
- Polygon union of CurveLoops is the hardest part — requires geometric algorithms (shared edge detection, outer hull computation) not provided by the Revit API.
- Point deduplication across element vertex sets required.
- Creation and point-assignment are straightforward once the union boundary is ready.

**Existing service dependencies:**
- `SimplifyPointsService` — model for vertex iteration.
- `AlignEdgesBoundaryCollectionService` — model for boundary collection.
- `SlabService.DuplicateElement` — not directly useful; creation is from scratch.
- `TransactionService.Run` — mandatory.

**Note for roadmap:** This command has the highest implementation risk due to polygon union geometry. A viable MVP approach is to support merge only when all source elements share a linear boundary (straight-line abutment) and reject curved-boundary merges with an error, deferring curved-union support to a later iteration.

---

### Command 6: Type to Linked Models

**What users expect:**
A project has multiple Floor or Toposolid types in the same document. The team wants to split the model into separate Revit files — one file per type — and link them all back into the host as Revit links with shared coordinates. The host model is then cleaned of the elements that were exported. This is a common coordination workflow for civil/landscape projects where terrain layers (paving, landscaping, base terrain) need to live in separate discipline models.

**Typical workflow:**
1. User opens command; sees a list of all Floor/Toposolid types present in the document with element counts.
2. User selects which types to export, sets the output folder, and sets naming pattern (e.g., `{ProjectName}_{TypeName}.rvt`).
3. For each selected type:
   a. Create a new blank Revit document from a template (using `Application.NewProjectDocument(template)`).
   b. Transfer shared coordinate system from host to new document.
   c. Copy elements of that type from host to the new document using `ElementTransformUtils.CopyElements(sourceDoc, ids, destDoc, transform, options)`.
   d. Save the new document as `{outputPath}/{TypeName}.rvt` using `Document.SaveAs(filepath)`.
   e. Create a `RevitLinkType` in the host document pointing to the saved file using `RevitLinkType.Create(doc, modelPath, options)`.
   f. Create a `RevitLinkInstance` in the host document using `RevitLinkInstance.Create(doc, revitLinkTypeId)`.
   g. Delete the original elements from the host document.
4. Report: files created, elements moved, links established.

**API facts (verified):**
- `Document.SaveAs(string filepath, SaveAsOptions options)` — confirmed.
- `RevitLinkType.Create(Document, ModelPath, RevitLinkOptions)` — confirmed.
- `RevitLinkInstance.Create(Document, ElementId revitLinkTypeId)` — confirmed.
- `RevitLinkInstance.MoveBasePointToHostBasePoint(bool)` — for shared coordinate alignment.
- Cross-document element copy: `ElementTransformUtils.CopyElements(sourceDoc, ids, destDoc, transform, options)` — confirmed in use by `SlabService.DuplicateElement` pattern (same doc), needs cross-doc variant.
- `Application.NewProjectDocument(string templatePath)` — standard API for creating blank documents.

**Type vs instance parameter distinction:**
- Type parameters (on FloorType/ToposolidType): name, materials, layer structure, function — these travel with the type definition when the type is copied to the new document.
- Instance parameters (on Floor/Toposolid instances): height offset, level, comments, mark, custom shared parameters — these travel with the instance.
- `ElementTransformUtils.CopyElements` with cross-document overload copies both element and its type. The type is created in the destination document automatically (or matched if a type with the same name already exists).
- User-visible impact: after export, the linked file contains the same type definitions. No manual type recreation needed.

**Table stakes:**
- Export elements by type to separate .rvt files.
- Save output files to a user-chosen folder.
- Establish Revit links in the host pointing to the exported files.
- Delete exported elements from the host after successful export.
- Show type list with element counts in config UI before export.
- Shared coordinate transfer to linked files.

**Differentiators:**
- Naming pattern control (project name prefix + type name suffix configurable).
- Selective export: user can choose which types to export and which to leave in host.
- Dry-run mode: create files and links but skip deletion of host elements (safe preview).
- Report of files created, element counts moved, link placement success/failure.

**Anti-features:**
- Attempting to export elements that are sub-divisions (`HostTopoId` set) — sub-divisions are hosted by a parent Toposolid; they cannot be independently placed in another document without their host. Skip with warning.
- Merging multiple types into one file — defeats the purpose; keep one file per type strictly.
- Cloud model (BIM 360/ACC) as target — project constraint prohibits cloud operations.
- Automatic workset assignment in the new document — over-engineering for the initial implementation; workset management stays manual.

**Edge cases:**
- Type exists in the document but has zero instances (unused type): skip export with info log.
- Output file already exists at target path: prompt user to overwrite or skip, or use a suffix.
- Cross-document copy fails for a subset of elements (e.g., element has an invalid reference): continue with remaining elements, log failures.
- Shared parameter attached to the Floor/Toposolid instance: `CopyElements` will copy the parameter value, but the shared parameter definition may not exist in the new document. The parameter value will be lost if the definition is absent. Flag in the log.
- Level in new document: when Floor/Toposolid is copied to a new blank document, the level it references may not exist in the destination. The copy may create a placeholder level or land the element at an unexpected elevation. Use `Transform.Identity` and verify elevation after copy.
- Two types with names that collapse to the same file name (after sanitization): add a numeric suffix disambiguator.

**Complexity:** HIGH
- Cross-document element copy is non-trivial — level and type dependencies are auto-resolved by Revit but may produce unexpected results.
- File system operations (`Directory.CreateDirectory`, `SaveAs`) need error handling.
- Link registration (`RevitLinkType.Create` + `RevitLinkInstance.Create` + coordinate alignment) is a multi-step API sequence.
- UI must handle type enumeration, selection checkboxes, folder picker, naming preview.

**Existing service dependencies:**
- `SlabService.DuplicateElement` — model for cross-element copy pattern; needs extension for cross-document variant.
- `TransactionService.Run` — all Revit modifications in host document must go through this.
- No existing service for file I/O or link creation — new service required.

---

## Feature Landscape

### Table Stakes (Users Expect These)

Features that every user of these commands will assume are present. Missing = command feels broken.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Preserve all shape points during conversion | Core value of plugin: geometric behavior preserved | MEDIUM | SlabShapeVertices iteration already proven in SimplifyPointsService |
| Preserve boundary sketch (loops) | Shape depends on boundary; wrong boundary = wrong element | MEDIUM | CurveArrArray-to-CurveLoop conversion needed |
| Preserve height offset from level | Absolute elevation is the most critical data for terrain | LOW | TOPOSOLID_HEIGHTABOVELEVEL_PARAM / FLOOR_HEIGHTABOVELEVEL_PARAM already used in OffsetService |
| Preserve level assignment | Elements must remain anchored to correct level | LOW | Level reading already exists in ChangeLevelService pattern |
| Delete source element option | After conversion, the old element is redundant | LOW | doc.Delete(elementId) wrapped in TransactionService |
| Config window before any selection | Project convention: all commands open config first | LOW | Established by RevitCommand base and all existing commands |
| Batch processing (multiple elements) | Users never want to run per-element manually | LOW | All existing commands process IList<Reference> |
| Log window showing per-element result | Users need to know what happened to each element | LOW | Logger + ShowLogWindow already in RevitCommand |
| Skip / warn on invalid elements, don't crash | Resilient batch processing | LOW | IsExpectedXxxException pattern established across codebase |
| Type/level selectors in UI (dropdowns) | Users must not type names manually | LOW | ChangeLevelViewModel pattern already shows level dropdown |
| Multi-boundary detection and clear messaging | "Split Boundaries" must tell user when source has only 1 boundary | LOW | Read from Sketch.Profile loop count |
| Mixed-category rejection for Merge | Cannot merge Floor with Toposolid | LOW | Simple type check before proceeding |
| Type list with element counts for Type to Linked | Users need to see what will be exported before committing | MEDIUM | FilteredElementCollector by type + count |
| Shared coordinates in exported files | Linked files must align correctly with host | MEDIUM | RevitLinkInstance.MoveBasePointToHostBasePoint |

### Differentiators (Competitive Advantage)

Features that go beyond what users assume. These are what make the tool worth having.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Toposolid native Split API for Split Boundaries | More accurate result than manual loop recreation; better internal mesh | LOW | Toposolid.Split(IList<CurveLoop>) confirmed in API |
| Fix Points with configurable XY tolerance | Lets users tune the "snap radius" for their project scale | LOW | Single double parameter in config |
| Fix Points resolution strategy (min/max/average Z) | Gives users control over which elevation wins | LOW | Three-option radio in config UI |
| Vertex count log per element for conversion commands | BIM managers need audit trails | LOW | Count before/after in each pass |
| Selective type export for Type to Linked | Not all types need export; user picks which ones | MEDIUM | Checkbox list in config UI |
| Naming pattern for Type to Linked output files | Team standards vary; allow prefix control | LOW | String template with {TypeName} token |
| Dry-run / no-delete mode for Type to Linked | Safety check before committing irreversible host cleanup | MEDIUM | Skip delete step, still create files + links |
| Merge warning for non-abutting boundaries | Prevents silent creation of geometrically wrong elements | LOW | Check bounding box overlap before merge |
| Auto-match type by name during conversion | Reduces config clicks for common naming conventions | LOW | String.Contains match on type names |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Preserve slab crease lines during Floor→Toposolid | User modeled crease lines and wants them kept | SlabShapeEditor crease creation API (AddSplitLine) only works for Floor; no equivalent on Toposolid | Warn user that crease lines are lost; document that Toposolid uses folding lines instead |
| Convert Toposolid sub-divisions during conversion commands | Sub-divisions look like independent elements | Sub-divisions (HostTopoId != InvalidElementId) have a host dependency; copying without the host produces an orphaned element | Skip sub-divisions with a clear warning; user must convert the parent Toposolid first |
| Polygon union for non-abutting elements in Merge | User wants to "merge" elements that have a gap | Creates a boundary that spans the gap; the resulting element may have invalid geometry | Validate adjacency before merge; block non-abutting merge or warn and require confirmation |
| Layer stack preservation across Floor↔Toposolid | User wants to keep material layers | FloorType and ToposolidType have different layer structures (HostObjAttributes); they cannot be mapped automatically | Require user to select target type explicitly; document that material layers must be manually reconciled |
| Convert slope-arrow Floors to Toposolid preserving slope | Floor has a slope arrow driving tilt | Slope arrows are a Floor-only feature; Toposolid uses shape points for slope | Read surface geometry via solid instead of SlabShapeEditor, extract point cloud from face, use as shape points |
| Cloud/BIM360 targets for Type to Linked | Team uses cloud collaboration | Project constraint: all operations are local document manipulation | Document scope limitation clearly; feature is local-only by design |
| Real-time preview of merge boundary in 3D view | Helps user visualize the union | Requires dynamic model modification (display model) or DirectContext3D rendering during config — significant complexity | Use simple text preview "3 elements → 1 element, approx. boundary: X×Y bounding box" |
| Undo history per sub-step inside a command | Users want fine-grained undo | Revit transactions cannot be subdivided after commit; outer TransactionService.Run wraps all changes in one undoable step | One transaction per command is the correct approach; document this in the log |

---

## Feature Dependencies

```
Floor→Toposolid
    └──requires──> [Read SlabShapeVertices via SimplifyPointsService pattern]
    └──requires──> [Read Sketch.Profile via AlignEdgesBoundaryCollectionService pattern]
    └──requires──> [Toposolid.Create API with profiles+points overload]

Toposolid→Floor
    └──requires──> [Same SlabShapeVertices read]
    └──requires──> [Same Sketch.Profile read]
    └──requires──> [SlabShapeEditor.AddPoints post-create]

Fix Points
    └──requires──> [SlabShapeVertices read from both/all elements]
    └──requires──> [SlabShapeEditor.ModifySubElement]
    └──enhances──> [Floor→Toposolid] (run Fix Points after conversion to correct boundary edges)

Split Boundaries
    └──requires──> [Sketch.Profile loop enumeration]
    └──requires──> [Toposolid.Split native API for Toposolid path]
    └──requires──> [Floor.Create per-loop for Floor path]
    └──requires──> [SlabShapeVertices redistribution → AddPoints]

Merge Elements
    └──requires──> [Sketch.Profile boundary collection from all source elements]
    └──requires──> [Polygon union computation (new — no existing service)]
    └──requires──> [SlabShapeEditor.AddPoints on merged element]
    └──conflicts──> [Split Boundaries] (inverse operations; not meaningful to run both in sequence)

Type to Linked Models
    └──requires──> [FilteredElementCollector by FloorType/ToposolidType]
    └──requires──> [Cross-document ElementTransformUtils.CopyElements]
    └──requires──> [Document.SaveAs for new file]
    └──requires──> [RevitLinkType.Create + RevitLinkInstance.Create]
    └──no shared logic with conversion commands]
```

### Dependency Notes

- **Fix Points enhances conversion commands:** After converting Floor→Toposolid in batch, the boundary vertices of adjacent converted elements may have small Z discrepancies. Running Fix Points after conversion is a natural follow-up workflow.
- **Split Boundaries and Merge Elements conflict:** They are inverses. The only meaningful combined use is: merge several Toposolids → split the merged result differently. Sequential use is valid but must be explicit.
- **Type to Linked Models has no dependency on conversion commands:** It operates on elements as-is, regardless of whether they were converted. Completely independent.
- **Fix Points requires SlabShapeEditor on all target elements:** Elements without an enabled SlabShapeEditor (flat) have no vertices to fix; the command must handle these gracefully without error.

---

## MVP Definition

### Launch With (v2.0)

All 6 commands are in scope for this milestone. Within each, the MVP is:

- [ ] Floor→Toposolid — single and batch conversion, full point/boundary/level/offset preservation, delete-source option
- [ ] Toposolid→Floor — single and batch conversion, same preservation guarantees
- [ ] Fix Points — detect nearby vertices across element boundaries, apply Z correction with configurable tolerance, one resolution strategy (min Z as default, configurable)
- [ ] Split Boundaries — detect multi-boundary elements; use native `Toposolid.Split` for Toposolids; recreate per-loop for Floors; redistribute shape points
- [ ] Merge Elements — union of abutting straight-boundary elements (defer curved-boundary union); collect all shape points; create merged element; delete sources
- [ ] Type to Linked Models — enumerate types, select output folder, copy elements to new doc, save files, create links with shared coordinates, delete host elements; selective type checkbox

### Add After Validation (v2.x)

- [ ] Fix Points preview mode (dry-run rollback) — add after confirming the correction logic produces good results
- [ ] Slope-arrow Floor→Toposolid via solid face sampling — add after basic SlabShapeEditor-based conversion is stable
- [ ] Curved-boundary merge (polygon union algorithm) — defer until straight-boundary merge is proven; geometric complexity is high
- [ ] Type to Linked Models: naming pattern template UI — add after basic export is working
- [ ] Type to Linked Models: dry-run mode — add after basic export is proven correct

### Future Consideration (v3+)

- [ ] Crease-line preservation (Floor→Toposolid via folding lines) — requires `CreateCreasesFromFoldingLines` on Toposolid side; API support is uncertain
- [ ] Toposolid subdivision handling during conversion (convert parent + promote subdivisions) — high complexity, low frequency

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Floor→Toposolid conversion | HIGH | MEDIUM | P1 |
| Toposolid→Floor conversion | HIGH | MEDIUM | P1 |
| Fix Points | HIGH | MEDIUM | P1 |
| Split Boundaries | HIGH | MEDIUM | P1 |
| Merge Elements (straight boundaries) | HIGH | HIGH | P1 |
| Type to Linked Models | HIGH | HIGH | P1 |
| Fix Points preview/dry-run | MEDIUM | MEDIUM | P2 |
| Merge Elements (curved boundaries) | MEDIUM | HIGH | P2 |
| Slope-arrow Floor sampling path | LOW | MEDIUM | P3 |
| Crease-line preservation | LOW | HIGH | P3 |

**Priority key:**
- P1: Must have for v2.0 launch
- P2: Should have, add in v2.x
- P3: Nice to have, future consideration

---

## Sources

- Revit API offline extract: `docs/review/revit-api/revit.autodesk.revit.db.public.members.jsonl` (HIGH confidence — extracted from installed Revit 2026 assemblies)
- Revit API offline extract: `docs/review/revit-api/revit.autodesk.revit.db.public.types.jsonl` (HIGH confidence)
- Existing codebase — `SlabService`, `SimplifyPointsService`, `AlignEdgesBoundaryCollectionService`, `OffsetService`, `TransactionService` (HIGH confidence — direct code review)
- Existing codebase — `ChangeLevelViewModel`, `ResetSlabsCommand`, `ConvertCadCommand` (HIGH confidence — UI and command pattern models)
- `PROJECT.md` — project constraints and existing service inventory (HIGH confidence)
- `docs/review/14-current-debt-audit.md` — technical debt context (HIGH confidence)

---

*Feature research for: LECG Revit Plugin — v2.0 Geometry Operations milestone*
*Researched: 2026-03-19*
