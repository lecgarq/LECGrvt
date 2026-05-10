# LECG Revit Addins — Project

## What This Is

A multi-command Revit add-in (`LECG.dll`, Revit 2026 / .NET) bundling utilities for the LECG architectural team: Batch Rename (search/replace across family parameters + standard items with formula and dimension-label safety), FormulaAutoGrouping, Compact Styles (text/line/dimension/object styles), Category Changer, Convert Family, Divide Toposolid, Split Boundaries, ConvertCad, ConvertToposolidToFloor / ConvertFloorToToposolid, Align Edges, Align Elements, Assign Material, Change Level, Offset Elevations, Reset Slabs, Simplify Points, Fix Points, Purge Unused.

WPF UI under `src/Views/`, MVVM ViewModels under `src/ViewModels/`, services under `src/Services/`. xUnit + FluentAssertions + NSubstitute test project at `LECG.Tests/` (175 GREEN as of v1.1).

## Core Value

Reliable, locale-safe, no-silent-failure bulk edits to Revit families and elements — every operation either succeeds visibly, skips with a reason surfaced in the UI, or refuses the batch with a structured log. No partial successes that look like full successes.

## Current Milestone: v2.0 Plugin Maturity

**Goal:** Mature the five core plugins (Purge Unused, Compacting Styles, Convert Family, Category Changer, Batch Rename) on a unified cross-cutting foundation — single structured logger, severity-preserving progress, whitelist dialog dismissal — and close the silent-failure / UX gaps surfaced in the 5-plugin audit (2026-05-08).

**Target features:**
- Cross-cutting infrastructure: unified logger interface, `IProgressReporter` severity preservation, whitelist-by-DialogId dismissal
- Purge Unused maturity (Deep-mode UX gate, options-object refactor, dedup handlers, parameterized filters)
- Compacting Styles maturity (real view + scope toggles + dry-run, `CompactionPipeline` base, LogView warnings)
- Convert Family maturity (blast-radius preview, collapse over-decomposition, structured summary, face-hosted preservation via HostFace capture)
- Category Changer maturity (extracted instance-swap service, real cancellation, typed `ChangeCategoryResult`, full-locationtype transplant)
- Batch Rename maturity (collapse `SearchReplaceService` facade, async `CollectBaseElements`, scope-pills UX, dead-reg cleanup, per-column funnel chrome)
- v1.1 carry-over gap closure (Phase 02 verification artifact, Phase 02.5 manual Revit checks, Phase 04 C1 live observation)

## Requirements

### Validated

- ✓ Fix blank element name/category in grid — v1.1
- ✓ Implement "Safe Rename" for formula-referenced parameters — v1.1
- ✓ Implement "Safe Rename" for dimension-label parameters — v1.1
- ✓ Provide "Reason for Skip" in Batch Rename UI/Logs — v1.1
- ✓ Unit tests for all renaming services — v1.1 (175 GREEN baseline)
- ✓ Consolidate Renaming services — v1.1
- ✓ Fix FormulaAutoGrouping: reliable move to "Other" group — v1.1 (code wired; verification artifact partial)
- ✓ Compact Styles locale-safe parameter lookup — v1.1
- ✓ Category Changer refuse-all on unsupported location types — v1.1 (refuse path only; transplant deferred)
- ✓ Convert Family load-before-delete + exact-symbol match — v1.1 (face-hosted deferred)

### Active (v2.0 Plugin Maturity)


See `.planning/research/SYNTHESIS.md` (5-plugin audit, 2026-05-08) for the source analysis.

- [ ] Cross-cutting infrastructure: unify logging stack, severity-preserving `IProgressReporter`, whitelist-by-DialogId dismissal
- [ ] Purge Unused maturity: Deep-mode UX gate, options-object refactor, dedup reload handlers, parameterize hardcoded filters
- [ ] Compacting Styles maturity: real view with scope toggles + dry-run, `CompactionPipeline` base class, surface warnings to LogView
- [ ] Convert Family maturity: blast-radius preview UI, collapse over-decomposition, structured per-family summary, face-hosted preservation (HostFace capture)
- [ ] Category Changer maturity: extract instance-swap service, real cancellation, typed `ChangeCategoryResult`, LocationCurve / hosted / face-hosted transplant placement
- [ ] Batch Rename maturity: collapse `SearchReplaceService` facade leakage, async `CollectBaseElements`, scope-pills UX, retire dead `IFormulaUpdateService` registration if unused, per-column header funnel chrome

### Out of Scope

- Pre-2026 Revit versions — single-version maintenance (Revit 2026 API only)
- Localized UI — code is locale-safe (Revit `BuiltInParameter` + `LabelUtils.GetLabelFor`), but plugin UI stays English

## Context

