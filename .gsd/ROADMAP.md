# ROADMAP.md

> **Current Phase**: 4
> **Current Status**: Executing Phase 4 (Polish & Responsive Layout)
> **Milestone**: v1.0.0-UI_Refresh


## Must-Haves (from SPEC)

- [x] Build standalone WPF `LecgUI` component library in `LECG.Core` or distinct Resource dictionaries.
- [x] Apply the 9-Color Hex earth-toned palette system globally.
- [x] Optimize the `Services/ViewModels` relationship to ensure extremely snappy `CommunityToolkit.Mvvm` binding.
- [x] Completely remove arbitrary width/height hardcodes scaling up UI resolution issues.
- [x] Integrate consistent Batch operations (expansion, collapse, multiple selections).
- [x] Omit heavy animations/glassmorphism specifically to maintain Revit 2026 threading performance.

## Phases


### Phase 1: Foundation (Generic Component Framework)
**Status**: ✅ Complete
**Objective**: Build a set of decoupled, generic standard WPF UI controls (Buttons, TextBoxes, DataGrids, TreeViews) that exclusively adopt the new LECG minimalist palette and professional interactions (hover states, click responses).
**Requirements**: REQ-01, REQ-02, REQ-03

### Phase 1a: Gap Closure (UI Library Breadth)
**Status**: ✅ Complete
**Objective**: Address gaps from milestone audit regarding missing standard controls.
**Gaps to Close:**
- [x] Implement `LecgButton` and `LecgTextBox` standard styles.
- [x] Ensure focus/hover states align with professional aesthetics.


### Phase 2: Refactoring Core Operations (Performance & Modularity)

**Status**: ✅ Complete

**Objective**: Decouple monolithic code behind existing views and properly structure `ViewModels` to deliver the ultra-fast real-time searching constraints against their correlated `Services`. Clean dependency boundaries based on the architectural plan.
**Requirements**: REQ-04, REQ-07

### Phase 3: Global UI Standardization (Unified Identity)
**Status**: ✅ Complete
**Objective**: Roll out the `LecgUI` standard to all application windows. Replace legacy styles with global resources, ensuring a unified professional identity across the entire add-in. Implementation of `LecgTreeView` with batch expansion logic.
**Requirements**: REQ-05

### Phase 4: Polish & Responsive Layout

**Status**: ✅ Complete

**Objective**: Finalize ultra-fast performance scaling. Rigorously optimize relative grid bindings and auto-resizing structures to guarantee a flawlessly professional interface regardless of the user's monitor setup or window scaling bounds.
**Requirements**: REQ-06

---

## Phase Status Summary

| Phase | Description | Plan | Goal | Date | Status |
|---|---|---|---|---|---|
| 1 | Foundation (Component Framework) | [COMPLETE](.gsd/phases/1/1-PLAN.md) | Standard Controls | 2026-03-14 | [VERIFIED](.gsd/phases/1/VERIFICATION.md) |
| 1a | Gap Closure (UI Breadth) | [COMPLETE](.gsd/phases/1a/1-PLAN.md) | Button/TextBox Styles | 2026-03-14 | [VERIFIED](.gsd/phases/1a/VERIFICATION.md) |
| 2 | Core Refactoring (Performance) | [COMPLETE](.gsd/phases/2/1-PLAN.md) | Decouple Services | 2026-03-14 | [VERIFIED](.gsd/phases/2/VERIFICATION.md) |
| 3 | Global UI Standardization | [COMPLETE](.gsd/phases/3/1-PLAN.md) | TreeView & Main Views | 2026-03-14 | [VERIFIED](.gsd/phases/3/VERIFICATION.md) |
| 4 | Polish & Responsiveness | [COMPLETE](.gsd/phases/4/1-PLAN.md) | Auto-Layout & Finishing | 2026-03-14 | [VERIFIED](.gsd/phases/4/VERIFICATION.md) |
