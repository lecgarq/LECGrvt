# Repo Context

> Maintained by GSD runs. Stable repo knowledge — corrected whenever the code proves it wrong. Current code beats this file.
> Seeded 2026-07-04 by a GSD protocol smoke test (see gsd-log.md).

## Product Summary

LECG is a Revit 2026 add-in: a suite of productivity commands (align edges/elements, assign material, category changer, change level, convert CAD, filter copy, material/PBR tooling, fix points, and more — see `src/Views/` and `src/Commands/`) exposed through a custom ribbon, with WPF/MVVM UI.

## Architecture Summary

Single main assembly (`LECG.csproj`, net8.0-windows, x64) plus `LECG.Core` class library and `LECG.Tests` test project. Layered `src/`: `Commands` → thin `IExternalCommand` entries; `Views`/`ViewModels` → WPF MVVM UI; `Services` → business + Revit API access (interfaces in `Services/Interfaces`); `Core` → bootstrapping, DI, command bases, ribbon; plus `Models`, `Utilities`, `Validation` (FluentValidation), `Configuration`. DI is Microsoft.Extensions.DependencyInjection configured in `src/Core/Bootstrapper.cs` at Revit startup and resolved via `Core.ServiceLocator`.

## Revit Add-in

- **Target Revit version(s):** Revit 2026 only — `Nice3point.Revit.Api.RevitAPI/RevitAPIUI 2026.4.10` NuGet refs (`LECG.csproj:25-26`), `net8.0-windows`, deploy path hardcoded to `...\Addins\2026\LECG` (`LECG.csproj:63`). No multi-version conditionals found.
- **Commands:** derive from `LECG.Core.RevitCommand` (`src/Core/RevitCommand.cs:14`) which implements `IExternalCommand`, provides `Doc`/`UIDoc`/`CommandData`, structured logging scope, and error dialog handling; command availability via `src/Core/ProjectDocumentAvailability.cs` / `FamilyDocumentAvailability.cs`.
- **Ribbon setup:** `src/Core/Ribbon/RibbonFactory.cs` + `RibbonService.cs` (`IRibbonService.InitializeRibbon` called from `App.OnStartup`, `src/App.cs:37-38`).
- **WPF windows:** 39 `.xaml` files; views in `src/Views/` (base helpers in `Views/Base`, e.g. `LecgDialog`); views registered in DI (`Bootstrapper.ConfigureViews`).
- **Deployment:** `DeployToRevit` MSBuild target copies output DLL/PDB/deps.json to `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG` after EVERY build unless `-p:SkipRevitDeploy=true` (`LECG.csproj:61-72`). CI sets `SkipRevitDeploy=true` automatically when `GITHUB_ACTIONS`/`CI` is set (`LECG.csproj:13`).
- **`.addin` manifest:** NOT build-generated and NOT deployed by `DeployToRevit` (which copies only dll/pdb/deps.json). The repo tracks only a template: `docs/deployment/LECG.addin.template`. The live manifest is manually installed machine-wide at `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG.addin` (verified on this machine 2026-07-04: `Type="Application"`, `FullClassName=LECG.App`, `Assembly=C:\ProgramData\Autodesk\Revit\Addins\2026\LECG\LECG.dll`; Revit journal from the same day confirms Revit loads LECG from exactly this path — no per-user `%AppData%` manifest exists). `docs/deployment/README.md` was updated 2026-07-04 to match this reality. Residual quirk: the live manifest uses `<ClientId>` while the tracked template uses `<AddInId>`; the live file demonstrably works — do not unify without an interactive Revit test. Do not create/modify `.addin` files unless the user explicitly asks.
- **Manual Revit smoke test steps:** see `docs/ai/revit-smoke-test.md` (checklist covering startup, ribbon load, representative command, selection edge cases, transaction behavior, modeless/ExternalEvent flows).

## Main Folders

`src/` (add-in code: App, Commands, Core, Views, ViewModels, Services, Models, Utilities, Validation, Configuration, Behaviors, Controls, Resources) · `LECG.Core/` (shared library) · `LECG.Tests/` (xUnit-style test project, builds with Revit API refs) · `scripts/` (build/search/validation helpers) · `docs/` (deployment, standards, runbook) · `adapters/`, `tools/`, `design/` (supporting material).

## Main Entry Points

