# 5-Plugin Audit — Synthesis

**Date:** 2026-05-08
**Scope:** Purge Unused, Compacting Styles, Convert Family, Category Changer, Batch Rename
**Source:** `.planning/research/plugin-*.md`

---

## TL;DR

Five plugins, ~6,000 LOC of plugin code audited. Findings cluster into three buckets:

1. **Silent data corruption / silent partial-success** (the urgent stuff)
2. **Cross-cutting infrastructure debt** (logging, dialogs, progress, dead consolidation artifacts)
3. **Per-plugin polish** (UX, perf, scope toggles)

The roadmap currently has REQ-01..REQ-05 covering only Batch Rename. Audit surfaced ~30 distinct issues across the other four plugins, several of them silent-data-loss class.

---

## Critical Findings (silent-failure class — fix first)

| # | Plugin | Issue | File:Line |
|---|--------|-------|-----------|
| 1 | Category Changer | `SwapInstances` only handles `LocationPoint` — line/host/face/curve instances reported `[SUCCESS]` but still reference the OLD family | CategoryChangerCommand.cs:190 |
| 2 | Convert Family | `HostId` captured but never restored — wall-hosted doors lose their host | FamilyConversionService.cs (host restore missing) |
| 3 | Convert Family | `LocationCurve` instances captured then discarded in `Apply` — silent loss | FamilyConversionService.cs |
| 4 | Convert Family | `FindReplacementSymbol` always picks first symbol — original type lost | FamilyConversionService.cs:259 |
| 5 | Convert Family | Originals deleted **before** new family loads — failure = data gone | FamilyConversionService.cs |
| 6 | Compact Styles | Text-style signature uses English `LookupParameter("Horizontal Alignment", ...)` — on localized Revit, visually different styles get merged | TextStyleCompactionService.cs:176 |
| 7 | Purge | `NativePurgeDocumentService` undercounts deletions + bare `catch {}` — Deep purge reports false numbers | NativePurgeDocumentService.cs:27-37 |
| 8 | Purge | Deep mode **silently ignores 12 of 13 checkboxes** + defaults to ON — user selections don't matter | PurgeCommand.cs:65-86, PurgeViewModel.cs:60 |
| 9 | Batch Rename | `count` incremented inside transaction lambda even when rollback occurs — final "Modified N" is wrong | BatchRenameExecutionService.cs:185 |
| 10 | Batch Rename | REQ-01 root cause: `ReplaceItem` model has no `Name`/`Category` fields, and `ElementName` never assigned | SearchReplaceViewModel.cs:16, SearchReplacePreviewService.cs:110 |

---

## Cross-Cutting Themes

### A. Logging fragmentation (every plugin)
Three+ logging stacks competing per plugin: `Logger.Instance` direct, `IProgressReporter`, VM callbacks. Swallowed exceptions never reach `LogView`. `RevitCommandProgressReporter.LogWarning/LogError` collapse to plain `Log` losing severity. Inconsistent tag taxonomy across `[V6-EVENT]`, `[OK]`, `[SUCCESS]`, etc.

**Fix shape:** Single structured logger interface, severity preserved, scope-tagged, surfaced consistently in `LogView`.

### B. Service over-decomposition (Purge, Compaction, FamilyConversion)
- Purge: 18 files, ~7 are pass-through shells, 2 services dead-code
- Compaction: 2,681 LOC across 6 files for a templatable workflow; legacy callback overloads with no callers
- FamilyConversion: 34 files / 1666 LOC for a 6-step flow; ~10 services are 20-30 LOC wrappers around one Revit call. Zero tests.

**Fix shape:** Collapse pass-throughs, extract a shared pipeline base where the workflow is genuinely templatable, delete dead services.

### C. No preview / no cancellation / blocking UI
Compact Styles has no view at all. Category Changer Cancel button is non-functional during execution. Convert Family deletes-then-loads with no preview of blast radius. `CollectBaseElements` runs synchronously on UI thread freezing Revit on large models.

**Fix shape:** Preview-then-execute pattern; real cancellation; background scope collection.

### D. Indiscriminate auto-dialog dismissal (Purge, Convert Family)
Both register `OnDialogShowing` and call `e.OverrideResult(2)` for ANY dialog — kills dialogs the user might need.

**Fix shape:** Whitelist by dialog ID, not blanket suppress.

### E. Localization gaps (Compact Styles, Category Changer)
Compact Styles uses English param names. Category Changer template search hardcodes `English/Spanish/English-Imperial`.

