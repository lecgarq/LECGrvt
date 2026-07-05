# Architecture

**Analysis Date:** 2026-07-04

## Pattern Overview

**Overall:** Layered MVVM + Service-Oriented, Command-driven Revit add-in architecture with explicit separation between UI (commands, views, view models), business logic (domain services), and Revit API access (transaction/collection services).

**Key Characteristics:**
- Microsoft.Extensions.DependencyInjection composition root (`Bootstrapper`), resolved statically via `ServiceLocator` for Revit-instantiated commands
- CommunityToolkit.Mvvm with source-generated `ObservableObject` and `RelayCommand` patterns
- Two command execution paths: modal (UI dialog + blocking) and modeless (ExternalEvent handler for async operations)
- All document modifications flow through `ITransactionService` (71+ call sites vs. 7 raw `Transaction` sites)
- Domain services organized by feature: `Alignment`, `CadConversion`, `FamilyConversion`, `Materials`, `PurgeAndCompaction`, `RenderAppearance`, `Topography`, `Schemas`, `Renaming`

## Layers

**Application (Revit Loading & Bootstrapping):**
- Purpose: Revit add-in entry point and DI initialization
- Location: `src/App.cs`
- Contains: `App : IExternalApplication` with `OnStartup` (DI init, ribbon register) and `OnShutdown` (cleanup)
- Depends on: `Bootstrapper`, `IRibbonService`, global WPF exception handlers
- Used by: Revit 2026 runtime, loaded via `LECG.addin` manifest

**Commands (IExternalCommand Implementations):**
- Purpose: Command palette entry points, connect UI to services, manage selection/transaction context
- Location: `src/Commands/` (34+ command classes)
- Contains: 
  - `RevitCommand` subclasses (modal/single-action): `AlignEdgesCommand`, `AssignMaterialCommand`, `PurgeCommand`, etc. — `Execute(UIDocument, Document)` delegates to services
  - `ExternalEventCommand<THandler>` subclasses (modeless/event-driven): `CategoryChangerCommand`, `ConvertCadCommand` — static handler + `ExternalEvent.Raise()` for Revit API calls from WPF UI
- Depends on: `IAlignEdgesService`, `IPurgeService`, etc. (resolved via `ServiceLocator`)
- Used by: Ribbon buttons, executed by Revit on user click

**Views & ViewModels (WPF MVVM):**
- Purpose: UI dialogs and their state machines
- Location: `src/Views/` (39 XAML files), `src/ViewModels/` (20+ VMs)
- Contains:
  - Views: `AlignEdgesView.xaml` → `AlignEdgesView.xaml.cs` (code-behind), inherit from base `LecgDialogWindow` or `LecgWindow`
  - ViewModels: `AlignEdgesViewModel : BaseViewModel` → `partial class` with source-gen properties, `ApplyCommand`/`CancelCommand` (RelayCommand), `ShouldRun` flag
  - Base classes: `LecgWindow` (window state persistence, icon DP, UI thread dispatching), `LecgDialog` (static modal dialogs), `BaseViewModel` (Apply/Cancel/validation orchestration)
- Depends on: Services (e.g., `IAlignEdgesService` injected into VM), `IValidationService`, local layout resources
- Used by: Commands (resolve VM → create View → `ShowDialog()` or `Show()`)

**Domain Services (Business Logic & Revit API Boundaries):**
- Purpose: Encapsulate feature-specific logic, decouple commands from Revit API intricacies
- Location: `src/Services/{FeatureName}/` (e.g., `Alignment/`, `CadConversion/`, `Materials/`)
- Contains: Feature-specific interfaces and implementations:
  - `Alignment/`: `IAlignEdgesService`, `IAlignElementsService`, `IToposolidService`, plus sub-services (`IAlignEdgesCurveDivisionService`, `IAlignEdgesPointInsertionService`, etc.)
  - `CadConversion/`: `ICadConversionService`, `ICadGeometryExtractionService`, `ICadFamilyLoadPlacementService`, etc. (50+ services for DWG→family pipeline)
  - `FamilyConversion/`: `IFamilyConversionService`, `IFamilyGeometryCopyService`, `IFamilySourceDocumentService`, etc.
  - `Materials/`: `IMaterialService`, `IMaterialTypeAssignmentService`, `IMaterialAppearanceAssetService`, `IMaterialPbrService`, etc.
  - `PurgeAndCompaction/`: `IPurgeService`, `IPurgePassExecutionService`, `ILinePatternCompactionService`, etc.
  - `RenderAppearance/`: `IRenderAppearanceService`, `IRenderMaterialSyncExecutionService`, etc.
  - `Topography/`: `IToposolidService`, `IToposolidBaseElevationService`, `IDivideToposolidService`
- Depends on: Revit API (`Document`, `Element`, `FilteredElementCollector`), `ITransactionService` (for writes), Utilities, Models
- Used by: Command handlers, ViewModels (setup), cross-service composition via DI

