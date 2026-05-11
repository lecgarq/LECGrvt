# ROADMAP — LECG Revit Addins

> **Current focus**: v2.0 Plugin Maturity — Phase 6 (Cross-cutting Foundation) next.

## Milestones

- ✅ **v1.1 Optimization** — Phases 1, 2, 2.5, 3, 4, 5 (shipped 2026-05-10) — see [milestones/v1.1-ROADMAP.md](milestones/v1.1-ROADMAP.md)
- 🚧 **v2.0 Plugin Maturity** — Phases 6–13 (planning complete, execution pending)

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

### 🚧 v2.0 Plugin Maturity (Phases 6–13)

> **Source**: `.planning/research/SYNTHESIS.md` (5-plugin audit, 2026-05-08) + `.planning/REQUIREMENTS.md` (32 reqs).
> **Theme**: Land cross-cutting foundation first (logger, severity progress, dialog whitelist), mature each of the five core plugins on top of it, then layer shared UI/UX chrome. Carry-over gaps from v1.1 fold into adjacent phases.

- [ ] **Phase 6: Cross-cutting Foundation** — Unified `ILogger`, severity-preserving `IProgressReporter`, dialog whitelist; close v1.1 documentation gap (GAPS-01).
- [ ] **Phase 7: Purge Unused Maturity** — Deep-mode honors user selections; typed `PurgeOptions`; accurate deletion counts; dedup failure handlers.
- [ ] **Phase 8: Compacting Styles Maturity** — Real view (scope toggles + dry-run); shared `CompactionPipeline` base; warnings reach `LogView`; v1.1 Phase 02.5 manual Revit checks executed (GAPS-02).
- [ ] **Phase 9: Convert Family Maturity** — Blast-radius preview; collapse over-decomposition; typed `FamilyConversionResult`; face-hosted preservation via `HostFace` capture.
- [ ] **Phase 10: Category Changer Maturity** — `IInstanceSwapService` extracted; real cancellation; typed `ChangeCategoryResult`; LocationCurve / hosted / face-hosted transplant.
- [ ] **Phase 11: Batch Rename Maturity** — Collapse `SearchReplaceService` facade; async `CollectBaseElements`; scope-pills UX; per-column funnel; close v1.1 Phase 04 C1 dimension null-clear observation (GAPS-03).
- [ ] **Phase 12: UI System** — Shared visual style across all command views; `LogView` filter/search/copy/groups; per-command ribbon icons + headers; uniform preview-then-execute pattern.
- [ ] **Phase 13: UX Polish** — Inline actionable errors with retry; cancellation contract everywhere; keyboard/focus parity; tooltips + inline help.

## Phase Details

### Phase 6: Cross-cutting Foundation
**Goal**: Establish the unified logging, progress, and dialog-suppression substrate every later v2.0 phase consumes — and close the one v1.1 documentation gap that lives in the same context.
**Depends on**: v1.1 baseline (175 GREEN xUnit)
**Requirements**: CROSS-01, CROSS-02, CROSS-03, GAPS-01
**Success Criteria** (what must be TRUE):
  1. A developer can route any plugin log entry through a single structured `ILogger` interface (Info/Warn/Error severity, scope tag) and see it land in `LogView` with the original severity preserved.
  2. A user running any command sees `LogWarning` / `LogError` from `IProgressReporter` rendered with their actual severity in `LogView` (never collapsed to plain `Log`).
  3. A user running Purge or Convert Family sees only whitelisted dialogs auto-dismissed; any non-whitelisted dialog (including unexpected error dialogs) reaches the user.
  4. A developer reviewing v1.1 Phase 02 finds a complete `02-VERIFICATION.md` artifact with wave-0 evidence for REQ-07 (FormulaAutoGrouping).
**Plans:** 2/5 plans executed
  - [ ] 06-00-PLAN.md — Wave 0: test scaffolding (LoggerSeverityTests, DialogWhitelistTests) + DialogId runtime discovery checkpoint
  - [ ] 06-01-PLAN.md — Wave 1: extend ILogger contract with scope + rewrite IProgressReporter impls for severity preservation
  - [ ] 06-02-PLAN.md — Wave 2: Logger.Instance migration sweep across 30+ files + singleton deletion + Bootstrapper rewire
  - [ ] 06-03-PLAN.md — Wave 3: DialogWhitelist class + IDialogOverride seam + PurgeCommand/ConvertFamilyCommand handler migration
  - [ ] 06-04-PLAN.md — Wave 1 (parallel, independent): retroactive 02-VERIFICATION.md compilation (GAPS-01)

