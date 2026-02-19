# Debug Session: Batch Rename No-Op

## Symptom
Clicking "RUN" in the Batch Rename dialog results in no action being taken.

**When:** User clicks the primary "RUN" or "Apply" button in the Batch Rename UI.
**Expected:** The batch rename logic should execute, renaming the selected items.
**Actual:** Nothing happens. No logs, no UI changes, no errors shown in UI.

## Resolution

**Root Cause:** The "Apply Rename" button in `SearchReplaceView.xaml` was bound to `ReplaceCommand`, but the ViewModel (`SearchReplaceViewModel`) did not define such a command. It only provided an `ApplyCommand` inherited from `BaseViewModel` (via `[RelayCommand] Apply()`).

**Fix:** Updated `SearchReplaceView.xaml` to bind the button to `ApplyCommand`.

**Verified:** 
- [x] Code inspection confirms the binding mismatch.
- [x] Correct command name identified in `BaseViewModel` and overridden in `SearchReplaceViewModel`.
- [x] Build successfully includes the fix.

**Regression Check:** Verified other views (`PurgeView`, `ResetSlabsView`, etc.) for similar mismatches. None found; other views use consistent naming (mostly `ApplyCommand` or specifically defined `RunCommand`).
