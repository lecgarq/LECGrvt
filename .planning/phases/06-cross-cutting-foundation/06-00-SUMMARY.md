---
phase: 06-cross-cutting-foundation
plan: "00"
subsystem: testing
tags: [xunit, logging, dialog-whitelist, tdd, red-tests, cross-cutting]

# Dependency graph
requires: []
provides:
  - "RED test suite for ILogger scope parameter (CROSS-01) under [Trait(\"Category\",\"CrossCutting\")]"
  - "RED test suite for IProgressReporter severity preservation (CROSS-02)"
  - "RED test suite for DialogWhitelist hit/miss/null/case-sensitivity (CROSS-03)"
  - "IDialogOverride seam interface defined in tests (RecordingDialogOverride fake)"
affects:
  - "06-01 (Wave 1 — ILogger contract + reporter migration): must turn LoggerSeverityTests GREEN"
  - "06-03 (Wave 3 — DialogWhitelist): must turn DialogWhitelistTests GREEN"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "TDD RED-first: test files committed before production types exist; build error = intended RED"
    - "IDialogOverride seam: thin adapter interface abstracting sealed Revit DialogBoxShowingEventArgs for unit testability"

key-files:
  created:
    - LECG.Tests/Services/Logging/LoggerSeverityTests.cs
    - LECG.Tests/Core/DialogWhitelistTests.cs
  modified: []

key-decisions:
  - "DialogWhitelistTests use IDialogOverride seam (not DialogBoxShowingEventArgs) to avoid Revit sealed type dependency in unit tests"
  - "Logger_ScopeParameterTests call scope: named arg — compile error confirms absence of scope parameter on ILogger"
  - "Reporter behavior tests reference future ILogger-based ctor signature — compile error confirms Wave 1 migration needed"
  - "Task 3 (DialogId runtime discovery) blocked: no live Revit 2026 session available; 06-DIALOG-DISCOVERY.md written as blocker artifact; Wave 3 must use LOW-confidence research fallback and mark all entries confidence: low"

patterns-established:
  - "CrossCutting trait: all Phase 6 tests use [Trait(\"Category\",\"CrossCutting\")] for filter isolation"
  - "RED-state acceptance: Wave 0 intentionally leaves build broken; Waves 1-3 restore GREEN"

requirements-completed: []

# Metrics
duration: 30min
completed: 2026-05-10
---

# Phase 6 Plan 00: Cross-Cutting Foundation Wave 0 Summary

**RED test scaffold for ILogger scope parameter (CROSS-01), IProgressReporter severity preservation (CROSS-02), and DialogWhitelist hit/miss/null/case behavior (CROSS-03) — all intentionally failing until Waves 1-3 land the production types**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-05-10T23:30:00Z
- **Completed:** 2026-05-10T23:55:00Z
- **Tasks:** 3 of 3 (Task 3 completed via blocker-path: 06-DIALOG-DISCOVERY.md written as blocker artifact)
- **Files modified:** 3

## Accomplishments

- Created `LoggerSeverityTests.cs` with 9 tests (4 scope-parameter compile guards + 6 severity-preservation behavior assertions) under `[Trait("Category","CrossCutting")]`
- Created `DialogWhitelistTests.cs` with 5 tests covering whitelist hit, miss, null DialogId, case-sensitivity, and empty whitelist under `[Trait("Category","CrossCutting")]`
- Established `IDialogOverride` seam pattern in test (eliminates Revit sealed type dependency)
- Build is RED as expected: missing `IDialogOverride`/`DialogWhitelist` types (CROSS-03 guard) and missing `scope:` parameter on `ILogger` methods (CROSS-01/02 guards)

## Task Commits

1. **Task 1: LoggerSeverityTests.cs (CROSS-01 + CROSS-02 RED tests)** - `88d7eda` (test)
2. **Task 2: DialogWhitelistTests.cs (CROSS-03 RED tests)** - `9386bb2` (test)
3. **Task 3: DialogId discovery — blocker artifact** - `547907e` (docs) — runtime access unavailable; 06-DIALOG-DISCOVERY.md written with blocker note, how-to-unblock procedure, and LOW-confidence research fallback table

## Files Created/Modified

