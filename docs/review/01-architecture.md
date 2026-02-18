# Architecture Overview

## Document Purpose

This document provides a comprehensive overview of the LECG Revit 2026 plugin architecture, including technology stack, design patterns, application flow, and key architectural decisions.

**Target Audience**: Developers, architects, and technical stakeholders

**Last Updated**: February 2026

---

## Executive Summary

The LECG plugin is a modern Revit 2026 add-in built with .NET 8.0 and WPF, following MVVM architectural patterns. The codebase comprises ~8,695 lines of C# code across 112 source files, organized into a clean layered architecture with clear separation of concerns.

**Key Characteristics**:
- **Pattern**: MVVM with Dependency Injection
- **Platform**: .NET 8.0 Windows (x64), WPF
- **API**: Revit 2026 API
- **Build Status**: ✅ Clean (3 nullable warnings only)
- **Size**: Medium (~8.7K LOC)
- **Maturity**: Active development, well-structured

---

## Technology Stack

### Core Framework
- **.NET 8.0 Windows (x64)**
  - Target Framework: `net8.0-windows`
  - Platform: x64 only (Revit requirement)
  - Nullable reference types: Enabled
  - Implicit usings: Enabled

### Revit Integration
- **Revit API 2026**
  - `RevitAPI.dll` - Core Revit API
  - `RevitAPIUI.dll` - UI extensions
  - Reference path: `C:\Program Files\Autodesk\Revit 2026\`
  - `SpecificVersion: false` - Allow version flexibility
  - `Private: false` - Don't copy to output (use Revit's loaded assemblies)

### UI Framework
- **Windows Presentation Foundation (WPF)**
  - `UseWPF: true` in project file
  - XAML-based UI definitions
  - Data binding with INotifyPropertyChanged
  - Custom window base class (`LecgWindow`)

### Key NuGet Packages

| Package | Version | Purpose | Notes |
|---------|---------|---------|-------|
| **CommunityToolkit.Mvvm** | 8.2.2 | MVVM framework | Source generators for `[ObservableProperty]` and `[RelayCommand]` |
| **Microsoft.Extensions.DependencyInjection** | 8.0.0 | DI container | `ExcludeAssets="runtime"` to use Revit's loaded version |
| **Microsoft.Extensions.DependencyInjection.Abstractions** | 8.0.0 | DI abstractions | `ExcludeAssets="runtime"` to use Revit's loaded version |
| **System.Windows.Extensions** | 8.0.0 | Windows-specific extensions | |

**Important**: The DI packages use `ExcludeAssets="runtime"` to avoid assembly version conflicts with Revit's already-loaded Microsoft.Extensions assemblies (Revit 2026 loads version 8.0.0).

---

## Architectural Patterns

### 1. MVVM (Model-View-ViewModel) Pattern

The codebase strictly follows MVVM for all UI components:

**Model Layer**: Revit API elements and business domain (not explicitly separated into Model classes)
**View Layer**: XAML files with minimal code-behind
**ViewModel Layer**: Presentation logic with data binding

**Benefits**:
- Clear separation of UI and business logic
- Testable presentation logic
- Reusable ViewModels
- Designer-developer workflow support

### 2. Service Layer Pattern

All Revit API interactions are encapsulated in **Services**:

```
Commands → Services → Revit API
   ↓          ↓
ViewModels  Logging/Progress Callbacks
```

**Characteristics**:
- Interface-based design (`ISexyRevitService`, `IMaterialService`, etc.)
- Registered as **Singletons** in DI container
- Accept callbacks for logging and progress reporting
- Handle their own transactions (giving commands flexibility)

**Benefits**:
- Revit API isolated from UI code
- Business logic is testable (mock Revit API interactions)
- Reusable across multiple commands
- Consistent error handling and logging

### 3. Dependency Injection with Service Locator

**Why Service Locator?**

Revit instantiates `IExternalCommand` implementations via reflection with a **parameterless constructor**. This prevents constructor injection.

**Solution**:
1. **Bootstrapper** initializes DI container at startup
2. **ServiceLocator** provides static access to `IServiceProvider`
3. Commands resolve dependencies via `ServiceLocator.GetRequiredService<T>()`

```csharp
// In App.OnStartup:
Bootstrapper.Initialize(); // Sets up DI container
ServiceLocator.Initialize(provider); // Makes provider accessible

