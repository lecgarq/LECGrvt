# Dependency Injection Guide

## Document Purpose

This document explains the dependency injection implementation in the LECG plugin, including why Service Locator is used, how to register services, and testing strategies.

**Target Audience**: Developers

**Last Updated**: February 2026

---

## Why Service Locator?

### The Revit Constraint

**Problem**: Revit instantiates `IExternalCommand` implementations via **reflection** with a **parameterless constructor**.

```csharp
// Revit's command instantiation (simplified):
var commandType = Type.GetType("LECG.Commands.SexyRevitCommand");
var instance = Activator.CreateInstance(commandType); // ← Requires parameterless constructor
var command = instance as IExternalCommand;
command.Execute(commandData, ref message, elements);
```

**This prevents constructor injection**:
```csharp
// ❌ This won't work - Revit can't provide dependencies
public SexyRevitCommand(ISexyRevitService service)
{
    _service = service;
}
```

### The Solution: Service Locator Pattern

**Pattern**: Static `ServiceLocator` provides access to the DI container.

```csharp
// ✅ This works - Command resolves dependencies at runtime
public class SexyRevitCommand : RevitCommand
{
    public override void Execute(UIDocument uiDoc, Document doc)
    {
        // Resolve service from static ServiceLocator
        var service = ServiceLocator.GetRequiredService<ISexyRevitService>();
        service.ApplyBeauty(doc, view, settings, Log, UpdateProgress);
    }
}
```

---

## DI Architecture

### Startup Flow

```
1. Revit Loads Plugin
   ↓
2. App.OnStartup (IExternalApplication)
   ↓
3. Bootstrapper.Initialize()
   - Create ServiceCollection
   - ConfigureServices() → Register services as Singletons
   - ConfigureViewModels() → Register VMs as Transients
   - ConfigureViews() → Register views as Transients
   - Build ServiceProvider
   ↓
4. ServiceLocator.Initialize(provider)
   - Store provider in static property
   ↓
5. Commands can now resolve dependencies via ServiceLocator
```

### Component Lifetimes

| Component Type | Lifetime | Reason |
|----------------|----------|--------|
| **Services** | Singleton | Stateless, can be reused across commands |
| **ViewModels** | Transient | Hold UI state, fresh instance per dialog |
| **Views** | Transient | Fresh instance per dialog show |

---

## Bootstrapper Implementation

### File: `src/Core/Bootstrapper.cs`

```csharp
public static class Bootstrapper
{
    public static void Initialize()
    {
        var services = new ServiceCollection();

        ConfigureServices(services);
        ConfigureViewModels(services);
        ConfigureViews(services);

        var provider = services.BuildServiceProvider();
        ServiceLocator.Initialize(provider);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core
        services.AddSingleton<IRibbonService, RibbonService>();
        services.AddSingleton<ILogger>(_ => Logger.Instance);

        // Domain Services (all Singletons)
        services.AddSingleton<ISlabService, SlabService>();
        services.AddSingleton<IOffsetService, OffsetService>();
        services.AddSingleton<IMaterialService, MaterialService>();
        services.AddSingleton<IPurgeService, PurgeService>();
        services.AddSingleton<ISchemaCleanerService, SchemaCleanerService>();
        services.AddSingleton<ISexyRevitService, SexyRevitService>();
        services.AddSingleton<ISearchReplaceService, SearchReplaceService>();
        services.AddSingleton<IFamilyConversionService, FamilyConversionService>();
        services.AddSingleton<IAlignEdgesService, AlignEdgesService>();
        services.AddSingleton<IToposolidService, ToposolidService>();
        services.AddSingleton<IChangeLevelService, ChangeLevelService>();
        services.AddSingleton<ISimplifyPointsService, SimplifyPointsService>();
        services.AddSingleton<IAlignElementsService, AlignElementsService>();
        services.AddSingleton<ICadConversionService, CadConversionService>();
    }

    private static void ConfigureViewModels(IServiceCollection services)
    {
        services.AddTransient<ResetSlabsVM>();
        services.AddTransient<ConvertFamilyViewModel>();
        services.AddTransient<ConvertCadViewModel>();
        services.AddTransient<SexyRevitViewModel>();
        services.AddTransient<PurgeViewModel>();
        services.AddTransient<SearchReplaceViewModel>();
        services.AddTransient<AssignMaterialViewModel>();
        services.AddTransient<OffsetElevationsVM>();
        services.AddTransient<AlignEdgesViewModel>();
        services.AddTransient<UpdateContoursViewModel>();
        services.AddTransient<ChangeLevelViewModel>();
        services.AddTransient<AlignElementsViewModel>();
        services.AddTransient<SimplifyPointsViewModel>();
        services.AddTransient<FilterCopyViewModel>();
        services.AddTransient<LogViewModel>();
        services.AddTransient<RenderAppearanceViewModel>();
    }

    private static void ConfigureViews(IServiceCollection services)
    {
        services.AddTransient<Views.ResetSlabsView>();
        services.AddTransient<Views.SexyRevitView>();
        services.AddTransient<Views.PurgeView>();
        services.AddTransient<Views.SearchReplaceView>();
        services.AddTransient<Views.AssignMaterialView>();
        services.AddTransient<Views.OffsetElevationsView>();
        services.AddTransient<Views.AlignEdgesView>();
        services.AddTransient<Views.UpdateContoursView>();
        services.AddTransient<Views.ChangeLevelView>();
        services.AddTransient<Views.AlignElementsView>();
        services.AddTransient<Views.SimplifyPointsView>();
        services.AddTransient<Views.FilterCopyView>();
        services.AddTransient<Views.ConvertCadView>();
        services.AddTransient<Views.ConvertFamilyView>();
        services.AddTransient<Views.LogView>();
        services.AddTransient<Views.HomeView>();
        services.AddTransient<Views.AlignDashboardView>();
        services.AddTransient<Views.RenderAppearanceView>();
    }
}
```

