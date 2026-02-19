# ROADMAP.md

> **Current Phase**: Finalized
> **Milestone**: v1.1 - Admin & Safety Update

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
