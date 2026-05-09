---
phase: 3
slug: grid-collection-fixes
status: draft
nyquist_compliant: false
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

> Filled in during planning. Each task in a PLAN.md must be reflected here, with an automated command or marked manual-only with a Wave 0 dependency.

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 3-W0-01 | W0 | 0 | REQ-01 | unit (stub) | `dotnet test --filter "FullyQualifiedName~ElementLabelServiceTests"` | ❌ W0 | ⬜ pending |
| 3-W0-02 | W0 | 0 | REQ-01 | unit (stub) | `dotnet test --filter "FullyQualifiedName~BaseElementCollectionServiceTests"` | ❌ W0 | ⬜ pending |
| 3-W0-03 | W0 | 0 | REQ-01 | unit (stub) | `dotnet test --filter "FullyQualifiedName~SearchReplacePreviewServiceTests"` | ❌ W0 | ⬜ pending |
| 3-W0-04 | W0 | 0 | REQ-01 | unit (stub) | `dotnet test --filter "FullyQualifiedName~SearchReplaceViewModelTests"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

*Planner must extend this table so every implementation task in every PLAN.md has a row mapped to one of the four test fixtures above (or a manual-only entry below).*

---

## Wave 0 Requirements

- [ ] `LECG.Tests/Services/ElementLabelServiceTests.cs` — name-blank fallback and category-null fallback (REQ-01)
- [ ] `LECG.Tests/Services/BaseElementCollectionServiceTests.cs` — removal of null-skip, fallback chain integration (REQ-01)
- [ ] `LECG.Tests/Services/SearchReplacePreviewServiceTests.cs` — Category propagation from `ElementData` into `ElementRowViewModel` (REQ-01)
- [ ] `LECG.Tests/ViewModels/SearchReplaceViewModelTests.cs` — default sort direction on `ICollectionView` (REQ-01)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Selection-backed screens render `ElementGridControl` with non-blank Name + Category for live Revit elements | REQ-01 | Requires running Revit addin host; WPF/Revit `Element` objects not instantiable in xUnit | Open Revit, load LECG addin, run each migrated screen (Batch Rename, DivideToposolid, FixPoints, ConvertCad, SplitBoundaries, ConvertToposolidToFloor, ConvertFloorToToposolid, plus all `SelectionViewModel`-backed screens). Verify every row shows non-blank Name + Category. Verify default sort is Category ascending in Batch Rename. Verify per-column filter + top-of-grid `FilterCategory` AND-combine. |
| FamilyParameter row falls back to family name when no Revit category resolves | REQ-01 | Requires Revit family-edit context | In Revit family editor, run Batch Rename on family parameters; confirm Category cell shows family name (not blank, not `<Uncategorized>`). |
| `LogView` records fallback-label inclusions instead of silent skips | REQ-01 | Requires live Revit element collection with null-Category elements | Trigger a collection over a model containing elements with null Category; open `LogView` and confirm one entry per fallback inclusion. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (4 test files)
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
