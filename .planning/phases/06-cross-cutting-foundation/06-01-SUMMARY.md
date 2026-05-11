---
phase: 06-cross-cutting-foundation
plan: 01
subsystem: infra
tags: [logging, ilogger, progress-reporter, scope, severity, cross-cutting]

# Dependency graph
requires:
  - phase: 06-00
    provides: Wave 0 RED tests (LoggerSeverityTests.cs, DialogWhitelistTests.cs) that this wave turns GREEN
provides:
  - Extended ILogger contract with required scope: string parameter on all four severity methods
  - LogEntry.Scope property for structured log forwarding
  - Three severity-preserving IProgressReporter implementations (Simple, Legacy, RevitCommand)
  - DialogWhitelist + IDialogOverride stubs enabling Wave 0 DialogWhitelist tests to compile
  - [Obsolete] temporary single-arg Logger overloads as Wave 2 migration inventory (684 CS0618 warnings)
affects:
  - 06-02 (Logger.Instance migration sweep builds on this contract)
  - 06-03 (DialogWhitelist implementation fills the stubs created here)
  - All later v2.0 phases that consume ILogger or IProgressReporter

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ILogger scope parameter: all log calls carry an explicit string scope (caller context tag)"
    - "[Obsolete] shim pattern: concrete Logger class exposes legacy single-arg overloads to keep Logger.Instance callers compiling through Wave 1; Wave 2 deletes them"
    - "Dual-constructor IProgressReporter pattern: new ILogger ctor is the preferred path; old Action<> ctor is Obsolete for backward compat"

key-files:
  created:
    - src/Core/DialogWhitelist.cs
  modified:
    - src/Services/Infrastructure/Logging/Logger.cs
    - src/Services/Infrastructure/Logging/LogEntry.cs
    - src/Services/Infrastructure/IProgressReporter.cs
    - src/Services/Infrastructure/LegacyProgressReporter.cs
    - src/Services/Infrastructure/RevitCommandProgressReporter.cs
    - src/Services/Renaming/BatchRenameExecutionService.cs
    - src/ViewModels/LogViewModel.cs
    - src/Commands/AssignMaterialCommand.cs
    - src/Commands/CompactingStylesCommand.cs
    - src/Commands/ConvertFamilyCommand.cs
    - src/Commands/FixPointsCommand.cs
    - src/Commands/PurgeCommand.cs
    - src/Commands/RenderAppearanceMatchCommand.cs
    - src/Commands/SearchReplaceCommand.cs
    - src/Commands/SexyRevitCommand.cs
    - src/Commands/SimplifyPointsCommand.cs
    - src/Commands/TypeToLinkedModelsCommand.cs
    - LECG.Tests/Services/Logging/LoggerSeverityTests.cs
    - LECG.Tests/Services/BatchRenameSafeRenameTests.cs

key-decisions:
  - "Scope is required (non-optional) on ILogger interface — missing scope is a compile error enforcing migration discipline"
  - "Legacy single-arg Logger.Log/LogWarning/LogError/LogSuccess overloads retained as [Obsolete] on concrete Logger class only (NOT on ILogger) to keep Logger.Instance callers compiling through Wave 1"
  - "LegacyProgressReporter and SimpleProgressReporter keep the old Action<> constructors as [Obsolete] overloads because the plan underestimated callers (25+ for Legacy, 4+ for Simple) — Wave 2 removes them"
  - "Logger constructor no longer auto-captures Dispatcher.CurrentDispatcher — callers must call SetDispatcher explicitly; this makes new Logger() predictable in tests"
  - "DialogWhitelist + IDialogOverride stubs created in src/Core/ to unblock Wave 0 test compilation without implementing Wave 3 logic"

patterns-established:
  - "Scope-tagged logging: every ILogger call includes explicit scope = 'ComponentName' identifying the call origin"
  - "IProgressReporter severity forwarding: Log/LogWarning/LogError on IProgressReporter forward to the backing ILogger with severity preserved"

requirements-completed: [CROSS-01, CROSS-02]

# Metrics
duration: 75min
completed: 2026-05-10
---

# Phase 06 Plan 01: ILogger Scope Contract + Severity-Preserving IProgressReporter Summary

**ILogger extended with required scope parameter and three IProgressReporter impls rewritten to forward LogWarning/LogError severity to Logger.Entries via constructor-injected ILogger**

## Performance

- **Duration:** ~75 min
- **Started:** 2026-05-10T14:00:00Z
- **Completed:** 2026-05-10T15:15:00Z
- **Tasks:** 2 (both TDD: RED → GREEN)
- **Files modified:** 19 production + 2 test files

## Accomplishments

