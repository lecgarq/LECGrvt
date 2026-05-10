# RETROSPECTIVE — LECG Revit Addins

Living retrospective. New milestone sections appended above "Cross-Milestone Trends".

---

## Milestone: v1.1 — Optimization

**Shipped:** 2026-05-10
**Phases:** 6 (1, 2, 2.5, 3, 4, 5) | **Plans:** 26 | **Tests:** 175 GREEN

### What Was Built

- **REQ-07** FormulaAutoGrouping bug fix — per-param SubTransaction loop, EnsureCurrentType guard, clear-replace-restore for cross-references
- **REQ-08/09/10** Silent data-loss hotfixes — Compact Styles locale-safe signature, Category Changer two-pass refuse-all, Convert Family load-before-delete + exact-symbol match + hosted/curve overloads
- **REQ-01** Grid no-blank invariant — ElementLabelService + ElementRowViewModel + ElementGridControl; 14 screens migrated to shared row model
- **REQ-02/03/04** Safe Rename — per-param SubTransaction formula reference rewriting + dimension FamilyLabel reassignment + skip-reason Status column UI
- **REQ-05** Renaming-service test matrix all-✅ + 3 polish helpers (count-on-rollback, pair-action FamilyLabel, progress-cap)
- **REQ-06** Renaming-service consolidation (`ISearchReplaceService` delegation chain via Bootstrapper)

### What Worked

- **Skip-gated RED + anchor-per-fixture pattern.** Established in Phase 03-00 and reused verbatim by 04-00 / 05-00. Kept the suite green while behavioural tests waited for their implementing plan; reason strings naming the plan ID gave a greppable flip-point. Zero regressions across the three Wave-0 setups.
- **Pure-data helper extraction for Revit-API-bound services.** `EvaluateFamilyParamSkipReason`, `CollectFormulaUpdates`, `ExecuteDimensionReassignments` (accepts `List<Action>`, not `List<Dimension>`), `AccumulateCommittedFamilyCount`, `BuildProgressSequence` — each isolated the testable arithmetic from Revit API surface, enabling direct xUnit coverage without RevitAPI.dll. Production wiring stayed thin.
- **Path A on Phase 03-08 (embed `ElementGridControl` in `SelectionControl.xaml`).** Avoided 8 per-View XAML edits and guaranteed grid consistency across selection-backed screens. Visibility bound to `HasSelection` kept empty state clean. The single decision saved ~8 hours of mechanical XAML work.
- **Refuse-all over partial-success.** The Category Changer scope decision (refuse entire batch on any unsupported instance vs silent skip + false success) aligned with the project's core value gate and gave a clean v1.2 transplant-placement story.
- **Trust-based manual Revit acceptance for Phases 04/05.** With 131/131 + 175 GREEN unit tests covering the testable arithmetic, deferring live observation where xUnit fixtures weren't possible (Dimension.FamilyLabel null-clear, polish helpers) shipped the milestone without the verification-fatigue cost of contrived Revit sessions. Documented as a deferred follow-up with named call sites.

### What Was Inefficient

- **Phase 02 / 02.5 predate Nyquist discipline.** Phase 02 has no `VERIFICATION.md`, VALIDATION.md never wave-0 completed. Phase 02.5 explicitly opted out (manual-only bar). The audit re-run on 2026-05-10 flagged this as the milestone's only verification gap — REQ-07 ships on user trust + integration check. A small retroactive verification phase (or `/gsd:validate-phase 2`) would have closed the audit cleanly.
- **Audit-first re-ordering.** The 5-plugin audit on 2026-05-08 inserted Phase 2.5 ahead of Phases 3/4/5, and the v1.1 audit on 2026-05-09 (`gaps_found`) needed a Phase 3/4/5 gap-closure plan because REQ-01..05 hadn't yet started when the audit ran. The audit→close pattern works but front-loads detection cost; running a quick scope-sanity check before kicking off each phase tree would catch this earlier.
- **ROADMAP.md drift.** Phase 2 stayed "In Progress (1/2 plans complete)" in ROADMAP.md after both plans completed — the audit caught it but downstream automation didn't. A periodic `roadmap analyze` sync against actual phase summaries would prevent silent drift.

### Patterns Established

- **Skip-gated RED + anchor-per-fixture** (Wave-0 convention; Phases 03-00/04-00/05-00)
- **Pure-data helper + delegate injection** for Revit-API-bound services (testable without RevitAPI.dll)
- **`InternalsVisibleTo` + concrete `FakeXxx` test doubles** for interfaces Castle DynamicProxy can't proxy without RevitAPI.dll (Phase 05-02 SearchReplaceFakes pattern)
- **Refuse-all batch over partial-success** for any command that mutates state (REQ-09 set the precedent; v1.2 commands should default to it)
- **`BuiltInParameter` over `LookupParameter(string)`** for all type-level parameter access; per-type sentinel for genuinely-unreadable names
- **Trust-based manual Revit acceptance with named deferred follow-ups** when xUnit can cover the testable arithmetic and live observation is contrived

### Key Lessons

1. **Verification discipline must precede execution discipline.** Phase 02 / 02.5 shipped working code but couldn't be audited cleanly — the cheapest fix was to write `VALIDATION.md` + Wave-0 scaffolds before plans 01/02 started. Phases 03-00/04-00/05-00 made this the default; the cost dropped to near-zero.
2. **Migration sweeps (Phase 03) work best with a shared control + cascade.** `ElementGridControl` + Path A `SelectionControl` cascade collapsed 14 screen migrations into 4 plans. The alternative (per-screen XAML edit) would have been ~2.5× the plan count and 5× the regression surface.
3. **Skip-deferred-by-design needs explicit follow-up tracking.** Face-hosted preservation, LocationCurve placement, funnel chrome, Dimension.FamilyLabel null-clear — each was a sound scope decision *for v1.1*, but the v1.2 ROADMAP sketch + `deferred-items.md` per phase made sure none of them got lost.
4. **Trust-based sign-off is fine when the testable arithmetic is GREEN.** It's not a substitute for verification when novel Revit behavior is the unknown — but when the helpers are proven (171/175 + Polish ×3 GREEN), deferring live observation buys real time at small risk.

### Cost Observations

- Sessions: ~13 active days (2026-04-28 → 2026-05-10).
- Model mix: not tracked — recommend tagging future sessions for v1.2.
- Notable: Phase 03 plan count (10) was high but each plan averaged <15 min execution time per the `Performance Metrics` table in STATE.md. Shared-control + delegate-injection patterns kept individual plan scope tight.

---

## Cross-Milestone Trends

*(populated as additional milestones ship)*

### Velocity

| Milestone | Phases | Plans | Days | Plans/Day |
|-----------|--------|-------|------|-----------|
| v1.1 | 6 | 26 | ~13 | ~2.0 |

### Verification Health

| Milestone | Nyquist-Compliant Phases | Trust-Based Phases | Audit Status |
|-----------|--------------------------|---------------------|--------------|
| v1.1 | 3 of 5 (03/04/05) | 2 of 5 (02/02.5 — predate discipline) | tech_debt |

### Test Suite

| Milestone | xUnit Count | Pass Rate |
|-----------|-------------|-----------|
| v1.1 close | 180 (175 GREEN + 5 Skipped) | 100% of non-skipped |
