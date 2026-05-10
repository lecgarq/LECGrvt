---
phase: 03-grid-collection-fixes
plan: 03
subsystem: renaming
tags: [revit, element-collection, locale-safe, no-blanks-invariant, REQ-01]

# Dependency graph
requires:
  - phase: 03-grid-collection-fixes
    provides: ElementLabelService.GetLabels(Element) + GetLabelsFromRaw (Plan 03-01)
provides:
  - Null-Category skip removed from BaseElementCollectionService typeCollector loop
  - GraphicsStyle null-category fallback now produces an ElementData row + LogView warning instead of silent skip
  - FamilyInstance null-Symbol/null-Family path now logs LogView warning before continuing
  - BaseElementCollectionServiceTests converted from Skip-gated RED scaffolds to GREEN coverage
affects: [03-04 SearchReplacePreviewService migration, 03-05 grid VM fallbacks, 03-09 Revit verification]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Collection layer is the single enforcement point for the no-blanks invariant; every Revit-API row goes through ElementLabelService.GetLabels"
    - "Null-skip → LogView warning + fallback row (visibility over silence)"

key-files:
  created: []
  modified:
    - src/Services/Renaming/BaseElementCollectionService.cs
    - LECG.Tests/Services/BaseElementCollectionServiceTests.cs

key-decisions:
  - "Tests delegate to ElementLabelService.GetLabelsFromRaw rather than introducing a thin NormalizeForRow shim — the SSoT helper is already public, no need to expand BaseElementCollectionService surface"
  - "GraphicsStyle null-category path emits a fallback row + warning rather than silently skipping (REQ-01: every collected element produces a row)"
  - "FamilyInstance null-Family path retains the skip (no usable parent for fallback) but is now observable via LogView warning"

patterns-established:
  - "Pattern: collector loops route every row through ElementLabelService.GetLabels(Element) — guarantees non-blank Name and Category, locale-safe"
  - "Pattern: any remaining 'continue' on degenerate Revit data emits Logger.Instance.LogWarning before continuing"

requirements-completed: [REQ-01]

# Metrics
duration: 12min
completed: 2026-05-09
---

# Phase 03 Plan 03: BaseElementCollectionService Null-Skip Removal Summary

**Removes the load-bearing data-loss bug behind REQ-01 — elements with `Category == null` (and GraphicsStyle entries with `GraphicsStyleCategory == null`) now produce ElementData rows via ElementLabelService.GetLabels instead of being silently dropped from grids.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-05-09T18:36:00Z
- **Completed:** 2026-05-09T18:48:25Z
- **Tasks:** 2
- **Files modified:** 2 (1 src, 1 test)

## Accomplishments

- typeCollector loop (former line 21): replaced `if (el.Category == null) continue;` with `ElementLabelService.GetLabels(el)`. Both Name and Category fields are now populated from the SSoT helper for every element.
- GraphicsStyle loop (former line 106): null-`GraphicsStyleCategory` no longer drops the row — falls through to `ElementLabelService.GetLabels(gs)`, emits `Logger.LogWarning`, and adds a row tagged `Type = "ObjectStyle"`.
- FamilyInstance Phase B (former line 251): null `fi.Symbol?.Family` skip retained (no usable parent label) but now logs a `Logger.LogWarning` so the skip is observable in LogView.
- `using LECG.Services.Logging;` added so the file can call `Logger.Instance.LogWarning`.
- `BaseElementCollectionServiceTests`: 3 Skip-gated placeholders converted to GREEN coverage (4/4 passing) targeting `ElementLabelService.GetLabelsFromRaw` — the SSoT helper that the collector now delegates to.

**Guard sites replaced:** 2 (typeCollector, GraphicsStyle).
**LogView warning surfaces added:** 2 (GraphicsStyle fallback, FamilyInstance null-Family skip).
**`if (... == null) continue;` lines remaining in file that relate to Category:** 0.

## Task Commits

1. **Task 1: Replace null-Category skip with ElementLabelService fallback** — `e18760b` (feat)
2. **Task 2: Smoke check — full unit suite green** — no commit (verification gate; pre-existing build errors documented in deferred-items.md as out-of-scope per STATE.md handoff to Plan 03-04)

## Files Created/Modified