**Infrastructure Services (Revit Runtime & Cross-Cutting):**
- Purpose: Provide Revit-specific cross-cutting concerns (transactions, logging, selection, linked models)
- Location: `src/Services/Infrastructure/`
- Contains:
  - `ITransactionService`: Named transactions, conditional rollback, failure handling (uses `SafeFailureHandler`)
  - `IAppMemoryCache`: Wrapped `MemoryCache` for caching (e.g., collector results)
  - `ILogger`/`Logger`: Structured logging via Serilog + optional UI log window
  - `ISelectionCoordinator`: Selection mode management
  - `ILinkedModelExportService`: Linked model geometry/transform access
  - Progress reporters (legacy `IProgressReporter`, `RevitCommandProgressReporter`)
- Depends on: Autodesk.Revit.DB, Serilog, Microsoft.Extensions.Caching.Memory
- Used by: Domain services (all use `ITransactionService`), Commands (logging), ViewModels (progress updates)

**Models & Data Transfer Objects:**
- Purpose: Represent domain data (settings, results, intermediate calculations)
- Location: `src/Models/`
- Contains: Pure data classes (e.g., `AlignEdgesSourceResult`, `PbrMaterialCreateRequest`, `RenderMaterialSyncResult`, `PurgeDialogSettings`, search/rename contexts)
- Depends on: None (intentionally isolated)
- Used by: Services (return types), ViewModels (binding), Validation

**Utilities & Helpers:**
- Purpose: Shared, cross-cutting logic (converters, filters, math, icons, images)
- Location: `src/Utilities/`
- Contains: ~12 utility classes (e.g., `CategoryUtils`, `ClipperUtils`, `ImageUtils`, `VertexSpatialIndex`, WPF converters, `SelectionFilters` base + subclasses)
- Depends on: Revit API, geometry3Sharp (for spatial indexing), Clipper2 (polygon operations)
- Used by: Services, Commands, Views

**Validation:**
- Purpose: FluentValidation rule sets per ViewModel
- Location: `src/Validation/Validators/`
- Contains: `{FeatureName}ViewModelValidator : AbstractValidator<{FeatureName}ViewModel>` (e.g., `OffsetElevationsViewModelValidator`, `AlignEdgesViewModelValidator`)
- Depends on: FluentValidation, Revit API (for constraint checking if needed)
- Used by: `IValidationService` (scans assembly, builds rules on demand), `BaseViewModel.CanApply()`

**Configuration:**
- Purpose: Static constants for UI strings, Revit-specific configurations
- Location: `src/Configuration/` (AppConstants.cs, RevitConstants.cs, UIConstants.cs)
- Contains: Ribbon panel names, feature flags, magic numbers, error codes
- Depends on: None
- Used by: Ribbon factory, Commands, Services

**Core & Composition:**
- Purpose: Revit add-in bootstrapping, base command class, DI, ribbon registration
- Location: `src/Core/`
- Contains:
  - `Bootstrapper.cs`: Builds `IServiceProvider`, registers 100+ singletons (domain services + infrastructure)
  - `ServiceLocator.cs`: Static access to DI container (necessary because Revit creates Commands via parameterless reflection)
  - `RevitCommand.cs`: Abstract base for all commands; supplies `Doc`/`UIDoc`/`CommandData`, structured logging scope, exception → dialog handling, progress updates
  - `ExternalEventCommand<THandler>`: Base for modeless commands; manages static handler + `ExternalEvent` (one per closed generic type)
  - `SelectionCoordinator` / `SelectionFilters` / `SelectionSeedHelper`: Selection mode management and filtering
  - Ribbon support: `IRibbonService`, `RibbonFactory`, `RibbonButtonConfig`

**LECG.Core (Shared Library):**
- Purpose: Reusable, non-Revit-dependent abstractions for filtering, naming, purge logic
- Location: `LECG.Core/`
- Contains: `Filtering/`, `Naming/`, `Purge/`, `Graphics/`, `Rename/` sub-namespaces with utility classes
  - `Purge/`: `PurgeOptions` record, `PurgeResult` record (API post-2026-05-28: replaced 13-positional-arg style)
  - `Filtering/`: Reusable collector/query helpers
  - `Naming/`: Element naming utilities
- Depends on: None (no Revit refs)
- Used by: Main assembly `src/Services/`, tests

## Data Flow

**Modal Command Flow (Blocking Dialog → Service Call → Result Dialog):**

