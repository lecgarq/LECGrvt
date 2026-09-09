# Codebase Structure

**Analysis Date:** 2026-07-04

## Directory Layout

```
C:\LECG\RevitAddins\LECG/
├── src/                                  # Main add-in assembly (net8.0-windows, x64)
│   ├── App.cs                            # IExternalApplication entry point
│   ├── Commands/                         # 34+ IExternalCommand implementations
│   ├── Core/                             # DI, base classes, ribbon, command infrastructure
│   │   ├── Bootstrapper.cs               # Service registration & composition root
│   │   ├── ServiceLocator.cs             # Static DI container access
│   │   ├── RevitCommand.cs               # Abstract command base
│   │   ├── ExternalEventCommand.cs       # Modeless event handler base
│   │   ├── Ribbon/                       # Ribbon registration (RibbonService, RibbonFactory)
│   │   ├── DependencyInjection/          # Safe scanning extensions
│   │   └── *.cs                          # Selection, availability, handlers
│   ├── ViewModels/                       # MVVM view models (partial classes, source-gen)
│   │   ├── BaseViewModel.cs              # MVVM foundation (Apply/Cancel/Validation)
│   │   ├── Components/                   # Reusable VM components
│   │   └── {FeatureName}ViewModel.cs     # 20+ feature-specific VMs
│   ├── Views/                            # WPF XAML + code-behind
│   │   ├── Base/                         # LecgWindow, LecgDialogWindow, LecgDialog
│   │   ├── Components/                   # Reusable XAML components
│   │   ├── {FeatureName}View.xaml(.cs)   # 39 feature-specific dialogs
│   │   └── Themes/                       # Global resource dictionaries
│   ├── Services/                         # Business logic & Revit API boundaries
│   │   ├── Alignment/                    # Align edges, align elements services
│   │   ├── CadConversion/                # DWG import → Family conversion (50+ services)
│   │   ├── FamilyConversion/             # Family creation/loading/manipulation
│   │   ├── Graphics/                     # Geometry visualization
│   │   ├── Infrastructure/               # TransactionService, Logger, LinkedModel, etc.
│   │   │   └── Logging/                  # Serilog integration
│   │   ├── Interfaces/                   # Sparse; most interfaces co-located with implementations
│   │   ├── Materials/                    # Material assignment, PBR, appearance sync
│   │   ├── PurgeAndCompaction/           # Purge native/deep, compact styles
│   │   ├── RenderAppearance/             # Render appearance material management
│   │   ├── Renaming/                     # Batch rename, formula auto-grouping
│   │   ├── Schemas/                      # ExtensibleStorage schema management
│   │   └── Topography/                   # Toposurface/Toposolid operations
│   ├── Models/                           # Data transfer objects & settings
│   ├── Utilities/                        # Helpers (filters, converters, math, images)
│   ├── Validation/                       # FluentValidation rule sets
│   │   └── Validators/                   # {FeatureName}ViewModelValidator classes
│   ├── Configuration/                    # AppConstants.cs, RevitConstants.cs, UIConstants.cs
│   ├── Behaviors/                        # WPF attached behaviors
│   ├── Controls/                         # Custom WPF controls
│   ├── Resources/                        # Images, XAML themes, embedded resources
│   └── InternalsVisibleTo.Tests.cs       # Allow LECG.Tests to access internal types
│
├── LECG.Core/                            # Shared library (no Revit refs)
│   ├── Filtering/                        # Reusable collector helpers
│   ├── Graphics/                         # Geometry utilities
│   ├── Naming/                           # Element naming helpers
│   ├── Purge/                            # PurgeOptions, PurgeResult records
│   ├── Rename/                           # Rename logic
│   └── Result.cs                         # Result<T> type
│
├── LECG.Tests/                           # xUnit test project (net8.0-windows, Revit API refs)
│   ├── Caching/                          # AppMemoryCache tests
│   ├── Commands/                         # Command integration tests
│   ├── Core/                             # Core infrastructure tests (DialogWhitelist, etc.)
│   ├── DependencyInjection/              # DI/validator registration tests
│   ├── Services/                         # Service logic tests
│   ├── Utils/                            # Utility function tests
│   ├── Validation/                       # Validator tests
│   └── ViewModels/                       # ViewModel binding/logic tests
│
├── LECG.csproj                           # Main assembly project (build, DI, deployment)
├── LECG.Tests/LECG.Tests.csproj          # Test project
├── LECG.Core/LECG.Core.csproj            # Shared library
├── Directory.Build.props                 # Build config (warning suppressions, Revit DLL overlaps)
│
├── docs/                                 # Documentation
│   ├── deployment/                       # Deployment runbook, .addin template
│   ├── ai/                               # Repository context, smoke test checklist
│   ├── review/                           # Code review guidelines, Revit API docs
│   └── standards/                        # Team standards
│
├── .planning/                            # Project planning and analysis
│   └── codebase/                         # This file + ARCHITECTURE.md
│
├── .github/                              # CI/CD workflows
│   └── workflows/                        # GitHub Actions (build, test, deploy)
│
├── .claude/                              # Claude project settings (local)
│   └── settings.local.json               # Model, token budget, etc.
│
├── scripts/                              # Build helpers, validation scripts
├── adapters/                             # External integrations (Clipper2, etc.)
├── tools/                                # Development/debug tools
├── design/                               # Design assets (UI mockups, etc.)
└── README.md                             # Project overview
```

