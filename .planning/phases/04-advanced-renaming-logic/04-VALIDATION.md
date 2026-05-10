---
phase: 4
slug: advanced-renaming-logic
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-10
---

# Phase 4 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (LECG.Tests.csproj) |
| **Config file** | LECG.Tests/LECG.Tests.csproj |
| **Quick run command** | `dotnet test LECG.Tests/LECG.Tests.csproj --filter "FullyQualifiedName~SearchReplace|FullyQualifiedName~FormulaName|FullyQualifiedName~BatchRename" --nologo -v minimal` |
| **Full suite command** | `dotnet test LECG.Tests/LECG.Tests.csproj --nologo -v minimal` |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Run quick run command (focused filter on touched area)
- **After every plan wave:** Run full suite command
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

> Populated by gsd-planner once tasks are decomposed. Each task row must have either an `<automated>` verify command OR a Wave 0 test-stub dependency.

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| TBD     | TBD  | TBD  | REQ-02/03/04 | unit/integration | `dotnet test ...` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `LECG.Tests/Services/BatchRenameExecutionServiceTests.cs` — stubs covering REQ-02 (formula-referenced rename), REQ-03 (dimension-label rename), and cross-batch name conflict detection
- [ ] `LECG.Tests/Services/SearchReplacePreviewServiceTests.cs` — stubs for REQ-04 (Status + IsRenameable population) and narrowed `GetRenameSkipReason` branches
- [ ] `LECG.Tests/ViewModels/SearchReplaceViewModelTests.cs` — stubs for `ElementRowViewModel.IsRenameable` propagation
- [ ] Shared Revit-API mocks/fakes reused from existing test fixtures (no new framework install required)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Dimension `FamilyLabel = newRef` assignment in live family editor | REQ-03 | Revit API behavior on label re-binding cannot be reliably mocked — setter may require null-clear first | Open a family with a labelled dimension, run BatchRename to rename the labelled parameter, confirm dimension still shows correct value and label visually points to renamed param |
| Muted-row Style + checkbox disabled state | REQ-04 | WPF visual rendering | Open SearchReplaceView with a family containing both renameable and non-renameable params; confirm non-renameable rows are muted, checkbox disabled, Status tooltip readable |
| Formula recompute after rename | REQ-02 | Revit recomputes on commit; needs visual confirmation | Rename a param referenced by ≥2 formulas, regenerate family, confirm formula values still compute correctly |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
