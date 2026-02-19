# ROADMAP.md

> **Current Phase**: Not started
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

**Status**: ⬜ Not Started
**Objective**: Update Purge infrastructure to support iterative cleaning.

**Tasks**:

- Modify `PurgeService` to allow 3-pass execution.
- Update `PurgeCommand` or `PurgeView` to expose "Triple Purge" option.
- Ensure proper logging of items removed in each pass.

### Phase 3: Selection & Selection Safety

**Status**: ⬜ Not Started
**Objective**: Refine `ConvertFamily` selection behavior and ensure data integrity.

**Tasks**:

- Update `ISelectionFilter` in `FamilySelectionFilter.cs` to check `Family.IsWorkPlaneBased`.
- Validate element hosting behavior during conversion.
- Add pre-flight checks for naming collisions.

### Phase 4: Verification & Documentation

**Status**: ⬜ Not Started
**Objective**: Empirically verify all features in Revit 2026.

**Tasks**:

- Verify renaming of all 4 new types.
- Verify triple purge effectiveness.
- Verify selection restriction in project environment.
- Update `ARCHITECTURE.md` if services are significantly refactored.