- `ILogger` interface now requires `string scope` on Log/LogSuccess/LogWarning/LogError — missing scope is a compile-time error enforcing migration discipline
- All three `IProgressReporter` implementations (`SimpleProgressReporter`, `LegacyProgressReporter`, `RevitCommandProgressReporter`) rewritten to forward severity: `LogWarning` → `LogLevel.Warning`, `LogError` → `LogLevel.Error` in `Logger.Entries`
- 15 CrossCutting xUnit tests (CROSS-01 + CROSS-02 + CROSS-03 scoping) pass GREEN
- 684 `CS0618` Obsolete warnings emitted on `Logger.Instance` one-arg callers — these form the exact Wave 2 migration inventory
- Full test suite: 190 passed / 5 skipped / 0 failed (baseline was 175/5/0; 15 new CrossCutting tests added)

## Task Commits

1. **Task 1: Extend ILogger contract with required scope parameter** - `f88c9fe` (feat)
2. **Task 2: Rewrite IProgressReporter implementations to forward severity via ILogger** - `6af236d` (feat)

## Files Created/Modified

- `src/Services/Infrastructure/Logging/Logger.cs` — New ILogger contract; Logger impl with scope; [Obsolete] legacy overloads; removed auto-dispatcher-capture in ctor
- `src/Services/Infrastructure/Logging/LogEntry.cs` — Added `string? Scope` property
- `src/Services/Infrastructure/IProgressReporter.cs` — SimpleProgressReporter rewritten with ILogger ctor; legacy Action<ProgressReport> ctor retained as [Obsolete]
- `src/Services/Infrastructure/LegacyProgressReporter.cs` — Rewritten with ILogger ctor; severity forwarding; legacy Action<> ctor retained as [Obsolete]
- `src/Services/Infrastructure/RevitCommandProgressReporter.cs` — Rewritten with ILogger ctor; severity forwarding
- `src/Core/DialogWhitelist.cs` — NEW: IDialogOverride interface + DialogWhitelist stub enabling Wave 0 tests to compile
- `src/Services/Renaming/BatchRenameExecutionService.cs` — All ILogger calls updated to new scope-aware signature ("BatchRename")
- `src/ViewModels/LogViewModel.cs` — LogSuccess updated to LogSuccess(msg, "LogView")
- 9 command files — RevitCommandProgressReporter construction sites updated to `Logger.Instance` (TEMPORARY: Wave 2 cleanup)
- `LECG.Tests/Services/Logging/LoggerSeverityTests.cs` — Added `using LECG.Services.Interfaces`
- `LECG.Tests/Services/BatchRenameSafeRenameTests.cs` — NSubstitute assertions updated to 2-arg ILogger interface

## Decisions Made

- **Scope required, not optional:** Context §1.3 mandated per-call scope; a missing scope is a compile error. This is the intended discipline mechanism.
- **[Obsolete] shim strategy:** `Logger.Instance` callers (30+ files, 684 call sites) continue compiling via single-arg overloads on the concrete `Logger` class marked `[Obsolete]`. Wave 2 eliminates all of them.
- **Dual-constructor on legacy reporters:** The plan stated "zero callers" for the old `Action<>` constructors — the actual count was 25+ for `LegacyProgressReporter` and 4+ for `SimpleProgressReporter`. Removing the old constructors immediately would have broken too many callers. Keeping them as `[Obsolete]` overloads is the Wave 1 pragmatic approach; Wave 2 migrates all callers.
- **Logger constructor no longer auto-captures dispatcher:** The old auto-capture caused `new Logger()` in tests to set up a `DispatcherTimer`, making `Entries.Add` asynchronous and causing severity tests to see empty `Entries`. Removing auto-capture makes constructor behavior predictable in tests. UI entry points already call `SetDispatcher` explicitly (RevitCommand.cs:121).
- **DialogWhitelist stub in Wave 1:** The `DialogWhitelistTests.cs` (from Wave 0) had a build error blocking the entire test project from compiling. Created stubs in `src/Core/` implementing the full CROSS-03 API contract so the tests compile and run (with Wave 3 filling in the actual whitelist data).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] BatchRenameExecutionService uses ILogger directly (not Logger.Instance)**
- **Found during:** Task 1 (ILogger contract extension)
- **Issue:** `BatchRenameExecutionService` injects `ILogger` via method parameters; the `[Obsolete]` single-arg shims live only on the concrete `Logger` class, not the interface — so its 20+ calls to `logger.Log(msg)` became compile errors under the new interface
- **Fix:** Updated all ILogger calls in BatchRenameExecutionService to use the new scope-aware 2-arg signatures with scope `"BatchRename"`
- **Files modified:** `src/Services/Renaming/BatchRenameExecutionService.cs`
- **Verification:** Build passes; existing Renaming tests pass
- **Committed in:** `6af236d`

