---
state_version: 1.0
milestone: warnings-review
milestone_name: Warnings Review
status: planning
stopped_at: Milestone opened — Phase 1 not yet planned
last_updated: "2026-07-27"
last_activity: 2026-07-27 — Milestone opened (2 phases, 8/8 requirements mapped)
progress:
  total_phases: 2
  completed_phases: 0
---

# Project State

## Current Position

Milestone: Warnings Review — a read-only Warnings command in the Health ribbon panel (grouped `Document.GetWarnings()` with select/show/isolate actions).
Phase: 1 of 2 (Warnings command end to end)
Status: planning — Phase 1 has an open modal-vs-modeless decision; run `/lecg-discuss 1` before `/lecg-phase 1`.

## Context

- v1.0-hardening closed as-is 2026-07-27; audit at `.planning/archive/v1.0-hardening/AUDIT.md`, accepted debt in `.planning/codebase/CONCERNS.md`.
- Relevant debt for this milestone: `ExternalEventCommand` has no reentrancy guard — weighs against a modeless WarningsView.
- Sanctioned validation: `dotnet build -p:SkipRevitDeploy=true` · `dotnet test -c Debug -p:SkipRevitDeploy=true` (211 green at milestone open).