- `src/App.cs:13` — `App : IExternalApplication`. `OnStartup`: pack-URI scheme registration workaround (`:22-27`), global WPF dictionaries, global exception handlers (once), `Core.Bootstrapper.Initialize()`, ribbon init. `OnShutdown`: `Bootstrapper.Shutdown()`.
- `src/Core/RevitCommand.cs:14` — abstract command base; subclasses implement `Execute(UIDocument, Document)`.
- `src/Core/ExternalEventCommand.cs:6` — `ExternalEventCommand<THandler> : RevitCommand` base for modeless flows; lazily creates a static `IExternalEventHandler` + `ExternalEvent`, raised via `RaiseExternalEvent()`.

## WPF / MVVM Structure

CommunityToolkit.Mvvm. `src/ViewModels/BaseViewModel.cs:10` — `BaseViewModel : ObservableObject` with lazy `RelayCommand` `ApplyCommand`/`CancelCommand` (`:23-27`). ViewModels are `partial` classes (source generators; CS0436 suppressed for this — `Directory.Build.props`). ViewModels and Views registered in DI (`Bootstrapper.ConfigureViewModels`/`ConfigureViews`).

## Revit API Service Boundaries

Services own Revit API work; `src/Services/` with `Services/Interfaces/` and `Services/Infrastructure/` (e.g. `TransactionService`, `LinkedModelExportService`). Document writes go through `ITransactionService` (used ~71× across `src/`; only 7 raw `new Transaction(` sites, concentrated in the infrastructure services themselves — `src/Services/Infrastructure/TransactionService.cs:31,70`).

## Build / Validation Commands

- Build (agent-safe, DEFAULT for validation): `dotnet build -p:SkipRevitDeploy=true` — verified 2026-07-04 (twice, incl. this stabilization run): succeeded, 0 warnings/0 errors, all 3 projects, no deploy step ran. Future GSD runs must use this as the default validation build unless repo evidence later proves otherwise.
- Build (deploying — AVOID unless deployment is explicitly requested): plain `dotnet build` also copies DLLs into the live Revit 2026 addins folder — see Concerns.
- `AGENTS.md:7` documents `dotnet build` as the standard per-step validation.
- Tests (sanctioned invocation, validated 2026-07-27): `dotnet test -c Debug -p:SkipRevitDeploy=true` — 211 passed / 0 failed / 5 skipped (the skips are Revit-runtime tests that skip by design outside Revit). The flag is mandatory whenever Revit is open: `dotnet test` builds LECG.csproj, which triggers `DeployToRevit`, and the copy fails with MSB3027 because Revit locks the deployed DLLs.
- Live Revit access: `mcp-server-for-revit` MCP is registered for this project (installed and verified end-to-end 2026-07-27). Plugin manifest: `%AppData%\Autodesk\Revit\Addins\2026\mcp-servers-for-revit.addin`; server: `node ~\.mcp\revit\node_modules\mcp-server-for-revit\build\index.js` (installed with an npm override `better-sqlite3: ^12.4.1` — v11 has no Node 24 prebuild and source-builds fail here; keep the override if reinstalling). Requires Revit open with the plugin's MCP service toggled on (off by default after every Revit restart). Lets validation query/execute in the live session; ribbon/dialog/undo checks remain visual.
  - Gotchas paid for: (1) `commandRegistry.json` ships empty — populate it from `command.json` (assemblyPath `RevitMCPCommandSet/{VERSION}/RevitMCPCommandSet.dll`, `enabled: true`) or no commands load; the plugin reads it only at Revit startup, not on service toggle. (2) The plugin's "Failed to create command instance" [Info] log lines are its success path — a copy-paste bug; only [Error] lines are real failures. (3) `RevitMCPCommandSet.dll` in that folder is a **locally patched build**: stock `send_code_to_revit` references every loaded assembly and corrupt license DLLs from the `archintelligence` add-in (Program Files x86) fail every compile with CS0009; the patch retries the compile dropping CS0009-flagged references. A plugin update overwrites the patch — reapply by patching `commandset/Commands/ExecuteDynamicCode/ExecuteCodeEventHandler.cs` (retry loop around `compilation.Emit`) and building `Release R26`. (4) `send_code_to_revit` code must be a bare method body — no `using` directives or class declaration (the tool wraps it in its own template with a `document` variable in scope) — and the compiler is old-C#-level: no value tuples or string interpolation; fully-qualify namespaces and use `string.Format` (verified 2026-07-27).

## As-Is Best Practices

### Architecture
- [Observed] (scope: project) DI via Microsoft.Extensions.DependencyInjection: `Bootstrapper.Initialize()` builds the container at `OnStartup`; consumers resolve via `Core.ServiceLocator`.
  - Evidence: `src/Core/Bootstrapper.cs:20-35`, `src/App.cs:34-38` | Why: single composition root; services/VMs/views all registered centrally | Preserve by: register new services/VMs/views in the matching `Configure*` method; resolve via ServiceLocator or constructor injection, don't `new` services ad hoc.