- `LECG.Tests/Services/Logging/LoggerSeverityTests.cs` — 4 scope compile tests + 6 severity behavior tests across all 3 IProgressReporter impls
- `LECG.Tests/Core/DialogWhitelistTests.cs` — 5 dialog whitelist tests with RecordingDialogOverride seam
- `.planning/phases/06-cross-cutting-foundation/06-DIALOG-DISCOVERY.md` — DialogId discovery blocker artifact: explains why capture was not performed, provides 8-step unblock procedure, lists LOW-confidence research fallback entries, and includes placeholder tables for future runtime-confirmed data

## Decisions Made

- `IDialogOverride` seam placed in test namespace as `file sealed class RecordingDialogOverride` — Wave 3 will define the production `IDialogOverride` interface in `LECG.Core` namespace
- Reporter behavior tests target the Wave 1 ILogger-based constructor signature, not the current callback-based signature — intentional; tests drive the contract change
- Logger scope tests use named argument `scope: "Test"` — C# named argument binding ensures the test will compile only once a `scope` parameter exists on the method signature
- Task 3 blocker path: 06-DIALOG-DISCOVERY.md written as a documented blocker artifact (not a captured enumeration) — Claude Code cannot drive a live Revit 2026 session; Wave 3 falls back to LOW-confidence research guesses and must mark each whitelist entry as `confidence: low — unverified` in code/tests until a real discovery pass replaces the file

## Deviations from Plan

### Blocker-Path Execution (Task 3)

**Task 3 was completed via the documented blocker path, not the nominal capture path.**

- **Found during:** Task 3 (checkpoint:human-action resume)
- **Issue:** Live Revit 2026 session was unavailable for DialogId runtime enumeration. Claude Code cannot drive a live Revit session.
- **Resolution:** Wrote `06-DIALOG-DISCOVERY.md` as a blocker artifact per the plan's `<resume-signal>` clause: "If blocked, the planner will need to defer Wave 3 OR Wave 3's whitelist will ship with the LOW-confidence guesses from RESEARCH.md." Wave 3 will use the LOW-confidence fallback with mandatory confidence annotations in code.
- **Files created:** `.planning/phases/06-cross-cutting-foundation/06-DIALOG-DISCOVERY.md`
- **Commit:** `547907e`

All other tasks executed exactly as written.

## Issues Encountered

- The LoggerSeverityTests.cs for `scope:` named argument: `Logger.cs` already has `LogWarning(string, string categoryName, Exception?)` overloads on the concrete class. The test uses `ILogger` (interface type) where the `scope:` overload does not exist, so the compile error is correctly produced via the interface constraint. Verified: build fails with CS errors for missing DialogWhitelist/IDialogOverride types from DialogWhitelistTests.cs.

## Wave 3 Fallback Requirement

**Wave 3 (plan 06-03 — DialogWhitelist) MUST follow these rules when 06-DIALOG-DISCOVERY.md is still in `status: blocked`:**

1. Use only the LOW-confidence entries from `06-DIALOG-DISCOVERY.md §"LOW-Confidence Research Fallback"` (sourced from `06-RESEARCH.md §"Proposed whitelist entries"`).
2. Annotate EVERY whitelist entry in production code with: `// confidence: low — unverified; replace after real discovery pass populates 06-DIALOG-DISCOVERY.md`
3. Mark corresponding test assertions with the same note.
4. Do NOT expand the whitelist with additional guesses beyond those listed in `06-DIALOG-DISCOVERY.md`.

**To unblock:** Follow the 8-step procedure in `06-DIALOG-DISCOVERY.md §"How to Unblock"`, then update that file's frontmatter to `status: captured` and rerun Wave 3's DialogWhitelist plan to replace provisional entries with confirmed values.

## Next Phase Readiness

- RED tests are in place — Waves 1-3 know exactly which tests they must turn GREEN
- Wave 1 (plan 06-01) can begin immediately: ILogger scope parameter + reporter migration (CROSS-01 + CROSS-02)
- Wave 2 (plan 06-02) Logger.Instance migration sweep — independent of dialog discovery
- Wave 3 (plan 06-03) DialogWhitelist uses LOW-confidence research fallback from `06-DIALOG-DISCOVERY.md`; all whitelist entries must be annotated as provisional
- Wave 4 (plan 06-04) GAPS-01 documentation artifact — independent of dialog discovery

---
*Phase: 06-cross-cutting-foundation*
*Completed: 2026-05-10 (all 3 tasks done — Task 3 via blocker-path)*
