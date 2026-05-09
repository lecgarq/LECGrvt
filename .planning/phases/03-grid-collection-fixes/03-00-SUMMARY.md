---
phase: 03-grid-collection-fixes
plan: 00
subsystem: testing
tags: [xunit, fluent-assertions, nsubstitute, red-tests, nyquist]

# Dependency graph
requires:
  - phase: 02.5
    provides: green test suite baseline (xUnit + FluentAssertions + NSubstitute infrastructure)
provides:
  - Four xUnit fixtures (RED scaffolds) wired into LECG.Tests, ready for Plans 03-01..05 to flip GREEN
  - Per-task verification map covering every implementation task in plans 03-01..09
  - wave_0_complete and nyquist_compliant flags flipped in 03-VALIDATION.md
affects: [03-01, 03-02, 03-03, 03-04, 03-05, 03-06, 03-07, 03-08, 03-09]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "RED test scaffolds with [Fact(Skip=...)] gating until implementing plan lands"
    - "Anchor tests on type references / parameterless ctors to keep skip-only fixtures discoverable"
    - "Pure-string overloads (GetLabelsFromRaw, future NormalizeForRow) for unit testability without Revit Document"

key-files:
  created:
    - LECG.Tests/Services/BaseElementCollectionServiceTests.cs
    - LECG.Tests/Services/SearchReplacePreviewServiceTests.cs
    - LECG.Tests/ViewModels/SearchReplaceViewModelTests.cs
  modified:
    - .planning/phases/03-grid-collection-fixes/03-VALIDATION.md
  pre_existing:
    - LECG.Tests/Services/ElementLabelServiceTests.cs (created/consumed by Plan 03-01 commit 7cc1923)

key-decisions:
  - "Skip-gate RED tests with reason strings pointing at implementing plan ID — keeps build green and gives plan authors a greppable 'remove this Skip' flip-point"
  - "One non-skipped anchor test per fixture (type-existence or ctor smoke) so the fixture is discoverable by `dotnet test --filter` even when all behavioral tests are skip-gated"
  - "SearchReplacePreviewService anchor test asserts today's ReplaceItem-shaped behavior; explicitly marked 'delete in 03-04' so the migration plan removes it as part of the GREEN flip"
  - "BaseElementCollectionService null-Category coverage is Skip-gated rather than direct — direct unit coverage requires a Revit Document stub. Plan 03-03 will add a pure-string NormalizeForRow helper that this fixture targets"

patterns-established:
  - "RED scaffold convention: [Fact(Skip=\"Awaiting Plan 03-XX — {feature}\")] + one anchor [Fact] per fixture"
  - "VALIDATION.md per-task map row format: Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status"

requirements-completed: [REQ-01]

# Metrics
duration: 6min
completed: 2026-05-09
---

# Phase 03 Plan 00: Wave 0 Test Scaffolds Summary

**Four xUnit RED fixtures (ElementLabelService, BaseElementCollectionService, SearchReplacePreviewService, SearchReplaceViewModel) wired into LECG.Tests with skip-gated assertions targeting Plans 03-01..05 — every downstream implementation task now has a concrete `dotnet test --filter` to flip GREEN.**

## Performance

- **Duration:** ~6 min
- **Started:** 2026-05-09T18:36:42Z
- **Completed:** 2026-05-09T18:42:13Z
- **Tasks:** 3
- **Files created:** 3 (test fixtures)
- **Files modified:** 1 (03-VALIDATION.md)

## Accomplishments

- Three new RED test fixtures committed to LECG.Tests, each with one non-skipped anchor and N skip-gated behavioral tests
- Confirmed Plan 03-01's pre-existing ElementLabelServiceTests fixture (commit 7cc1923) satisfies row 3-W0-01
- Per-task verification map in 03-VALIDATION.md updated to cover plans 03-01 through 03-09 with concrete `dotnet test --filter` / `dotnet build` commands
- `wave_0_complete: true` and `nyquist_compliant: true` flipped — every downstream implementation task now references an existing fixture or a build/suite gate
- Test project compiles green with `-p:SkipRevitDeploy=true` (post-build copy to ProgramData is locked by running Revit; not a code defect)

## Task Commits

1. **Task 1: ElementLabelServiceTests fixture (3-W0-01)** — `7cc1923` (feat — combined with Plan 03-01 GREEN flip)
2. **Task 2: Three RED fixtures (3-W0-02..04)** — `98fcfb8` (test)
3. **Task 3: VALIDATION.md flag flip + per-task map** — `53d3174` (docs)

_Note: Task 1's fixture was authored and consumed by Plan 03-01 in a single commit (7cc1923) per the linter-applied edit on the original RED scaffold — the production type already existed when Plan 03-00 reached Task 1, so the Skip-gated RED state collapsed straight to GREEN. This is acceptable per Plan 03-00's stated done criteria ("either skipped with reason ... or failing with NotImplementedException ... commit lands on branch") since the underlying fixture and verification command exist on the branch._

## Files Created/Modified