### Revit API
- [Observed] (scope: project) Commands derive from `RevitCommand` (never raw `IExternalCommand`): base supplies Doc/UIDoc, logging scope, exception → dialog handling.
  - Evidence: `src/Core/RevitCommand.cs:14-50`; command classes in `src/Commands/` | Why: uniform error handling/logging; valid API context guaranteed by `Execute` | Preserve by: new commands subclass `RevitCommand` (or `ExternalEventCommand<T>` for modeless).

### WPF / MVVM
- [Observed] (scope: project) CommunityToolkit.Mvvm with `BaseViewModel : ObservableObject`; commands are lazy `RelayCommand` properties (Apply/Cancel pattern in the base).
  - Evidence: `src/ViewModels/BaseViewModel.cs:10,23-27`; `ObservableObject` partial VMs across `src/ViewModels/` | Why: consistent binding + source-generated notification | Preserve by: new VMs extend `BaseViewModel` (or `ObservableObject` for models), use RelayCommand, keep `partial`.

### Transactions
- [Observed] (scope: project) Document writes go through `ITransactionService` (named transactions, `using` disposal), not scattered raw `Transaction` objects.
  - Evidence: `src/Services/Infrastructure/TransactionService.cs:31,70`; ~71 `ITransactionService` usages vs 7 raw `new Transaction(` in all of `src/` | Why: centralizes commit/rollback and naming | Preserve by: call `ITransactionService` for writes; raw `Transaction` only inside infrastructure services with justification.

### External Events / Modeless UI
- [Observed] (scope: command) Modeless flows use `ExternalEventCommand<THandler>` with a static, lazily-created handler + `ExternalEvent`, raised via `RaiseExternalEvent()`.
  - Evidence: `src/Core/ExternalEventCommand.cs:6-28`; used by `src/Commands/CategoryChangerCommand.cs`, `src/Commands/ConvertCadCommand.cs` | Why: keeps document changes in valid API context from modeless UI | Preserve by: new modeless commands subclass it; note the handler/event are static per closed generic — one shared instance per command type.

### Units / Geometry / Math
- [Inferred] (scope: unknown) ForgeTypeId-based unit API in use (11 `ForgeTypeId` vs 5 `UnitUtils` hits; Revit 2026 has no legacy `DisplayUnitType`). Too few sites to call a convention.
  - Evidence: grep counts across `src/` | Why: matters for any conversion code | Preserve by: prefer ForgeTypeId APIs; check for a shared helper before hand-rolling.

### Parameters
- [Inferred] (scope: unknown) `StorageType` is checked at ~33 sites before parameter access; no single shared parameter helper identified yet.
  - Evidence: grep count across `src/` | Why: wrong-StorageType writes throw | Preserve by: check StorageType/read-only before set; look for an existing helper near the code being changed.

### Element Filtering / Performance
- No stable repo evidence found yet. (104 `FilteredElementCollector` uses exist; no shared collector/caching convention identified this run.)

### UI / Ribbon / Plugin Entry Points
- [Observed] (scope: service) Ribbon built by `RibbonFactory`/`RibbonService` behind `IRibbonService`, initialized once from `App.OnStartup`.
  - Evidence: `src/Core/Ribbon/RibbonFactory.cs`, `src/Core/Ribbon/RibbonService.cs`, `src/App.cs:37-38` | Why: single ribbon registration path | Preserve by: add new buttons through the ribbon service/factory, not inline in App.

### Runtime Validation
- [Observed] (scope: startup/ribbon only — NOT commands) LECG loads in actual Revit 2026: external application `LECG.App` starts successfully and the full ribbon (all panels/pushbuttons) registers, loading from the machine-wide ProgramData install.
  - Evidence: Revit journal `journal.0879.txt` (user session 2026-07-04 22:12–22:15, DLL deployed 2026-07-02): `API_SUCCESS { Starting External Application: LECG, Class: LECG.App, ... Assembly Version: 0.1.1.0 }` + per-button `API_SUCCESS` entries; clean session shutdown. Details: `docs/ai/revit-smoke-test.md` Run History; `docs/ai/gsd-log.md` 2026-07-04 runtime pass.
  - Why it matters: confirms the add-in loads and the ribbon builds in actual Revit, not only at compile time — and proves the ProgramData manifest is the one Revit reads.
  - Preserve by: future runtime claims must name the Revit version, command tested, document context, and smoke-test result. This entry covers startup/ribbon ONLY — no LECG command has ever been runtime-validated (journal shows none executed); do not generalize.