## Directory Purposes

### `src/` — Main Add-in Assembly

**Core Responsibility:** Revit 2026 add-in implementation; all user-facing commands, UI, and domain logic.

**Key Subdirectories:**

#### `src/Commands/` (34+ files)
- Purpose: `IExternalCommand` implementations; entry points for user actions
- Contains: `{FeatureName}Command.cs` classes (e.g., `AlignEdgesCommand`, `PurgeCommand`)
- Pattern: Inherit from `RevitCommand` (sync modal) or `ExternalEventCommand<T>` (async modeless)
- Responsibilities: Initialize Revit context (Doc/UIDoc), resolve ViewModel/Service, show dialog, invoke service, display result
- Key pattern: Commands create ViewModels and Views, don't contain business logic
- Example: `AlignEdgesCommand.cs` → creates `AlignEdgesViewModel` + `AlignEdgesView` → calls `IAlignEdgesService.AlignEdges()`

#### `src/Core/` (bootstrapping, DI, command bases, ribbon)
- `Bootstrapper.cs`: Builds `IServiceProvider`, registers 100+ singletons (services, VMs, views)
- `ServiceLocator.cs`: Static access to DI container (necessary for parameterless-constructor commands)
- `RevitCommand.cs`: Abstract base for all commands; supplies Doc/UIDoc, logging scope, exception handling, progress updates
- `ExternalEventCommand.cs`: Base for modeless commands; manages static, shared `IExternalEventHandler` + `ExternalEvent`
- `Ribbon/`: Ribbon UI registration (panels, buttons, icons) via `RibbonFactory` + `RibbonService`
- Selection/Availability: `SelectionCoordinator`, `SelectionFilters`, `ProjectDocumentAvailability`, `FamilyDocumentAvailability`
- Handlers: `SafeFailureHandler` (swallows non-critical Revit warnings), `WarningSwallower`, `DialogWhitelist`

#### `src/ViewModels/` (20+ files, all partial classes)
- Purpose: UI state machines with validation, observable properties, Apply/Cancel commands
- Base: `BaseViewModel : ObservableObject` (CommunityToolkit.Mvvm) with lazy RelayCommand, validation orchestration
- Pattern: Each feature has one ViewModel (e.g., `AlignEdgesViewModel`)
  - Properties marked `[ObservableProperty]` (source-gen notification)
  - Methods: `SetTargets()`, `Apply()`, `Cancel()`, validation callbacks
- Responsibilities: Hold UI state, expose RelayCommands, coordinate Apply/Cancel flows, pass user input to services
- Validation: Each ViewModel has a paired `{FeatureName}ViewModelValidator` in `src/Validation/Validators/`

#### `src/Views/` (39+ XAML files)
- Purpose: WPF dialogs and windows for user interaction
- Base: 
  - `LecgWindow.cs`: Modeless window base (state persistence, icon DP, window state save/restore)
  - `LecgDialogWindow.xaml`: Modal dialog base (custom chrome, no title bar)
  - `LecgDialog.cs`: Static utility for simple message dialogs
- Pattern: Each feature has one View (e.g., `AlignEdgesView.xaml` + `.xaml.cs`)
  - `.xaml`: XAML markup (bindings to ViewModel properties)
  - `.xaml.cs`: Code-behind (minimal; mostly `InitializeComponent()` and `Initialize(UIDocument)`)
- Resources: Global theme dictionary in `src/Resources/Themes/LecgTheme.xaml`

