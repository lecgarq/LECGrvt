---
phase: 6
slug: cross-cutting-foundation
status: planned
nyquist_compliant: true
wave_0_complete: false
created: 2026-05-10
updated: 2026-05-10
---

# Phase 6 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (dotnet test) |
| **Config file** | `LECG.Tests/LECG.Tests.csproj` |
| **Quick run command** | `dotnet test LECG.Tests --filter "Category=CrossCutting|Category=FormulaGrouping"` |
| **Full suite command** | `dotnet test LECG.Tests` |
| **Estimated runtime** | ~15 seconds (quick), ~30 seconds (full) |
| **Baseline** | 175 PASSED, 5 SKIPPED — must not regress |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test LECG.Tests --filter "Category=CrossCutting|Category=FormulaGrouping"`
- **After every plan wave:** Run `dotnet test LECG.Tests`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** ~15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 06-00 / Task 1 — Create LoggerSeverityTests.cs | 06-00 | 0 | CROSS-01, CROSS-02 | unit (RED expected) | `dotnet test LECG.Tests --filter "Category=CrossCutting"` | ❌ creates `LECG.Tests/Services/Logging/LoggerSeverityTests.cs` | ⬜ pending |
| 06-00 / Task 2 — Create DialogWhitelistTests.cs | 06-00 | 0 | CROSS-03 | unit (RED expected) | `dotnet test LECG.Tests --filter "Category=CrossCutting"` | ❌ creates `LECG.Tests/Core/DialogWhitelistTests.cs` | ⬜ pending |
| 06-00 / Task 3 — DialogId runtime discovery | 06-00 | 0 | CROSS-03 | manual (human-action) | manual capture → `06-DIALOG-DISCOVERY.md` exists | ❌ creates `.planning/phases/06-cross-cutting-foundation/06-DIALOG-DISCOVERY.md` | ⬜ pending |
| 06-01 / Task 1 — Extend ILogger contract with scope | 06-01 | 1 | CROSS-01 | unit + build | `dotnet build` (0 errors) + `dotnet test LECG.Tests --filter "Category=CrossCutting"` (scope-parameter test GREEN) | ✅ edits `Logger.cs` | ⬜ pending |
| 06-01 / Task 2 — Rewrite IProgressReporter impls | 06-01 | 1 | CROSS-02 | unit | `dotnet test LECG.Tests --filter "Category=CrossCutting"` (6 reporter-severity tests GREEN) | ✅ edits 3 reporter files + 2 command call sites | ⬜ pending |
| 06-02 / Task 1 — Migrate non-command services + delete singleton (sweep) | 06-02 | 2 | CROSS-01 | build + unit | `dotnet build` (0 errors, CS0618 count ≤ 10) + `dotnet test LECG.Tests` (full suite GREEN) + `grep -rn "Logger\.Instance" src/` excluding Logger.cs/Bootstrapper.cs/Commands = empty | ✅ edits 30+ files per Migration Inventory | ⬜ pending |
| 06-02 / Task 2 — Migrate RevitCommand + 4 Commands via ServiceLocator | 06-02 | 2 | CROSS-01 | build + unit | `dotnet build` (0 errors) + `dotnet test LECG.Tests` (GREEN) + `grep -rn "Logger\.Instance" src/` excluding Logger.cs/Bootstrapper.cs = empty | ✅ edits `RevitCommand.cs` + 4 Command files | ⬜ pending |
| 06-02 / Task 3 — Delete Logger.Instance + rewire Bootstrapper | 06-02 | 2 | CROSS-01 | build + unit + static check | `grep -rn "Logger\.Instance" src/` = empty (PHASE EXIT GATE) + `dotnet test LECG.Tests` GREEN | ✅ edits `Logger.cs` + `Bootstrapper.cs` | ⬜ pending |
| 06-03 / Task 1 — Create DialogWhitelist + IDialogOverride + populate entries | 06-03 | 3 | CROSS-03 | unit | `dotnet test LECG.Tests --filter "Category=CrossCutting"` (DialogWhitelist tests GREEN) | ✅ creates `src/Core/DialogWhitelist.cs` + `src/Core/IDialogOverride.cs` | ⬜ pending |
| 06-03 / Task 2 — Migrate PurgeCommand + ConvertFamilyCommand handlers | 06-03 | 3 | CROSS-03 | unit + static check | `dotnet test LECG.Tests` GREEN + `grep -nE "OverrideResult\(" src/Commands/PurgeCommand.cs src/Commands/ConvertFamilyCommand.cs` returns no direct overrides outside `DialogWhitelist` | ✅ edits both Command files | ⬜ pending |
| 06-04 / Task 1 — Compile 02-VERIFICATION.md retroactively | 06-04 | 1 (independent) | GAPS-01 | file-existence + structural check | `test -f` path + heading-count parity with `03-VERIFICATION.md` + REQ-07 + at-phase test count (79) cited; no 175 reference | ✅ creates `.planning/milestones/v1.1-phases/02-formula-auto-grouping-bug-fix/02-VERIFICATION.md` | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

New test files required before regular waves can verify their work:

- [ ] `LECG.Tests/Services/Logging/LoggerSeverityTests.cs` — covers:
  - CROSS-01: `ILogger` interface carries scope parameter on all methods (compile + behavior)
  - CROSS-02: `RevitCommandProgressReporter.LogWarning(msg)` produces `LogLevel.Warning` `LogEntry`
  - CROSS-02: `RevitCommandProgressReporter.LogError(msg)` produces `LogLevel.Error` `LogEntry`
  - CROSS-02: `LegacyProgressReporter.LogWarning(msg)` produces `LogLevel.Warning` `LogEntry`
  - CROSS-02: `LegacyProgressReporter.LogError(msg)` produces `LogLevel.Error` `LogEntry`
  - CROSS-02: `SimpleProgressReporter.LogWarning(msg)` produces `LogLevel.Warning` `LogEntry`
  - CROSS-02: `SimpleProgressReporter.LogError(msg)` produces `LogLevel.Error` `LogEntry`
- [ ] `LECG.Tests/Core/DialogWhitelistTests.cs` — covers:
  - CROSS-03: Whitelisted `DialogId` → `OverrideResult` invoked with correct value + `Info` log entry produced
  - CROSS-03: Unknown `DialogId` → `OverrideResult` NOT invoked + `Warning` log entry produced (reach-user default)
  - CROSS-03: Null `DialogId` → reach-user behavior
  - CROSS-03: Case-sensitive exact match (ordinal)
- [ ] `[Trait("Category","CrossCutting")]` attribute applied to all new test classes so the filter works.
- [ ] `06-DIALOG-DISCOVERY.md` captured from runtime observation of Purge + Convert Family on a representative model (drives `DialogWhitelist.Entries`).

*GAPS-01 has no Wave 0 test gap — it is a documentation artifact, validated by file existence + content checklist (see Manual-Only Verifications below).*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `02-VERIFICATION.md` exists at `.planning/milestones/v1.1-phases/02-formula-auto-grouping-bug-fix/02-VERIFICATION.md` with section parity to `03-VERIFICATION.md` (header, REQ-07 evidence, test results citation, sign-off) | GAPS-01 | Documentation artifact, not code | `test -f` the path; heading-count parity with template; spot-review REQ-07 evidence rows against 02-01-SUMMARY.md / 02-02-SUMMARY.md / 02-UAT.md |
| DialogId enumeration covers real Purge + Convert Family flows | CROSS-03 | Revit `DialogId` strings observable only at runtime; Wave 0 Task 3 captures live values | Run logging-only Purge and Convert Family handler against a representative dirty model; record every emitted `DialogId` in `06-DIALOG-DISCOVERY.md`; Plan 03 transcribes captured set into `DialogWhitelist.Entries` |
| Visual polish on existing severity colors in `LogView` (allowed by CONTEXT §2.4) does not regress existing color cues | CROSS-02 | Visual judgment, no automated check | Open `LogView` after a run, confirm Info/Warning/Error rows are visually distinct and accessible. (Not bundled into any task — optional polish.) |
| Post-deletion grep gate: `grep -rn "Logger\.Instance" src/` returns zero hits | CROSS-01 | Static check treated as phase-exit gate | Run grep after Plan 02 Task 3; must be empty before Plan 03 ships and before `/gsd:verify-work`. |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies (Wave 0 Task 3 is the only manual-only step — explicitly marked checkpoint:human-action)
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (LoggerSeverityTests.cs + DialogWhitelistTests.cs)
- [x] No watch-mode flags
- [x] Feedback latency < 30s (xUnit quick filter ~15s)
- [x] `nyquist_compliant: true` set in frontmatter (Per-Task Verification Map complete)

**Approval:** planned — pending execution.