---

## ServiceLocator Implementation

### File: `src/Core/ServiceLocator.cs`

```csharp
public static class ServiceLocator
{
    public static IServiceProvider? ServiceProvider { get; private set; }

    public static void Initialize(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public static T? GetService<T>()
    {
        return (T?)ServiceProvider?.GetService(typeof(T));
    }

public static T GetRequiredService<T>() where T : notnull
{
        if (ServiceProvider == null)
        {
            throw new InvalidOperationException("ServiceLocator has not been initialized.");
        }

        var service = ServiceProvider.GetService(typeof(T));

        if (service == null)
        {
            throw new InvalidOperationException($"Service of type {typeof(T).Name} could not be resolved.");
        }

    return (T)service;
}

public static T CreateWith<T>(params object[] args) where T : notnull
{
    if (ServiceProvider == null)
    {
        throw new InvalidOperationException("ServiceLocator has not been initialized.");
    }

    return ActivatorUtilities.CreateInstance<T>(ServiceProvider, args);
}
}
```

**Key Methods**:
- `GetService<T>()` - Returns `null` if service not found
- `GetRequiredService<T>()` - Throws exception if service not found (prefer this)
- `CreateWith<T>(args...)` - Creates registered types that require runtime arguments (e.g., `UIDocument`, `Document`, mode)

---

## Using DI in Commands

### Resolving Services

**Pattern**:
```csharp
public override void Execute(UIDocument uiDoc, Document doc)
{
    // Resolve service (throws if not registered)
    var service = ServiceLocator.GetRequiredService<ISexyRevitService>();

    // Use service
    service.ApplyBeauty(doc, view, settings, Log, UpdateProgress);
}
```

**Multiple Services**:
```csharp
public override void Execute(UIDocument uiDoc, Document doc)
{
    var materialService = ServiceLocator.GetRequiredService<IMaterialService>();
    var purgeService = ServiceLocator.GetRequiredService<IPurgeService>();

    materialService.AssignMaterial(doc, ...);
    purgeService.PurgeUnused(doc, ...);
}
```

**Optional Services** (rare):
```csharp
var optionalService = ServiceLocator.GetService<IOptionalService>();
if (optionalService != null)
{
    optionalService.DoWork();
}
```

---

## Registering New Components

### Adding a Service

**1. Create Interface** (`src/Services/Interfaces/IMyService.cs`):
```csharp
namespace LECG.Services.Interfaces
{
    public interface IMyService
    {
        void DoWork(Document doc, Action<string>? log = null);
    }
}
```

**2. Create Implementation** (`src/Services/MyService.cs`):
```csharp
namespace LECG.Services
{
    public class MyService : IMyService
    {
        public void DoWork(Document doc, Action<string>? log = null)
        {
            // Implementation
        }
    }
}
```

**3. Register in Bootstrapper** (`src/Core/Bootstrapper.cs`):
```csharp
private static void ConfigureServices(IServiceCollection services)
{
    // ... existing services

    services.AddSingleton<IMyService, MyService>(); // ← Add here
}
```