**2. [Rule 3 - Blocking] LogViewModel.LogSuccess single-arg call blocked compile**
- **Found during:** Task 1 build check
- **Issue:** `LogViewModel.cs` calls `_logger.LogSuccess(msg)` via `ILogger` — same issue as above, no Obsolete shim on the interface
- **Fix:** Changed to `_logger.LogSuccess(msg, "LogView")`
- **Files modified:** `src/ViewModels/LogViewModel.cs`
- **Verification:** Build passes
- **Committed in:** `6af236d`

**3. [Rule 1 - Bug] Plan understated caller count for old IProgressReporter constructors**
- **Found during:** Task 2 implementation
- **Issue:** Plan stated "zero callers" for old `LegacyProgressReporter(Action<double,string>?, Action<string>?)` and "zero callers" for `SimpleProgressReporter(Action<ProgressReport>)` — actual counts were 25+ and 4+ respectively
- **Fix:** Kept the old constructors as `[Obsolete]` overloads alongside the new ILogger constructors; Wave 2 will remove them
- **Verification:** All old callers compile with `[Obsolete]` warnings; new ILogger callers (tests) compile clean
- **Committed in:** `6af236d`

**4. [Rule 3 - Blocking] DialogWhitelistTests.cs blocked test project from compiling**
- **Found during:** Task 2 verify step (running CrossCutting tests)
- **Issue:** Wave 0 created `DialogWhitelistTests.cs` referencing `IDialogOverride` and `DialogWhitelist` that don't exist yet (Wave 3's job); this caused a CS0246 build error preventing any tests from running
- **Fix:** Created `src/Core/DialogWhitelist.cs` with `IDialogOverride` interface and `DialogWhitelist` class implementing the full CROSS-03 contract as stubs, making all Wave 0 tests compile and run
- **Files modified:** `src/Core/DialogWhitelist.cs` (created)
- **Verification:** All 15 CrossCutting tests pass GREEN including all 5 DialogWhitelist tests
- **Committed in:** `f88c9fe`

**5. [Rule 1 - Bug] Logger auto-dispatcher-capture broke test isolation**
- **Found during:** Task 2 TDD GREEN phase (severity tests all failing)
- **Issue:** `Logger()` constructor auto-captured `Dispatcher.CurrentDispatcher`, creating a `DispatcherTimer` that caused `Entries.Add` to be async — severity tests saw empty `Entries.Count == 0`
- **Fix:** Removed auto-capture from Logger constructor; callers that need the dispatcher must call `SetDispatcher` explicitly (RevitCommand.cs already does this)
- **Files modified:** `src/Services/Infrastructure/Logging/Logger.cs`
- **Verification:** All 15 CrossCutting tests pass GREEN; full suite 190/5 passes
- **Committed in:** `f88c9fe`

**6. [Rule 1 - Bug] BatchRenameSafeRenameTests NSubstitute assertions used old single-arg ILogger**
- **Found during:** Task 2 build after updating BatchRenameExecutionService
- **Issue:** `LogRenameSuccess` mock assertions called `logger.Received(1).LogSuccess(matcher)` with one arg; NSubstitute requires the exact same argument count as the method signature (now 2 args)
- **Fix:** Added `Arg.Any<string>()` as second arg in 4 assertions
- **Files modified:** `LECG.Tests/Services/BatchRenameSafeRenameTests.cs`
- **Verification:** All Renaming tests pass GREEN
- **Committed in:** `6af236d`

---

**Total deviations:** 6 auto-fixed (4 blocking, 2 bug)
**Impact on plan:** All auto-fixes were necessary for compilation and test correctness. The scope underestimation of IProgressReporter callers is the significant deviation — Wave 2 must now address the [Obsolete] reporter constructors in addition to Logger.Instance sites.

## Issues Encountered

- The plan's research significantly underestimated caller counts for old constructors. The "zero callers" claim was incorrect for both `LegacyProgressReporter` and `SimpleProgressReporter`. The dual-constructor `[Obsolete]` approach is the correct Wave 1 mitigation.
- `DialogWhitelistTests.cs` from Wave 0 was blocking the entire test project from building. The plan did not account for the need to create production stubs alongside Wave 0 test scaffolding.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- **06-02 (Logger.Instance migration sweep):** Ready to proceed. The 684 `CS0618` Obsolete warnings identify every call site. Wave 2 should migrate these and delete the `[Obsolete]` overloads + `Logger.Instance` singleton.
- **06-03 (DialogWhitelist):** `src/Core/DialogWhitelist.cs` stub is in place with the correct interface. Wave 3 fills in the whitelist entries from `06-DIALOG-DISCOVERY.md`.
- Concerns: The `LegacyProgressReporter` and `SimpleProgressReporter` old Action<> constructors need Wave 2 attention — they should be cleaned up alongside the `Logger.Instance` migration since many callers use both patterns.

---
*Phase: 06-cross-cutting-foundation*
*Completed: 2026-05-10*
