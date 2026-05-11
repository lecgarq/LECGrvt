---
phase: 06-cross-cutting-foundation
plan: "03"
subsystem: core
tags: [dialog-whitelist, cross-cutting, tdd, command-refactor]

# Dependency graph
requires:
  - phase: 06-00
    provides: Wave 0 RED tests (DialogWhitelistTests.cs) that this wave turns GREEN
  - phase: 06-02
    provides: ServiceLocator-based ILogger resolution in all command handlers
provides:
  - DialogWhitelist.Global production instance populated with 5 LOW-confidence research-fallback entries
  - Apply(DialogBoxShowingEventArgs, ILogger) convenience overload for command handler use
  - PurgeCommand.OnDialogShowing delegates entirely to DialogWhitelist — cancel-all removed
  - ConvertFamilyCommand.OnDialogShowing delegates entirely to DialogWhitelist — substring heuristic removed
  - CROSS-03 requirement fulfilled
affects:
  - Phases 7-11: any command adding dialog handling should use DialogWhitelist.Global.Apply pattern

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "DialogWhitelist.Global: static production instance; tests instantiate via constructor for isolation"
    - "EventArgsAdapter: private sealed class bridging sealed Revit DialogBoxShowingEventArgs to IDialogOverride seam"
    - "Reach-user default: unknown DialogId emits Warning and does NOT call OverrideResult"
    - "Confidence annotation: every LOW-confidence entry annotated with // confidence: low — unverified"

key-files:
  created: []
  modified:
    - src/Core/DialogWhitelist.cs
    - src/Commands/PurgeCommand.cs
    - src/Commands/ConvertFamilyCommand.cs

key-decisions:
  - "DialogWhitelist uses instance constructor for tests + static Global for production — not a static class; this matches the Wave 0 test contract (new DialogWhitelist(dict))"
  - "Fallback to 5 LOW-confidence entries from 06-DIALOG-DISCOVERY.md (blocked) — annotated confidence: low — unverified on every entry"
  - "ConvertFamilyCommand substring heuristic (safe/dangerous/unknown branches) fully deleted — behavior change accepted per CONTEXT §3.6"
  - "PurgeCommand cancel-all blanket dismiss (e.OverrideResult(2)) fully deleted — behavior change accepted"
  - "IDialogOverride stays in same file as DialogWhitelist (not a separate IDialogOverride.cs) — Wave 1 stub shape preserved for compile continuity"

requirements-completed: [CROSS-03]

# Metrics
duration: 3min
completed: 2026-05-11
---

# Phase 06 Plan 03: DialogWhitelist Implementation Summary

**DialogWhitelist populated with 5 LOW-confidence provisional entries; PurgeCommand cancel-all and ConvertFamilyCommand substring heuristic replaced with single-line delegation to DialogWhitelist.Global.Apply**

## Performance

- **Duration:** ~3 min
- **Started:** 2026-05-11T06:17:37Z
- **Completed:** 2026-05-11T06:20:31Z
- **Tasks:** 2 (both TDD-aligned: tests were already GREEN from Wave 1 stub; this wave fills real content)
- **Files modified:** 3

## Accomplishments

- `DialogWhitelist.Global` populated with 5 entries from the LOW-confidence fallback table in `06-DIALOG-DISCOVERY.md`
- Every entry annotated: `// confidence: low — unverified; replace after real discovery pass populates 06-DIALOG-DISCOVERY.md`
- `Apply(DialogBoxShowingEventArgs e, ILogger logger)` convenience overload added via private `EventArgsAdapter`
- `PurgeCommand.OnDialogShowing`: 4-line cancel-all body replaced with `DialogWhitelist.Global.Apply(e, logger)`
- `ConvertFamilyCommand.OnDialogShowing`: 40-line substring-heuristic block (3 branches) deleted and replaced with same single-line delegation
- All 15 CrossCutting tests GREEN (DialogWhitelist + severity + reporter)
- Full test suite: 190 passed / 5 skipped / 0 failed (no regressions)
- Zero `Logger.Instance` references in `src/` (Wave 2 result intact)
- Zero direct `e.OverrideResult(...)` calls inside Purge/ConvertFamily dialog handlers

## Task Commits