**4. Use in Command**:
```csharp
var service = ServiceLocator.GetRequiredService<IMyService>();
service.DoWork(doc, Log);
```

---

### Adding a ViewModel

**1. Create ViewModel** (`src/ViewModels/MyFeatureViewModel.cs`):
```csharp
public partial class MyFeatureViewModel : BaseViewModel
{
    [ObservableProperty]
    private string _inputValue = "";

    public MyFeatureViewModel()
    {
        Title = "My Feature";
    }
}
```

**2. Register in Bootstrapper** (`src/Core/Bootstrapper.cs`):
```csharp
private static void ConfigureViewModels(IServiceCollection services)
{
    // ... existing VMs

    services.AddTransient<MyFeatureViewModel>(); // ← Add here
}
```

**3. Resolve in Command**:
```csharp
// Resolve from DI
var vm = ServiceLocator.GetRequiredService<MyFeatureViewModel>();

// Optional: load persisted settings and copy into DI instance
var saved = SettingsManager.Load<MyFeatureViewModel>("MyFeatureSettings.json");
vm.SomeOption = saved.SomeOption;
```

---

### Adding a View

**1. Create View** (`src/Views/MyFeatureView.xaml` + `.xaml.cs`):
```csharp
public partial class MyFeatureView : LecgWindow
{
    public MyFeatureView(MyFeatureViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseAction = () =>
        {
            DialogResult = true;
            Close();
        };
    }
}
```

**2. Register in Bootstrapper** (`src/Core/Bootstrapper.cs`):
```csharp
private static void ConfigureViews(IServiceCollection services)
{
    // ... existing views

    services.AddTransient<Views.MyFeatureView>(); // ← Add here
}
```

**3. Use in Command**:
```csharp
var vm = ServiceLocator.GetRequiredService<MyFeatureViewModel>();
var view = ServiceLocator.CreateWith<Views.MyFeatureView>(vm, uiDoc);
```

---

## Testing with DI

### Unit Testing Services

**Pattern**: Mock dependencies, test service in isolation

**Example**: Testing `SexyRevitService`

```csharp
using Moq;
using Xunit;

public class SexyRevitServiceTests
{
    [Fact]
    public void ApplyBeauty_SetsDisplayStyle_ToRealistic()
    {
        // Arrange
        var mockDoc = new Mock<Document>();
        var mockView = new Mock<View>();
        var settings = new SexyRevitViewModel
        {
            UseConsistentColors = true
        };
        var service = new SexyRevitService();

        // Act
        service.ApplyBeauty(mockDoc.Object, mockView.Object, settings);

        // Assert
        mockView.VerifySet(v => v.DisplayStyle = DisplayStyle.Realistic);
    }

    [Fact]
    public void ApplyBeauty_LogsProgress()
    {
        // Arrange
        var mockDoc = new Mock<Document>();
        var mockView = new Mock<View>();
        var settings = new SexyRevitViewModel();
        var logMessages = new List<string>();
        Action<string> log = msg => logMessages.Add(msg);
        var service = new SexyRevitService();

        // Act
        service.ApplyBeauty(mockDoc.Object, mockView.Object, settings, log);

        // Assert
        Assert.Contains(logMessages, m => m.Contains("GRAPHICS"));
    }
}
```

---

### Integration Testing (Manual)

**Pattern**: Load plugin in Revit, run commands, verify behavior

**Checklist**:
1. **DI Initialization Test**:
   - Load plugin
   - Check Revit journal for startup errors
   - Verify ribbon appears

2. **Service Resolution Test**:
   - Click any command
   - Check journal for "Service of type X could not be resolved" errors
   - Verify command executes without DI errors

3. **Full Feature Test**:
   - Run each command
   - Verify expected behavior
   - Check for exceptions in journal

---

## Common DI Issues

### Issue 1: Service Not Registered

**Error**:
```
InvalidOperationException: Service of type IMyService could not be resolved.
```

**Fix**: Register in `Bootstrapper.ConfigureServices()`:
```csharp
services.AddSingleton<IMyService, MyService>();
```

---

### Issue 2: ServiceLocator Not Initialized

**Error**:
```
InvalidOperationException: ServiceLocator has not been initialized.
```

**Fix**: Ensure `Bootstrapper.Initialize()` is called in `App.OnStartup()`:
```csharp
public Result OnStartup(UIControlledApplication application)
{
    Core.Bootstrapper.Initialize(); // ← Must be called
    // ...
}
```

