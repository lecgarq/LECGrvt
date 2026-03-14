# ROADMAP.md

> **Current Phase**: Not started
> **Milestone**: v1.0.0-UI_Refresh

## Must-Haves (from SPEC)
- [ ] Build standalone WPF `LecgUI` component library in `LECG.Core` or distinct Resource dictionaries.
- [ ] Apply the 9-Color Hex earth-toned palette system globally.
- [ ] Optimize the `Services/ViewModels` relationship to ensure extremely snappy `CommunityToolkit.Mvvm` binding.
- [ ] Completely remove arbitrary width/height hardcodes scaling up UI resolution issues.
- [ ] Integrate consistent Batch operations (expansion, collapse, multiple selections).
- [ ] Omit heavy animations/glassmorphism specifically to maintain Revit 2026 threading performance.

## Phases

### Phase 1: Foundation (Generic Component Framework)
**Status**: ✅ Complete
**Objective**: Build a set of decoupled, generic standard WPF UI controls (Buttons, TextBoxes, DataGrids, TreeViews) that exclusively adopt the new LECG minimalist palette and professional interactions (hover states, click responses).
**Requirements**: REQ-01, REQ-02, REQ-03

### Phase 1a: Gap Closure (UI Library Breadth)
**Status**: ⬜ Not Started
**Objective**: Address gaps from milestone audit regarding missing standard controls.
**Gaps to Close:**
- [ ] Implement `LecgButton` and `LecgTextBox` standard styles.
- [ ] Ensure focus/hover states align with professional aesthetics.


### Phase 2: Refactoring Core Operations (Performance & Modularity)
**Status**: ⬜ Not Started
**Objective**: Decouple monolithic code behind existing views and properly structure `ViewModels` to deliver the ultra-fast real-time searching constraints against their correlated `Services`. Clean dependency boundaries based on the architectural plan.
**Requirements**: REQ-04, REQ-07

### Phase 3: Core UI Integration (Implementation Base)
**Status**: ⬜ Not Started
**Objective**: Implement the generic WPF components from Phase 1 back into the primary tools (e.g., SearchReplaceView, CategorChangerView), replacing all ad-hoc UI XAML elements simultaneously enforcing batch selection logic.
**Requirements**: REQ-05

### Phase 4: Polish & Responsive Layout
**Status**: ⬜ Not Started
**Objective**: Finalize ultra-fast performance scaling. Rigorously optimize relative grid bindings and auto-resizing structures to guarantee a flawlessly professional interface regardless of the user's monitor setup or window scaling bounds.
**Requirements**: REQ-06
