# Requirements — v2.0 Plugin Maturity

**Milestone:** v2.0 Plugin Maturity
**Source:** `.planning/research/SYNTHESIS.md` (5-plugin audit, 2026-05-08) + user-confirmed UI/UX additions
**Status:** Roadmap approved — Phases 6–13 mapped (2026-05-10)

Carries forward the **core value** gate from v1.1: every command either succeeds visibly, skips with a reason surfaced in the UI, or refuses the batch with a structured log. No partial-success that looks like full-success.

---

## v2.0 Requirements

### Cross-cutting infrastructure (CROSS)

- [ ] **CROSS-01**: A developer can route every plugin log entry through a single structured `ILogger` interface with preserved severity (Info/Warn/Error), scope tag, and consistent `LogView` surfacing — replacing the `Logger.Instance` / `IProgressReporter` / VM-callback fragmentation.
- [ ] **CROSS-02**: A user sees `LogWarning` and `LogError` entries from `IProgressReporter` rendered with their original severity in `LogView` (no more collapse-to-`Log`).
- [ ] **CROSS-03**: Auto-dialog dismissal (Purge, Convert Family) suppresses only dialogs whose `DialogId` is in an explicit whitelist; any other dialog reaches the user.

### Purge Unused maturity (PURGE)

- [ ] **PURGE-01**: A user's Deep-mode checkbox selections are honored — no checkbox is silently ignored; unselected items are not purged.
- [ ] **PURGE-02**: Deep purge runs against a single `PurgeContext` built outside the Revit transaction and configured via a typed `PurgeOptions` object (no 13-bool checkbox sprawl in the executor).
- [ ] **PURGE-03**: Deep purge reports the actual count of deletions (no undercount, no bare `catch {}`); failure-handler logic is implemented once and shared between `DeepPurge` and `PurgeParameter` paths.

### Compacting Styles maturity (COMPACT)

- [ ] **COMPACT-01**: A user sees a Compacting Styles view with per-style-type scope toggles and a dry-run preview of merge candidates before committing.
- [ ] **COMPACT-02**: Compaction services for the 4 style families share a `CompactionPipeline` base class (templatable workflow); legacy callback overloads with no callers are removed.
- [ ] **COMPACT-03**: Compaction warnings surface in `LogView` with the correct severity (not swallowed).

### Convert Family maturity (CONVERT)

- [ ] **CONVERT-01**: A user sees a blast-radius preview (count + categories of instances to be replaced/deleted) before committing a family conversion.
- [ ] **CONVERT-02**: A developer reads a Convert Family codebase with pass-through services collapsed (~10 single-Revit-call wrappers removed), `FamilyConversionNamingService` deleted (never called), and inert `customName` / `isTemporary` parameters removed.
- [ ] **CONVERT-03**: A user receives a typed `FamilyConversionResult` summary listing every family with per-family success / skip / refuse and a reason; reasons surface in the UI (not log-only).
- [ ] **CONVERT-04**: A face-hosted family instance is re-placed on its original face after conversion — `FamilyInstanceData` captures `HostFace` and the placement pipeline uses it (closes v1.1 deferred item).

### Category Changer maturity (CATEGORY)

- [ ] **CATEGORY-01**: A developer reads `CategoryChangerCommand` with the instance-swap logic extracted into a dedicated `IInstanceSwapService` (proper separation of concerns).
- [ ] **CATEGORY-02**: A user pressing Cancel during a Category Changer run interrupts mid-batch and surfaces a "Cancelled after N of M elements" summary.
- [ ] **CATEGORY-03**: Category Changer returns a typed `ChangeCategoryResult` per element (success / skip / refuse + reason) — replaces the boolean+log mishmash.
- [ ] **CATEGORY-04**: A user can transplant `LocationCurve`, hosted (wall/floor/ceiling), and face-hosted instances across categories without silent loss; the v1.1 refuse-all gate is lifted now that placement is real.

### Batch Rename maturity (BATCH)

- [ ] **BATCH-01**: A developer reads `BatchRenameExecutionService` with the `SearchReplaceService` facade collapsed — one clean delegation surface (no facade leakage from v1.1 REQ-06 consolidation).
- [ ] **BATCH-02**: A user opening Batch Rename on a large model sees the UI remain responsive — `CollectBaseElements` runs asynchronously with progress.
- [ ] **BATCH-03**: A user selects scope (categories, types, instances) via visual scope-pill chrome (replaces dropdown sprawl).
- [ ] **BATCH-04**: A user sees a per-column funnel popup (`SetColumnFilter` visual) listing distinct values for selection; `IFormulaUpdateService` DI registration is removed if confirmed unused (closes v1.1 dead-reg gap).

### UI overhaul (UI)

- [ ] **UI-01**: Every command view applies a shared visual style (colors, typography, control sizing) — no one-off view chrome.
- [ ] **UI-02**: `LogView` supports severity filter, text search, copy-to-clipboard, and collapsible scope groups.
- [ ] **UI-03**: Each command has a ribbon icon plus an in-view header matching LECG visual identity.
- [ ] **UI-04**: Every command that performs destructive work (Purge, Compact, Convert, Category, Batch Rename) follows a uniform preview-then-execute pattern (preview pane → commit button).

### UX interaction (UX)

