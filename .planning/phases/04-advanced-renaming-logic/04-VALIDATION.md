---
phase: 4
slug: advanced-renaming-logic
status: ready
nyquist_compliant: true
wave_0_complete: true
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

> Populated by Wave 0 (plan 04-00). Each implementing plan has an automated verify command pointing at its RED test fixture.

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 04-01-T1 | 04-01 | 1 | REQ-04 | unit | `dotnet test LECG.Tests/LECG.Tests.csproj -p:SkipRevitDeploy=true --filter "FullyQualifiedName~RenameSkipDetectorTests" --nologo -v minimal` | ✅ | ⬜ pending |
| 04-02-T1 | 04-02 | 1 | REQ-04 | unit | `dotnet test LECG.Tests/LECG.Tests.csproj -p:SkipRevitDeploy=true --filter "FullyQualifiedName~SearchReplacePreviewServiceTests" --nologo -v minimal` | ✅ | ⬜ pending |
| 04-03-T1 | 04-03 | 2 | REQ-02 | unit | `dotnet test LECG.Tests/LECG.Tests.csproj -p:SkipRevitDeploy=true --filter "FullyQualifiedName~FormulaUpdateServiceTests|FullyQualifiedName~BatchRenameSafeRenameTests" --nologo -v minimal` | ✅ | ⬜ pending |
| 04-04-T1 | 04-04 | 3 | REQ-03 | unit | `dotnet test LECG.Tests/LECG.Tests.csproj -p:SkipRevitDeploy=true --filter "FullyQualifiedName~BatchRenameSafeRenameTests" --nologo -v minimal` | ✅ | ⬜ pending |
| 04-05-T1 | 04-05 | 4 | REQ-02/03/04 | manual | Phase-end Revit verify session | — | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] `LECG.Tests/Services/RenameSkipDetectorTests.cs` — stubs covering REQ-04 (GetStandardItemSkipReason) and narrowed `GetRenameSkipReason` branches (REQ-02/03/04)
- [x] `LECG.Tests/Services/SearchReplacePreviewServiceTests.cs` — stubs for REQ-04 (Status + IsRenameable population) and cross-batch collision detection
- [x] `LECG.Tests/Services/FormulaUpdateServiceTests.cs` — stubs for IFormulaUpdateService injection and delegation (REQ-02)
- [x] `LECG.Tests/Services/BatchRenameSafeRenameTests.cs` — stubs for formula-referenced (REQ-02), dimension-label (REQ-03), and pre-flight dry-run (REQ-04) paths

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Dimension `FamilyLabel = newRef` assignment in live family editor | REQ-03 | Revit API behavior on label re-binding cannot be reliably mocked — setter may require null-clear first | Open a family with a labelled dimension, run BatchRename to rename the labelled parameter, confirm dimension still shows correct value and label visually points to renamed param |
| Muted-row Style + checkbox disabled state | REQ-04 | WPF visual rendering | Open SearchReplaceView with a family containing both renameable and non-renameable params; confirm non-renameable rows are muted, checkbox disabled, Status tooltip readable |
| Formula recompute after rename | REQ-02 | Revit recomputes on commit; needs visual confirmation | Rename a param referenced by ≥2 formulas, regenerate family, confirm formula values still compute correctly |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 30s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-05-10 — Wave 0 complete
