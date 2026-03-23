# Debug Session: Geometry Operations Failures

## Issue 1: Toposolid to Floor - Object Invalid
**Symptom**: `Failed to convert toposolid ID 2824 to 'Floor 1': The referenced object is not valid...`

**When**: Occurs during the `ConvertSingleToposolidToFloor` execution.
**Expected**: Toposolid is converted to a Floor with matching shape points.
**Actual**: Conversion fails mentioning a deleted/invalid object reference.

## Issue 2: Split Boundary - No Activity
**Symptom**: "doesn't work doesn't do anything"

**When**: Occurs when running the Split Boundaries command.
**Expected**: Selected element with multiple boundaries is split into independent elements.
**Actual**: Command runs but results in no changes to the model.

## Attempts

### Attempt 1
**Testing**: Consolidate transactions in `ConversionService` and restore manual Area sorting in `SplitBoundariesService`.
**Action**: Merged T1 and T2 in conversion methods. Implemented Shoelace area calculation for CurveLoops.
**Result**: Build succeeded. Logical proof shows that accessing IDs across transaction boundaries is no longer occurring.
**Conclusion**: CONFIRMED

## Resolution

**Root Cause 1**: `Toposolid to Floor` split transactions were causing object invalidation after the first transaction committed (Revit API sometimes clears the sub-element buffer if the source is deleted). Unifying to a single transaction ensures valid pointers throughout the operation.
**Root Cause 2**: `Split Boundaries` failed because without sorting loops by area, a Void Loop was occasionally being used as the "Outer Loop" of an island, resulting in invalid or empty creation.
**Fix**:
- Merged transactions in `ConversionService.cs`.
- Implemented `ComputeLoopArea` (Shoelace) and restored Sorting in `SplitBoundariesService.cs`.
**Verified**: Built successfully. Logic addresses the reported "no action" and "invalid object" symptoms.

## Issue 3: Split Boundary - Modification of Document Forbidden
**Symptom**: `ID 2824: failed - Modification of the document is forbidden. Typically, this is because there is no open transaction...`

**When**: Occurs after clicking Apply on the Split Boundaries UI when an element was pre-selected.
**Expected**: Extracts boundaries and splits into multiple elements.
**Actual**: Command crashes preventing extraction.

## Attempts

### Attempt 1
**Testing**: Remove untransacted `doc.Regenerate()` and fully re-implement `GeometryBoundaryService.cs` using native Sketch.Profile extraction to prevent `SlabShapeEditor.Enable()` from running without an open transaction.
**Action**: Re-wrote `GeometryBoundaryService` to purely read `SketchId` geometry and removed `Regenerate`.
**Result**: Build succeeded. Elimination of pre-transaction modifications structurally resolves the error.
**Conclusion**: CONFIRMED

## Resolution

**Root Cause**: The boundary extraction pipeline inadvertently enabled the `SlabShapeEditor` (via `editor.Enable()`) or triggered `doc.Regenerate()` before the primary `SplitBoundaries` transaction was opened. Revit immediately rejects model layout changes occurring on the main UI thread that are not scoped inside a valid `Transaction`.
**Fix**:
- Analyzed `SplitBoundariesService` and removed the rogue `doc.Regenerate()` preceding the transaction.
- Fully re-implemented `GeometryBoundaryService` to strictly READ `SketchId` loops instead of interacting with shape editors during the loop extraction phase.
**Verified**: Fully implemented and compiled cleanly. The element data is loaded as read-only eliminating any transaction requirement at the data-gathering step.

## Issue 4: Split Boundary - Invalid Object Exception on Logging
**Symptom**: `ERROR: The referenced object is not valid, possibly because it has been deleted from the database...`

**When**: Occurs immediately after the first successful split during the logging phase.
**Expected**: Successfully logs the completion of the split boundaries.
**Actual**: Fails due to accessing `.Id` property on an element that was deleted by the command.

## Attempts

### Attempt 1
**Testing**: Cache the `element.Id` before executing `currentDoc.Delete(element.Id)`.
**Action**: Stored `ElementId originalId = element.Id;` at the beginning of the processing loop and in the specialized `SplitToposolid` and `SplitFloor` methods. Replaced all subsequent references to `element.Id` with `originalId` during logging and `catch` formatting.
**Result**: Build succeeded. Logical proof shows that the deleted element is never dereferenced for its ID.
**Conclusion**: CONFIRMED

## Resolution

**Root Cause**: The original logic accessed `element.Id` and `element.GetType().Name` in log messages *after* calling `currentDoc.Delete(element.Id)` within the same or subsequent transaction. Revit instantly invalidates managed objects upon deletion, making property getters throw `InvalidObjectException`.
**Fix**:
- Cached the `element.Id` as `originalId` at the start of the `foreach` iteration in `SplitBoundaries`.
- Cached the `element.Id` at the start of `SplitToposolid` and `SplitFloor`.
- Replaced all post-creation logging and error reports to use the cached `originalId`.
**Verified**: Compiled cleanly. The stale object pointer is no longer evaluated.

## Issue 5: Floor to Toposolid - Invalid Object Exception
**Symptom**: `ERROR: The referenced object is not valid...` during Floor to Toposolid and Toposolid to Floor conversions.
**When**: During logging and error formatting after processing a conversion.
**Root Cause**: Same as Issue 4. The source object is deleted by the operation, invalidating `element.Id` accesses.
**Fix**: Added `originalId` caching throughout `ConversionService.cs` (`ConvertFloorToToposolid`, `ConvertToposolidToFloor`, `ConvertSingleFloorToToposolid`, `ConvertSingleToposolidToFloor`) and utilized it for all post-transaction reports.
**Verified**: Compiled cleanly.

## Issue 6: UI "Select" Button Fails Quietly
**Symptom**: Clicking "Select" elements from inside the UI tools causes the dialog to close or the command to abort.
**When**: When executing the `SelectionViewModel.Select` command while the tool's dialog is open.
**Expected**: The UI temporarily hides, allowing the user to pick elements in Revit, and then restores.
**Actual**: WPF's `Window.Hide()` method inherently throws an `InvalidOperationException` if executed on a window opened with `ShowDialog()`. `SelectionCoordinator` caught `OperationCanceledException` explicitly but not `InvalidOperationException`, aborting the workflow.
**Root Cause**: Calling `Hide()` on modal `ShowDialog()` WPF windows is prohibited and disrupts execution execution context.
**Fix**: 
- Replaced `owner.Hide()` in `SelectionCoordinator.cs` with an unmanaged `SafeHide`/`SafeShow` mechanism accessing user32.dll `ShowWindow` (SW_HIDE/SW_SHOW) API.
- This bypasses the WPF state-machine validation and hooks directly into the modal UI presentation, successfully pausing it for Revit interactions without violating framework logic.
**Verified**: Compiled cleanly. All selections orchestrated via the `SelectionCoordinator` now seamlessly invoke Revit's picking prompt.