1. User clicks ribbon button → Revit instantiates `SomeCommand : RevitCommand` (parameterless constructor)
2. `RevitCommand.Execute(ExternalCommandData)` initializes context (Doc, UIDoc), sets up logging scope
3. Command resolves `ISomeService` + `SomeViewModel` via `ServiceLocator`
4. Command creates `SomeView(viewModel)`, calls `view.ShowDialog()` (blocks until closed)
5. User fills form, clicks Apply → `BaseViewModel.Apply()` runs validation via `IValidationService`
6. If valid, `ShouldRun = true`, closes dialog
7. Command checks `if (result == true && vm.ShouldRun)`, then calls `service.DoWork(doc, vm.UserInputs)`
8. Service method runs (typically wraps changes in `ITransactionService.Run(doc, txnName, action)`)
9. Command displays result dialog (`LecgDialog.Show(title, message)`)
10. Returns `Result.Succeeded` or `Result.Failed`

**Modeless Command Flow (UI Dialog + ExternalEvent Handler → Async Document Access):**

1. User clicks ribbon button → Revit instantiates `SomeCommand : ExternalEventCommand<SomeEventHandler>`
2. Command resolves ViewModel, creates View, injects callbacks (`viewModel.RequestRun = () => RaiseExternalEvent()`)
3. Command calls `view.Show()` (non-blocking, window stays open)
4. User performs UI operations (setup, preview, etc.) in modeless window
5. User clicks Apply/Execute in ViewModel → calls `RequestRun()` → `RaiseExternalEvent()` (posts to Revit event queue)
6. Revit invokes static `SomeEventHandler.Execute(UIApplication)` from API context
7. Handler operates on Document (modifications, reads, filtered collections)
8. Handler posts results back to ViewModel via callback (logs, progress)
9. ViewModel updates UI thread (via `Dispatcher.Invoke()`)
10. Handler lifecycle: single static instance per command type, reused across invocations

**State Management:**

- **ViewModel State:** Observable properties (source-gen via partial + `SetProperty()`) notify UI on change
- **Document State:** Managed entirely within service methods; no ViewModel holds post-commit state
- **Modeless State:** Handler initialized fresh per command invocation; ViewModel holds UI/async state; handler holds Revit API context

## Key Abstractions

**RevitCommand:**
- Purpose: Revit API context provider, error handler, logging scope
- Files: `src/Core/RevitCommand.cs` (subclasses in `src/Commands/`)
- Pattern: Abstract `Execute(UIDocument, Document)` method, framework handles Revit reflection instantiation, error dialogs
- Usage: All synchronous commands subclass this; modeless commands add `ExternalEventCommand<T>` layer

**ExternalEventCommand<THandler>:**
- Purpose: Lazily-created, static, reusable `ExternalEvent` + handler for modeless flows
- Files: `src/Core/ExternalEventCommand.cs` (examples: `CategoryChangerCommand`, `ConvertCadCommand`)
- Pattern: Static fields hold handler and event (one per closed generic); `RaiseExternalEvent()` posts to Revit queue
- Caveat: Handler state persists across invocations; must be stateless or explicitly cleared

**ITransactionService:**
- Purpose: Single point for all document modifications; standardizes transaction naming, failure handling, rollback
- Files: `src/Services/Infrastructure/ITransactionService.cs`, `src/Services/Infrastructure/TransactionService.cs`
- Pattern: 
  - `Run<T>(doc, name, func)`: Non-conditional transaction, returns result
  - `RunConditional(doc, name, func)`: Conditional commit (rollback if func returns false)
  - `RunWithOptions(doc, name, action, configureOptions)`: Custom failure handling
  - `RunRollbackOnly(...)`: Execute without commit (test/inspection)
- Usage: 71+ call sites across services; only 7 raw `new Transaction(` in infrastructure

**ServiceLocator:**
- Purpose: Static access to DI container (necessity for Revit reflection-instantiated, parameterless-constructor commands)
- Files: `src/Core/ServiceLocator.cs`
- Pattern: Initialize once in `App.OnStartup` → `Bootstrapper.Initialize()` → `ServiceLocator.Initialize(provider)`; commands call `GetRequiredService<ISomeService>()`
- Caveat: Service not found throws `InvalidOperationException` (early detection)

**BaseViewModel:**
- Purpose: MVVM foundation with Apply/Cancel pattern and validation orchestration
- Files: `src/ViewModels/BaseViewModel.cs` (abstract partial)
- Pattern:
  - Properties: `Title`, `IsBusy`, `ShouldRun` (observable)
  - Commands: lazy `ApplyCommand` (calls `Apply()`), lazy `CancelCommand` (calls `Cancel()`)
  - `Apply()`: runs `CanApply()` (validation via `IValidationService`), sets `ShouldRun = true`, closes dialog
  - `Cancel()`: sets `ShouldRun = false`, closes dialog
- Usage: All 20+ ViewModels in `src/ViewModels/` extend this

**IValidationService:**
- Purpose: FluentValidation rules registry and orchestration
- Files: `src/Validation/IValidationService.cs`, `src/Validation/ValidationService.cs`
- Pattern: Scans assembly for `AbstractValidator<T>` subclasses, builds rule set on demand, `TryValidate(obj, out message)`
- Usage: `BaseViewModel.CanApply()` calls this before `Apply()` logic

