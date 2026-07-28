---
state_version: 1.0
milestone: warnings-review
milestone_name: Warnings Review
status: executing
stopped_at: Phase 2 planned and blocked on the user — needs Revit closed for a deploying build, then the eyes-on checklist
last_updated: "2026-07-27"
last_activity: 2026-07-27 — Phase 2 planned; live MCP probe fixed the expected numbers (10 warnings / 1 group / 11 distinct ids) and surfaced an unresolved transaction question on isolate
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
- **Blocked on the user.** The deployed DLL is dated 2026-07-25 and predates the whole Warnings feature, so the running Revit session does not contain the command. Deploying needs Revit closed (`dotnet build -c Release`), and the open model has unsaved changes — closing it is the user's call, not the agent's.
- Live MCP probe (2026-07-27, `LECG_RVT_ARQUITECTURA` / `3D View 1`) fixed the expected numbers: 10 warnings, 1 group (`"Highlighted toposolids overlap."`, severity `Warning`), 20 failing-element refs → **11 distinct** Toposolid ids. Select must therefore select **11**, which is the check that proves distinct-id grouping (R3).
- **Unresolved and the main risk:** whether `View.IsolateElementsTemporary` is legal with no transaction open (`src/Services/Health/WarningsService.cs:75`). The MCP probe could not settle it — the harness holds its own transaction (`IsModifiable=True`), so its success proves nothing about the shipped path. If isolate fails live, it needs `ITransactionService` and the R6 "no transaction anywhere" claim must be restated as "no persistent model writes".
- Still open from Phase 1: does `ShowElements` refresh well behind a modal WPF dialog (fallback: close dialog before showing).
- Coverage gap: this model has one warning group, so count-descending ordering cannot be observed live; it stays unit-test-only.
- Sanctioned validation: `dotnet build -p:SkipRevitDeploy=true` · `dotnet test -c Debug -p:SkipRevitDeploy=true` (222 green / 5 skipped, re-verified 2026-07-27).