#### `src/Services/` (domain logic, organized by feature)
- Purpose: Encapsulate business logic and Revit API access; all document modifications via `ITransactionService`
- Subdirectories by feature:
  - `Alignment/`: `IAlignEdgesService`, `IAlignElementsService`, sub-services (boundary, vertex, intersection handling)
  - `CadConversion/`: 50+ services for DWG → Family pipeline (extraction, tessellation, rendering, placement)
  - `FamilyConversion/`: Family creation, loading, geometry copy, parameter setup
  - `Graphics/`: Geometry visualization utilities
  - `Infrastructure/`: 
    - `ITransactionService`: Central transaction wrapper for all document writes
    - `ILogger` + `Logger`: Structured logging via Serilog
    - `IAppMemoryCache`: Caching wrapper
    - `ILinkedModelExportService`: Linked model geometry/transform
    - `ISelectionCoordinator`: Selection mode management
    - `Logging/`: Serilog bootstrap, log context, log entry buffering
  - `Materials/`: Material assignment, PBR creation, appearance sync, texture lookup
  - `PurgeAndCompaction/`: Purge execution (native, deep), style compaction (line, fill, text)
  - `RenderAppearance/`: Render material/graphics synchronization
  - `Renaming/`: Batch rename, formula auto-grouping, search/replace
  - `Schemas/`: ExtensibleStorage schema scanning and cleanup
  - `Topography/`: Toposurface/Toposolid operations, contour updates, division

**Key Pattern:** Each service interface + implementation pair; implementations depend on `ITransactionService` for all writes

#### `src/Models/` (data transfer objects)
- Purpose: Plain data classes for ViewModel binding and service return types
- Contains: `AlignEdgesSourceResult`, `PbrMaterialCreateRequest`, `RenderMaterialSyncResult`, search/rename contexts, compaction results
- Intentionally isolated: No Revit API, no business logic

#### `src/Utilities/` (~12 files)
- Purpose: Shared, cross-cutting helpers (filters, converters, math, images, icons)
- Contains:
  - Selection filters: `SelectionFilters` base + subclasses (`SlabFilter`, `AnyFamilyInstanceFilter`, etc.)
  - WPF converters: `BoolConverters`, `MathConverter`, `FilterStatusConverter`
  - Helpers: `CategoryUtils`, `ClipperUtils`, `ImageUtils`, `VertexSpatialIndex` (spatial geometry indexing), `ExecutionTimer`
  - Icons/Images: `Icons.cs` (Geometry definitions), `AppImages.cs` (ImageSource resources)

#### `src/Validation/` (FluentValidation rule sets)
- Purpose: Declarative input validation per ViewModel
- Contains: `Validators/` subfolder with `{FeatureName}ViewModelValidator` classes
  - Each validator: `AbstractValidator<{FeatureName}ViewModel>` with fluent rules
  - Invoked by `BaseViewModel.CanApply()` → `IValidationService.TryValidate()`
- Interface: `IValidationService` (registers and runs validators)

#### `src/Configuration/`
- Purpose: Constants and settings for UI, Revit, app behavior
- Contains: `AppConstants.cs`, `RevitConstants.cs`, `UIConstants.cs`
- Example: Magic numbers, feature flags, ribbon panel/button names

#### `src/Behaviors/`, `src/Controls/`, `src/Resources/`
- `Behaviors/`: WPF attached behaviors (e.g., multi-select checkbox behavior)
- `Controls/`: Custom WPF controls (`ElementGridControl`, `LecgDataGrid`, `LecgTreeView`)
- `Resources/`: Embedded images (`src/Resources/Images/`), XAML theme dictionary

---

### `LECG.Core/` — Shared Library (No Revit References)

**Core Responsibility:** Reusable, non-Revit-dependent utilities and abstractions.

**Subdirectories:**

#### `Filtering/`
- Reusable collector and query helpers (generic geometry/element filtering logic)

#### `Graphics/`
- Geometry visualization and utility functions

#### `Naming/`
- Element naming and string manipulation helpers

#### `Purge/`
- `PurgeOptions.cs` (record): Configuration for purge operations (passed to service)
- `PurgeResult.cs` (record): Results of purge (counts, status per element type)
- `PurgeSequence.cs` (enum): Purge mode selection (e.g., native, deep, by-type)

#### `Rename/`
- Batch rename logic and rule definitions

---

### `LECG.Tests/` — Test Project (xUnit)

**Core Responsibility:** Unit and integration tests for services, ViewModels, commands.

