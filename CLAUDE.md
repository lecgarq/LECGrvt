# LECG — Revit Add-in (project knowledge)

Revit add-in. C# / WPF / .NET. Tab **"LECG"** with ribbon panels of tools
(toposolids, project health, standards, alignment, visualization/render,
cloud batch). This file is the entry point; deep docs live under `docs/`.

## Build & environment (verified 2026-09)

- **Revit 2026.5+ runs on .NET 10.** `RevitAPI.dll` is v26.5, compiled
  against `System.Runtime` v10. Targeting `net8.0` fails with **CS1705**.
  Both projects target **net10** (`LECG.csproj` → `net10.0-windows`,
  `LECG.Core.csproj` → `net10.0`). Do NOT revert to net8 for 2026.
  (`AGENTS.md` / older `docs/` still say ".NET 8" — stale.)
- SDK pinned in `global.json` (10.0.201). Only Revit **2026** is a full
  install on this machine (2023/2025 lack RevitAPI.dll; "Revit Assistant
  2027" is not full Revit).
- Build + deploy (the `DeployToRevit` MSBuild target auto-copies to the
  Revit add-ins folder after Build):
  ```bash
  dotnet build LECG.csproj -p:RevitVersion=2026 -c Debug
  ```
- Deploy target output: `%APPDATA%\Autodesk\Revit\Addins\2026\` — writes
  `LECG.addin` + `LECG\*.dll|pdb|deps.json`. Restart Revit to load changes.
- Manifest `LECG.addin`: `FullClassName=LECG.App`, AddInId
  `9B0AB379-D085-4F8A-AA18-A89A4C8180FF`.
- CI sets `SkipRevitDeploy=true` (via `GITHUB_ACTIONS`/`CI`).

## Architecture — layers

`App` (IExternalApplication) → `Bootstrapper` (DI) → `RibbonService` (UI) →
`Command` (thin) → `ViewModel` (WPF) → `Service` (all Revit API + logic).

**Rule: all Revit API calls live in the Service layer.** Commands and
ViewModels never touch the API directly. See
[docs/review/05-revit-api.md](docs/review/05-revit-api.md) and
[docs/standards/SERVICE_ARCHITECTURE.md](docs/standards/SERVICE_ARCHITECTURE.md).

### Startup — `src/App.cs`
`OnStartup` registers `pack://` URI scheme (needed on .NET 8+/Revit 2026),
loads global WPF theme, registers global exception handlers, calls
`Bootstrapper.Initialize()`, then `IRibbonService.InitializeRibbon(app)`.

### DI — `src/Core/Bootstrapper.cs` + `ServiceLocator.cs`
- `Microsoft.Extensions.DependencyInjection`. Services + `IRibbonService`
  are **singletons**; ViewModels and Views are **transient**.
- Revit instantiates commands by reflection (parameterless ctor), so
  commands can't get constructor injection. They pull deps from the static
  `ServiceLocator.GetRequiredService<T>()`. `ServiceLocator.CreateWith<T>(...)`
  = `ActivatorUtilities.CreateInstance` for mixing DI + runtime args
  (e.g. passing `uiDoc` to a View).
- Register new services in `ConfigureServices` (interface in
  `src/Services/Interfaces`, impl in `src/Services`). ViewModels/Views in
  `ConfigureViewModels` / `ConfigureViews`.

## Commands — `src/Core/RevitCommand.cs`
- Base class for all commands; inherit it, don't implement `IExternalCommand`
  directly. Attribute every command `[Transaction(TransactionMode.Manual)]`.
- Override `void Execute(UIDocument uiDoc, Document doc)`. Base handles:
  active-doc guard, structured (Serilog) logging scope, try/catch → error
  dialog + log window, progress plumbing (`UpdateProgress`, `ShowLogWindow`,
  `RunOnUI`).
- Commands are **thin**: resolve service/VM, show dialog, delegate work to a
  service. Example: [src/Commands/RenderAppearanceMatchCommand.cs](src/Commands/RenderAppearanceMatchCommand.cs).
- See [docs/standards/REVIT_COMMANDS.md](docs/standards/REVIT_COMMANDS.md),
  [docs/standards/MVVM_PATTERN.md](docs/standards/MVVM_PATTERN.md).

## Ribbon UI — `src/Core/Ribbon/`
- `RibbonService.InitializeRibbon` creates the `LECG` tab then one method per
  panel. **To add/remove buttons, edit `RibbonService.cs`.**
- `RibbonFactory.CreateButton(panel, RibbonButtonConfig, assemblyPath,
  availabilityClassName)` — resilient PushButton builder;
  `CreatePulldownButton` / `AddItemToPulldown` for dropdowns.
- Button text/tooltips/names centralized in `src/Configuration/UIConstants.cs`;
  tab/panel names in `src/Configuration/AppConstants.cs`; icons in `AppImages`.
- **Availability** (enable/disable per context) via
  `IExternalCommandAvailability` classes passed by full name string:
  `LECG.Core.ProjectDocumentAvailability` (project docs only),
  `LECG.Core.FamilyDocumentAvailability` (family editor only), `""` = always.
- Current simplified state: only the **Visualization** panel is enabled
  (Render Match + PBR Material). Other panel calls are commented out in
  `InitializeRibbon` — uncomment to restore the full ribbon.
- Substance Batch creates Advanced Opaque materials from `C:\LECG\SubstanceBakes` at 2500 mm. PBR Material also recognizes that root and opens the batch review. Spec: `docs/superpowers/specs/2026-09-04-substance-batch-pbr-design.md`; validation: `docs/review/16-substance-batch-validation.md`.

## Transactions — `src/Services/TransactionService.cs`
Never `new Transaction(...)` in feature code. Use `ITransactionService`:
`Run` (void/`T`), `RunConditional` (commit only if action returns true),
`RunRollbackOnly`, `RunWithOptions`, `RunWithWarningHandler` (custom
`IFailuresPreprocessor`).

## Element access
Standard Revit `FilteredElementCollector` inside services, e.g.
`new FilteredElementCollector(doc).OfClass(typeof(Material))`. Resolve by id
with `doc.GetElement(id)` and pattern-match the type.

## APS / Autodesk Platform Services (cloud batch) — `src/Batch/`
- Batch pipeline: browse APS cloud models, open/sync/publish. Entry command
  `LECG.Batch.Commands.BatchProcessCommand` (Cloud panel).
- Services in `src/Batch/Services`, interfaces alongside. Auth: PKCE OAuth —
  `ApsAuthService` + `ApsTokenProvider` + `ApsSessionStore` +
  `ApsAuthSettingsProvider` (settings from local config + env overrides;
  loopback redirect via `LoopbackCallbackListener`, `PkceHelper`).
- Data Management API via `ApsDataManagementService`; publish via
  `ApsPublishService`. One shared `HttpClient` (DI singleton).
- Job model: `BatchManifest` → `BatchJob`s, routed by `BatchRoutineSelector`
  over `IBatchJobRoutine` impls (`DefaultBatchJobRoutine`,
  `PublishToCloudJobRoutine`), executed by `RevitBatchJobHandler`.
- Never put secrets in source. See project memory for pipeline status.

## Deeper references
- `docs/review/` — architecture (01), components (02), organization (03),
  patterns (04), **revit-api (05)**, **dependency-injection (06)**,
  onboarding (07), quality/debt (08/14), roadmap (09), release (10).
- `docs/standards/` — REVIT_COMMANDS, MVVM_PATTERN, SERVICE_ARCHITECTURE,
  UI_STANDARDS, INTERACTION_STANDARDS.

## Conventions / gotchas
- `Nullable` + `ImplicitUsings` enabled; x64 only.
- Analyzers on (`AnalysisLevel=latest`); CA1062 null-check warnings are
  common on public service methods — not errors.
- No IFC and effectively no Python in this codebase (despite general Revit
  material that may suggest otherwise) — it's C# Revit API + WPF + APS.