**RibbonFactory / IRibbonService:**
- Purpose: Centralized ribbon button/panel registration
- Files: `src/Core/Ribbon/RibbonFactory.cs`, `src/Core/Ribbon/RibbonService.cs` (implements `IRibbonService`)
- Pattern: `RibbonFactory` builds buttons from config (`RibbonButtonConfig`), `RibbonService.InitializeRibbon(UIControlledApplication)` registers all panels/buttons
- Usage: Called once from `App.OnStartup` via `ServiceLocator.GetRequiredService<IRibbonService>()`

## Entry Points

**Revit Add-In Load:**
- Location: `src/App.cs:13` — `App : IExternalApplication`
- Triggers: Revit startup (reads `LECG.addin` manifest at `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG.addin`)
- Responsibilities: 
  - Register `pack://` URI scheme workaround for .NET 8 resource loading
  - Load global WPF resource dictionaries
  - Initialize DI via `Bootstrapper.Initialize()`
  - Register ribbon via `IRibbonService.InitializeRibbon()`
  - Register global exception handlers (once, static)

**Command Execution:**
- Location: `src/Commands/{FeatureName}Command.cs` — `IExternalCommand` implementations
- Triggers: Ribbon button click (Revit reflection-instantiates parameterless constructor)
- Responsibilities:
  - Initialize Revit API context (`Doc`, `UIDoc`, `CommandData`)
  - Resolve services and ViewModel from DI
  - Create and show View (modal `ShowDialog()` or modeless `Show()`)
  - Await user action (`ShouldRun` flag)
  - Invoke service methods to perform work
  - Display results or errors
  - Return `Result.Succeeded` / `Result.Failed`

**Modeless Event Handler:**
- Location: `src/Commands/{FeatureName}Command.cs` — Nested `IExternalEventHandler` class
- Triggers: `ExternalEvent.Raise()` from modeless window
- Responsibilities:
  - Execute work within valid Revit API context (from `Execute(UIApplication)`)
  - Access Document via UIApplication
  - Perform transactions (via `ITransactionService`)
  - Post results back to ViewModel (callbacks, logging)
  - Never access WPF dispatcher directly; communicate via callbacks

## Error Handling

**Strategy:** Catch-at-command-level, show user dialog, log to file and UI log window.

**Patterns:**
- `RevitCommand.Execute()` wraps user `Execute(UIDoc, Doc)` in try-catch, shows error dialog, logs via `ILogger`
- Service methods throw exceptions; command catches known types, unknown types bubble as `Result.Failed`
- Modeless handlers catch `Exception`, log to ViewModel callback (`viewModel.OnLog?.Invoke(message)`)
- `SafeFailureHandler` (implements `IFailuresPreprocessor`) swallows non-critical Revit warnings in transactions
- Validation failures shown via `LecgDialog.Show(title, message)` before command execution

## Cross-Cutting Concerns