**Subdirectories:**

#### `Caching/`
- Tests for `AppMemoryCache` behavior and TTL

#### `Commands/`
- Integration tests for command execution (e.g., `FormulaAutoGroupingCommandTests`)
- Note: Full command testing requires Revit runtime; most tests are mock-based

#### `Core/`
- Infrastructure tests (e.g., `DialogWhitelistTests` for allowed dialogs)

#### `DependencyInjection/`
- Tests for service registration and validator scanning

#### `Services/`, `Utils/`, `Validation/`, `ViewModels/`
- Service logic tests, utility function tests, validator tests, ViewModel binding tests

**Build:** Includes Revit API references (Nice3point NuGet); builds on CI and dev machines

---

## Key File Locations

### Entry Points

- **Revit Startup:** `src/App.cs:13` — `App : IExternalApplication`
- **DI Initialization:** `src/Core/Bootstrapper.cs:20` — `Bootstrapper.Initialize()`
- **Command Base:** `src/Core/RevitCommand.cs:30` — `IExternalCommand.Execute()` wrapper
- **Modeless Base:** `src/Core/ExternalEventCommand.cs:6` — Handler + event pool

### Configuration Files

- **Project:** `LECG.csproj` (NuGet refs, deployment target, build props)
- **Tests:** `LECG.Tests/LECG.Tests.csproj`
- **Shared Library:** `LECG.Core/LECG.Core.csproj`
- **Build Props:** `Directory.Build.props` (warning suppressions, Revit DLL overlaps)

### Core Logic

- **Services (Domain):** `src/Services/{FeatureName}/*.cs`
- **Transaction Wrapper:** `src/Services/Infrastructure/TransactionService.cs:31,70` (raw Transaction creation sites)
- **Validation Service:** `src/Validation/ValidationService.cs`
- **Logging:** `src/Services/Infrastructure/Logging/Logger.cs`
- **Ribbon:** `src/Core/Ribbon/RibbonService.cs`, `src/Core/Ribbon/RibbonFactory.cs`

### Testing

- **Test Infrastructure:** `LECG.Tests/Core/` (e.g., bootstrapping test setup)
- **Test Validators:** `LECG.Tests/Validation/`
- **Service Tests:** `LECG.Tests/Services/`

---

## Naming Conventions

### Files

**Commands:**
- `{FeatureName}Command.cs` (e.g., `AlignEdgesCommand.cs`, `PurgeCommand.cs`)
- Modeless commands may include nested `EventHandler` class (e.g., `CategoryChangerEventHandler`)

**ViewModels:**
- `{FeatureName}ViewModel.cs` (e.g., `AlignEdgesViewModel.cs`)
- Must be `partial class` (source-gen)

**Views:**
- `{FeatureName}View.xaml` + `{FeatureName}View.xaml.cs` (e.g., `AlignEdgesView.xaml`)

**Services:**
- Interface: `I{FeatureName}Service.cs` (e.g., `IAlignEdgesService.cs`)
- Implementation: `{FeatureName}Service.cs` (e.g., `AlignEdgesService.cs`)
- Sub-services: `I{FeatureName}{SubFeature}Service.cs`, `{FeatureName}{SubFeature}Service.cs`

**Validators:**
- `{FeatureName}ViewModelValidator.cs` in `src/Validation/Validators/`

**Models/DTOs:**
- `{FeatureName}Result.cs`, `{FeatureName}Context.cs`, `{FeatureName}Settings.cs`

### Directories

**Service Groupings:**
- `src/Services/{DomainName}/` (e.g., `Alignment/`, `CadConversion/`, `Materials/`)

**Utilities:**
- Helper classes grouped by concern (e.g., `CategoryUtils`, `ImageUtils`)

---

## Where to Add New Code

### New Command (Modal Workflow)

**Steps:**

1. Create command class:
   - File: `src/Commands/{FeatureName}Command.cs`
   - Inherit from: `RevitCommand` (or `ExternalEventCommand<T>` if modeless)
   - Implement: `Execute(UIDocument, Document)` method

2. Create ViewModel:
   - File: `src/ViewModels/{FeatureName}ViewModel.cs`
   - Inherit from: `BaseViewModel`
   - Mark as: `partial class` with `[ObservableProperty]` properties

3. Create View:
   - File: `src/Views/{FeatureName}View.xaml` + `.xaml.cs`
   - Inherit from: `LecgDialogWindow` (modal) or `LecgWindow` (modeless)
   - Bind to ViewModel properties