### Validation / Deployment
- [Observed] (scope: repo-wide) Clean-build discipline: `Directory.Build.props` deliberately suppresses CS0436 (MVVM source-gen duplicates) and downgrades MSB3277 (Revit DLL version overlaps) with explanatory comments; build verified at 0 warnings.
  - Evidence: `Directory.Build.props:11-19`; build run 2026-07-04 | Why: warning noise hides real problems; these two are documented as expected | Preserve by: don't remove these suppressions; don't add new ones without the same comment discipline.

### Known Concerns and Pitfalls
- [Concern] (scope: repo-wide) Plain `dotnet build` DEPLOYS: the `DeployToRevit` target copies DLLs into `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG` after every build unless `-p:SkipRevitDeploy=true`. A live `LECG.addin` manifest in ProgramData points Revit at that exact folder, so a stray build overwrites the running add-in.
  - Evidence: `LECG.csproj:61-72`; live manifest inspected 2026-07-04 | Why: an agent "just validating" can overwrite the live add-in mid-session (fails noisily if Revit has the DLL locked, silently swaps it if not) | Guardrail: agents build with `dotnet build -p:SkipRevitDeploy=true` unless deployment is explicitly the goal; never run deployment scripts unprompted.
- [Concern] (scope: project) `App.OnStartup` registers the `pack://` URI scheme manually before any WPF work — a deliberate workaround for .NET 8 / Revit 2026 ("The URI prefix is not recognized").
  - Evidence: `src/App.cs:22-27` (commented) | Why: removing it breaks resource loading at startup | Guardrail: do not remove/reorder; new early-startup code goes after it.
- [Concern] (scope: command) `ExternalEventCommand<THandler>` holds its handler and `ExternalEvent` in static fields — state is shared across invocations of the same command type.
  - Evidence: `src/Core/ExternalEventCommand.cs:9-16` | Why: stale handler state can leak between runs | Guardrail: keep handlers stateless or explicitly reset per invocation.
- [Concern] (scope: repo-wide, runtime) Revit logs assembly version conflicts at LECG load time: LECG's `Clipper2Lib 2.0.0.0` conflicts with a preloaded `1.1.1.0`, and `Microsoft.Extensions.DependencyInjection.Abstractions 8.0.0.0` conflicts with a preloaded `9.0.0.0` (other installed add-ins — e.g. Enscape/ModPlus/Forma — load their copies first). The add-in still loads, but in Revit's shared AppDomain the first-loaded assembly version can win.
  - Evidence: journal `journal.0879.txt` 2026-07-04 `API_ERROR { Assembly version conflict in some references in LECG.dll assembly ... }` | Why: Clipper2-dependent code (e.g. Align Edges) may silently run against Clipper2Lib 1.1.1.0 instead of 2.0.0.0 depending on add-in load order; there is no AssemblyResolve handler in `App.cs` (a README claim to the contrary was corrected 2026-07-27) | Guardrail: when debugging geometry/DI oddities that only reproduce in Revit (not tests), check the journal for these conflict lines first; verify which Clipper2 version is actually loaded before blaming LECG code.

### Open Questions
- [Open] Manifest GUID element: the live manifest uses `<ClientId>`, the tracked template `docs/deployment/LECG.addin.template` uses `<AddInId>`. The live file demonstrably loads (journal-verified), so this is cosmetic until someone installs from the template; verify with an interactive Revit test before unifying. (README/actual path divergence RESOLVED 2026-07-04: journal proved the machine-wide ProgramData install is what Revit loads; README updated to match.)
- [Open] Is there an intended shared helper/convention for `FilteredElementCollector` usage (quick-filter ordering, caching), or is per-site usage deliberate?
- [Open] Is there a canonical unit-conversion helper, or is direct ForgeTypeId/UnitUtils usage the intent? (Re-checked 2026-07-04: no `*UnitHelper/Service/Utils` class exists in `src/`.)
- [Open] Linked-model transform conventions: `LinkedModelExportService` exists but no `GetTotalTransform`/`GetTransform(` call was found in `src/` (re-checked 2026-07-04) — how are link coordinates handled?
- [Open] Interactive Revit validation of any LECG *command* (checklist steps 3–8: command launch, selection edge cases, transaction commit/rollback, modeless double-run) has never happened — journal evidence covers startup/ribbon/shutdown only. The checklist in `docs/ai/revit-smoke-test.md` still needs its first human-driven execution.
