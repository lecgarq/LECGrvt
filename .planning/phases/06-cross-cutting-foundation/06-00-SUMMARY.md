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
  - "Task 3 (DialogId runtime discovery) is a checkpoint:human-action requiring live Revit access; blocked until user runs Purge+ConvertFamily discovery session"

patterns-established:
  - "CrossCutting trait: all Phase 6 tests use [Trait(\"Category\",\"CrossCutting\")] for filter isolation"
  - "RED-state acceptance: Wave 0 intentionally leaves build broken; Waves 1-3 restore GREEN"

requirements-completed: []

# Metrics
duration: 25min
completed: 2026-05-10
---

# Phase 6 Plan 00: Cross-Cutting Foundation Wave 0 Summary

**RED test scaffold for ILogger scope parameter (CROSS-01), IProgressReporter severity preservation (CROSS-02), and DialogWhitelist hit/miss/null/case behavior (CROSS-03) — all intentionally failing until Waves 1-3 land the production types**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-05-10T23:30:00Z
- **Completed:** 2026-05-10T23:55:00Z
- **Tasks:** 2 of 3 (Task 3 blocked at checkpoint:human-action — live Revit required)
- **Files modified:** 2

## Accomplishments

- Created `LoggerSeverityTests.cs` with 9 tests (4 scope-parameter compile guards + 6 severity-preservation behavior assertions) under `[Trait("Category","CrossCutting")]`
- Created `DialogWhitelistTests.cs` with 5 tests covering whitelist hit, miss, null DialogId, case-sensitivity, and empty whitelist under `[Trait("Category","CrossCutting")]`
- Established `IDialogOverride` seam pattern in test (eliminates Revit sealed type dependency)
- Build is RED as expected: missing `IDialogOverride`/`DialogWhitelist` types (CROSS-03 guard) and missing `scope:` parameter on `ILogger` methods (CROSS-01/02 guards)

## Task Commits

1. **Task 1: LoggerSeverityTests.cs (CROSS-01 + CROSS-02 RED tests)** - `88d7eda` (test)
2. **Task 2: DialogWhitelistTests.cs (CROSS-03 RED tests)** - `9386bb2` (test)
3. **Task 3: DialogId runtime discovery** - BLOCKED (checkpoint:human-action — see below)

## Files Created/Modified

- `LECG.Tests/Services/Logging/LoggerSeverityTests.cs` — 4 scope compile tests + 6 severity behavior tests across all 3 IProgressReporter impls
- `LECG.Tests/Core/DialogWhitelistTests.cs` — 5 dialog whitelist tests with RecordingDialogOverride seam

## Decisions Made

- `IDialogOverride` seam placed in test namespace as `file sealed class RecordingDialogOverride` — Wave 3 will define the production `IDialogOverride` interface in `LECG.Core` namespace
- Reporter behavior tests target the Wave 1 ILogger-based constructor signature, not the current callback-based signature — intentional; tests drive the contract change
- Logger scope tests use named argument `scope: "Test"` — C# named argument binding ensures the test will compile only once a `scope` parameter exists on the method signature

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered

- The LoggerSeverityTests.cs for `scope:` named argument: `Logger.cs` already has `LogWarning(string, string categoryName, Exception?)` overloads on the concrete class. The test uses `ILogger` (interface type) where the `scope:` overload does not exist, so the compile error is correctly produced via the interface constraint. Verified: build fails with CS errors for missing DialogWhitelist/IDialogOverride types from DialogWhitelistTests.cs.

## User Setup Required (Task 3 Checkpoint)

**Task 3 requires live Revit access.** The user must:

1. Temporarily patch `PurgeCommand.OnDialogShowing` and `ConvertFamilyCommand.OnDialogShowing` with logging-only handlers (see plan §how-to-verify Steps 1-5)
2. Run Purge (Deep mode) and Convert Family against a representative dirty model
3. Capture every emitted `DialogId` from the log output
4. Create `.planning/phases/06-cross-cutting-foundation/06-DIALOG-DISCOVERY.md` with the captured data
5. Revert the temporary patch: `git checkout -- src/Commands/PurgeCommand.cs src/Commands/ConvertFamilyCommand.cs`
6. Commit only the markdown: `git add .planning/phases/06-cross-cutting-foundation/06-DIALOG-DISCOVERY.md && git commit -m "docs(phase-06): capture DialogId enumeration from runtime discovery"`
7. Signal resume: type "captured: N Purge, M Convert" (or describe the blocker)

## Next Phase Readiness

- RED tests are in place — Waves 1-3 know exactly which tests they must turn GREEN
- Wave 1 (plan 06-01) can begin immediately: ILogger scope parameter + reporter migration
- Wave 3 (plan 06-03) DialogWhitelist requires `06-DIALOG-DISCOVERY.md` to be populated; may use LOW-confidence RESEARCH.md guesses if discovery is blocked

---
*Phase: 06-cross-cutting-foundation*
*Completed: 2026-05-10 (partial — Task 3 awaits human-action checkpoint)*