4. Create Validator (if needed):
   - File: `src/Validation/Validators/{FeatureName}ViewModelValidator.cs`
   - Inherit from: `AbstractValidator<{FeatureName}ViewModel>`
   - Define fluent rules

5. Register in DI:
   - File: `src/Core/Bootstrapper.cs`
   - Add lines in `ConfigureViewModels()` and `ConfigureViews()`
   - Service registration in `ConfigureServices()`

6. Register Ribbon Button:
   - File: `src/Core/Ribbon/RibbonFactory.cs` or `RibbonService.cs`
   - Add `RibbonButtonConfig` with button metadata
   - Icon: Add image to `src/Resources/Images/`

**Example Snippet (AlignEdgesCommand):**
```csharp
// src/Commands/AlignEdgesCommand.cs
[Transaction(TransactionMode.Manual)]
public class AlignEdgesCommand : RevitCommand
{
    public override void Execute(UIDocument uiDoc, Document doc)
    {
        var service = ServiceLocator.GetRequiredService<IAlignEdgesService>();
        var vm = ServiceLocator.GetRequiredService<AlignEdgesViewModel>();
        var view = ServiceLocator.CreateWith<AlignEdgesView>(vm);
        
        if (view.ShowDialog() == true && vm.ShouldRun)
        {
            var results = service.AlignEdges(doc, vm.TargetRefs);
            LecgDialog.Show("Align Edges", BuildMessage(results));
        }
    }
}
```

### New Service (Business Logic)

**Steps:**

1. Create interface:
   - File: `src/Services/{FeatureName}/I{FeatureName}Service.cs`
   - Define public methods (no implementation)

2. Create implementation:
   - File: `src/Services/{FeatureName}/{FeatureName}Service.cs`
   - Inherit from: (none; plain class)
   - Implement interface, inject dependencies (services, `ITransactionService`)

3. Register in DI:
   - File: `src/Core/Bootstrapper.cs:ConfigureServices()`
   - Add: `services.AddSingleton<I{FeatureName}Service, {FeatureName}Service>();`

4. Use from Command or ViewModel:
   - Resolve: `ServiceLocator.GetRequiredService<I{FeatureName}Service>()`
   - Call methods

**Key Pattern for Service Methods:**
```csharp
public void DoWork(Document doc, SomeInput input)
{
    _transactionService.Run(doc, "Do Work", _ =>
    {
        // All document modifications here
        doc.Modify(element, ...);
    });
}
```

### New Shared Utility

**Steps:**

1. Create utility class:
   - File: `src/Utilities/{FeatureName}Utils.cs` or `LECG.Core/...`
   - Static methods or helper class

2. Use from anywhere:
   - No registration needed; direct reference
   - Example: `CategoryUtils.GetCategoryName(element)`

### New ViewModel Validator

**Steps:**

1. Create validator:
   - File: `src/Validation/Validators/{FeatureName}ViewModelValidator.cs`
   - Inherit: `AbstractValidator<{FeatureName}ViewModel>`
   - Define rules in constructor

2. Validator is auto-discovered:
   - DI scans assembly in `Bootstrapper.ConfigureServices()` via `AddValidatorsFromAssemblyContaining()`
   - No manual registration needed

3. Automatically invoked:
   - `BaseViewModel.CanApply()` calls `IValidationService.TryValidate(this, out message)`

---

## Special Directories

### `.planning/codebase/` (Codebase Analysis)
- Purpose: Architecture and structure analysis documents (this file)
- Generated by: `/lecg-map`
- Tracked: Yes; refreshed when the architecture changes

### `docs/deployment/` (Deployment Runbook)
- `LECG.addin.template`: Revit add-in manifest template (not deployed; live at `%APPDATA%\...`)
- `README.md`: Deployment instructions and troubleshooting

### `docs/ai/` (Repository Context)
- `repo-context.md`: Stable repository knowledge
- `revit-smoke-test.md`: Manual smoke-test checklist

### `.github/workflows/` (CI/CD)
- `build.yml`, `test.yml`: GitHub Actions for build and test
- Deploy script: Skips auto-deploy on CI

### `.claude/` (Claude Project Settings)
- `settings.local.json`: Model, token budget, local preferences
- `projects/`: Project memory (per-user)

---

## Build & Deployment

### Build Paths

