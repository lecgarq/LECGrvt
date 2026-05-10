# Milestones Log

Historical record of shipped milestones. For active work, see [ROADMAP.md](ROADMAP.md).

---

## v1.1 Optimization — Shipped 2026-05-10

**Phases:** 6 (1, 2, 2.5, 3, 4, 5) | **Plans:** 26 | **Tests:** 175 GREEN xUnit (5 Skipped, 0 Failed)
**Timeline:** 2026-04-28 → 2026-05-10 (~13 days)
**Tag:** `v1.1`
**Audit:** `tech_debt` (9/10 requirements satisfied, REQ-07 partial verification artifact) — see [milestones/v1.1-MILESTONE-AUDIT.md](milestones/v1.1-MILESTONE-AUDIT.md)

### Delivered

Core renaming pain addressed (formula/dimension safety, skip-reason visibility), silent data-loss bugs from the 5-plugin audit (2026-05-08) closed for Compact Styles / Category Changer / Convert Family, every Batch-Rename / selection-backed grid lifted to a shared no-blank-fields row model, and a 175-test renaming-service coverage matrix established.

### Key Accomplishments

1. **REQ-07** — FormulaAutoGrouping bug fix: per-parameter SubTransaction loop + `EnsureCurrentType` guard + clear-replace-restore for shared parameters with cross-references; xUnit 79/79 GREEN.
2. **REQ-08/09/10** — Silent data-loss hotfixes: Compact Styles locale-safe text-style signature (`BuiltInParameter.TEXT_ALIGNMENT` + orientation sentinel), Category Changer two-pass refuse-all in `SwapInstances`, Convert Family pre-flight + load-before-delete + exact-symbol-name match + hosted/curve placement overloads.
3. **REQ-01** — Grid no-blank invariant: `ElementLabelService` + `ElementRowViewModel` + `ElementGridControl` shared UserControl; 14 screens migrated (Batch Rename + 6 text-summary + 8 selection-backed) with LogView fallback warnings.
4. **REQ-02/03/04** — Safe Rename: `IFormulaUpdateService` injected into `BatchRenameExecutionService` with per-param SubTransaction + formula reference rewriting + `BuildDimensionsByLabelName` collector + `ExecuteDimensionReassignments` pair-action overload; `SearchReplaceView.xaml` Status column + muted rows + disabled checkboxes + side-effect counts.
5. **REQ-05** — Renaming-service test coverage matrix all-✅: 3 new xUnit fixtures (RenameRulePipelineService, SearchReplaceService, BatchRenameExecutionService) + deepening of BaseElementCollectionService; 3 polish helpers shipped (`AccumulateCommittedFamilyCount`, `ExecuteDimensionReassignments` pair-action overload, `BuildProgressSequence`).
6. **REQ-06** — Renaming services consolidated; `ISearchReplaceService` delegation chain wired through `Bootstrapper.cs`.

### Known Gaps Carried Forward (tech debt)

- Phase 02 lacks formal `VERIFICATION.md` (trust-based REQ-07 sign-off; integration check confirmed code wiring)
- Phase 02.5 manual Revit verification of 4 human-checkable items deferred to v1.2
- Phase 03 per-column header funnel chrome (visual WPF popup) deferred to v1.2
- Phase 04 C1 Dimension.FamilyLabel null-clear live observation deferred (pair-action overload in production via Phase 5 Polish #2)
- Phase 05 Polish #1/#2/#3 manual Revit observation trust-based per Phase 4 precedent

### Archives

- [milestones/v1.1-ROADMAP.md](milestones/v1.1-ROADMAP.md) — full phase details
- [milestones/v1.1-REQUIREMENTS.md](milestones/v1.1-REQUIREMENTS.md) — requirements traceability
- [milestones/v1.1-MILESTONE-AUDIT.md](milestones/v1.1-MILESTONE-AUDIT.md) — re-audit report (2026-05-10)