### F. Duplicated patterns
- Purge: two near-identical failure-handler implementations (DeepPurge vs PurgeParameter)
- Batch Rename: `ParamGroup` swallowed try/catch duplicated at lines 213 + 275

### G. Dead code from prior consolidation passes
- Batch Rename: `IFormulaUpdateService` registered in DI, zero consumers (placeholder for REQ-02 never wired up)
- Convert Family: `FamilyConversionNamingService` injected, never called; `customName`/`isTemporary` parameters inert
- Convert Family: `FamilyEditorService` (340 LOC) lives in FamilyConversion folder but is only used by CategoryChangerCommand

---

## Roadmap Implications

**Current roadmap (v1.1 Optimization):**
- Phase 1 ✓ Research & Foundation
- Phase 2 ✓ FormulaAutoGrouping Bug Fix (code complete, awaiting Revit verify)
- Phase 3 — Grid & Collection Fixes (REQ-01 only, narrow)
- Phase 4 — Advanced Renaming Logic (REQ-02/03/04)
- Phase 5 — Verification & Polish (REQ-05)

**Audit shows:**
- REQ-01 root cause is now known (`ReplaceItem` model + missing `ElementName` assignment) — fix is small
- Phase 4 work (REQ-02/03) is ~half-implemented as *skip* logic; flipping skip→safe-rename + wiring `IFormulaUpdateService` is the natural next step
- The 9 critical silent-failure issues outside Batch Rename are NOT in the current roadmap at all
- Cross-cutting infrastructure (logging, dialogs, progress) appears in 3-5 plugins each — better as a horizontal phase than as per-plugin patches

---

## Suggested Restructuring (3 options below)

### Option A — Finish v1.1, then v1.2 "Plugin Maturity"
Keep v1.1 as-is (fix REQ-01 narrowly + ship REQ-02..05). Open a fresh **v1.2 milestone "Plugin Maturity"** with:
- Phase 1: Cross-cutting (logging, dialog whitelist, progress contract)
- Phase 2: Convert Family — preview + host/curve fidelity + collapse over-decomposition
- Phase 3: Category Changer — instance-swap fix (all location types) + service extraction
- Phase 4: Purge — Deep mode UX + options object refactor + dedup failure handlers
- Phase 5: Compact Styles — scope toggles + localization fix + build view + extract pipeline base

**Pros:** Clean separation; v1.1 ships fast; v1.2 has room to breathe.
**Cons:** Critical silent-data-loss bugs (Category Changer, Convert Family) wait for v1.2.

### Option B — Insert "hotfix" phases into v1.1
Keep current v1.1 phase structure, but insert:
- Phase 2.5: **Silent-data-loss hotfixes** (Category Changer instance swap, Convert Family host/curve, Compact Styles locale)
- Phases 3-5 unchanged
- Then v1.2 = remaining upgrade themes

**Pros:** Critical bugs fixed immediately. Roadmap remains coherent.
**Cons:** Phase 2.5 spans 3 plugins — wide scope for a single phase.

### Option C — Reframe v1.1 as "Plugin Health"
Replace v1.1's Phase 3-5 entirely. New phases:
- Phase 3: Cross-cutting infrastructure (logging, dialogs, progress)
- Phase 4: Critical silent-failure fixes across 4 plugins
- Phase 5: Batch Rename overhaul (rolls REQ-01 + REQ-02/03/04/05 into one pass)
- Phase 6: Per-plugin polish (Purge UX, Compact view, Convert preview, Category UX)

**Pros:** Reflects what the audit actually found. Logging fix lands first so all subsequent fixes benefit.
**Cons:** Bigger milestone, longer to ship. Risks REQ-04/05 getting watered down.

---

## My recommendation

**Option B.** Insert Phase 2.5 to stop the bleeding (silent data loss is the kind of thing that loses trust fast), keep Phases 3-5 narrow as planned, then start v1.2 "Plugin Maturity" for the cross-cutting and polish work. The audit findings are too varied to cram into one milestone; splitting respects scope discipline.

**Within Phase 2.5**, I'd order: Compact Styles locale fix (smallest, highest impact-per-line) → Category Changer instance swap (single file, contained) → Convert Family host/curve fidelity (largest, most invasive).

---

*Synthesis date: 2026-05-08*
*Per-plugin reports: `.planning/research/plugin-{purge-unused,compact-styles,convert-family,category-changer,batch-rename}.md`*