### Phase 7: Purge Unused Maturity
**Goal**: Make Deep purge honor user intent and report truthfully — no silent ignored checkboxes, no undercounted deletions, no hidden swallowed failures.
**Depends on**: Phase 6 (consumes unified logger + severity progress)
**Requirements**: PURGE-01, PURGE-02, PURGE-03
**Success Criteria** (what must be TRUE):
  1. A user's Deep-mode checkbox selections drive the purge exactly — unchecked items are never purged, checked items are always attempted.
  2. A user sees a final deletion count in `LogView` that matches the actual number of elements removed (no undercount, no bare `catch {}` swallowing failures).
  3. A developer reading the Deep-purge code path sees one typed `PurgeOptions` parameter object passed into a single `PurgeContext` built outside the Revit transaction (no 13-bool sprawl, no duplicated failure-handler implementations).
**Plans**: TBD

### Phase 8: Compacting Styles Maturity
**Goal**: Give Compact Styles a real user-facing view with scope control and dry-run, share the workflow across the four style families, and clear the v1.1 manual-Revit-verification debt that lives in this surface.
**Depends on**: Phase 6 (consumes unified logger + severity progress)
**Requirements**: COMPACT-01, COMPACT-02, COMPACT-03, GAPS-02
**Success Criteria** (what must be TRUE):
  1. A user opens Compacting Styles and sees per-style-type scope toggles (text / line / dimension / object styles) and a dry-run preview listing merge candidates before any commit.
  2. A user sees compaction warnings (locale fallbacks, sentinel-unreadable cases, skip reasons) surfaced in `LogView` with the correct severity.
  3. A developer reading the four compaction services sees them share a `CompactionPipeline` base class; legacy callback overloads with zero callers are gone.
  4. A reviewer can verify all four v1.1 Phase 02.5 manual Revit items (Spanish-locale Compact Styles, English regression, mixed-batch refuse-all, wall-hosted Convert Family) have been executed in Revit and logged with sign-off.
**Plans**: TBD

### Phase 9: Convert Family Maturity
**Goal**: Stop Convert Family from feeling like a black box — show the user what will be replaced, deliver a structured result they can read, preserve face-hosted placements, and collapse the over-decomposed service graph.
**Depends on**: Phase 6 (logger + dialog whitelist)
**Requirements**: CONVERT-01, CONVERT-02, CONVERT-03, CONVERT-04
**Success Criteria** (what must be TRUE):
  1. A user about to convert a family sees a blast-radius preview (instance count, categories impacted, hosted/face-hosted breakdown) before committing.
  2. A user receives a typed `FamilyConversionResult` summary listing every family with per-family success / skip / refuse and a reason; reasons appear in the UI, not log-only.
  3. A face-hosted family instance lands back on its original face after conversion — `FamilyInstanceData` captures `HostFace` and the placement pipeline uses it (closes v1.1 deferral).
  4. A developer reads the Convert Family codebase with ~10 pass-through single-Revit-call wrappers gone, `FamilyConversionNamingService` deleted, and inert `customName` / `isTemporary` parameters removed.
**Plans**: TBD

### Phase 10: Category Changer Maturity
**Goal**: Lift the v1.1 refuse-all gate now that placement is real — transplant `LocationCurve`, hosted, and face-hosted instances correctly, and give the user real cancellation and typed results.
**Depends on**: Phase 6 (logger + severity progress)
**Requirements**: CATEGORY-01, CATEGORY-02, CATEGORY-03, CATEGORY-04
**Success Criteria** (what must be TRUE):
  1. A user transplants `LocationCurve`, hosted (wall/floor/ceiling), and face-hosted instances across categories and sees them land in the correct geometric position with no silent loss; the v1.1 refuse-all gate is lifted.
  2. A user pressing Cancel mid-run sees the batch stop on the current element and a "Cancelled after N of M elements" summary surface in the UI.
  3. A user reads a typed `ChangeCategoryResult` per element (success / skip / refuse + reason) in the UI — no more boolean+log mishmash.
  4. A developer reads `CategoryChangerCommand` with the instance-swap logic extracted into a dedicated `IInstanceSwapService` (proper separation of concerns).
**Plans**: TBD

