# ROADMAP — LECG Revit Addins

> **Current focus**: v1.2 Plugin Maturity planning (queued — start with `/gsd:new-milestone`)

## Milestones

- ✅ **v1.1 Optimization** — Phases 1, 2, 2.5, 3, 4, 5 (shipped 2026-05-10) — see [milestones/v1.1-ROADMAP.md](milestones/v1.1-ROADMAP.md)
- 📋 **v1.2 Plugin Maturity** — Phases planned (sketch below; detailed via `/gsd:new-milestone`)

## Phases

<details>
<summary>✅ v1.1 Optimization (Phases 1–5) — SHIPPED 2026-05-10</summary>

- [x] Phase 1: Research & Foundation — completed pre-scope (REQ-06 service consolidation)
- [x] Phase 2: FormulaAutoGrouping Bug Fix (2/2 plans) — completed 2026-04-28 (REQ-07; verification artifact partial)
- [x] Phase 2.5: Silent Data-Loss Hotfixes (3/3 plans) — completed 2026-05-08 (REQ-08/09/10; INSERTED for 5-plugin audit)
- [x] Phase 3: Grid & Collection Fixes (10/10 plans) — completed 2026-05-09 (REQ-01; 22/22 manual Revit PASS)
- [x] Phase 4: Advanced Renaming Logic (6/6 plans) — completed 2026-05-10 (REQ-02/03/04; 131/131 unit tests + trust-based sign-off)
- [x] Phase 5: Verification & Polish (5/5 plans) — completed 2026-05-10 (REQ-05; 175 GREEN xUnit + 3 polish helpers)

Full details: [milestones/v1.1-ROADMAP.md](milestones/v1.1-ROADMAP.md)

</details>

### 📋 v1.2 Plugin Maturity (queued — not started)

> **Source**: `.planning/research/SYNTHESIS.md` (5-plugin audit, 2026-05-08)
> **Theme**: Cross-cutting infrastructure + per-plugin maturity passes. Phases will be detailed when v1.2 starts; sketch below for tracking.

- [ ] v1.2 Phase 1: Cross-cutting infrastructure — unify logging stack, severity-preserving `IProgressReporter`, whitelist-by-DialogId dismissal
- [ ] v1.2 Phase 2: Purge Unused maturity — Deep-mode UX gate, options-object refactor, dedup reload handlers, false-deletion-count fix, parameterize `"Enscape"` filter, single `PurgeContext` outside transactions
- [ ] v1.2 Phase 3: Compacting Styles maturity — real view with scope toggles + dry-run, `CompactionPipeline` base, surface warnings to LogView, per-original-per-group rescan fix
- [ ] v1.2 Phase 4: Convert Family maturity — blast-radius preview UI, collapse over-decomposition, structured per-family summary, face-hosted preservation (HostFace capture)
- [ ] v1.2 Phase 5: Category Changer maturity — extract instance-swap service, real cancellation, typed `ChangeCategoryResult`, LocationCurve / hosted / face-hosted transplant placement
- [ ] v1.2 Phase 6: Batch Rename maturity — collapse `SearchReplaceService` facade leakage, async `CollectBaseElements`, scope-pills UX, retire dead `IFormulaUpdateService` registration if unused, per-column funnel chrome

### v1.2 Follow-up Items (gap-closure carried from v1.1)

- [ ] Per-column header funnel chrome (visual WPF popup of distinct values) — `SetColumnFilter` API functional
- [ ] `Dimension.FamilyLabel` null-clear live observation (pair-action overload in production)
- [ ] Optional retroactive `/gsd:validate-phase 2` to author `02-VERIFICATION.md` + fill wave-0 (REQ-07 verification artifact)
- [ ] Phase 02.5 4 manual Revit human-verification items (Spanish-locale Compact Styles, English regression, mixed-batch refuse-all, wall-hosted Convert Family)

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Research & Foundation | v1.1 | — | Complete | pre-scope |
| 2. FormulaAutoGrouping Bug Fix | v1.1 | 2/2 | Complete | 2026-04-28 |
| 2.5. Silent Data-Loss Hotfixes | v1.1 | 3/3 | Complete | 2026-05-08 |
| 3. Grid & Collection Fixes | v1.1 | 10/10 | Complete | 2026-05-09 |
| 4. Advanced Renaming Logic | v1.1 | 6/6 | Complete | 2026-05-10 |
| 5. Verification & Polish | v1.1 | 5/5 | Complete | 2026-05-10 |
| (v1.2 phases) | v1.2 | — | Not started | — |