// In Command.Execute:
var service = ServiceLocator.GetRequiredService<ISexyRevitService>();
```

**Trade-off**: Service Locator is an anti-pattern in normal DI scenarios, but it's the pragmatic solution for Revit's constraints.

### 4. Command Pattern with Base Class

All commands extend `RevitCommand` base class:

**Base Class Responsibilities**:
- Exception handling (catches all exceptions, shows user-friendly errors)
- Logging setup (`Logger.Instance.Clear()`, dispatcher setup)
- **Optional** automatic transaction management (via `TransactionName` property)
- Document access (`Doc`, `UIDoc` properties)
- Log window management

**Command Responsibilities**:
- Override `Execute(UIDocument uiDoc, Document doc)`
- Optionally set `TransactionName` for auto-transaction
- Load/save settings via `SettingsManager`
- Show UI dialogs
- Delegate work to services

**Benefits**:
- Consistent error handling across all commands
- Reduces boilerplate (transaction management, logging setup)
- Centralized command behavior modifications

### 5. Settings Persistence Pattern

User preferences are persisted using **SettingsManager**:

```csharp
// Load settings (deserialize from JSON)
var settings = SettingsManager.Load<SexyRevitViewModel>("SexyRevitSettings.json");

// Show dialog with settings as DataContext
var dialog = new SexyRevitView(settings);
if (dialog.ShowDialog() != true) return;

// Save settings (serialize to JSON)
SettingsManager.Save(settings, "SexyRevitSettings.json");
```

**Storage**: ViewModels are serialized as JSON to `%AppData%\LECG\` or similar.

**Benefits**:
- Settings persist across Revit sessions
- ViewModels are directly serializable (no mapping layer)
- Type-safe settings access

---

## Application Flow

### Startup Sequence

```
1. Revit Loads Plugin
   ↓
2. App.OnStartup (IExternalApplication)
   ↓
3. Assembly Resolution Handler (for DI version conflicts)
   ↓
4. Bootstrapper.Initialize()
   - Create ServiceCollection
   - Register Services (13 services)
   - Register ViewModels (3 currently, should be 18)
   - Register Views (1 currently, should be 18)
   - Build ServiceProvider
   ↓
5. ServiceLocator.Initialize(provider)
   ↓
6. RibbonService.InitializeRibbon()
   - Create "LECG" tab
   - Create 6 panels (Home, Health, Standards, Toposolids, Align, Visualization)
   - Add 19+ buttons
   ↓
7. Revit UI Ready
```

**Entry Point**: `src/App.cs` (`IExternalApplication` implementation)

### Command Execution Flow

```
1. User Clicks Ribbon Button
   ↓
2. Revit Instantiates Command via Reflection
   - Calls parameterless constructor
   - Calls IExternalCommand.Execute(commandData, ref message, elements)
   ↓
3. RevitCommand.Execute (base class)
   - Extract UIDoc, Doc from commandData
   - Reset Logger (clear previous logs, set dispatcher)
   - Check if TransactionName is set
       ↓ YES: Wrap Execute() in Transaction
       ↓ NO: Call Execute() directly (command handles transactions)
   - Call abstract Execute(UIDocument, Document)
   - Catch exceptions → show error dialog, return Result.Failed
   ↓
4. ConcreteCommand.Execute (derived class)
   - Load settings from JSON (SettingsManager)
   - Show WPF dialog (pass settings as DataContext)
   - User interacts, clicks OK or Cancel
       ↓ Cancel: Return early
   - Save settings to JSON (SettingsManager)
   - Resolve service via ServiceLocator
       var service = ServiceLocator.GetRequiredService<ISexyRevitService>();
   - Show log window (ShowLogWindow)
   - Call service method (pass callbacks for logging and progress)
       service.ApplyBeauty(doc, view, settings, Log, UpdateProgress);
   - Update progress to 100%
   - Log completion message
   ↓
