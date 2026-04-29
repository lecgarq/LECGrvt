---
phase: 2
slug: formula-auto-grouping-bug-fix
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-28
---

# Phase 2 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (dotnet test) |
| **Config file** | LECG.Tests/LECG.Tests.csproj |
| **Quick run command** | `dotnet test LECG.Tests --filter "FormulaAutoGroup"` |
| **Full suite command** | `dotnet test LECG.Tests` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test LECG.Tests --filter "FormulaAutoGroup"`
- **After every plan wave:** Run `dotnet test LECG.Tests`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** ~15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 2-01-01 | 01 | 1 | REQ-07 | unit | `dotnet test LECG.Tests --filter "FormulaAutoGroup"` | ❌ W0 | ⬜ pending |
| 2-01-02 | 01 | 1 | REQ-07 | unit | `dotnet test LECG.Tests --filter "FormulaAutoGroup"` | ❌ W0 | ⬜ pending |
| 2-01-03 | 01 | 1 | REQ-07 | manual | manual Revit test | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `LECG.Tests/Commands/FormulaAutoGroupingCommandTests.cs` — unit test stubs for REQ-07 logic paths

*Note: Full integration tests (transaction commit, `ReplaceParameter`, post-reload) require a live Revit instance and are manual-only.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Parameters actually move to "Other" group in Revit UI | REQ-07 | Requires live Revit API + family document | Open a family with formula params, run FormulaAutoGrouping, verify "Other" group in Family Parameters dialog |
| Shared params move without formula loss | REQ-07 | Requires live Revit instance + shared parameter file | Open family with shared formula param, run command, verify group changed and formula intact |
| Partial success: some params move, others skip cleanly | REQ-07 | Requires live Revit instance | Family with mixed moveable/unmoveable formula params — verify moved ones commit, skipped ones log clearly |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
