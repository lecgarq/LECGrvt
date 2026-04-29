# ROADMAP.md — v1.1 Optimization

> **Milestone**: v1.1 Optimization
> **Current Phase**: 2

## Must-Haves

- [ ] Reliable family parameter renaming
- [ ] FormulaAutoGrouping correctly moves formula parameters to "Other" group
- [ ] No blank fields in grid
- [ ] Clear error/skip feedback

## Phases

### Phase 1: Research & Foundation
**Status**: Complete
**Goal**: Verified formula/dimension safety boundaries and implemented a tested formula update foundation.
**Requirements**: REQ-06

### Phase 2: FormulaAutoGrouping Bug Fix
**Status**: Not Started
**Goal**: Fix the FormulaAutoGrouping command so parameters with formulas are reliably moved to the "Other" group without rolling back or silently failing.
**Requirements**: REQ-07
**Plans:** 2 plans

Plans:
- [ ] 02-01-PLAN.md — Test scaffold + SubTransaction loop, remove in-transaction group check, EnsureCurrentType guard, remove EnsureParametersPersistInGroup from live transaction
- [ ] 02-02-PLAN.md — Clear-replace-restore sequence in TryReplaceSharedParameterGroup for cross-parameter formula dependencies + Revit verification checkpoint

### Phase 3: Grid & Collection Fixes
**Status**: Not Started
**Goal**: Fix blank fields and collection logic so all elements appear with valid name and category labels.
**Requirements**: REQ-01

### Phase 4: Advanced Renaming Logic
**Status**: Not Started
**Goal**: Implement safe renaming for stubborn parameters (formula-referenced and dimension-label parameters).
**Requirements**: REQ-02, REQ-03, REQ-04

### Phase 5: Verification & Polish
**Status**: Not Started
**Goal**: Full test coverage and final bug fixes.
**Requirements**: REQ-05
