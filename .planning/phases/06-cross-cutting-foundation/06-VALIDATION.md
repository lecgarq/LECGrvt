---
phase: 6
slug: cross-cutting-foundation
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-10
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

> Filled by the planner once PLAN.md files exist. Each task in each plan must appear here with: requirement, test type, automated command, and Wave-0 status.

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| _TBD by planner_ | | | | | | | ⬜ pending |

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
- [ ] `LECG.Tests/Core/DialogWhitelistTests.cs` — covers:
  - CROSS-03: Whitelisted `DialogId` → `OverrideResult` invoked with correct value + `Info` log entry produced
  - CROSS-03: Unknown `DialogId` → `OverrideResult` NOT invoked + `Warning` log entry produced (reach-user default)
- [ ] `[Category("CrossCutting")]` attribute applied to all new test classes so the filter works.

*GAPS-01 has no Wave 0 test gap — it is a documentation artifact, validated by file existence + content checklist (see Manual-Only Verifications below).*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `02-VERIFICATION.md` exists at `.planning/milestones/v1.1-phases/02-formula-auto-grouping-bug-fix/02-VERIFICATION.md` with all required sections from `03-VERIFICATION.md` template (header, REQ-07 evidence, test results citation, sign-off) | GAPS-01 | Documentation artifact, not code | `test -f` the path; eyeball-review section coverage against `03-VERIFICATION.md` siblings |
| Dialog whitelist enumeration covers real Purge + Convert Family flows | CROSS-03 | Revit `DialogId` strings are observable only at runtime; wave-0 discovery task captures the live values | Run logging-only Purge and Convert Family handler against a representative dirty model; record every emitted `DialogId`; planner converts captured set into whitelist entries before CROSS-03 implementation tasks land |
| Visual polish on existing severity colors in `LogView` (allowed by CONTEXT §2.4) does not regress existing color cues | CROSS-02 | Visual judgement, no automated check | Open `LogView` after a run, confirm Info/Warning/Error rows are visually distinct and accessible |
| Post-deletion grep for `Logger\.Instance` in `src/` returns zero hits | CROSS-01 | Static check, but treated as a phase-exit gate | `grep -rn "Logger\.Instance" src/` must return no matches |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter (planner sets this after Per-Task Verification Map is filled)

**Approval:** pending
