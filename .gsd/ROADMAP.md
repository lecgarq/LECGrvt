# ROADMAP.md

> **Current Phase**: Final Verification
> **Milestone**: v1.2 - Aesthetic Unity & Window Stability (COMPLETE)

## Must-Haves (from SPEC)

- [ ] Rename Materials, Object Styles, Line Styles, Fill Patterns
- [ ] Triple pass Purge Unused functionality
- [ ] Element-based only selection for Family Conversion
- [ ] Zero constraint breakage

## Phases

### Phase 1: Naming System Extension

**Status**: ✅ Complete
**Objective**: Extend `SearchReplaceService` to handle new element types.

**Tasks**:

- Implement `Material` renaming logic.
- Implement `Object Style` (GraphicsStyle) renaming logic.
- Implement `Line Style` and `Fill Pattern` renaming logic.
- Update `SearchReplaceVM` to included these categories.

### Phase 2: Triple Purge Implementation

**Status**: ✅ Complete
**Objective**: Update Purge infrastructure to support iterative cleaning.

**Tasks**:

- Modify `PurgeService` to allow 3-pass execution.
- Update `PurgeCommand` or `PurgeView` to expose "Triple Purge" option.
- Ensure proper logging of items removed in each pass.

### Phase 3: Selection & Selection Safety

**Status**: ✅ Complete
**Objective**: Refine `ConvertFamily` selection behavior and ensure data integrity.

**Tasks**:

- Update `ISelectionFilter` in `FamilySelectionFilter.cs` to check `Family.IsWorkPlaneBased`.
- Validate element hosting behavior during conversion.
- Add pre-flight checks for naming collisions.

### Phase 4: Verification & Documentation

**Status**: ✅ Complete
**Objective**: Empirically verify all features in Revit 2026.

**Tasks**:

- Verify renaming of all 4 new types.
- Verify triple purge effectiveness.
- Verify selection restriction in project environment.
- Update `ARCHITECTURE.md` if services are significantly refactored.

### Phase 5: Advanced Search & Parameter Renaming

**Status**: ✅ Complete
**Objective**: Implement advanced parameter filtering, enhanced string operations, and advanced search logic.

**Tasks**:

- Implement `Family Parameter` collection and renaming (filtering out shared/system params).
- Add "Replace Spaces" utility command relative to input fields.
- Implement advanced filter rules: "Contains", "Doesn't Contain", "Begins With", "Ends With".
- Update `SearchReplaceVM` and View to support new filter logic and parameter scope.

### Phase 6: Stability & Bug Fixes

**Status**: ✅ Complete
**Objective**: Fix family renaming transaction errors, restore Object/Line Styles collection, and improve "Replace Spaces" visibility.

**Tasks**:
- [x] Refactor `BatchRenameExecutionService` to separate family renaming from main transaction.
- [x] Fix logic filtering Object Styles / Line Styles (ensure user-created ones display).
- [x] Add explicit "Replace Spaces" buttons/tooltips to both filter and replace inputs.
- [x] Includes Formula Parameters in Renaming (See Plan 6.3)

### Phase 7: UI/UX & Advanced Filtering

**Status**: ✅ Complete
**Objective**: Enable multi-selection scopes, fix list updates, add bulk selection, and implement advanced parameter filtering.

**Tasks**:
- [x] Enable Multi-Selection Scopes (Plan 7.1)
- [x] Add "Select All / Select None" functionality (Plan 7.1)
- [x] Implement Advanced Parameter Filtering (Group, Type/Instance, Formula) (Plan 7.1)

### Phase 8: Parameter Fix & Premium UI

**Status**: ✅ Complete
**Objective**: Fix incomplete parameter listing (not all family parameters are being collected) and redesign the Batch Rename UI to a premium, polished level.

**Tasks**:

- [x] Fix parameter collection to iterate ALL FamilySymbols per family, deduplicating by name (Plan 8.1)
- [x] Premium UI redesign: section headers with accent bars, pill badges, polished DataGrid, status bar, branded accents, better typography (Plan 8.2)

### Phase 9: Scope Exclusivity, Instance Params & Per-Scope Filters

**Status**: ✅ Complete
**Objective**: Fix instance parameter collection, make scopes mutually exclusive (radio behavior), and add per-scope advanced filters.

**Tasks**:

- [x] Mutual exclusivity: selecting one scope deactivates all others (radio-group behavior in VM and UI) (Plan 9.1)
- [x] Fix instance parameter collection by also scanning FamilyInstances to capture instance-only params (Plan 9.1)
- [x] Per-scope advanced filters: each scope gets its own contextual filters (View Type, Sheet Number, etc.) (Plan 9.2)

### Phase 10: Purge Unused Family Parameters

**Status**: ✅ Complete
**Objective**: Add a "Purge Unused Parameters" capability to the Purge command that opens each family in the project, identifies truly unused parameters (no formula, no dimension label, no constraint, not referenced by nested families), and safely removes them.

**Tasks**:

- [x] Create `IPurgeParameterService` / `PurgeParameterService` following existing purge service pattern (Plan 10.1)
- [x] Implement safety scanner: check Formula, dimension labels, nested family references, and other param references (Plan 10.1)
- [x] Wire into purge pipeline: add `_purgeParameters` toggle to ViewModel, UI checkbox, pass execution flow (Plan 10.2)
- [x] Add `PurgeParameterService` to DI, Coordinator, PurgeService (Plan 10.2)
- [x] Test with build verification (Plan 10.2)

## Milestone v1.2: Aesthetic Unity & Window Stability

### Phase 11: UI Baseline & Design System Audit
**Status**: ✅ Complete
**Objective**: Audit all 19+ XAML files, consolidate ad-hoc styles into `src/Resources/Styles.xaml`, and standardize layout spacing.

### Phase 12: Window Management Engine
**Status**: ✅ Complete
**Objective**: Enhance `LecgWindow.cs` to handle DPI scaling, persistence, and responsive resizing.

### Phase 13: Bulk Aesthetic Conversion
**Status**: ✅ Complete
**Objective**: Systematically update every View to the "Premium Liquid Glass" style.

### Phase 14: Interaction & Animation Polish
**Status**: ✅ Complete
**Objective**: Add micro-animations and interaction polish (hover states, transitions).

### Phase 15: Verification & Stress Testing
**Status**: ✅ Complete
**Objective**: Verify DPI scaling and resizing stability across various resolutions.

## Milestone v1.3: Advanced Family Automation & Performance

> **Goal**: Super-responsive family processing and batch conversion tools.

### Phase 16: Background Family Editor Engine
**Status**: ✅ Complete
**Objective**: Implement a high-performance "Silent Family Editor" service to modify family-level properties without manual intervention.

### Phase 17: Category Changer Plugin
**Status**: ✅ Complete
**Objective**: Create a tool to quickly switch instance categories by modifying the family definition on-the-fly in the project.

### Phase 18: Convert Family V2 (Replace & Batch)
**Status**: ⬜ Not Started
**Objective**: Enhance `ConvertFamily` with multi-selection support and an "In-Place Replace" mode that preserves model data.

### Phase 19: Performance Validation
**Status**: ⬜ Not Started
**Objective**: Benchmark batch processing and optimize transaction overhead for high responsiveness.