1. **Task 1: Populate DialogWhitelist.Global + add EventArgs overload** — `4199a7f` (feat)
2. **Task 2: Migrate PurgeCommand + ConvertFamilyCommand handlers** — `3cfc2ae` (feat)

## Files Modified

- `src/Core/DialogWhitelist.cs` — Added 5 LOW-confidence whitelist entries to `Global`; added `Apply(DialogBoxShowingEventArgs, ILogger)` overload; added private `EventArgsAdapter` sealed class
- `src/Commands/PurgeCommand.cs` — `OnDialogShowing` body replaced; old cancel-all + `OverrideResult(2)` deleted
- `src/Commands/ConvertFamilyCommand.cs` — `OnDialogShowing` body replaced; old 3-branch substring heuristic (40 lines) deleted

## Decisions Made

- **Instance + Global pattern:** `DialogWhitelist` is a class (not static) to support `new DialogWhitelist(dict)` in tests. `Global` is a static field holding the production instance. This matches the Wave 0 test contract exactly.
- **IDialogOverride in same file:** The interface was defined in `DialogWhitelist.cs` by Wave 1. No separate `IDialogOverride.cs` file was created. The plan listed it as a separate file but the Wave 1 stub merged them — preserving the existing shape avoids churn.
- **Confidence annotations not `TODO:`:** Wave 0 mandate used `// confidence: low — unverified` phrasing. The plan action section used `// TODO: confirm at runtime` as an alternative. Used the Wave 0 mandate wording for consistency.
- **Behavior change accepted:** Any dialog NOT in the whitelist now reaches the user. This replaces the previous "cancel everything" defaults in both commands. Per CONTEXT §3.6, this is the intended outcome of CROSS-03.

## Deviations from Plan

### Structural Deviation: Tests Already GREEN Before Task 1 Implementation

- **Found during:** Pre-execution test run
- **Issue:** Wave 1 (plan 06-01) created a fully-functional `DialogWhitelist` stub with the correct constructor and `Apply(string?, IDialogOverride, ILogger)` signature to unblock Wave 0 test compilation. All 15 CrossCutting tests were already passing before Task 1 started.
- **Resolution:** Task 1 still executed (populating entries + adding EventArgs overload) as these are production requirements, not test-only. The TDD flow was GREEN from the start — the "RED" phase was Wave 0's contribution.
- **Impact:** None on output quality. The tests correctly validated the final implementation.

No other deviations. Plan executed exactly as written.

## Wave 3 Fallback Compliance

As required by `06-00-SUMMARY.md §"Wave 3 Fallback Requirement"`:
- [x] Only the 5 LOW-confidence entries from `06-DIALOG-DISCOVERY.md §"LOW-Confidence Research Fallback"` were used
- [x] Every entry annotated: `// confidence: low — unverified; replace after real discovery pass populates 06-DIALOG-DISCOVERY.md`
- [x] No additional guesses added beyond the 5 listed entries
- [x] `06-DIALOG-DISCOVERY.md` frontmatter remains `status: blocked` (not modified)

## To Unblock Runtime Discovery

Once a live Revit 2026 session is available, follow the 8-step procedure in `06-DIALOG-DISCOVERY.md §"How to Unblock"`. After capturing real DialogId values:
1. Update `06-DIALOG-DISCOVERY.md` frontmatter to `status: captured`
2. Replace the 5 provisional entries in `DialogWhitelist.Global` with confirmed values
3. Remove the `confidence: low` annotations from confirmed entries
4. Run `dotnet test LECG.Tests --filter "Category=CrossCutting"` to verify

## Self-Check: PASSED

- FOUND: `src/Core/DialogWhitelist.cs` — modified with 5 entries + EventArgsAdapter
- FOUND: `src/Commands/PurgeCommand.cs` — OnDialogShowing is single-line delegation
- FOUND: `src/Commands/ConvertFamilyCommand.cs` — OnDialogShowing is single-line delegation
- FOUND commits: `4199a7f`, `3cfc2ae`
- 15 CrossCutting tests GREEN
- 190 passed / 5 skipped / 0 failed (full suite)
- Zero Logger.Instance refs in src/
- Zero direct OverrideResult calls in Purge/ConvertFamily dialog handlers

---
*Phase: 06-cross-cutting-foundation*
*Completed: 2026-05-11*
