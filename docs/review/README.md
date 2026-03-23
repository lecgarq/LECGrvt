# LECG Revit 2026 - Review Documentation

## Current Snapshot

- Target: Revit 2026 only.
- Historical roadmap phases P1-P9: completed.
- Active planning source: `.planning/ROADMAP.md`, `.planning/STATE.md`, `.planning/PROJECT.md`.
- Machine-readable project summary: `docs/review/master-context.jsonl`.
- Build status: `dotnet build -c Debug` clean.
- Test status: `dotnet test` passing (`LECG.Tests`).
- CI model:
  - Hosted (`windows-latest`): builds/tests `LECG.Core` + `LECG.Tests`.
  - Self-hosted (`revit2026`): plugin build gate for `LECG.csproj`.

## Document Index

1. `01-architecture.md` - high-level architecture and boundaries.
2. `02-components.md` - component inventory.
3. `03-organization.md` - folder and naming rules.
4. `04-patterns.md` - implementation patterns and examples.
5. `05-revit-api.md` - Revit API usage practices.
6. `06-dependency-injection.md` - DI conventions and registration.
7. `07-onboarding.md` - setup and developer onboarding.
8. `08-quality-report.md` - current quality baseline and gates.
9. `09-refactoring-roadmap.md` - completed roadmap and maintenance direction.
10. `10-release-checklist.md` - release flow and checks.
11. `11-runner-operations.md` - self-hosted runner operations.
12. `12-gemini-workspace.md` - Gemini workspace usage for vibe coding.
13. `13-compacting-styles.md` - compaction feature implementation notes.
14. `14-current-debt-audit.md` - current technical debt and bug-risk audit.
15. `15-revit-api-export.md` - offline Revit API export summary and usage.
16. `master-context.jsonl` - canonical machine-readable architecture, state, and inventory summary.

## Recommended Read Order

1. `08-quality-report.md`
2. `09-refactoring-roadmap.md`
3. `03-organization.md`
4. `04-patterns.md`
5. `11-runner-operations.md`
6. `12-gemini-workspace.md`
7. `master-context.jsonl`
8. `14-current-debt-audit.md`
9. `15-revit-api-export.md`

## Maintenance Rule

- Keep this folder as the source of truth for architecture, quality, and operations.
- Keep `.planning/` as the source of truth for active roadmap and session state.
- When behavior, CI gates, or structure changes, update:
  - `08-quality-report.md` for current quality state.
  - `09-refactoring-roadmap.md` for lifecycle state.
  - `master-context.jsonl` for machine-readable context.
  - `README.md` for index and reading order.
