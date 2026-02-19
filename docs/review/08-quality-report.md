# Code Quality Report

## Scope

This document tracks the current quality baseline for the Revit 2026 plugin and its CI/test split:

- Plugin project: `LECG` (`net8.0-windows`, Revit 2026).
- Revit-free core: `LECG.Core` (`net8.0`).
- Unit tests: `LECG.Tests` (`net8.0`), focused on Revit-free logic.

## Current Verification Baseline

Verification commands:

```powershell
dotnet build -c Debug
dotnet test
```

Current status:

- Build: passing.
- Warnings: `0`.
- Errors: `0`.
- Tests: passing (`LECG.Tests`).
- Nullable warnings (CS86xx): none in current build.

## Quality Gates

Hosted CI (`windows-latest`):

- Workflow: `ci`.
- Scope: builds `LECG.Core` and runs `LECG.Tests`.
- Coverage policy: enforced threshold (`COVERAGE_MIN=90`).

Self-hosted CI (`revit2026` runner):

- Workflow: `plugin-build`.
- Scope: validates plugin build path requiring Revit API availability.

## Architecture and Structure Status

Completed and stable:

- DI registration coverage for production services/viewmodels/views.
- Service interface consolidation:
  - Path: `src/Services/Interfaces/`
  - Namespace: `LECG.Services.Interfaces`
- Build artifact hygiene (`bin/`, `obj/`, `build_logs/` ignored).
- Large-service decomposition completed under P5 slices.
- Static analysis and CI enforcement enabled.

## Residual Risks

- Plugin runtime behavior still depends on Revit host context and self-hosted validation.
- Test coverage is intentionally concentrated on Revit-free logic, not direct Revit API execution.

## Ongoing Maintenance Checklist

For each change set:

1. Run `dotnet build -c Debug`.
2. Run `dotnet test`.
3. Keep new interfaces under `src/Services/Interfaces/` with namespace `LECG.Services.Interfaces`.
4. Keep Revit-dependent logic isolated from `LECG.Core`.
5. Update this document if quality gates, warnings, or CI topology changes.
