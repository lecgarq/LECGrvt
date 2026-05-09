# ROADMAP.md — v1.1 Optimization

> **Milestone**: v1.1 Optimization
> **Current Phase**: 2 (code complete, awaiting Revit verify) → next: 2.5

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
**Status**: In Progress (1/2 plans complete)
**Goal**: Fix the FormulaAutoGrouping command so parameters with formulas are reliably moved to the "Other" group without rolling back or silently failing.
**Requirements**: REQ-07
**Plans:** 2/2 plans complete

Plans:
- [x] 02-01-PLAN.md — Test scaffold + SubTransaction loop, remove in-transaction group check, EnsureCurrentType guard, remove EnsureParametersPersistInGroup from live transaction
- [ ] 02-02-PLAN.md — Clear-replace-restore sequence in TryReplaceSharedParameterGroup for cross-parameter formula dependencies + Revit verification checkpoint

### Phase 2.5: Silent Data-Loss Hotfixes
**Status**: Planned
**Goal**: Stop three silent partial-success / data-loss bugs surfaced by the 5-plugin audit (2026-05-08). Contained, fast.
**Requirements**: REQ-08, REQ-09, REQ-10
**Source**: `.planning/research/SYNTHESIS.md`
**Plans:** 3 plans
- [ ] 02.5-01-PLAN.md — Compact Styles (REQ-08, Wave 1): locale-safe text-style signature using `BuiltInParameter.TEXT_ALIGNMENT` / `LEADER_ARROWHEAD` + per-type sentinel for `"Text Orientation"` (no BIP) in `TextStyleCompactionService.BuildSignature`
- [x] 02.5-02-PLAN.md — Category Changer (REQ-09, Wave 1): refuse-all pre-flight in `SwapInstances` detecting `LocationCurve` / `Host` / `HostFace`; abort batch + log every unsupported instance instead of silent `[SKIP]` + false `[SUCCESS]`
- [ ] 02.5-03-PLAN.md — Convert Family (REQ-10, Wave 2): pre-flight validator + load-before-delete ordering + exact-name symbol match (capture `OriginalSymbolName`) + `NewFamilyInstance` hosted/curve overloads in `FamilyConversionService` / `FamilyInstanceData`

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

---

## Future Milestone: v1.2 Plugin Maturity (queued — not started)

> **Source**: `.planning/research/SYNTHESIS.md` (5-plugin audit, 2026-05-08)
> **Theme**: Cross-cutting infrastructure + per-plugin upgrade pass for the 5 plugins audited (Purge Unused, Compacting Styles, Convert Family, Category Changer, Batch Rename). Phases will be detailed when v1.2 starts; sketch below for tracking.

### v1.2 Phase 1: Cross-cutting Infrastructure
Unify logging stack (single severity-preserving interface; consistent tag taxonomy), `IProgressReporter` contract preserves `LogWarning`/`LogError` severity, replace blanket `e.OverrideResult(2)` dialog dismissal with whitelist-by-DialogId. Lands first so subsequent phases benefit.

### v1.2 Phase 2: Purge Unused Maturity
Fix Deep-mode-silently-ignores-12-of-13-checkboxes UX gate, options-object refactor (replace 13-positional-arg API), dedup `DeepPurgeService.ReloadPurgedFamily` and `PurgeParameterService.ReloadFamily` failure handlers, fix false deletion count in `NativePurgeDocumentService`, parameterize hardcoded `"Enscape"` third-party-param filter, build single `PurgeContext` outside transactions.

### v1.2 Phase 3: Compacting Styles Maturity
Build a real view (currently has none) with scope toggles + dry-run, extract `CompactionPipeline` base for the 4 templatable scopes, surface `Logger.Instance.LogWarning` calls to `LogView`, fix `LineStyleCompactionService.RewireCurveElements` per-original-per-group rescan, build context once outside transactions, completion summary, gate semantic line-pattern rename behind opt-in.

### v1.2 Phase 4: Convert Family Maturity
Preview/confirmation UI showing instance blast radius before delete; collapse over-decomposition (delete dead `FamilyConversionNamingService`, `customName`/`isTemporary` inert params, ~10 single-Revit-call wrappers; move `FamilyEditorService` out of FamilyConversion folder); structured per-family summary report; tighten `OnDialogShowing` handler; collapse three parallel template resolvers to one.

### v1.2 Phase 5: Category Changer Maturity
Extract instance-swap to a service (out of 200-LOC `CategoryChangerEventHandler` god-class); real cancellation; typed `ChangeCategoryResult` instead of bool + magic-string substring matching on exception messages; structured logging; batch progress; remove `[V6-EVENT]` dev tag; fix locale-hardcoded template search.

### v1.2 Phase 6: Batch Rename Maturity
Collapse `SearchReplaceService` pass-through facade leaking ViewModel types; fix `count` increment on transaction rollback (`BatchRenameExecutionService.cs:185`); fix standard-item progress capping below 100%; fix `SwapStyle` orphaning non-`CurveElement` consumers of GraphicsStyle; async `CollectBaseElements` (currently freezes UI on large models with Parameters scope); scope UX (single-select pills, currently multi-select-rendered exclusive scope is a UX trap); dedup `ParamGroup` swallowed try/catch (`BaseElementCollectionService.cs:213`+`:275`); retire dead `IFormulaUpdateService` registration or wire it up via REQ-02.
