---
state_version: 1.0
milestone: warnings-review
milestone_name: Warnings Review
status: executing
stopped_at: Phase 2 deployed and partially validated — ribbon, read path, grouping and Select confirmed live; dialog rendering, Show, Isolate-by-click and Undo still need eyes
last_updated: "2026-07-28"
last_activity: 2026-07-28 — deployed the fixed build; journal confirms the Warnings button, and the shipped service returned 10 warnings / 1 group / 11 selected against the real model
progress:
  total_phases: 2
  completed_phases: 1
---

# Project State

## Current Position

Milestone: Warnings Review — a read-only Warnings command in the Health ribbon panel (grouped `Document.GetWarnings()` with select/show/isolate actions).
Phase: 2 of 2 (Live Revit validation)
Status: Phase 1 complete — Warnings command, service, VM, view, DI, and ribbon button shipped (R1–R7). Phase 2 needs the user at the keyboard for the smoke test (R8).

## Context

- Phase 1 plan and evidence: `.planning/phases/01-warnings-command/PLAN.md`. Modal `LecgDialog`-style flow (ShowDialog inside Execute) — no ExternalEvent, no `ITransactionService` anywhere in the new types.
- Phase 2 plan: `.planning/phases/02-live-validation/PLAN.md`. Checklist also lives in `docs/ai/revit-smoke-test.md` under *Targeted: Warnings command*.
- **Deployed 2026-07-28 00:20** (`dotnet build -c Release`, Revit closed). Verified the deployed binary is the fixed build, not a stale copy: `Isolate` is 76 IL bytes (bare was ~30) and calling it raises Revit's nested-transaction error, which is only reachable if `Transaction.Start()` runs. Assembly version is still 0.1.1.0, so never use version alone to judge freshness.
- **Live evidence so far** (journal + shipped code executed via MCP): add-in starts with `NoError`; `btnWarnings` registered into the Project Health panel; `ServiceLocator` resolves `WarningsService`; `ReadWarnings` → 10, grouping → 1 group, `Select` → 11 elements selected. Full detail in `docs/ai/revit-smoke-test.md` Run History 2026-07-28.
- **Still needs eyes:** dialog rendering and WPF bindings, availability greying with no document open, count parity vs Manage → Warnings, `Show` behind the modal, `Isolate` by click plus Reset Temporary Hide/Isolate, and the Undo-list check. MCP cannot cover Isolate — the harness holds a transaction and Revit forbids nesting.
- Live MCP probe (2026-07-27, `LECG_RVT_ARQUITECTURA` / `3D View 1`) fixed the expected numbers: 10 warnings, 1 group (`"Highlighted toposolids overlap."`, severity `Warning`), 20 failing-element refs → **11 distinct** Toposolid ids. Select must therefore select **11**, which is the check that proves distinct-id grouping (R3).
- **RESOLVED — and it was a real bug.** `View.IsolateElementsTemporary` requires an open transaction; the shipped Isolate button would have failed every time. Proved live by running against a second open document with no ambient transaction (`IsModifiable=False`): bare call threw `ModificationOutsideTransactionException`, wrapped call succeeded. Fixed in `src/Services/Health/WarningsService.cs:77-91` with a raw `Transaction` — **not** `ITransactionService`, which cannot be injected into anything tests construct (reference assemblies only; it broke 7 tests with `FileNotFoundException: RevitAPI`).
- **R6 restated:** "no persistent model writes" rather than "no transaction anywhere". Isolate adds one `Isolate Warning Elements` Undo entry; Select and Show add none.
- Still open from Phase 1: does `ShowElements` refresh well behind a modal WPF dialog (fallback: close dialog before showing).
- Coverage gap: this model has one warning group, so count-descending ordering cannot be observed live; it stays unit-test-only.
- Sanctioned validation: `dotnet build -p:SkipRevitDeploy=true` · `dotnet test -c Debug -p:SkipRevitDeploy=true` (222 green / 5 skipped, re-verified 2026-07-27).