### Phase 11: Batch Rename Maturity
**Goal**: Finish the v1.1 Batch Rename consolidation — collapse the lingering facade, keep the UI responsive on big models, give scope selection visual chrome, and clear the v1.1 Phase 04 C1 live-observation gap that lives in this code.
**Depends on**: Phase 6 (logger + severity progress); v1.1 Phase 04 helpers
**Requirements**: BATCH-01, BATCH-02, BATCH-03, BATCH-04, GAPS-03
**Success Criteria** (what must be TRUE):
  1. A user opens Batch Rename on a large model and the UI remains responsive — `CollectBaseElements` runs asynchronously with visible progress.
  2. A user selects scope (categories, types, instances) via visual scope-pill chrome; the prior dropdown sprawl is gone.
  3. A user clicks a column header and sees a funnel popup of distinct values for filtering; `SetColumnFilter` is functional.
  4. A developer reads `BatchRenameExecutionService` with the `SearchReplaceService` facade collapsed to one clean delegation surface; the unused `IFormulaUpdateService` DI registration is removed if confirmed dead.
  5. A reviewer can verify the v1.1 Phase 04 C1 `Dimension.FamilyLabel` null-clear pair-action overload has been observed live in Revit and signed off.
**Plans**: TBD

### Phase 12: UI System
**Goal**: After per-plugin maturity lands, unify the visual layer — one shared chrome, a competent `LogView`, ribbon identity, and a uniform preview-then-execute pattern for destructive commands.
**Depends on**: Phases 7–11 (per-plugin command shapes settled before they get re-skinned)
**Requirements**: UI-01, UI-02, UI-03, UI-04
**Success Criteria** (what must be TRUE):
  1. A user opens any command view and sees the same shared visual style (colors, typography, control sizing) — no one-off chrome remains.
  2. A user inside `LogView` can filter by severity, search by text, copy entries to the clipboard, and collapse scope groups.
  3. A user finds each command in the Revit ribbon with a recognizable icon, and opening the command shows an in-view header matching LECG visual identity.
  4. A user running any destructive command (Purge, Compact, Convert, Category, Batch Rename) follows the same preview-then-execute flow: preview pane → commit button.
**Plans**: TBD

### Phase 13: UX Polish
**Goal**: Close the interaction quality gaps — actionable errors with retry, cancellation everywhere, keyboard parity, and discoverable help.
**Depends on**: Phases 7–12 (UX-02 cancellation contract needs per-plugin internals from 7–11; tooltips/help in 13 layer on 12's chrome)
**Requirements**: UX-01, UX-02, UX-03, UX-04
**Success Criteria** (what must be TRUE):
  1. A user with failed elements sees inline, actionable error messages and a selectable failed-element list they can retry (no more log-only feedback).
  2. A user pressing Cancel mid-batch in Purge, Compact, Convert, or Batch Rename sees the same cancellation contract Category Changer ships in Phase 10 — stop on current element, surface "Cancelled after N of M".
  3. A user navigates every command view via Tab order, commits with Enter where safe, cancels with Esc, and uses common shortcuts (Ctrl+A select-all, etc.).
  4. A user hovers any option in any command view and sees a tooltip explaining its effect; scope toggles, dry-run, and refuse-all semantics carry inline help text.
**Plans**: TBD

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Research & Foundation | v1.1 | — | Complete | pre-scope |
| 2. FormulaAutoGrouping Bug Fix | v1.1 | 2/2 | Complete | 2026-04-28 |
| 2.5. Silent Data-Loss Hotfixes | v1.1 | 3/3 | Complete | 2026-05-08 |
| 3. Grid & Collection Fixes | v1.1 | 10/10 | Complete | 2026-05-09 |
| 4. Advanced Renaming Logic | v1.1 | 6/6 | Complete | 2026-05-10 |
| 5. Verification & Polish | v1.1 | 5/5 | Complete | 2026-05-10 |
| 6. Cross-cutting Foundation | 2/5 | In Progress|  | — |
| 7. Purge Unused Maturity | v2.0 | 0/4 | Not started | — |
| 8. Compacting Styles Maturity | v2.0 | 0/4 | Not started | — |
| 9. Convert Family Maturity | v2.0 | 0/4 | Not started | — |
| 10. Category Changer Maturity | v2.0 | 0/4 | Not started | — |
| 11. Batch Rename Maturity | v2.0 | 0/4 | Not started | — |
| 12. UI System | v2.0 | 0/4 | Not started | — |
| 13. UX Polish | v2.0 | 0/4 | Not started | — |