5. Service Method (e.g., SexyRevitService.ApplyBeauty)
   - Accept Document, settings, and optional callbacks
   - Create transaction (or use command's transaction)
   - Read Revit elements via Revit API
   - Modify elements
   - Commit transaction
   - Call logging/progress callbacks throughout
   ↓
6. Return to Revit
   - Result.Succeeded or Result.Failed
```

**Key Points**:
- **Commands** are thin orchestrators (UI lifecycle, settings, service coordination)
- **Services** contain all business logic and Revit API interactions
- **Logging** is centralized via `Logger.Instance` with UI thread marshaling
- **Transactions** can be managed automatically (base class) or manually (in services)

### Ribbon UI Initialization

The `RibbonService` creates the plugin's UI:

**Structure**:
```
LECG Tab
├── Home Panel
│   └── Home Button (launcher/router)
├── Health Panel
│   ├── Purge
│   └── Clean Schemas
├── Standards Panel
│   ├── Search/Replace
│   └── Filter Copy
├── Toposolids Panel
│   ├── Assign Material
│   ├── Offset Elevations
│   ├── Reset Slabs
│   ├── Simplify Points
│   ├── Align Edges
│   └── Update Contours
├── Align Panel
│   └── Align Elements (sub-menu: Left, Center, Right, Top, Middle, Bottom, Distribute H/V)
└── Visualization Panel
    ├── Sexy Revit
    └── Render Appearance Match
```

**Button Configuration**:
- Uses `UIConstants` for text, tooltips, names
- Uses `AppImages` for icons
- Sets `ProjectDocumentAvailability` for most commands (requires active project document)

---

## Project Structure

```
LECG/
├── src/
│   ├── Commands/              (19 files, ~1,400 LOC)
│   │   ├── *Command.cs        - IExternalCommand implementations
│   │   └── AlignCommands.cs   - 8 alignment commands in one file
│   │
│   ├── Configuration/         (3 files, ~250 LOC)
│   │   ├── AppConstants.cs    - Tab/panel names
│   │   ├── UIConstants.cs     - Button text, tooltips
│   │   └── RevitConstants.cs  - Revit-specific constants
│   │
│   ├── Core/                  (6 files, ~650 LOC)
│   │   ├── Bootstrapper.cs    - DI container setup
│   │   ├── ServiceLocator.cs  - Static service provider access
│   │   ├── RevitCommand.cs    - Base class for commands
│   │   ├── SelectionFilters.cs - ISelectionFilter implementations
│   │   ├── ProjectDocumentAvailability.cs - Command availability logic
│   │   ├── FamilyDocumentAvailability.cs  - Family doc availability
│   │   └── Ribbon/
│   │       ├── RibbonService.cs    - Ribbon initialization
│   │       ├── IRibbonService.cs   - Interface
│   │       ├── RibbonFactory.cs    - Button/panel creation helpers
│   │       └── RibbonButtonConfig.cs - Config data class
│   │
│   ├── Interfaces/            (5 files, ~200 LOC) ⚠️ Inconsistent location
│   │   └── I*Service.cs       - Service interfaces (old location)
│   │
│   ├── Services/              (16 files, ~3,200 LOC)
│   │   ├── *Service.cs        - Service implementations
│   │   ├── SettingsManager.cs - JSON settings persistence
│   │   ├── Interfaces/        (9 files) ⚠️ New interface location
│   │   │   └── I*Service.cs   - Service interfaces
│   │   └── Logging/
│   │       ├── Logger.cs      - Singleton logger
│   │       └── LogEntry.cs    - Log entry data class
│   │
│   ├── Utils/                 (~5 files, ~200 LOC)
│   │   ├── ImageUtils.cs      - Image processing
│   │   ├── MathConverter.cs   - WPF value converter
│   │   ├── EnumBindingSource.cs - Enum → ComboBox binding
│   │   └── FamilyInstanceFilter.cs - ISelectionFilter for families
│   │
│   ├── ViewModels/            (18 files, ~1,900 LOC)
│   │   ├── BaseViewModel.cs   - Base class with INotifyPropertyChanged
│   │   ├── *ViewModel.cs      - Feature-specific ViewModels
│   │   └── Components/
│   │       └── SelectionViewModel.cs - Reusable selection component VM
│   │
│   ├── Views/                 (18 files + XAML, ~900 LOC code-behind)
│   │   ├── *View.xaml         - WPF XAML UI definitions
│   │   ├── *View.xaml.cs      - Code-behind (minimal)
│   │   ├── Base/
│   │   │   └── LecgWindow.cs  - Base window class
│   │   └── Components/
│   │       └── SelectionControl.xaml - Reusable selection UI
│   │
│   ├── Resources/
│   │   ├── Base/              - Base resource dictionaries
│   │   └── Images/            - Image resources
│   │
│   └── App.cs                 - IExternalApplication entry point
│
├── img/                       - Legacy image location
├── bin/                       - Build outputs
├── obj/                       - Intermediate build files
├── docs/                      - Documentation (this review)
├── LECG.csproj                - Project file
├── LECG.sln                   - Solution file
└── global.json                - SDK version specification
```

**Size Breakdown**:
- **Commands**: ~16% of code (thin orchestrators)
- **Services**: ~37% of code (core business logic)
- **ViewModels**: ~22% of code (presentation logic)
- **Views**: ~10% of code (code-behind, mostly boilerplate)
- **Core/Config/Utils**: ~15% of code (infrastructure)

---

## Key Design Decisions

### 1. Why Service Locator Instead of Pure Constructor Injection?

**Decision**: Use Service Locator pattern via static `ServiceLocator` class.

**Rationale**:
- Revit's `IExternalCommand` requires parameterless constructor (reflection-based instantiation)
- Cannot use constructor injection in commands
- Service Locator provides a pragmatic bridge between Revit's constraints and modern DI practices

**Trade-offs**:
- ✅ Enables DI in Revit commands
- ✅ Centralized service resolution
- ❌ Hides dependencies (not explicit in constructor)
- ❌ Service Locator is generally an anti-pattern (but necessary here)

**Alternative Considered**: Property injection - rejected because it's more verbose and doesn't add value.

### 2. Why Singletons for Services?

**Decision**: Register all services as **Singletons** in DI container.

**Rationale**:
- Services are stateless (they operate on Revit Document passed as parameter)
- Singleton lifetime improves performance (no repeated instantiation)
- Revit commands are short-lived, but services can be reused across multiple command invocations

**Trade-offs**:
- ✅ Better performance
- ✅ Simpler lifecycle management
- ⚠️ Services must be thread-safe and stateless (currently not an issue, as Revit API is single-threaded)

### 3. Why Transient Lifetime for ViewModels?

**Decision**: Register ViewModels as **Transient** (currently incomplete, only 3 registered).

**Rationale**:
- Each dialog invocation should have a fresh ViewModel instance
- Settings are loaded from JSON per command execution
- Transient prevents state leakage between command invocations

**Current Issue**: Most ViewModels are not registered; commands use `new` keyword directly.

### 4. Why Optional Automatic Transaction Management?

**Decision**: `RevitCommand` base class provides **optional** automatic transaction via `TransactionName` property.

**Rationale**:
- Some commands need a single transaction for all work → set `TransactionName`
- Some commands need multiple transactions, or transactions within services → set `TransactionName = null`
- Provides flexibility while reducing boilerplate for simple cases

**Examples**:
- `SexyRevitCommand`: Sets `TransactionName = null` (service handles transaction)
- `PurgeCommand`: Sets `TransactionName = "Purge Unused"` (single transaction)

### 5. Why Callback Pattern for Logging/Progress?

**Decision**: Services accept `Action<string>` for logging and `Action<double, string>` for progress.

**Rationale**:
- Services are decoupled from UI (testable without WPF)
- Commands provide callbacks that update UI (log window, progress bar)
- Services can be called from non-UI contexts (tests, batch scripts)

**Example**:
```csharp
service.ApplyBeauty(doc, view, settings, Log, UpdateProgress);
//                                        ^^^  ^^^^^^^^^^^^^^
//                                    Callbacks from RevitCommand
```

### 6. Why CommunityToolkit.Mvvm Instead of Manual INotifyPropertyChanged?

**Decision**: Use **CommunityToolkit.Mvvm** source generators.

**Rationale**:
- Reduces boilerplate (no manual `OnPropertyChanged` calls)
- `[ObservableProperty]` generates properties with change notification
- `[RelayCommand]` generates command implementations
- Compile-time code generation (no runtime reflection)

**Example**:
```csharp
[ObservableProperty]
private string _title = "LECG Tool";

// Generates:
public string Title
{
    get => _title;
    set => SetProperty(ref _title, value);
}
```

---

## Dependency Graph

### Top-Level Dependencies

```
App.cs
  └─> Bootstrapper.Initialize()
      └─> ServiceCollection
          ├─> Services (Singletons)
          ├─> ViewModels (Transients) ⚠️ Incomplete
          └─> Views (Transients) ⚠️ Incomplete
      └─> ServiceLocator.Initialize(provider)
      └─> RibbonService.InitializeRibbon()

Commands
  ├─> RevitCommand (base class)
  ├─> ServiceLocator.GetRequiredService<T>() → Services
  ├─> SettingsManager → ViewModels (as settings)
  └─> Views (new keyword, or will use DI after refactoring)

Services
  ├─> Revit API (RevitAPI.dll, RevitAPIUI.dll)
  ├─> Logger.Instance (logging)
  └─> Callbacks (Action<string>, Action<double, string>)

ViewModels
  ├─> BaseViewModel (CommunityToolkit.Mvvm.ObservableObject)
  └─> No direct Revit API dependencies ✅

Views
  ├─> ViewModels (DataContext binding)
  └─> LecgWindow (base class for consistent window behavior)
```

### Service Dependencies

Most services are **independent** (no inter-service dependencies). Some exceptions:

- **RibbonService** → Uses UIConstants, AppConstants, AppImages
- **SettingsManager** → Generic, depends on nothing
- **Logger** → Singleton, depends on Dispatcher (for UI thread marshaling)

**Low coupling** between services is a strength of the architecture.

---

## Assembly Resolution Strategy

**Problem**: Revit 2026 loads `Microsoft.Extensions.DependencyInjection` version 8.0.0. If the plugin also includes this assembly, version conflicts can occur.

**Solution** (in `App.cs`):

1. **Project-Level**: `ExcludeAssets="runtime"` in package references (don't include DLLs in output)
2. **Runtime**: `AssemblyResolve` event handler checks if Revit already loaded the assembly
3. **Fallback**: If not loaded, load from plugin's directory

```csharp
AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
{
    var requestedName = new AssemblyName(args.Name);
    if (requestedName.Name.StartsWith("Microsoft.Extensions.DependencyInjection"))
    {
        // 1. Check if already loaded (use Revit's version)
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name == requestedName.Name) return asm;
        }

        // 2. Load from plugin directory
        string assemblyPath = Path.Combine(folderPath, requestedName.Name + ".dll");
        if (File.Exists(assemblyPath)) return Assembly.LoadFrom(assemblyPath);
    }
    return null;
};
```

**Result**: No version conflicts, plugin uses Revit's loaded DI assemblies.

---

## Build Configuration

**Target Framework**: `net8.0-windows`
**Platform**: `x64` (Revit requirement)
**Output Type**: Class Library (DLL)
**Assembly Name**: `LECG.dll`

**Compiler Settings**:
- `Nullable: enable` - Nullable reference types
- `ImplicitUsings: enable` - Implicit global usings
- `UseWPF: true` - Enable WPF support
- `CopyLocalLockFileAssemblies: true` - Copy dependencies to output
- `NoWarn: MSB3277;CS0436` - Suppress specific warnings

**Build Output**:
- `bin/Debug/net8.0-windows/LECG.dll`
- `bin/Debug/net8.0-windows/LECG.pdb`
- Dependencies (CommunityToolkit.Mvvm, etc.)

**Deployment**: Copy LECG.dll and dependencies to Revit add-ins folder:
- `%AppData%\Autodesk\Revit\Addins\2026\`
- Requires `.addin` manifest file (not currently in repository ⚠️)

---

## Testing Strategy

**Current State**: No unit tests found in repository.

**Recommended Testing Approach**:

### 1. Service Layer Testing (High Value)
- Services are interface-based → easy to mock
- Test business logic independently of Revit API
- Use Moq or NSubstitute to mock `Document`, `View`, etc.

**Example**:
```csharp
[Fact]
public void ApplyBeauty_SetsDisplayStyle_ToRealistic()
{
    // Arrange
    var mockDoc = new Mock<Document>();
    var mockView = new Mock<View>();
    var settings = new SexyRevitViewModel { UseConsistentColors = true };
    var service = new SexyRevitService();

    // Act
    service.ApplyBeauty(mockDoc.Object, mockView.Object, settings);

    // Assert
    mockView.VerifySet(v => v.DisplayStyle = DisplayStyle.Realistic);
}
```

### 2. ViewModel Testing (Medium Value)
- ViewModels extend `ObservableObject` → testable without WPF
- Test property change notifications
- Test command logic

### 3. Integration Testing (Manual, High Value)
- Load plugin in Revit
- Test each command with sample Revit files
- Verify log window output
- Check for exceptions in Revit journal files

---

## Performance Considerations

### Startup Performance
- **DI Container Initialization**: Fast (~ms), one-time cost
- **Ribbon Creation**: Fast (~ms), one-time cost
- **Total Startup**: < 100ms (negligible impact on Revit startup)

### Command Execution Performance
- **Service Resolution**: Fast (singleton lookup)
- **Settings Load/Save**: Fast (small JSON files)
- **Dialog Show**: Instant (WPF)
- **Revit API Operations**: Depends on document size and operation complexity

**Bottlenecks** (if any):
- Large Revit documents (millions of elements)
- Complex geometric operations (alignment, simplification)
- Purge operations (iterating all unused elements)

**Optimization Strategies**:
- Use `FilteredElementCollector` with filters (avoid `WhereElementIsNotElementType()` when possible)
- Batch transaction commits (commit once per command, not per element)
- Progress reporting (keep users informed during long operations)

---

## Security Considerations

### 1. Assembly Loading
- **Risk**: Malicious assemblies loaded via `AssemblyResolve`
- **Mitigation**: Only resolve Microsoft.Extensions.* assemblies, load from known plugin directory

### 2. Settings Persistence
- **Risk**: User settings stored as JSON in AppData
- **Mitigation**: Validate deserialized settings (current code doesn't validate ⚠️)
- **Recommendation**: Add schema validation or use typed settings classes

### 3. Revit API Access
- **Risk**: Plugin has full access to Revit document (read/write/delete)
- **Mitigation**: User must explicitly click commands (no automatic modifications)
- **Recommendation**: Add confirmation dialogs for destructive operations (purge, delete, etc.)

### 4. Log Files
- **Risk**: Log files may contain sensitive project information
- **Mitigation**: Logs are in-memory during command execution, not persisted by default
- **Recommendation**: Add option to export logs (user-initiated)

---

## Future Architecture Considerations

### 1. Complete DI Registration
- Register all 18 ViewModels and 18 Views
- Remove `new` keyword usage in commands
- Consistent dependency resolution

### 2. Interface Consolidation
- Keep all service interfaces in `src/Services/Interfaces/`
- Enforce `namespace LECG.Services.Interfaces` for that folder

### 3. Service Decomposition
- Break large services (CadConversionService 566 LOC) into smaller focused services
- Follow Single Responsibility Principle

### 4. Add Unit Tests
- Focus on service layer (business logic)
- Use mocking framework for Revit API
- Target >70% code coverage for services

### 5. Add `.addin` Manifest
- Create `LECG.addin` file for deployment
- Automate deployment (post-build copy to Revit add-ins folder)

### 6. Add Build Automation
- CI/CD pipeline (GitHub Actions or Azure DevOps)
- Automated testing
- Automated deployment to staging

---

## References

- **Revit API Documentation**: https://www.revitapidocs.com/2026/
- **CommunityToolkit.Mvvm**: https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/
- **Microsoft.Extensions.DependencyInjection**: https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection

---

## Conclusion

The LECG plugin demonstrates a well-thought-out architecture that balances modern .NET practices with Revit API constraints. The use of MVVM, DI, and service layer patterns results in maintainable, testable code. Key areas for improvement include completing DI registration, consolidating interfaces, and adding automated tests.

**Overall Architecture Grade**: **B+** (Very Good, with room for improvement)

**Next Steps**: See `09-refactoring-roadmap.md` for prioritized improvements.
