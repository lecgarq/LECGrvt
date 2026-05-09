---
phase: 3
slug: grid-collection-fixes
status: planned
nyquist_compliant: true
wave_0_complete: false
created: 2026-05-09
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.6.5 + FluentAssertions 6.12.0 + NSubstitute 5.1.0 |
| **Config file** | `LECG.Tests/LECG.Tests.csproj` (no separate xunit.runner.json) |
| **Quick run command** | `dotnet test LECG.Tests/LECG.Tests.csproj -x` |
| **Full suite command** | `dotnet test LECG.Tests/LECG.Tests.csproj` |
| **Estimated runtime** | ~30 seconds (quick) / ~60 seconds (full) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test LECG.Tests/LECG.Tests.csproj -x`
- **After every plan wave:** Run `dotnet test LECG.Tests/LECG.Tests.csproj`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

> Every implementation task in every PLAN.md is mapped here. Status flips during execution.

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 3-W0-01 | 00 | 0 | REQ-01 | unit (red) | `dotnet test --filter "FullyQualifiedName~ElementLabelServiceTests"` | ⬜ planned | ⬜ pending |
| 3-W0-02 | 00 | 0 | REQ-01 | unit (red) | `dotnet test --filter "FullyQualifiedName~BaseElementCollectionServiceTests"` | ⬜ planned | ⬜ pending |
| 3-W0-03 | 00 | 0 | REQ-01 | unit (red) | `dotnet test --filter "FullyQualifiedName~SearchReplacePreviewServiceTests"` | ⬜ planned | ⬜ pending |
| 3-W0-04 | 00 | 0 | REQ-01 | unit (red) | `dotnet test --filter "FullyQualifiedName~SearchReplaceViewModelTests"` | ⬜ planned | ⬜ pending |
| 3-W0-T3 | 00 | 0 | REQ-01 | doc | `grep -c "nyquist_compliant: true" .planning/phases/03-grid-collection-fixes/03-VALIDATION.md` | ✅ | ⬜ pending |
| 3-01-T1 | 01 | 1 | REQ-01 | unit (green) | `dotnet test --filter "FullyQualifiedName~ElementLabelServiceTests"` | ✅ after W0 | ⬜ pending |
| 3-02-T1 | 02 | 1 | REQ-01 | compile | `dotnet build LECG.sln` | ✅ | ⬜ pending |
| 3-03-T1 | 03 | 2 | REQ-01 | unit (green) | `dotnet test --filter "FullyQualifiedName~BaseElementCollectionServiceTests"` | ✅ after W0 | ⬜ pending |
| 3-03-T2 | 03 | 2 | REQ-01 | suite | `dotnet test LECG.Tests/LECG.Tests.csproj` | ✅ | ⬜ pending |
| 3-04-T1 | 04 | 2 | REQ-01 | unit (green) | `dotnet test --filter "FullyQualifiedName~SearchReplacePreviewServiceTests"` | ✅ after W0 | ⬜ pending |
| 3-04-T2 | 04 | 2 | REQ-01 | compile+suite | `dotnet build LECG.sln; dotnet test LECG.Tests/LECG.Tests.csproj` | ✅ | ⬜ pending |
| 3-05-T1 | 05 | 3 | REQ-01 | compile | `dotnet build LECG.sln` | ✅ | ⬜ pending |
| 3-05-T2 | 05 | 3 | REQ-01 | unit (green) + suite | `dotnet test --filter "FullyQualifiedName~SearchReplaceViewModelTests"` then full suite | ✅ after W0 | ⬜ pending |
| 3-05-T3 | 05 | 3 | REQ-01 | manual-only (Revit) | N/A — checkpoint:human-verify | N/A | ⬜ pending |
| 3-06-T1 | 06 | 4 | REQ-01 | compile+suite | `dotnet build LECG.sln; dotnet test LECG.Tests/LECG.Tests.csproj` | ✅ | ⬜ pending |
| 3-06-T2 | 06 | 4 | REQ-01 | compile+suite | `dotnet build LECG.sln; dotnet test LECG.Tests/LECG.Tests.csproj` | ✅ | ⬜ pending |
| 3-07-T1 | 07 | 4 | REQ-01 | compile+suite | `dotnet build LECG.sln; dotnet test LECG.Tests/LECG.Tests.csproj` | ✅ | ⬜ pending |
| 3-07-T2 | 07 | 4 | REQ-01 | compile+suite | `dotnet build LECG.sln; dotnet test LECG.Tests/LECG.Tests.csproj` | ✅ | ⬜ pending |
| 3-08-T1 | 08 | 5 | REQ-01 | compile+suite | `dotnet build LECG.sln; dotnet test LECG.Tests/LECG.Tests.csproj` | ✅ | ⬜ pending |
| 3-08-T2 | 08 | 5 | REQ-01 | compile+suite | `dotnet build LECG.sln; dotnet test LECG.Tests/LECG.Tests.csproj` | ✅ | ⬜ pending |
| 3-09-T1 | 09 | 6 | REQ-01 | manual-only (Revit) | N/A — checkpoint:human-verify | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `LECG.Tests/Services/ElementLabelServiceTests.cs` — name-blank fallback and category-null fallback (REQ-01)
- [ ] `LECG.Tests/Services/BaseElementCollectionServiceTests.cs` — removal of null-skip, fallback chain integration (REQ-01)
- [ ] `LECG.Tests/Services/SearchReplacePreviewServiceTests.cs` — Category propagation from `ElementData` into `ElementRowViewModel` (REQ-01)
- [ ] `LECG.Tests/ViewModels/SearchReplaceViewModelTests.cs` — default sort direction on `ICollectionView`, AND-combined filter (REQ-01)

(`nyquist_compliant: true` is set because every implementation task above maps to one of these four fixtures or a compile/suite gate. The four checkboxes flip during Plan 03-00 execution; `wave_0_complete` flips at end of Plan 03-00 Task 3.)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Where Verified |
|----------|-------------|------------|----------------|
| Batch Rename grid: Category column visible, default Category sort, AND-combined filter | REQ-01 | WPF rendering | Plan 03-05 Task 3 |
| All migrated screens render non-blank Name + Category for live Revit elements | REQ-01 | Requires running Revit addin host | Plan 03-09 Task 1 |
| FamilyParameter row falls back to family name when no Revit category resolves | REQ-01 | Revit family-edit context | Plan 03-09 Task 1 |
| LogView records fallback-label inclusions instead of silent skips | REQ-01 | Live Revit element collection with null-Category elements | Plan 03-09 Task 1 |

---

## Validation Sign-Off

- [x] All implementation tasks have an `<automated>` verify, OR are manual-only checkpoints with a clear human-verify gate (3-05-T3, 3-09-T1).
- [x] Sampling continuity: every wave has at least one `dotnet test` automated gate; no run of 3 consecutive tasks lacks an automated verify (each plan ends with a build+suite gate).
- [x] Wave 0 covers all MISSING test references (4 fixtures).
- [x] No watch-mode flags.
- [x] Feedback latency < 60s (full suite ~60s, quick run ~30s).
- [x] `nyquist_compliant: true` set in frontmatter.

**Approval:** plan complete; awaiting Wave 0 execution.
