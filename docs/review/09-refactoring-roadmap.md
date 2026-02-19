# Refactoring Roadmap

## Status

The original roadmap is complete. Phases P1 through P9 are done for the Revit 2026 target.

## Completed Outcomes

### P1: DI Registration

- Service, ViewModel, and View registrations are established in `src/Core/Bootstrapper.cs`.

### P2: Interface Consolidation

- Service interfaces are located in `src/Services/Interfaces/`.
- Interface namespace rule: `LECG.Services.Interfaces`.

### P3: Build Artifact Hygiene

- Root artifact noise moved/controlled.
- Ignore rules cover `bin/`, `obj/`, `build_logs/`, test outputs.

### P4: Unit Test Foundation

- `LECG.Tests` added and integrated into local/CI workflow.
- Initial deterministic tests established for Revit-free logic.

### P5: Large Service Decomposition

- Large service responsibilities split into focused services/interfaces.
- DI seams expanded; orchestration responsibilities reduced in legacy large classes.

### P6: Nullable Warning Remediation

- Current baseline build is clean for CS86xx warnings.

### P7: CI/CD Pipeline

- Hosted CI validates `LECG.Core` + `LECG.Tests` on `windows-latest`.
- Plugin build validation runs via self-hosted `revit2026` runner.

### P8: Static Analysis

- Analyzer gates are active as part of quality enforcement.

### P9: Architecture Documentation

- Review docs and operational docs are in place under `docs/review/`.

## Final CI Decision

- CI must run without Revit installed on hosted runners.
- Therefore hosted CI builds/tests only Revit-free units (`LECG.Core`, `LECG.Tests`).
- Full plugin build remains validated on local environments with Revit API availability or self-hosted infrastructure.

## Current Baseline Checks

Use these commands as the minimum verification gate:

```powershell
dotnet build -c Debug
dotnet test
```

Expected baseline:

- `0 Warning(s), 0 Error(s)` for build.
- Tests pass in `LECG.Tests`.

## Maintenance Backlog (Post-Roadmap)

These are continuous improvements, not blocked roadmap items:

1. Expand deterministic core extraction for higher test ROI.
2. Increase policy/rules coverage in `LECG.Core` tests.
3. Keep plugin orchestration thin and DI-first.
4. Keep operational runbooks current with CI/runner changes.
5. Track release readiness via `docs/review/10-release-checklist.md`.