Tech stack: C# / .NET 8 / WPF (CommunityToolkit.Mvvm source generators) / Revit 2026 API. Build: `dotnet build LECG.Tests/LECG.Tests.csproj -p:SkipRevitDeploy=true` while Revit is open (bypasses MSB3027 file-lock on the ProgramData add-ins folder). Test framework: xUnit + FluentAssertions + NSubstitute. Skip-gated RED test pattern with anchor-per-fixture is the project Wave-0 convention (Phases 03-00 / 04-00 / 05-00). All renaming services have dedicated unit-test coverage; Revit-API-bound code paths use `InternalsVisibleTo` + `FakeXxx` test doubles where Castle DynamicProxy can't mock the interface without RevitAPI.dll.

Branching: GSD planning uses `codex/review` workflow branch. Tag releases as `v[X.Y]`.

## Key Decisions

| Date | Decision | Outcome |
|------|----------|---------|
| 2026-04-28 | SubTransaction-per-parameter in `MoveParamsToGroup` (failed move logs + continues) | ✓ Good (REQ-07 wired; xUnit 79/79) |
| 2026-04-28 | Remove in-transaction `GetGroupTypeId()` check — false-negative inside FamilyManager open tx | ✓ Good (post-reload verification only) |
| 2026-05-08 | Category Changer refuse-all (not partial-success) on mixed batches | ✓ Good (REQ-09 wired; transplant deferred to v1.2 by design) |
| 2026-05-08 | Convert Family ordinal exact-name match; no first-symbol fallback; throw before delete | ✓ Good (REQ-10 wired; face-hosted deferred to v1.2 — `FamilyInstanceData` lacks `HostFace` capture) |
| 2026-05-08 | `BuiltInParameter.TEXT_ALIGNMENT` for type-level horizontal alignment (not `TEXT_ALIGN_HORZ` which is instance-level) + per-type sentinel for unreadable Text Orientation | ✓ Good (REQ-08 wired) |
| 2026-05-09 | `ElementRowViewModel` uses `[ObservableProperty]` only for `IsChecked`; identity/display fields plain auto-properties | ✓ Good (Phases 03/04 follow it) |
| 2026-05-09 | Skip-gated RED tests with reason strings naming implementing plan (anchor-per-fixture pattern) | ✓ Good (Wave-0 convention reused by Phases 03-00/04-00/05-00) |
| 2026-05-09 | ICollectionView ownership lives on the ViewModel, not the control (per-screen sort/filter semantics) | ✓ Good (Phase 03-05) |
| 2026-05-09 | SelectionControl Path A: embed `ElementGridControl` in `SelectionControl.xaml` (vs 8 per-View edits) | ✓ Good (Phase 03-08) |
| 2026-05-10 | `ExecuteDimensionReassignments` accepts `List<Action>` (not `List<Dimension>`) — decouples helper from Revit API | ✓ Good (Phase 04-04) |
| 2026-05-10 | Manual Revit verification trust-based for Phases 04/05 — helpers proven via GREEN unit tests; live observation deferred where xUnit fixture unavailable | ⚠️ Revisit if production issues surface (C1 null-clear, Polish #1/#2/#3 specifically) |
| 2026-05-10 | `BuildProgressSequence` Option A — production uses direct arithmetic; helper exists as unit-test contract verifier (no production call site) | ⚠️ Revisit if helper drifts from production arithmetic |
| 2026-05-10 | Face-hosted Convert Family preservation deferred to v1.2 (FamilyInstanceData needs HostFace capture; Branch 3 free-standing fallback is known regression for face-hosted) | — Pending (v1.2 Convert Family Maturity) |

## Constraints

- **Revit 2026 API only.** Some 2026 APIs lack stable surrogates: `Family.IsSystemFamily` doesn't exist — `Family.IsInPlace` used as proxy for standard-item skip detection.
- **xUnit can't load RevitAPI.dll** (native-code deps require Revit host). Revit-bound code paths use `InternalsVisibleTo` + concrete `FakeXxx` test doubles for direct unit testing; live behavior is the manual Revit verification bar.
- **Locale-safety is non-optional.** All parameter lookups must prefer `BuiltInParameter` over `LookupParameter(string)`; category labels via `LabelUtils.GetLabelFor((BuiltInCategory)cat.Id.Value)` (with `(long)` cast — not `cat.Id?.Value` short-circuit).
- **File-lock during Revit-open builds.** Canonical command is `dotnet build LECG.Tests/LECG.Tests.csproj -p:SkipRevitDeploy=true`.
- **No partial-success that looks like success.** Either commit fully, skip with surfaced reason, or refuse the batch with structured log — the core value gate for every command added or modified.

---

*Last updated: 2026-05-10 — v2.0 Plugin Maturity milestone started*