**Logging:** 
- Framework: Serilog (file sink to `%AppData%\LECG\logs\`)
- Structured via `ILoggerFactory` in DI, `Logger` singleton provides `ILogger` interface
- Commands wrapped in `CommandLogContext` (scope with command name, doc title, Revit version)
- UI log window: `LogView` shows buffered entries, commands call `ShowLogWindow(title)` to display

**Validation:** 
- FluentValidation: `{ViewModel}Validator` classes in `src/Validation/Validators/`
- Scanned and registered in DI via `services.AddValidatorsFromAssemblyContaining<SomeValidator>()`
- Invoked by `BaseViewModel.CanApply()` before `Apply()` logic
- Error message shown in dialog; form remains open for correction

**Authentication:** 
- Not explicitly scoped (Revit document access is implicit via command execution context)
- Revit handles user licensing; add-in assumes valid session

**Selection Management:**
- `ISelectionCoordinator`: Mode management (`ISelectionMode` implementations)
- `SelectionFilters`: Reusable filters (e.g., `SlabFilter`, `AnyFamilyInstanceFilter`)
- Commands call `SelectionSeedHelper.GetSelectedReferences(uiDoc, filter)` to pre-populate VM

---

## Revit Add-in Map

### Add-in Registration

**Manifest Location:** `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG.addin` (machine-wide, manually installed)

**Manifest Template:** `docs/deployment/LECG.addin.template` (tracked in repo; runtime version differs: uses `<ClientId>` instead of `<AddInId>`, but works)

**Assembly Deployment:** `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG\` directory
- Main DLL: `LECG.dll` (built to `bin/x64/{config}/net8.0-windows/`)
- Deployed by: MSBuild `DeployToRevit` target after build (unless `-p:SkipRevitDeploy=true`)
- On CI: Automatically skipped when `GITHUB_ACTIONS` or `CI` env vars set

**External Application Entry Point:**
- Class: `LECG.App : IExternalApplication` (`src/App.cs:13`)
- Manifest FullClassName: `LECG.App`
- Revit loads at startup; calls `OnStartup(UIControlledApplication)` once per session

### External Commands

**Base Class Hierarchy:**
- `IExternalCommand` (Revit API)
  - `RevitCommand` abstract (`src/Core/RevitCommand.cs`) — supplies Doc/UIDoc, logging, error dialogs
    - All 34 command implementations (e.g., `AlignEdgesCommand`, `PurgeCommand`, etc.)
  - `ExternalEventCommand<THandler>` abstract (`src/Core/ExternalEventCommand.cs`) — static event handler pool
    - Modeless commands: `CategoryChangerCommand`, `ConvertCadCommand`

**Command Discovery & Registration:**
- Ribbon factory reads command class names and full names
- Buttons registered in `RibbonService.InitializeRibbon()` via `RibbonFactory`
- Each button maps to a fully-qualified command class name (e.g., `LECG.Commands.AlignEdgesCommand`)
- Revit invokes via reflection: `Activator.CreateInstance(commandType)` (parameterless constructor required)

**All 34 Commands:**
Located in `src/Commands/`:
- Alignment: `AlignEdgesCommand`, `AlignElementsCommand`, `AlignCommands` (multi-select variant)
- Materials: `AssignMaterialCommand`, `PbrMaterialCreatorCommand`, `RenderAppearanceMatchCommand`
- Purge/Compaction: `PurgeCommand`, `CompactingStylesCommand`, `CleanSchemasCommand`
- CAD/Family Conversion: `ConvertCadCommand`, `ConvertFamilyCommand`, `ConvertSharedCommand`, `ConvertFloorToToposolidCommand`, `ConvertToposolidToFloorCommand`
- Topography: `UpdateContoursCommand`, `DivideToposolidCommand`
- Geometry: `SimplifyPointsCommand`, `FixPointsCommand`, `SplitBoundariesCommand`, `OffsetElevationsCommand`, `ResetSlabsCommand`
- Search/Replace: `SearchReplaceCommand`
- Selection/Utilities: `FilterCopyCommand`, `CategoryChangerCommand`, `ChangeLevelCommand`, `TypeToLinkedModelsCommand`, `SharedToFamilyParameterCommand`
- Special: `HomeCommand` (opens main UI dashboard), `DebugCommand`, `TestHarvestGeometryCommand`, `SexyRevitCommand`

### Ribbon Registration

**Ribbon Service:** `src/Core/Ribbon/RibbonService.cs` (singleton, DI-resolved)

**Ribbon Panels & Buttons:**
- `RibbonFactory.cs` defines button configurations via `RibbonButtonConfig` objects
- Panels are organized by feature (e.g., "Alignment", "Materials", "Purge")
- Each button: name, display text, command class, tooltip, icon 16×16/32×32
- `RibbonService.InitializeRibbon()` iterates configs, creates push buttons, sets click handlers
- Buttons use image resources from `src/Resources/Images/`

**Example Flow:**
1. `App.OnStartup()` → `Bootstrapper.Initialize()` → registers `IRibbonService` singleton
2. `App.OnStartup()` → `ServiceLocator.GetRequiredService<IRibbonService>()` → `RibbonService` instance
3. `ribbonService.InitializeRibbon(application)` → `RibbonFactory.CreateButtons()` → defines all 34+ button configs
4. For each button: `application.CreatePushButton()`, set command class name, icon, text, tooltip
5. Button click → Revit instantiates command class → `Execute()` called

### WPF Windows (Modal & Modeless)

**Base Window Classes:**
- `LecgWindow` (`src/Views/Base/LecgWindow.cs`): Base for all modeless windows
  - Window state persistence (position, size via registry/settings)
  - Dependency property for icon (Geometry)
  - Commands: `CloseCommand`, `MinimizeCommand`
  - `Initialize(UIDocument)`: Store reference to Revit UIDoc for context

- `LecgDialogWindow` (`src/Views/Base/LecgDialogWindow.xaml`): Base for modal dialogs
  - Extends `LecgWindow`
  - Custom chrome (no standard title bar/buttons; uses custom close/minimize buttons)
  - XAML template defines layout

**Dialog Patterns:**
- Modal: Command creates View, calls `view.ShowDialog()` (blocks; returns `true`/`false`)
  - ViewModel's `ShouldRun` flag signals "Apply was clicked"
  - Command checks `if (result == true && vm.ShouldRun)` before invoking service
  - Example: `AlignEdgesCommand` → `AlignEdgesView.ShowDialog()` → user sets params → clicks "Apply" → returns `true`

- Modeless: Command creates View, calls `view.Show()` (non-blocking; window persists)
  - ViewModel exposes `RequestRun` callback (set by command)
  - User clicks UI button → callback invokes → command calls `RaiseExternalEvent()`
  - Example: `CategoryChangerCommand` → `CategoryChangerView.Show()` → user selects items → clicks "Execute" → fires external event

**All WPF Views (39 total):**
In `src/Views/`:
- Material/Render: `AssignMaterialView`, `PbrMaterialCreatorView`, `RenderAppearanceView`
- Alignment: `AlignEdgesView`, `AlignElementsView`, `AlignDashboardView`
- Topography: `UpdateContoursView`, `DivideToposolidView`, `ConvertFloorToToposolidView`, `ConvertToposolidToFloorView`
- Geometry: `OffsetElevationsView`, `ResetSlabsView`, `SimplifyPointsView`, `FixPointsView`, `SplitBoundariesView`
- CAD/Family: `ConvertCadView`, `CategoryChangerView`, `ChangeLevelView`, `TypeToLinkedModelsView`
- Search/Filter: `SearchReplaceView`, `FilterCopyView`
- Purge/Compact: `PurgeView`, `CompactingStylesView`
- Home/Dashboard: `HomeView`
- Log: `LogView` (shared for all commands' progress/error output)
- Utilities: `ConvertFloorToToposolidView`, `ConvertToposolidToFloorView`, `SexyRevitView`

### MVVM Structure

**BaseViewModel Hierarchy:**
- All ViewModels inherit from `BaseViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject` (partial classes, source-gen)
- Defines: `Apply()` / `Cancel()` (RelayCommand), `CloseAction` callback, `IsBusy`, `ShouldRun`, `Title`
- Subclasses extend with feature-specific properties (observable via `SetProperty()`)

**Example ViewModel Flow:**
```
public partial class AlignEdgesViewModel : BaseViewModel
{
    [ObservableProperty]
    private ObservableCollection<TargetRefItem> targets;
    
    [ObservableProperty]
    private bool findMyEdge;
    
    public void SetTargets(IList<Reference> refs, Document doc) { ... }
    
    public override void Apply()
    {
        if (!CanApply()) return;
        ShouldRun = true;
        CloseAction?.Invoke();
    }
}
```

**Validators:**
- `AlignEdgesViewModelValidator : AbstractValidator<AlignEdgesViewModel>`
- Rules check: non-null collections, valid parameters, Revit constraints
- Invoked by `BaseViewModel.CanApply()` → `IValidationService.TryValidate()`

### Revit API Service Layer

**Revit API Boundaries:**

All Revit API usage is confined to:
1. **Domain Services** (`src/Services/`): Business logic with Revit API calls
2. **Infrastructure Services** (`src/Services/Infrastructure/`): Revit context providers
3. **RevitCommand Base** (`src/Core/RevitCommand.cs`): Command execution context setup

**No direct Revit API access in:**
- ViewModels (no `using Autodesk.Revit.DB`)
- Views
- Models (data transfer only)
- Utilities (geometry-only, no Revit document access)

**Common Patterns:**

1. **FilteredElementCollector Usage (104+ sites):**
   - No shared collector cache convention identified; per-site usage deliberate
   - Example: `AlignEdgesService` → `AlignEdgesBoundaryCollectionService` → collector for slabs
   - Ordering: typically `new FilteredElementCollector(doc).OfClass(typeof(Slab)).Cast<Slab>()`

2. **Parameter Access (33+ StorageType checks):**
   - Check `StorageType` before read/write (prevent exceptions)
   - No unified parameter helper; checks are inline per service
   - Example: `if (param.StorageType == StorageType.String) value = param.AsString();`

3. **Units & Geometry:**
   - ForgeTypeId-based APIs (11 sites) vs. legacy `DisplayUnitType` (5 sites)
   - Revit 2026 no longer has `DisplayUnitType`; all ForgeTypeId
   - Example: `Autodesk.Revit.DB.UnitTypeId.Meters`

4. **Linked Models:**
   - `ILinkedModelExportService` exists but transform conventions unclear (open question)
   - No direct `GetTotalTransform()` call found in `src/`

### Transactions & ITransactionService

**Every Document modification is wrapped:**
```csharp
_transactionService.Run(doc, "Align Edges Vertices", doc =>
{
    foreach (var vertex in vertices)
    {
        // Document writes here
        doc.Modify(vertex, ...);
    }
});
```

**Transaction Attributes on Commands:**
- `[Transaction(TransactionMode.Manual)]` on nearly all command classes
- Means: command is responsible for creating/committing transactions
- Commands delegate to `ITransactionService` (which creates Transaction internally)

**Example Transaction Hierarchy:**
1. `AlignEdgesCommand.Execute()` calls `service.AlignEdges(doc, targets, refs)`
2. `AlignEdgesService.AlignEdges()` iterates and calls sub-services
3. Sub-service (e.g., `AlignEdgesVertexAlignmentService`) calls `_transactionService.Run(doc, "Update Vertex", _handler)`
4. Inside lambda: raw `doc.Modify()` calls (no nested transactions)

### ExternalEvent Handlers

**Modeless Execution Pattern:**

Used by: `CategoryChangerCommand`, `ConvertCadCommand` (and potentially others)

**How It Works:**
1. Command creates static `IExternalEventHandler` (lazily, once per closed generic type)
2. Static `ExternalEvent` wraps the handler (also once per type)
3. Command: `viewModel.RequestRun = () => RaiseExternalEvent()`
4. Modeless View: User clicks button → calls `RequestRun()` → raises event
5. Revit: Invokes `handler.Execute(UIApplication)` from valid API context
6. Handler: Accesses `UIApplication → ActiveUIDocument → Document`, performs work
7. Handler: Posts results to ViewModel via callback (no direct UI thread access)
8. ViewModel: Updates UI on main thread (via `Dispatcher.Invoke()`)

**State Management:**
- Handler is shared across all invocations of the same command type
- Must be stateless or explicitly reset between runs
- ViewModel (in command) holds UI state and callback references
- Handler receives ViewModel reference via `Initialize()` method call (command → handler)

### Unit/Geometry/Parameter Helpers

**No centralized helpers found.** Usage is per-service:

**Example: Storage Type Check (CategoryChangerCommand)**
```csharp
// No helper; inline check
Element el = doc.GetElement(ref);
if (el is FamilyInstance fi)
{
    // Direct parameter access after type check
    Family fam = fi.Symbol.Family;
}
```

**Example: Unit Conversion (Materials Services)**
- Use ForgeTypeId directly: `UnitTypeId.Meters`, `UnitTypeId.DegreesCelsius`
- No shared unit helper identified in `src/`; assume direct API or per-service

### Collector Patterns

**No Single Shared Collector Convention:**
- Each service builds its own filtered collections
- Pattern is flexible: `OfClass()`, `OfCategory()`, optional `OfType()`, `Cast<T>()`
- Example `AlignEdgesService`:
  ```csharp
  var collector = new FilteredElementCollector(doc)
      .OfClass(typeof(TopographySurface))
      .Cast<TopographySurface>();
  ```

### Version Targeting

**Target:** Revit 2026 only
- NuGet: `Nice3point.Revit.Api.RevitAPI` version `2026.4.10`
- No multi-version support (no conditional compilation, no version checks in code)
- CI target: net8.0-windows (same as production)

### Deployment Paths

**Build Output:** `bin/x64/{config}/net8.0-windows/`
- LECG.dll, LECG.pdb, LECG.deps.json

**Live Installation Path:** `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG\`
- Deployed by MSBuild `DeployToRevit` target (default, unless `-p:SkipRevitDeploy=true`)
- Manifest: `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG.addin` (manually installed, not auto-deployed)

**Deployment Trigger:**
- `DeployToRevit` target runs after `Build` (AfterTargets)
- Copies dll/pdb/deps.json to addins folder
- Skipped on CI (env check in .csproj)

---

## Practice Candidates

### [Observed] (scope: project) Layered MVVM + Service-Oriented Architecture
- **Evidence:** `src/App.cs:33-38` (DI init), `src/Core/RevitCommand.cs:26` (abstract Execute), `src/Services/Alignment/AlignEdgesService.cs` (domain service), `src/ViewModels/BaseViewModel.cs:10` (MVVM base), `src/Views/AlignEdgesView.xaml` (UI layer)
- **Why it matters:** Clear separation of concerns enables testing, code reuse, and maintainability; Revit API is isolated in services
- **Preserve by:** New features follow this layering: Command → ViewModel → View + Service → TransactionService; don't bypass layers

### [Observed] (scope: project) All Document Writes via ITransactionService
- **Evidence:** `src/Services/Infrastructure/ITransactionService.cs:6-18` (interface), 71+ call sites across `src/Services/`, only 7 raw `new Transaction(` in infrastructure
- **Why it matters:** Centralizes transaction naming, failure handling, conditional rollback; prevents orphaned/leaked transactions
- **Preserve by:** New writes call `_transactionService.Run()` or `RunConditional()`; raw `Transaction` only inside `TransactionService` itself

### [Observed] (scope: project) ServiceLocator Pattern for Revit-Instantiated Commands
- **Evidence:** `src/Core/ServiceLocator.cs:10-35`, `src/App.cs:34-38`, `src/Commands/AlignEdgesCommand.cs:23` (GetRequiredService)
- **Why it matters:** Revit creates commands via parameterless reflection; static access is only way to reach DI
- **Preserve by:** Commands always resolve services via `ServiceLocator`, never `new` services ad hoc

### [Observed] (scope: command) Modal Dialog → Service Call → Result Pattern
- **Evidence:** `src/Commands/AlignEdgesCommand.cs:18-45` (ShowDialog, check ShouldRun, call service), `src/ViewModels/BaseViewModel.cs:29-38` (Apply/Cancel logic)
- **Why it matters:** Consistent UX flow; validation enforced before service invocation
- **Preserve by:** New modal commands follow: Create VM → Create View → ShowDialog → check ShouldRun → call service

### [Observed] (scope: command) ExternalEventCommand for Modeless Flows
- **Evidence:** `src/Core/ExternalEventCommand.cs:6-28`, `src/Commands/CategoryChangerCommand.cs:17,28-40` (RequestRun, RaiseExternalEvent)
- **Why it matters:** Keeps document modifications in valid Revit API context; avoids dead-lock with modeless UI
- **Preserve by:** Modeless commands subclass `ExternalEventCommand<THandler>`, handlers access Document via `UIApplication`

### [Observed] (scope: project) CommunityToolkit.Mvvm with ObservableObject and RelayCommand
- **Evidence:** `src/ViewModels/BaseViewModel.cs:10` (ObservableObject), `:23-27` (lazy RelayCommand), `src/ViewModels/AlignEdgesViewModel.cs` (partial class with [ObservableProperty])
- **Why it matters:** Source-generated property notification; compile-time safety
- **Preserve by:** New VMs extend BaseViewModel, mark properties partial, use [ObservableProperty], use RelayCommand for all ICommand properties

### [Observed] (scope: project) Ribbon Centralization via Factory Pattern
- **Evidence:** `src/Core/Ribbon/RibbonFactory.cs`, `src/Core/Ribbon/RibbonService.cs`, `src/App.cs:37-38` (InitializeRibbon call)
- **Why it matters:** Single point of button registration; consistent icon/text/tooltip management
- **Preserve by:** Add new command buttons through RibbonFactory/RibbonService, not inline in App

### [Observed] (scope: project) FluentValidation with BaseViewModel.CanApply()
- **Evidence:** `src/Validation/Validators/AlignEdgesViewModelValidator.cs`, `src/Validation/IValidationService.cs`, `src/ViewModels/BaseViewModel.cs:46-57` (CanApply → TryValidate)
- **Why it matters:** Declarative rules, reusable validators, prevents invalid command execution
- **Preserve by:** New VMs create `{FeatureName}ViewModelValidator`, register in DI via `AddValidatorsFromAssemblyContaining()`, never skip validation in Apply()

### [Observed] (scope: infrastructure) Structured Logging via Serilog + UI Log Window
- **Evidence:** `src/Services/Infrastructure/Logging/Logger.cs`, `src/Services/Infrastructure/Logging/SerilogBootstrapper.cs`, `src/Views/LogView.xaml`, `src/Core/RevitCommand.cs:54` (ShowLogWindow)
- **Why it matters:** Persistent file logging + optional UI feedback; commands auto-register logging scope
- **Why it matters:** Persistent file logging + optional UI feedback; commands auto-register logging scope
- **Preserve by:** Commands call `Log(message)` (inherited from RevitCommand), call `ShowLogWindow()` for long operations; services use `ILogger` singleton for structured logging

### [Observed] (scope: project) Selection Filters & Pre-population Pattern
- **Evidence:** `src/Core/SelectionFilters.cs`, `src/Utilities/FamilyInstanceFilter.cs`, `src/Commands/AlignEdgesCommand.cs:26-30` (SelectionSeedHelper.GetSelectedReferences, SetTargets)
- **Why it matters:** Reduces user input; enables command chaining and selection workflows
- **Preserve by:** Commands check current selection, pre-populate VM if relevant; create new SelectionFilter subclasses as needed

### [Inferred] (scope: project) Domain Services Organized by Feature
- **Evidence:** `src/Services/Alignment/`, `src/Services/CadConversion/`, `src/Services/Materials/`, etc. (8 feature folders), each with interface + implementation classes
- **Why it matters:** Logical grouping; easy to navigate and extend; clear domain boundaries
- **Preserve by:** New feature logic: create `src/Services/{FeatureName}/` folder, add interfaces + implementations, register in Bootstrapper

### [Concern] (scope: project) No Shared Collector/Unit/Parameter Helpers
- **Evidence:** 104 FilteredElementCollector sites with no convention; 33 StorageType checks inline; no shared unit helper
- **Why it matters:** Risk of duplicated logic, inconsistent patterns
- **Preserve by:** If pattern emerges (3+ identical uses), extract to utility class (`src/Utilities/`) and reference it

### [Concern] (scope: command) ExternalEventCommand Handler State Persistence
- **Evidence:** `src/Core/ExternalEventCommand.cs:9-16` (static fields; _handler, _externalEvent shared across invocations)
- **Why it matters:** Stale state can leak between runs if handler holds document-specific data
- **Preserve by:** Handlers must be stateless or explicitly cleared per invocation (see `CategoryChangerCommand.Initialize()` pattern)

### [Open] (scope: unknown) Linked Model Transform Conventions
- **Evidence:** `ILinkedModelExportService` exists (`src/Services/Infrastructure/ILinkedModelExportService.cs`), but no `GetTotalTransform(` call found in codebase
- **Why it matters:** Linked model coordinate handling is critical for geometry operations
- **Preserve by:** When implementing linked-model features, verify transform assumptions with a Revit test