- `src/Services/Renaming/BaseElementCollectionService.cs` — null-skip removal in typeCollector + GraphicsStyle paths; LogView warnings on FamilyInstance null-Family + GraphicsStyle fallback paths; `using LECG.Services.Logging;` added.
- `LECG.Tests/Services/BaseElementCollectionServiceTests.cs` — 3 Skip-gated tests converted to GREEN tests against `ElementLabelService.GetLabelsFromRaw` (no-blanks invariant for blank name, null category, both-blank cases). Anchor test retained.
- `.planning/phases/03-grid-collection-fixes/deferred-items.md` — created to log out-of-scope pre-existing build errors in `SearchReplaceService` / `SearchReplacePreviewService` (Plan 03-04's deliverable per STATE.md).

## Lines Changed

`src/Services/Renaming/BaseElementCollectionService.cs`:
- Line 4 (added): `using LECG.Services.Logging;`
- Lines 19-30 (modified): typeCollector inner block — removed null-Category skip, swap raw `el.Name`/`el.Category.Name` for `ElementLabelService.GetLabels(el)` tuple destructure.
- Lines 106-127 (modified): GraphicsStyle null-Category block — replace `continue;` with fallback row construction + LogView warning.
- Lines 250-260 (modified): FamilyInstance null-Family block — retain `continue;` but precede with LogView warning.

`LECG.Tests/Services/BaseElementCollectionServiceTests.cs`:
- Whole file rewritten: 3 Skip-gated placeholders (lines 35-66) replaced with 3 GREEN tests asserting the no-blanks invariant via `ElementLabelService.GetLabelsFromRaw`. Anchor test preserved.

## Decisions Made

- Per plan task-1 step 6 prefer-the-latter guidance: tests directly assert the SSoT helper rather than introducing a thin `NormalizeForRow` shim on `BaseElementCollectionService`. Keeps `ElementLabelService` as the single API, avoids namespace pollution.
- GraphicsStyle null-category produces a fallback row (not just a warning + skip) because REQ-01's invariant says "every collected element produces a row" — a GraphicsStyle the user can see in Revit must appear in the grid even when its category is unset.
- FamilyInstance null-Family retains the skip — there's no usable parent label, but the change makes the skip observable.

## Deviations from Plan

None. Plan executed as specified. The minor judgment call (test rewrite vs NormalizeForRow shim) was explicitly the plan's preferred path.

## Issues Encountered

- **Pre-existing build errors in `SearchReplaceService.cs` and `SearchReplacePreviewService.cs`** surfaced during Task 2 full-suite build. These reference `ElementRowViewModel` (Plan 03-02) vs `ReplaceItem` (legacy) and are explicitly Plan 03-04's domain per STATE.md (`[Phase 03]: ReplaceItem retained intact in Plan 03-02; Plan 03-04 owns SearchReplacePreviewService migration to keep wave-1 builds clean`). Verified pre-existing by building HEAD~1 (4 errors there; Plan 03-03's clean compile of BaseElementCollectionService reduced count to 2). Documented in `deferred-items.md`. **Not a regression introduced by Plan 03-03.**
- BaseElementCollectionServiceTests run successful (4/4 passing) against the existing assembly — the test execution path didn't require rebuilding the WPF main assembly.

## User Setup Required

None.

## Next Phase Readiness

- BaseElementCollectionService is now the single enforcement point of the no-blanks invariant for Revit-API-backed grids.
- Plan 03-04 can proceed: SearchReplacePreviewService → ElementRowViewModel migration (will resolve the deferred CS0029/CS1503 build errors as part of its scope).
- Plan 03-09 (Revit-session manual verification of REQ-01) will exercise the new fallback rows on a real document.

## Self-Check: PASSED

- **`src/Services/Renaming/BaseElementCollectionService.cs`** — modified, FOUND.
- **`LECG.Tests/Services/BaseElementCollectionServiceTests.cs`** — modified, FOUND.
- **Commit `e18760b`** — present in git log, FOUND.
- **`if (el.Category == null) continue;`** — 0 occurrences remaining in BaseElementCollectionService.cs.
- **`ElementLabelService.GetLabels`** call sites — 2 (typeCollector + GraphicsStyle fallback). Match plan key_links pattern.
- **`Logger.Instance.LogWarning`** call sites added — 2 (GraphicsStyle fallback + FamilyInstance Phase B). Match plan key_links pattern.
- **BaseElementCollectionServiceTests** — 4/4 passing.

---
*Phase: 03-grid-collection-fixes*
*Completed: 2026-05-09*
