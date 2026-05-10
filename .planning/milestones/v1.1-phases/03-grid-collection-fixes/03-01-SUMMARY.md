---
phase: 03-grid-collection-fixes
plan: 01
subsystem: services
tags: [revit, locale-safe, labels, no-blanks-invariant, label-utils, built-in-category]

requires:
  - phase: 03-grid-collection-fixes
    provides: Wave 0 RED test scaffold (ElementLabelServiceTests with 7 Skip-gated tests)
provides:
  - LECG.Services.ElementLabelService — centralized non-blank Name+Category helper
  - GetLabels(Element) — locale-safe Revit-API overload
  - GetLabelsFromRaw(string,string,string,long) — pure-string overload (unit-testable)
affects:
  - 03-03 (BaseElementCollectionService null-Category replacement)
  - 03-06, 03-07, 03-08 (ViewModel element-to-row mapping)

tech-stack:
  added: []
  patterns:
    - "Locale-safe category labels via BuiltInCategory + LabelUtils.GetLabelFor"
    - "ALL_MODEL_TYPE_NAME fallback for blank Element.Name in non-English Revit"
    - "Pure-string overload alongside Revit-API overload for unit-testability without Revit Document"
    - "Synthetic name format <{ClrTypeName} {Id}> when raw name is blank"

key-files:
  created:
    - src/Services/ElementLabelService.cs
  modified:
    - LECG.Tests/Services/ElementLabelServiceTests.cs

key-decisions:
  - "Service placed in src/Services (LECG.csproj) — GetLabels(Element) requires Revit API, cannot live in LECG.Core"
  - "Last-resort category fallback is the CLR type name; if clrTypeName itself is blank, use the literal 'Element' (defensive)"
  - "Logger.LogWarning is fired only when a fallback path actually triggered (rawCategory blank OR Element.Name blank), not when ALL_MODEL_TYPE_NAME succeeded"
  - "Used explicit null-check on element.Category instead of `element.Category?.Id.Value` chain — the `?.` short-circuits to nullable long which mis-casts to BuiltInCategory"

patterns-established:
  - "Centralized label invariant: any Element-to-row mapping must call ElementLabelService.GetLabels — eliminates per-VM inline DescribeElement formatters"
  - "Pure-string overload pattern: extract pure logic from Revit-API entry point so unit tests run without Revit Document"

requirements-completed: [REQ-01]

duration: 8min
completed: 2026-05-09
---

# Phase 03 Plan 01: ElementLabelService Summary

**Centralized non-blank Name+Category helper for Revit elements, locale-safe via BuiltInCategory + LabelUtils, with pure-string overload covered by 11 GREEN unit tests.**

## Performance

- **Duration:** ~8 min
- **Completed:** 2026-05-09T18:39:07Z
- **Tasks:** 1 (TDD GREEN — RED scaffold authored by Plan 03-00)
- **Files created:** 1 (ElementLabelService.cs)
- **Files modified:** 1 (ElementLabelServiceTests.cs — removed stub + Skip attributes)

## Accomplishments

- Implemented `LECG.Services.ElementLabelService` static class with both Revit-API and pure-string overloads.
- Locale-safe category resolution via `LabelUtils.GetLabelFor((BuiltInCategory)cat.Id.Value)` with `cat.Name` fallback on cast failure.
- ALL_MODEL_TYPE_NAME fallback for blank `Element.Name` (non-English Revit pitfall, RESEARCH §Pitfall 6).
- Synthetic name format `<{ClrTypeName} {Id}>` enforced when raw name is null/empty/whitespace.
- Logger.LogWarning hook fires only when a fallback actually triggered.
- All 7 ElementLabelServiceTests methods (11 test cases including Theory inline data) flipped from RED/Skip to GREEN.

## Task Commits

1. **Task 1 (GREEN — implement ElementLabelService and pass tests):** `7cc1923` (feat)

_TDD note: Plan 03-00 already authored the RED scaffold; this plan went straight to GREEN. No separate test commit needed since the test file content was a refactor of the 03-00 scaffold (stub removal + unskip)._

## Files Created/Modified

- `src/Services/ElementLabelService.cs` (CREATED) — Static class with `GetLabels(Element)` and `GetLabelsFromRaw(string,string,string,long)`. Locale-safe; logs warnings on fallback.
- `LECG.Tests/Services/ElementLabelServiceTests.cs` (MODIFIED) — Removed RED stub class and `[Fact(Skip=...)]`/`[Theory(Skip=...)]` attributes. References `using LECG.Services;`.

## Decisions Made

- **Placement in LECG.csproj (not LECG.Core):** `GetLabels(Element)` requires Autodesk.Revit.DB; LECG.Core has no Revit reference. The pure-string overload `GetLabelsFromRaw` is what tests target — tests reference LECG.csproj in non-CI mode (the user's local dev path), so the unit-test path works.
- **Defensive `safeClr` value:** If `clrTypeName` itself is blank, fall back to the literal `"Element"` so the no-blanks invariant holds even for pathological inputs. The Theory test `GetLabelsFromRaw_never_returns_null_or_whitespace_for_either_field` exercises this.
- **Explicit null-check on Category** instead of `element.Category?.Id.Value` cast chain (per plan instructions). The `?.` chain returns `long?` which cannot be directly cast to `BuiltInCategory`.
- **Logger fallback gate:** Warning fires when `rawCategory` is blank OR original `element.Name` is blank — not when `ALL_MODEL_TYPE_NAME` successfully recovered the type name (no-op recovery is silent).

## Deviations from Plan

None — plan executed exactly as written. The plan provided a near-complete code sketch; only minor defensive improvements were added (`safeClr` for the case where `clrTypeName` itself is whitespace, ensuring the property-style theory test passes).

## Issues Encountered

- **Build deploy step fails when Revit is running** (file lock on `LECG.dll`/`LECG.Core.dll`/etc. in `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG`). This is the documented environmental blocker per the orchestrator's environment_note. Workaround: build with `-p:SkipRevitDeploy=true`. Compile and tests both succeed cleanly under this flag.
  - `dotnet build LECG.sln -p:SkipRevitDeploy=true` → Build succeeded, 0 warnings, 0 errors.
  - `dotnet test LECG.Tests --filter ~ElementLabelServiceTests -p:SkipRevitDeploy=true` → **Passed: 11, Failed: 0, Skipped: 0** (6 Facts + 5 Theory inline cases).

## Next Phase Readiness

- `ElementLabelService.GetLabels` is ready for callers in:
  - **Plan 03-03** — `BaseElementCollectionService` will replace the `if (el.Category == null) continue;` skip with `var (name, category) = ElementLabelService.GetLabels(el);`.
  - **Plans 03-06..08** — ViewModel element-to-row mappers will call `ElementLabelService.GetLabels` instead of inline `DescribeElement` formatters.
- No blockers for downstream plans.

## Self-Check: PASSED

- File exists: `src/Services/ElementLabelService.cs` ✓
- File modified: `LECG.Tests/Services/ElementLabelServiceTests.cs` ✓
- Commit `7cc1923` exists in git log ✓
- 11/11 ElementLabelServiceTests GREEN ✓
- Solution builds clean (0 errors, 0 warnings) with `SkipRevitDeploy=true` ✓

---
*Phase: 03-grid-collection-fixes*
*Plan: 01*
*Completed: 2026-05-09*