---

### Issue 3: Circular Dependency

**Error**: Stack overflow or initialization error

**Scenario**:
```csharp
// ServiceA depends on ServiceB
public class ServiceA : IServiceA
{
    public ServiceA(IServiceB serviceB) { }
}

// ServiceB depends on ServiceA
public class ServiceB : IServiceB
{
    public ServiceB(IServiceA serviceA) { } // ← Circular!
}
```

**Fix**: Refactor to remove circular dependency:
- Extract shared logic to a third service
- Use events/callbacks instead of direct dependency
- Redesign service boundaries

---

### Issue 4: ViewModel Not Transient

**Scenario**: ViewModel registered as Singleton, state persists between dialog shows

**Problem**:
```csharp
services.AddSingleton<MyViewModel>(); // ❌ Wrong lifetime
```

**Fix**:
```csharp
services.AddTransient<MyViewModel>(); // ✅ Fresh instance per dialog
```

---

## Best Practices

### ✅ Do

1. **Register all services in Bootstrapper**
   - Centralized configuration
   - Easy to see all dependencies

2. **Use `GetRequiredService<T>()` in commands**
   - Fails fast if service not registered
   - Clear error messages

3. **Keep services stateless**
   - Operate on `Document` passed as parameter
   - Singleton lifetime is safe

4. **Use interface-based services**
   - Enables mocking for tests
   - Clear contracts

5. **Validate DI setup at startup**
   - Call `GetRequiredService<T>()` for critical services in `App.OnStartup()`
   - Fail fast if misconfigured

### ❌ Don't

1. **Don't use `new` for services**
   - Defeats DI, hard to test
   - Use `ServiceLocator.GetRequiredService<T>()`

2. **Don't store state in Singleton services**
   - State leaks between command invocations
   - Use method parameters for state

3. **Don't pass `ServiceLocator` to services**
   - Services shouldn't resolve other services
   - Use constructor injection (in services, not commands)

4. **Don't register commands in DI**
   - Revit instantiates commands directly
   - Commands resolve dependencies, not vice versa

---

## Future Improvements

### 1. Runtime-Argument Creation Helper

**Current**: Implemented via `ServiceLocator.CreateWith<T>(params object[] args)`

**Goal**: Keep command code consistent when constructors need runtime values (`UIDocument`, `Document`, modes)

**Benefits**:
- No `new` for registered View/ViewModel types in commands
- Constructor dependencies still resolved by DI
- Runtime Revit context values can be passed explicitly

---

### 2. Optional Factory Pattern for Views

**Current**: Commands use `ServiceLocator.CreateWith<T>()`

**Goal**: Optionally wrap `CreateWith` in a dedicated view factory for stricter abstraction

**Pattern**:
```csharp
public interface IViewFactory
{
    TView Create<TView, TViewModel>(TViewModel viewModel)
        where TView : LecgWindow
        where TViewModel : BaseViewModel;
}

// Usage:
var viewFactory = ServiceLocator.GetRequiredService<IViewFactory>();
var view = viewFactory.Create<MyFeatureView, MyFeatureViewModel>(vm);
```

**Benefits**:
- Views can have dependencies injected
- Centralized view creation logic

---

### 3. Settings Service

**Current**: `SettingsManager` uses static methods

**Goal**: Create `ISettingsService` registered in DI

**Pattern**:
```csharp
public interface ISettingsService
{
    T Load<T>(string filename) where T : new();
    void Save<T>(T settings, string filename);
}

// Usage:
var settingsService = ServiceLocator.GetRequiredService<ISettingsService>();
var settings = settingsService.Load<MyViewModel>("MySettings.json");
```

**Benefits**:
- Testable settings persistence
- Can mock for unit tests

---

## Conclusion

The LECG plugin uses Service Locator pattern to bridge Revit's reflection-based command instantiation with modern DI practices:

**Pros**:
- ✅ Enables DI in Revit commands
- ✅ Interface-based services (testable)
- ✅ Centralized configuration (Bootstrapper)
- ✅ Clear service lifetimes (Singleton/Transient)

**Cons**:
- ❌ Service Locator is an anti-pattern (but necessary here)
- ❌ Dependencies not visible in constructor (hidden)

**Next Steps**:
- Complete ViewModel/View registration
- Consider factory pattern for views
- Add unit tests for services

For service patterns, see `04-patterns.md`. For architecture, see `01-architecture.md`.
