---
phase: 5
slug: verification-and-polish
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-10
---

# Phase 5 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.6.5 + FluentAssertions 6.12.0 + NSubstitute 5.1.0 |
| **Config file** | `LECG.Tests/LECG.Tests.csproj` (net8.0-windows, x64) |
| **Quick run command** | `dotnet test LECG.Tests/LECG.Tests.csproj --filter "FullyQualifiedName~<FixtureName>" -p:SkipRevitDeploy=true` |
| **Full suite command** | `dotnet test LECG.Tests/LECG.Tests.csproj -p:SkipRevitDeploy=true` |
| **Estimated runtime** | ~30 seconds (full suite); < 5 seconds per fixture |

---

## Sampling Rate

- **After every task commit:** Run quick fixture filter on the touched fixture.
- **After every plan wave:** Run full suite command.
- **Before `/gsd:verify-work`:** Full suite must be GREEN AND service→fixture matrix in `05-VERIFICATION.md` must be all ✅.
- **Max feedback latency:** 30 seconds.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 5-00-01 | 00 | 0 | REQ-05 | scaffold | `dotnet test LECG.Tests/LECG.Tests.csproj -p:SkipRevitDeploy=true` | ❌ W0 | ⬜ pending |
| 5-00-02 | 00 | 0 | REQ-05 | scaffold | (same) | ❌ W0 | ⬜ pending |
| 5-01-01 | 01 | 1 | REQ-05 | unit | `dotnet test --filter "RenameRulePipelineServiceTests"` | ❌ W0 | ⬜ pending |
| 5-01-02 | 01 | 1 | REQ-05 | unit | `dotnet test --filter "BaseElementCollectionServiceTests"` | ✅ (deepen) | ⬜ pending |
| 5-02-01 | 02 | 2 | REQ-05 | unit | `dotnet test --filter "BatchRenameExecutionServiceTests"` | ❌ W0 | ⬜ pending |
| 5-02-02 | 02 | 2 | REQ-05 | unit | `dotnet test --filter "SearchReplaceServiceTests"` | ❌ W0 | ⬜ pending |
| 5-03-01 | 03 | 3 | Polish #1 | unit | `dotnet test --filter "ExecuteBatchRename_FamilyTransactionRollsBack"` | ❌ W3 | ⬜ pending |
| 5-03-02 | 03 | 3 | Polish #2 | unit | `dotnet test --filter "ExecuteDimensionReassignments_PreClearsLabel"` | ❌ W3 | ⬜ pending |
| 5-03-03 | 03 | 3 | Polish #3 | unit | `dotnet test --filter "BatchRenameProgress"` | ❌ W3 | ⬜ pending |
| 5-04-01 | 04 | 4 | REQ-05 | manual | `05-VERIFICATION.md` matrix + checklist | manual | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `LECG.Tests/Services/RenameRulePipelineServiceTests.cs` — new fixture, anchor + skip-gated RED tests naming Wave 1 plan
- [ ] `LECG.Tests/Services/SearchReplaceServiceTests.cs` — new fixture, anchor + skip-gated RED tests naming Wave 2 plan
- [ ] `LECG.Tests/Services/BatchRenameExecutionServiceTests.cs` — new fixture, anchor + skip-gated RED tests naming Wave 2 + Wave 3 plans
- [ ] `LECG.Tests/Services/BaseElementCollectionServiceTests.cs` — append skip-gated RED rows naming Wave 1 plan (existing 4 tests remain GREEN as anchor)
- [ ] `05-VERIFICATION.md` — service→fixture coverage matrix scaffold rows (✅/⚠/❌ as appropriate)
- No framework install needed — xUnit + FluentAssertions + NSubstitute already in `LECG.Tests.csproj`.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `count`-on-rollback observed on live Revit family-edit refusal | Polish #1 | Requires Revit refusing a family commit; unit harness cannot trigger Revit-side commit refusal | Force a name collision after pre-flight (concurrent edit) and confirm reported success count = 0 |
| `Dimension.FamilyLabel` pre-clear path on live family with existing label | Polish #2 | Requires a live family with a dimension whose `FamilyLabel.Definition.Name` already binds to a different parameter | Rename a dimension-label-driving parameter and confirm the reassignment commits cleanly |
| Standard-item progress bar reaches 100% | Polish #3 | Progress reporting renders on UI thread via real `IProgressReporter`; unit harness covers sequence, not pixel | Run a Sheets or Materials batch ≥ 20 items, confirm progress reaches 100% |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
