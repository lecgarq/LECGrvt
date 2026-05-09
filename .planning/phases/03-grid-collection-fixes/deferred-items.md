# Phase 03 — Deferred Items

## Plan 03-03 (recorded 2026-05-09)

### Out-of-scope build errors observed during Task 2 smoke check

Two pre-existing CS errors block a clean full-suite build. Both stem from
Plan 03-02's introduction of `ElementRowViewModel` while leaving
`SearchReplaceService` / `SearchReplacePreviewService` referencing the
legacy `ReplaceItem` type. STATE.md captures the deliberate handoff:

> [Phase 03]: ReplaceItem retained intact in Plan 03-02; Plan 03-04 owns
> SearchReplacePreviewService migration to keep wave-1 builds clean

#### Errors

1. `src/Services/Renaming/SearchReplaceService.cs(58,20) CS0029`
   Cannot implicitly convert
   `List<LECG.ViewModels.Components.ElementRowViewModel>`
   to `List<LECG.ViewModels.ReplaceItem>`.

2. `src/Services/Renaming/SearchReplacePreviewService.cs(111,29) CS1503`
   Argument 1: cannot convert `LECG.ViewModels.ReplaceItem` to
   `LECG.ViewModels.Components.ElementRowViewModel`.

#### Resolution

Plan 03-04 (SearchReplacePreviewService → ElementRowViewModel migration)
will eliminate both errors as part of its scope. Confirmed pre-existing
by building HEAD~1 (the parent of Plan 03-03's Task 1 commit) — same
errors present (in fact, four; Plan 03-03's BaseElementCollectionService
refactor reduced the count to two).

#### Why not auto-fix here

SCOPE BOUNDARY: errors are not caused by Plan 03-03's changes; they
exist in unrelated files; the migration is the planned deliverable of
Plan 03-04 in the same wave-2.