- **Source:** `src/`, `LECG.Core/`, `LECG.Tests/`
- **Output:** `bin/x64/{config}/net8.0-windows/` (main assembly) + individual project outputs
- **Artifacts:** LECG.dll, LECG.pdb, LECG.deps.json

### Deployment Target

- **Live Path:** `%APPDATA%\Autodesk\Revit\Addins\2026\LECG\`
- **Manifest:** `%APPDATA%\Autodesk\Revit\Addins\2026\LECG.addin` (manually installed)
- **Trigger:** MSBuild `DeployToRevit` target after build (unless `-p:SkipRevitDeploy=true`)

### CI Behavior

- **Condition:** `GITHUB_ACTIONS` or `CI` env var set
- **Action:** Skips deployment; only builds and tests
- **Command:** `dotnet build -p:SkipRevitDeploy=true` (default for validation)

---

## Generated/Excluded Directories

### `bin/`, `obj/`
- Generated by .NET build process
- Excluded from git

### `LECG.Tests/bin/`, `LECG.Tests/obj/`
- Test-specific build output

### `.git/`, `.github/`
- Git repository and CI workflows (committed)

### `node_modules/`, `.venv/`
- Not applicable (C# project; no npm or Python deps)

---

## Practice Candidates

### [Observed] (scope: project) File Naming: {FeatureName} Pattern
- **Evidence:** `src/Commands/AlignEdgesCommand.cs`, `src/ViewModels/AlignEdgesViewModel.cs`, `src/Views/AlignEdgesView.xaml`, `src/Services/Alignment/IAlignEdgesService.cs`, `src/Validation/Validators/AlignEdgesViewModelValidator.cs`
- **Why it matters:** Clear traceability between command, VM, view, service, validator; predictable file discovery
- **Preserve by:** New features follow exact name alignment across all 5 files; e.g., if command is `SomeCommand`, VM is `SomeViewModel`, View is `SomeView`, etc.

### [Observed] (scope: project) Service Organization by Domain Folder
- **Evidence:** `src/Services/Alignment/`, `src/Services/CadConversion/`, `src/Services/Materials/` (8 domain folders), each with interface + implementation classes
- **Why it matters:** Logical grouping, easy navigation, clear feature boundaries
- **Preserve by:** New feature logic always creates `src/Services/{DomainName}/` folder; group related services together

### [Observed] (scope: project) Validator Auto-Discovery via Assembly Scanning
- **Evidence:** `src/Core/Bootstrapper.cs:77` (AddValidatorsFromAssemblyContaining), `src/Validation/Validators/` (all validators inherit AbstractValidator)
- **Why it matters:** New validators don't require manual DI registration; Scrutor scans and registers automatically
- **Preserve by:** All validators must inherit AbstractValidator, live in Validators/ folder, follow naming convention; never manually register

### [Observed] (scope: project) Partial ViewModels with Source-Gen Properties
- **Evidence:** `src/ViewModels/BaseViewModel.cs:10` (partial class), `src/ViewModels/AlignEdgesViewModel.cs` (partial with [ObservableProperty])
- **Why it matters:** CommunityToolkit.Mvvm generates INotifyPropertyChanged boilerplate at compile time; type-safe binding
- **Preserve by:** All ViewModels must be `partial class`, all bindable properties must have `[ObservableProperty]` attribute, never use manual property setters

### [Inferred] (scope: project) View Code-Behind Minimal (Initialization Only)
- **Evidence:** `src/Views/AlignEdgesView.xaml.cs:1-25` (mostly InitializeComponent, Initialize method)
- **Why it matters:** UI logic in ViewModel, code-behind only for required initialization and Revit context setup
- **Preserve by:** View code-behind should be <50 lines; put logic in ViewModel or event binding

### [Observed] (scope: service-layer) All Transaction Sites Use ITransactionService
- **Evidence:** `src/Services/Infrastructure/TransactionService.cs:31,70` (raw Transaction), 71+ call sites via interface
- **Why it matters:** Centralized transaction naming, failure handling, conditional rollback; prevents orphaned transactions
- **Preserve by:** New service writes always call `_transactionService.Run()` or `RunConditional()`; raw Transaction only inside TransactionService

### [Open] (scope: unknown) No Shared Collector Convention
- **Evidence:** 104+ FilteredElementCollector sites, no central cache or helper pattern identified
- **Why it matters:** Risk of duplicated logic if pattern emerges
- **Preserve by:** If same collector pattern (e.g., `OfClass().OfCategory()`) appears 3+ times, extract to utility class in `src/Utilities/`