- [ ] **UX-01**: A user receives inline, actionable error messages for failed elements; the failed-element list is selectable for retry (vs current log-only feedback).
- [ ] **UX-02**: Every long-running command honors Cancel mid-batch — Purge, Compact, Convert, Batch Rename match the Category Changer cancellation contract (CATEGORY-02).
- [ ] **UX-03**: Every command view supports Tab order, Enter-to-commit (where safe), Esc-to-cancel, and common shortcuts (Ctrl+A select-all etc).
- [ ] **UX-04**: Every option in every command view has a tooltip explaining its effect; inline help text explains scope toggles, dry-run, and refuse-all semantics.

### v1.1 carry-over gap closure (GAPS)

- [x] **GAPS-01**: Phase 02 (FormulaAutoGrouping) has a formal `02-VERIFICATION.md` artifact authored retroactively, with wave-0 evidence for REQ-07.
- [ ] **GAPS-02**: Phase 02.5's 4 manual Revit human-verification items (Spanish-locale Compact Styles, English regression, mixed-batch refuse-all, wall-hosted Convert Family) are executed and logged.
- [ ] **GAPS-03**: Phase 04 C1 `Dimension.FamilyLabel` null-clear behavior is observed live in Revit (pair-action overload in production) and signed off.

---

## Future Requirements (deferred from v2.0)

- Tag taxonomy unification across `[V6-EVENT]`/`[OK]`/`[SUCCESS]`/etc — wait until CROSS-01 lands; revisit cosmetic cleanup if needed.
- Hardcoded `"Enscape"` filter parameterization — keep in code, surface to config only if user demand arises.
- Compacting Styles per-original-per-group rescan fix — small bug; bundle with COMPACT-01/02 work if cheap, otherwise defer.
- Convert Family `FamilyEditorService` relocation (only used by CategoryChangerCommand) — cosmetic; revisit during CATEGORY-01 service extraction.
- Phase 05 Polish #1/#2/#3 live Revit observation — trust-based v1.1 sign-off stands; revisit only if production issues surface.

## Out of Scope

- **Pre-2026 Revit support** — single-version maintenance (Revit 2026 API only). [Reason: matches PROJECT.md constraint]
- **Localized UI** — plugin UI stays English; code remains locale-safe via `BuiltInParameter` + `LabelUtils.GetLabelFor`. [Reason: matches PROJECT.md out-of-scope]
- **New command additions** — v2.0 matures the existing command set; new commands go in v2.1+. [Reason: scope discipline]
- **API refactor / public-surface changes** — internal refactors only; no breaking changes for downstream consumers. [Reason: stability]

## Traceability

| REQ-ID | Phase | Status |
|--------|-------|--------|
| CROSS-01 | Phase 6 — Cross-cutting Foundation | Pending |
| CROSS-02 | Phase 6 — Cross-cutting Foundation | Pending |
| CROSS-03 | Phase 6 — Cross-cutting Foundation | Pending |
| GAPS-01  | Phase 6 — Cross-cutting Foundation | Complete |
| PURGE-01 | Phase 7 — Purge Unused Maturity | Pending |
| PURGE-02 | Phase 7 — Purge Unused Maturity | Pending |
| PURGE-03 | Phase 7 — Purge Unused Maturity | Pending |
| COMPACT-01 | Phase 8 — Compacting Styles Maturity | Pending |
| COMPACT-02 | Phase 8 — Compacting Styles Maturity | Pending |
| COMPACT-03 | Phase 8 — Compacting Styles Maturity | Pending |
| GAPS-02 | Phase 8 — Compacting Styles Maturity | Pending |
| CONVERT-01 | Phase 9 — Convert Family Maturity | Pending |
| CONVERT-02 | Phase 9 — Convert Family Maturity | Pending |
| CONVERT-03 | Phase 9 — Convert Family Maturity | Pending |
| CONVERT-04 | Phase 9 — Convert Family Maturity | Pending |
| CATEGORY-01 | Phase 10 — Category Changer Maturity | Pending |
| CATEGORY-02 | Phase 10 — Category Changer Maturity | Pending |
| CATEGORY-03 | Phase 10 — Category Changer Maturity | Pending |
| CATEGORY-04 | Phase 10 — Category Changer Maturity | Pending |
| BATCH-01 | Phase 11 — Batch Rename Maturity | Pending |
| BATCH-02 | Phase 11 — Batch Rename Maturity | Pending |
| BATCH-03 | Phase 11 — Batch Rename Maturity | Pending |
| BATCH-04 | Phase 11 — Batch Rename Maturity | Pending |
| GAPS-03 | Phase 11 — Batch Rename Maturity | Pending |
| UI-01 | Phase 12 — UI System | Pending |
| UI-02 | Phase 12 — UI System | Pending |
| UI-03 | Phase 12 — UI System | Pending |
| UI-04 | Phase 12 — UI System | Pending |
| UX-01 | Phase 13 — UX Polish | Pending |
| UX-02 | Phase 13 — UX Polish | Pending |
| UX-03 | Phase 13 — UX Polish | Pending |
| UX-04 | Phase 13 — UX Polish | Pending |

**Coverage:** 32 / 32 requirements mapped ✓ (no orphans, no duplicates)

---

*Last updated: 2026-05-10 — Phases 6–13 mapped by gsd-roadmapper*