- `LECG.Tests/Services/BaseElementCollectionServiceTests.cs` — RED scaffold for 3-W0-02; anchor test + 3 skip-gated tests targeting Plan 03-03 NormalizeForRow helper
- `LECG.Tests/Services/SearchReplacePreviewServiceTests.cs` — RED scaffold for 3-W0-03; ReplaceItem-shaped anchor (delete in 03-04) + 3 skip-gated ElementRowViewModel-shaped tests
- `LECG.Tests/ViewModels/SearchReplaceViewModelTests.cs` — RED scaffold for 3-W0-04; ctor anchor + 2 skip-gated tests targeting Plan 03-05 ICollectionView wiring
- `.planning/phases/03-grid-collection-fixes/03-VALIDATION.md` — flags flipped, per-task map updated to plans 03-01..09, Wave 0 checkboxes ticked, approval line updated

## Skip-Gate Inventory

| Fixture | Anchor (always run) | Skip-gated | Reason → unblocks at |
|---------|---------------------|------------|----------------------|
| ElementLabelServiceTests | (none — already GREEN) | 0 | n/a (consumed by 03-01) |
| BaseElementCollectionServiceTests | Type_is_referenceable_anchor | 3 | "Awaiting Plan 03-03 — NormalizeForRow helper" |
| SearchReplacePreviewServiceTests | Anchor_ProcessPreview_returns_ReplaceItem_for_typed_element | 3 | "Awaiting Plan 03-04 — ProcessPreview returns ReplaceItem today" |
| SearchReplaceViewModelTests | ViewModel_constructs_with_default_state | 2 | "Awaiting Plan 03-05 — ICollectionView sort/filter wiring" |

## Decisions Made

- **Skip-gate over compile-fail RED:** plan allowed both; chose skip-gate so the rest of LECG.Tests stays buildable. Skip reasons are greppable strings naming the plan that flips them GREEN.
- **Anchor tests:** every skip-only fixture would fail to be discovered by `dotnet test --filter "FullyQualifiedName~..."` on some runners. One non-skipped anchor per fixture guarantees discovery and forces a real type/ctor reference.
- **Anchor in 03-W0-03 marked "delete in 03-04":** the anchor exercises today's `List<ReplaceItem>` return shape; Plan 03-04 migrates to `ElementRowViewModel`, so the anchor must be deleted at that point. Naming the anchor with the deletion plan in `DisplayName` makes it self-documenting.

## Deviations from Plan

None affecting Tasks 2 or 3.

**Note on Task 1 ordering:** When Plan 03-00 began Task 1, `LECG.Services.ElementLabelService` was already implemented on disk (Plan 03-01 had been executed in advance, commit 7cc1923 on this branch). The linter promptly stripped the Skip attributes and stub from the freshly-written RED scaffold, collapsing the file to the GREEN form Plan 03-01 expected. Both Plan 03-00 done criteria ("commit lands on branch", "fixture exists") and Plan 03-01's GREEN gate are satisfied by the single 7cc1923 commit. No deviation rule fired — this was a sequencing artifact of the prior planner-executor cycle, not a defect introduced now.

## Issues Encountered

- **Revit-locked DLL copy:** `dotnet build LECG.Tests/LECG.Tests.csproj` failed with `MSB3027/MSB3021` errors trying to copy `LECG.dll`, `Serilog.dll`, etc. to `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG\` because Autodesk Revit (PID 36872) holds the files. Per the spawning prompt's `<environment_note>`, this is **not a code defect** — it is the running Revit instance holding the loaded assemblies. Resolved by passing `-p:SkipRevitDeploy=true` (existing flag in `LECG.csproj`'s `DeployToRevit` target). With the deploy step skipped, the build is green and tests run.

## Verification

- `dotnet build LECG.Tests/LECG.Tests.csproj --no-restore -p:SkipRevitDeploy=true` → Build succeeded
- `dotnet test --filter "FullyQualifiedName~ElementLabelServiceTests"` → 11 passed, 0 skipped (already GREEN via 03-01)
- `dotnet test --filter "FullyQualifiedName~BaseElementCollectionServiceTests"` → 1 passed (anchor), 3 skipped — RED
- `dotnet test --filter "FullyQualifiedName~SearchReplacePreviewServiceTests"` → 1 passed (anchor), 3 skipped — RED
- `dotnet test --filter "FullyQualifiedName~SearchReplaceViewModelTests"` → 1 passed (anchor), 2 skipped — RED
- `grep -c "nyquist_compliant: true" 03-VALIDATION.md` → 1+ occurrence in frontmatter (also appears in body prose; both expected)
- `grep -c "wave_0_complete: true" 03-VALIDATION.md` → 1

## Next Plan Readiness

Plans 03-03, 03-04, 03-05 each have a concrete fixture file + skip-gated test list to flip GREEN. Plan 03-04 must additionally delete the `Anchor_ProcessPreview_returns_ReplaceItem_for_typed_element` test as part of its GREEN migration (this is documented in the test's `DisplayName`). Plans 03-06..09 use compile/suite gates only — they consume the test infrastructure but do not drive new fixtures.

---
*Phase: 03-grid-collection-fixes*
*Completed: 2026-05-09*

## Self-Check: PASSED

All 6 expected files exist on disk; all 3 task commits (7cc1923, 98fcfb8, 53d3174) found in git log.
