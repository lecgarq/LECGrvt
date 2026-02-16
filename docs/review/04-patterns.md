# Common Patterns Guide

## Document Purpose

This document provides concrete code examples for implementing common patterns in the LECG Revit plugin. Use these patterns as templates when adding new features.

**Target Audience**: Developers

**Last Updated**: February 2026

---

## Table of Contents

1. [Command Implementation Pattern](#command-implementation-pattern)
2. [Service Implementation Pattern](#service-implementation-pattern)
3. [ViewModel Implementation Pattern](#viewmodel-implementation-pattern)
4. [View Implementation Pattern](#view-implementation-pattern)
5. [Settings Persistence Pattern](#settings-persistence-pattern)
6. [Transaction Management Patterns](#transaction-management-patterns)
7. [Logging and Progress Reporting](#logging-and-progress-reporting)
8. [DI Registration Pattern](#di-registration-pattern)
9. [Ribbon Button Pattern](#ribbon-button-pattern)

---

## Command Implementation Pattern

### Basic Command with Service

**Use Case**: Most commands follow this pattern - load settings, show dialog, call service.

**Example**: Simplified from `SexyRevitCommand.cs`

```csharp
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Views;
using LECG.Services;
using LECG.ViewModels;

namespace LECG.Commands
{
    /// <summary>
    /// Command to beautify the current view with optimal visual settings.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SexyRevitCommand : RevitCommand
    {
        // Service handles transaction, so TransactionName = null
        protected override string? TransactionName => null;

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            View view = doc.ActiveView;
            if (view == null)
            {
                TaskDialog.Show("Error", "No active view.");
                return;
            }

            // 1. Load Settings (deserialize from JSON)
            var settings = SettingsManager.Load<SexyRevitViewModel>("SexyRevitSettings.json");

            // 2. Show Dialog
            SexyRevitView dialog = new SexyRevitView(settings);
            if (dialog.ShowDialog() != true) return; // User cancelled

            // 3. Save Settings (serialize to JSON)
            SettingsManager.Save(settings, "SexyRevitSettings.json");

            // 4. Show Log Window
            ShowLogWindow("Sexy Revit ✨");

            // 5. Log Header
            Log("Sexy Revit - View Beautification");
            Log("=================================");
            Log($"View: {view.Name}");
            Log("");

            // 6. Resolve Service from DI Container
            var service = ServiceLocator.GetRequiredService<ISexyRevitService>();

            // 7. Call Service (pass callbacks for logging and progress)
            service.ApplyBeauty(doc, view, settings, Log, UpdateProgress);

            // 8. Final Progress and Logging
            UpdateProgress(100, "Complete!");
            Log("");
            Log("=== COMPLETE ===");
            Log("✨ Your view is now sexy!");
        }
    }
}
```

**Key Points**:
- Extend `RevitCommand` base class
- Set `TransactionName` (`null` if service handles it, or transaction name for auto-transaction)
- Use `SettingsManager` for persistence
- Resolve services via `ServiceLocator.GetRequiredService<T>()`
- Pass `Log` and `UpdateProgress` callbacks to service
- Show log window with `ShowLogWindow()`

---

### Command with Manual Transaction

**Use Case**: Simple commands that need a single transaction.

**Example**: Simplified from `ResetSlabsCommand.cs`

```csharp
[Transaction(TransactionMode.Manual)]
public class ResetSlabsCommand : RevitCommand
{
    // Auto-transaction wrapper
    protected override string? TransactionName => "Reset Slabs";

    public override void Execute(UIDocument uiDoc, Document doc)
    {
        // Select slabs
        var slabs = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Floors)
            .WhereElementIsNotElementType()
            .ToElements();

        if (!slabs.Any())
        {
            TaskDialog.Show("Info", "No slabs found.");
            return;
        }

        ShowLogWindow("Reset Slabs");
        Log($"Resetting {slabs.Count} slabs...");

        // Resolve service
        var service = ServiceLocator.GetRequiredService<ISlabService>();

        // Call service (transaction is auto-managed by base class)
        service.ResetSlabElevations(doc, slabs, Log);

        Log("=== COMPLETE ===");
    }
}
```

**Key Points**:
- Set `TransactionName` to enable auto-transaction
- Base class wraps `Execute()` in `Transaction.Start()` / `Commit()`
- If exception occurs, transaction is auto-rolled back

---

### Command with Dialog and Confirmation

**Use Case**: Commands that need user confirmation before executing.

**Example**: Pattern from `AssignMaterialCommand.cs`

```csharp
[Transaction(TransactionMode.Manual)]
public class AssignMaterialCommand : RevitCommand
{
    protected override string? TransactionName => null; // Manual transaction in service

    public override void Execute(UIDocument uiDoc, Document doc)
    {
        // 1. Resolve Service
        var service = ServiceLocator.GetRequiredService<IMaterialService>();

        // 2. Initialize ViewModel & View
        var vm = new AssignMaterialViewModel(service);
        var view = new AssignMaterialView(vm, uiDoc);

        // 3. Set window owner (for proper modality)
        WindowInteropHelper helper = new WindowInteropHelper(view);
        helper.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

        // 4. Show Dialog
        bool? result = view.ShowDialog();

        // 5. Check if user confirmed and has selections
        if (result == true && vm.ShouldRun && vm.SelectedRefs.Any())
        {
            ShowLogWindow("Assigning Materials...");
            Log("Material Assignment");
            Log("==================");

            // 6. Execute service method
            service.AssignMaterials(doc, vm.SelectedRefs, vm.SelectedMaterial, Log, UpdateProgress);

            UpdateProgress(100, "Complete");
            Log("=== COMPLETE ===");
        }
    }
}
```

**Key Points**:
- Use `WindowInteropHelper` to set proper window owner (for modality over Revit)
- Check `dialog.ShowDialog() == true` for user confirmation
- Check ViewModel flags (`vm.ShouldRun`, `vm.HasSelection`, etc.)

---

## Service Implementation Pattern

### Basic Service with Transaction

**Use Case**: Service that performs Revit API operations within a transaction.

**Example**: Simplified from `SexyRevitService.cs`

```csharp
using System;
using Autodesk.Revit.DB;
using LECG.ViewModels;

namespace LECG.Services
{
    public class SexyRevitService : ISexyRevitService
    {
        public void ApplyBeauty(Document doc, View view, SexyRevitViewModel settings,
                                Action<string>? logCallback = null,
                                Action<double, string>? progressCallback = null)
        {
            if (view == null) return;

            // Helper lambdas for null-safe logging/progress
            Action<string> log = logCallback ?? (_ => { });
            Action<double, string> progress = progressCallback ?? ((_, __) => { });

            // Start transaction
            using (Transaction t = new Transaction(doc, "Sexy Revit"))
            {
                t.Start();

                // 1. Graphics Settings
                if (settings.UseConsistentColors)
                {
                    log("GRAPHICS & LIGHTING");
                    progress(10, "Applying sexy graphics...");

                    try
                    {
                        view.DisplayStyle = DisplayStyle.Realistic;
                        log("  ✓ Display Style: Realistic");
                    }
                    catch (Exception ex)
                    {
                        log($"  ⚠ Could not set display style: {ex.Message}");
                    }

                    if (settings.UseDetailFine)
                    {
                        view.DetailLevel = ViewDetailLevel.Fine;
                        log("  ✓ Detail Level: Fine");
                    }
                }

                // 2. Sun Settings (3D only)
                if (settings.ConfigureSun && view is View3D v3d)
                {
                    log("");
                    log("SUN SETTINGS");
                    progress(30, "Setting sun...");

                    try
                    {
                        SunAndShadowSettings? sunSettings = v3d.SunAndShadowSettings;
                        if (sunSettings != null)
                        {
                            sunSettings.SunAndShadowType = SunAndShadowType.StillImage;
                            log("  ✓ Sun Type: Still Image");
                        }
                    }
                    catch (Exception ex)
                    {
                        log($"  ⚠ Sun settings: {ex.Message}");
                    }
                }

                // 3. Hide Categories
                if (settings.HideGrids || settings.HideLevels)
                {
                    log("");
                    log("HIDING CATEGORIES");
                    progress(60, "Hiding categories...");

                    if (settings.HideGrids)
                    {
                        HideCategory(doc, view, BuiltInCategory.OST_Grids, log);
                    }

                    if (settings.HideLevels)
                    {
                        HideCategory(doc, view, BuiltInCategory.OST_Levels, log);
                    }
                }

                // Commit transaction
                t.Commit();
            }

            progress(100, "Complete");
            log("View beautification complete!");
        }

        private void HideCategory(Document doc, View view, BuiltInCategory category, Action<string> log)
        {
            try
            {
                Category cat = Category.GetCategory(doc, category);
                if (cat != null)
                {
                    view.SetCategoryHidden(cat.Id, true);
                    log($"  ✓ Hidden: {cat.Name}");
                }
            }
            catch (Exception ex)
            {
                log($"  ⚠ Could not hide {category}: {ex.Message}");
            }
        }
    }
}
```

**Key Points**:
- Accept optional logging and progress callbacks (`Action<string>?`, `Action<double, string>?`)
- Use null-coalescing for safe callback invocation
- Wrap all operations in `using (Transaction t = new Transaction(doc, "Name"))`
- Use try/catch for individual operations (fail gracefully)
- Log successes with `✓`, warnings with `⚠`, errors with `✗`
- Report progress at key milestones (0%, 25%, 50%, 100%)

---

### Service with FilteredElementCollector

**Use Case**: Services that query elements from the document.

**Pattern**:

```csharp
public void ProcessElements(Document doc, Category category, Action<string>? log = null)
{
    var log = log ?? (_ => { });

    // Collect elements
    var elements = new FilteredElementCollector(doc)
        .OfCategory(category)
        .WhereElementIsNotElementType()
        .ToElements();

    log($"Found {elements.Count} elements in category {category.Name}");

    using (Transaction t = new Transaction(doc, "Process Elements"))
    {
        t.Start();

        foreach (var elem in elements)
        {
            try
            {
                // Process element
                ProcessElement(elem);
                log($"  ✓ Processed: {elem.Name}");
            }
            catch (Exception ex)
            {
                log($"  ⚠ Skipped {elem.Id}: {ex.Message}");
            }
        }

        t.Commit();
    }
}
```

**Key Points**:
- Use `WhereElementIsNotElementType()` to exclude type elements (when needed)
- Use `ToElements()` or `ToElementIds()` depending on need
- Process in batches if large collections (commit every N elements)
- Log progress per element or per batch

---

## ViewModel Implementation Pattern

### Basic ViewModel with Properties

**Use Case**: Simple ViewModels with bindable properties.

**Example**: Simplified from `SexyRevitViewModel.cs`

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LECG.ViewModels
{
    /// <summary>
    /// ViewModel for view beautification settings.
    /// </summary>
    public partial class SexyRevitViewModel : BaseViewModel
    {
        // Observable properties (generates INotifyPropertyChanged automatically)
        [ObservableProperty]
        private bool _useConsistentColors = true;

        [ObservableProperty]
        private bool _useDetailFine = true;

        [ObservableProperty]
        private bool _configureSun = true;

        [ObservableProperty]
        private bool _hideGrids = true;

        [ObservableProperty]
        private bool _hideLevels = true;

        [ObservableProperty]
        private bool _hideRefPoints = false;

        [ObservableProperty]
        private bool _hideScopeBox = false;

        // Constructor can set defaults
        public SexyRevitViewModel()
        {
            Title = "Sexy Revit - View Beautification";
        }

        // Override Apply from BaseViewModel
        [RelayCommand]
        protected override void Apply()
        {
            // Validation (if needed)
            if (!UseConsistentColors && !ConfigureSun && !HideGrids && !HideLevels)
            {
                // Nothing selected
                TaskDialog.Show("Warning", "Please select at least one option.");
                return;
            }

            // Signal close (invokes CloseAction delegate)
            CloseAction?.Invoke();
        }
    }
}
```

**Key Points**:
- Use `partial class` (required for source generators)
- Use `[ObservableProperty]` for properties (generates public property from private field)
- Private fields use `_camelCase` naming
- Generated public properties use `PascalCase` (e.g., `_useGrids` → `UseGrids`)
- Extend `BaseViewModel` for common functionality
- Override `Apply()` for custom OK button logic
- Set `Title` in constructor

---

### ViewModel with Commands

**Use Case**: ViewModels with custom button commands.

**Pattern**:

```csharp
public partial class MyFeatureViewModel : BaseViewModel
{
    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private ObservableCollection<string> _results = new();

    [ObservableProperty]
    private string? _selectedResult;

    [ObservableProperty]
    private bool _isSearching;

    // Custom command (generates SearchCommand property)
    [RelayCommand]
    private async Task Search()
    {
        if (string.IsNullOrWhiteSpace(SearchText)) return;

        IsSearching = true;
        IsBusy = true;

        try
        {
            // Perform search (async)
            var results = await PerformSearchAsync(SearchText);

            Results.Clear();
            foreach (var result in results)
            {
                Results.Add(result);
            }
        }
        finally
        {
            IsSearching = false;
            IsBusy = false;
        }
    }

    // Command with parameter
    [RelayCommand]
    private void SelectResult(string result)
    {
        SelectedResult = result;
    }

    private Task<List<string>> PerformSearchAsync(string query)
    {
        // Implement search logic
        return Task.FromResult(new List<string>());
    }
}
```

**XAML Binding**:
```xml
<TextBox Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}" />
<Button Content="Search" Command="{Binding SearchCommand}"
        IsEnabled="{Binding IsSearching, Converter={StaticResource InverseBoolConverter}}" />
<ListBox ItemsSource="{Binding Results}" SelectedItem="{Binding SelectedResult}" />
```

**Key Points**:
- Use `[RelayCommand]` for methods that should become commands
- Method name becomes command name + "Command" suffix (e.g., `Search()` → `SearchCommand`)
- Use `async Task` for async commands
- Set `IsBusy` during long operations (enables loading indicators)

---

### ViewModel with Validation

**Use Case**: ViewModels that validate user input.

**Pattern**:

```csharp
public partial class MyFeatureViewModel : BaseViewModel
{
    [ObservableProperty]
    [NotifyDataErrorInfo] // Enables validation
    [Required(ErrorMessage = "Name is required")]
    [MinLength(3, ErrorMessage = "Name must be at least 3 characters")]
    private string _name = "";

    [ObservableProperty]
    [Range(0, 100, ErrorMessage = "Value must be between 0 and 100")]
    private double _value = 50.0;

    [RelayCommand(CanExecute = nameof(CanApply))]
    protected override void Apply()
    {
        // Validation passed
        CloseAction?.Invoke();
    }

    private bool CanApply()
    {
        // Command is enabled only if no validation errors
        return !HasErrors;
    }
}
```

**Key Points**:
- Use `[NotifyDataErrorInfo]` on properties to enable validation
- Use data annotation attributes (`[Required]`, `[Range]`, `[MinLength]`, etc.)
- Use `CanExecute` to disable commands when validation fails
- `HasErrors` property is generated automatically

---

## View Implementation Pattern

### Basic View (XAML + Code-Behind)

**XAML** (`MyFeatureView.xaml`):

```xml
<base:LecgWindow x:Class="LECG.Views.MyFeatureView"
                 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 xmlns:base="clr-namespace:LECG.Views.Base"
                 Title="{Binding Title}" Width="400" Height="300"
                 WindowStartupLocation="CenterScreen">
    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Content -->
        <StackPanel Grid.Row="0" Spacing="10">
            <TextBlock Text="Enter Name:" FontWeight="Bold"/>
            <TextBox Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" />

            <TextBlock Text="Select Value:" FontWeight="Bold" Margin="0,10,0,0"/>
            <Slider Value="{Binding Value}" Minimum="0" Maximum="100"
                    TickFrequency="10" IsSnapToTickEnabled="True"/>
            <TextBlock Text="{Binding Value, StringFormat='Value: {0:F1}'}"
                       HorizontalAlignment="Center"/>

            <CheckBox Content="Enable Option" IsChecked="{Binding IsOptionEnabled}"
                      Margin="0,10,0,0"/>
        </StackPanel>

        <!-- Buttons -->
        <StackPanel Grid.Row="1" Orientation="Horizontal"
                    HorizontalAlignment="Right" Margin="0,10,0,0">
            <Button Content="OK" Command="{Binding ApplyCommand}"
                    Width="80" Margin="0,0,10,0" IsDefault="True"/>
            <Button Content="Cancel" Command="{Binding CancelCommand}"
                    Width="80" IsCancel="True"/>
        </StackPanel>
    </Grid>
</base:LecgWindow>
```

**Code-Behind** (`MyFeatureView.xaml.cs`):

```csharp
using LECG.ViewModels;
using LECG.Views.Base;

namespace LECG.Views
{
    public partial class MyFeatureView : LecgWindow
    {
        public MyFeatureView(MyFeatureViewModel viewModel)
        {
            InitializeComponent();

            // Set DataContext
            DataContext = viewModel;

            // Wire up CloseAction delegate
            viewModel.CloseAction = () =>
            {
                DialogResult = true; // OK
                Close();
            };
        }
    }
}
```

**Key Points**:
- Extend `LecgWindow` for consistent window behavior
- Bind `Title` to ViewModel's `Title` property
- Use `Grid.RowDefinitions` for layout (content + buttons)
- Buttons in bottom-right corner (standard Windows dialog layout)
- Set `IsDefault="True"` on OK button (activated by Enter key)
- Set `IsCancel="True"` on Cancel button (activated by Esc key)
- Code-behind is minimal (DataContext setup, CloseAction wiring)

---

### View with Busy Indicator

**XAML**:

```xml
<base:LecgWindow ...>
    <Grid>
        <!-- Main content -->
        <Grid IsEnabled="{Binding IsBusy, Converter={StaticResource InverseBoolConverter}}">
            <!-- ... -->
        </Grid>

        <!-- Busy overlay -->
        <Grid Background="#80000000" Visibility="{Binding IsBusy, Converter={StaticResource BoolToVisibilityConverter}}">
            <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center">
                <ProgressBar IsIndeterminate="True" Width="200" Height="20"/>
                <TextBlock Text="Processing..." Foreground="White"
                           HorizontalAlignment="Center" Margin="0,10,0,0"/>
            </StackPanel>
        </Grid>
    </Grid>
</base:LecgWindow>
```

**ViewModel**:

```csharp
[RelayCommand]
private async Task ProcessAsync()
{
    IsBusy = true;
    try
    {
        await Task.Run(() => DoWork());
    }
    finally
    {
        IsBusy = false;
    }
}
```

**Key Points**:
- Bind `IsBusy` from `BaseViewModel`
- Disable main content when busy (using `InverseBoolConverter`)
- Show overlay with `ProgressBar` when busy
- Use semi-transparent background (`#80000000` = 50% black)

---

## Settings Persistence Pattern

### Saving and Loading Settings

**Use Case**: Persist ViewModel as JSON between command executions.

**Command Pattern**:

```csharp
public override void Execute(UIDocument uiDoc, Document doc)
{
    // 1. Load settings (deserialize from JSON)
    var settings = SettingsManager.Load<MyFeatureViewModel>("MyFeatureSettings.json");

    // 2. Show dialog
    var dialog = new MyFeatureView(settings);
    if (dialog.ShowDialog() != true) return;

    // 3. Save settings (serialize to JSON)
    SettingsManager.Save(settings, "MyFeatureSettings.json");

    // 4. Use settings...
}
```

**SettingsManager Implementation** (reference):

```csharp
public static class SettingsManager
{
    private static string GetSettingsFolder()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LECG"
        );
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static T Load<T>(string filename) where T : new()
    {
        string path = Path.Combine(GetSettingsFolder(), filename);

        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<T>(json) ?? new T();
            }
            catch
            {
                // Return default if deserialization fails
                return new T();
            }
        }

        // Return default if file doesn't exist
        return new T();
    }

    public static void Save<T>(T settings, string filename)
    {
        string path = Path.Combine(GetSettingsFolder(), filename);
        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(path, json);
    }
}
```

**Storage Location**: `%AppData%\LECG\MyFeatureSettings.json`

**Key Points**:
- Call `Load<T>()` before showing dialog (gets previous settings or defaults)
- Call `Save<T>()` after user confirms dialog
- ViewModel must be serializable (all properties must have public getters/setters)
- Settings persist across Revit sessions

---

## Transaction Management Patterns

### Auto-Transaction (via TransactionName)

**Use Case**: Simple commands that need one transaction for all operations.

```csharp
[Transaction(TransactionMode.Manual)]
public class MyCommand : RevitCommand
{
    protected override string? TransactionName => "My Operation";

    public override void Execute(UIDocument uiDoc, Document doc)
    {
        // Base class wraps this in:
        // using (Transaction t = new Transaction(doc, "My Operation"))
        // {
        //     t.Start();
        //     Execute(uiDoc, doc); // this method
        //     t.Commit();
        // }

        // Modify elements
        foreach (var elem in elements)
        {
            elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).Set("Modified");
        }
    }
}
```

**Pros**:
- Simple, less boilerplate
- Automatic rollback on exception

**Cons**:
- Single transaction only (can't have multiple transactions)
- Can't control commit timing

---

### Manual Transaction (in Service)

**Use Case**: Commands that need multiple transactions, or fine-grained transaction control.

**Command**:

```csharp
[Transaction(TransactionMode.Manual)]
public class MyCommand : RevitCommand
{
    protected override string? TransactionName => null; // Manual

    public override void Execute(UIDocument uiDoc, Document doc)
    {
        var service = ServiceLocator.GetRequiredService<IMyService>();
        service.DoWork(doc, Log);
    }
}
```

**Service**:

```csharp
public void DoWork(Document doc, Action<string>? log = null)
{
    // Transaction 1: Modify elements
    using (Transaction t1 = new Transaction(doc, "Modify Elements"))
    {
        t1.Start();
        // ...
        t1.Commit();
    }

    // Transaction 2: Update views
    using (Transaction t2 = new Transaction(doc, "Update Views"))
    {
        t2.Start();
        // ...
        t2.Commit();
    }
}
```

**Pros**:
- Fine-grained control
- Multiple transactions possible
- Can rollback selectively

**Cons**:
- More boilerplate
- Must handle exceptions manually

---

### Sub-Transaction Pattern

**Use Case**: Operations that might fail, within a larger transaction.

```csharp
using (Transaction t = new Transaction(doc, "Modify Elements"))
{
    t.Start();

    foreach (var elem in elements)
    {
        using (SubTransaction st = new SubTransaction(doc))
        {
            st.Start();

            try
            {
                // Try to modify
                elem.get_Parameter(param).Set(value);
                st.Commit();
                log($"  ✓ Modified: {elem.Name}");
            }
            catch (Exception ex)
            {
                st.RollBack();
                log($"  ⚠ Failed: {elem.Name} - {ex.Message}");
            }
        }
    }

    t.Commit(); // Commit main transaction
}
```

**Key Points**:
- SubTransaction must be inside a Transaction
- Use for "try this, but rollback if it fails" scenarios
- Main transaction remains valid even if sub-transaction rolls back

---

## Logging and Progress Reporting

### Logging Pattern

**Service Method Signature**:

```csharp
public void DoWork(Document doc, Settings settings,
                  Action<string>? logCallback = null,
                  Action<double, string>? progressCallback = null)
```

**Usage in Service**:

```csharp
var log = logCallback ?? (_ => { });
var progress = progressCallback ?? ((_, __) => { });

log("Starting operation...");
progress(0, "Initializing");

// Do work
log("  ✓ Step 1 complete");
progress(33, "Processing step 2");

log("  ✓ Step 2 complete");
progress(66, "Processing step 3");

log("  ✓ Step 3 complete");
progress(100, "Complete");
```

**Calling from Command**:

```csharp
service.DoWork(doc, settings, Log, UpdateProgress);
```

**Key Points**:
- Accept callbacks as optional parameters (`Action<string>?`)
- Use null-coalescing to provide no-op implementations
- Log successes with `✓`, warnings with `⚠`, errors with `✗`
- Report progress at regular intervals (0%, 25%, 50%, 75%, 100%)

---

### Progress Reporting Levels

**Simple Progress** (milestone-based):

```csharp
progress(0, "Starting");
progress(50, "Halfway");
progress(100, "Complete");
```

**Detailed Progress** (loop-based):

```csharp
int total = elements.Count;
for (int i = 0; i < total; i++)
{
    var elem = elements[i];
    ProcessElement(elem);

    double percent = (i + 1) * 100.0 / total;
    progress(percent, $"Processing {i + 1} of {total}");
}
```

---

## DI Registration Pattern

### Registering Services

**Location**: `src/Core/Bootstrapper.cs`

**Method**: `ConfigureServices(IServiceCollection services)`

```csharp
private static void ConfigureServices(IServiceCollection services)
{
    // Core
    services.AddSingleton<IRibbonService, RibbonService>();

    // Domain Services (Singletons - stateless)
    services.AddSingleton<ISlabService, SlabService>();
    services.AddSingleton<IOffsetService, OffsetService>();
    services.AddSingleton<IMaterialService, MaterialService>();
    services.AddSingleton<IMyNewService, MyNewService>(); // ← Add here
}
```

**Key Points**:
- Use `AddSingleton<TInterface, TImplementation>()` for services
- Services are stateless (operate on `Document` passed as parameter)
- Order doesn't matter (no inter-service dependencies)

---

### Registering ViewModels

**Location**: `src/Core/Bootstrapper.cs`

**Method**: `ConfigureViewModels(IServiceCollection services)`

```csharp
private static void ConfigureViewModels(IServiceCollection services)
{
    services.AddTransient<ResetSlabsVM>();
    services.AddTransient<ConvertFamilyViewModel>();
    services.AddTransient<MyNewViewModel>(); // ← Add here
}
```

**Key Points**:
- Use `AddTransient<T>()` for ViewModels (new instance per command)
- ViewModels hold UI state (should be short-lived)

---

### Registering Views

**Location**: `src/Core/Bootstrapper.cs`

**Method**: `ConfigureViews(IServiceCollection services)`

```csharp
private static void ConfigureViews(IServiceCollection services)
{
    services.AddTransient<Views.ResetSlabsView>();
    services.AddTransient<Views.MyNewView>(); // ← Add here
}
```

**Key Points**:
- Use `AddTransient<T>()` for Views (new instance per dialog show)
- Fully qualify with `Views.` prefix

---

## Ribbon Button Pattern

### Adding a Button to Existing Panel

**Location**: `src/Core/Ribbon/RibbonService.cs`

**Method**: `CreateMyPanel(UIControlledApplication app, string tabName, string assemblyPath)`

```csharp
private void CreateMyPanel(UIControlledApplication app, string tabName, string assemblyPath)
{
    RibbonPanel panel = GetOrCreatePanel(app, tabName, AppConstants.Panels.MyPanel);
    string availability = "LECG.Core.ProjectDocumentAvailability";

    RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
        UIConstants.ButtonMyFeature_Name,
        UIConstants.ButtonMyFeature_Text,
        "LECG.Commands.MyFeatureCommand", // Full class name
        UIConstants.ButtonMyFeature_Tooltip,
        AppImages.MyFeatureIcon // or image URI
    ), assemblyPath, availability);
}
```

**Add Constants** (`src/Configuration/UIConstants.cs`):

```csharp
// My Feature Button
public const string ButtonMyFeature_Name = "MyFeature";
public const string ButtonMyFeature_Text = "My Feature";
public const string ButtonMyFeature_Tooltip = "Perform my feature operation on selected elements.";
```

**Call Panel Creation** (in `InitializeRibbon`):

```csharp
public void InitializeRibbon(UIControlledApplication app)
{
    string assemblyPath = Assembly.GetExecutingAssembly().Location;
    string tabName = AppConstants.TabName;

    try { app.CreateRibbonTab(tabName); } catch { }

    CreateHomePanel(app, tabName, assemblyPath);
    CreateMyPanel(app, tabName, assemblyPath); // ← Add here
    // ...
}
```

---

## Complete Example: Adding a New Feature

### Scenario: Add "Renumber Elements" Feature

**Step-by-Step**:

#### 1. Create Interface

**File**: `src/Services/Interfaces/IRenumberService.cs`

```csharp
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace LECG.Services.Interfaces
{
    public interface IRenumberService
    {
        void RenumberElements(Document doc, IEnumerable<Element> elements,
                             string prefix, int startNumber,
                             Action<string>? log = null);
    }
}
```

#### 2. Create Service

**File**: `src/Services/RenumberService.cs`

```csharp
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Services
{
    public class RenumberService : IRenumberService
    {
        public void RenumberElements(Document doc, IEnumerable<Element> elements,
                                    string prefix, int startNumber,
                                    Action<string>? log = null)
        {
            var logFunc = log ?? (_ => { });

            using (Transaction t = new Transaction(doc, "Renumber Elements"))
            {
                t.Start();

                int currentNumber = startNumber;
                foreach (var elem in elements.OrderBy(e => e.Id.IntegerValue))
                {
                    try
                    {
                        string newMark = $"{prefix}{currentNumber:D3}";
                        elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set(newMark);
                        logFunc($"  ✓ {elem.Name} → {newMark}");
                        currentNumber++;
                    }
                    catch (Exception ex)
                    {
                        logFunc($"  ⚠ Failed {elem.Id}: {ex.Message}");
                    }
                }

                t.Commit();
            }

            logFunc($"Renumbered {elements.Count()} elements.");
        }
    }
}
```

#### 3. Create ViewModel

**File**: `src/ViewModels/RenumberViewModel.cs`

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LECG.ViewModels
{
    public partial class RenumberViewModel : BaseViewModel
    {
        [ObservableProperty]
        private string _prefix = "";

        [ObservableProperty]
        private int _startNumber = 1;

        public RenumberViewModel()
        {
            Title = "Renumber Elements";
        }

        [RelayCommand]
        protected override void Apply()
        {
            if (StartNumber < 1)
            {
                TaskDialog.Show("Error", "Start number must be at least 1.");
                return;
            }

            CloseAction?.Invoke();
        }
    }
}
```

#### 4. Create View

**File**: `src/Views/RenumberView.xaml`

```xml
<base:LecgWindow x:Class="LECG.Views.RenumberView"
                 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 xmlns:base="clr-namespace:LECG.Views.Base"
                 Title="{Binding Title}" Width="350" Height="200">
    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <StackPanel Grid.Row="0" Spacing="10">
            <TextBlock Text="Prefix:" FontWeight="Bold"/>
            <TextBox Text="{Binding Prefix, UpdateSourceTrigger=PropertyChanged}" />

            <TextBlock Text="Start Number:" FontWeight="Bold" Margin="0,10,0,0"/>
            <TextBox Text="{Binding StartNumber, UpdateSourceTrigger=PropertyChanged}" />
        </StackPanel>

        <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Right">
            <Button Content="OK" Command="{Binding ApplyCommand}" Width="80" Margin="0,0,10,0" IsDefault="True"/>
            <Button Content="Cancel" Command="{Binding CancelCommand}" Width="80" IsCancel="True"/>
        </StackPanel>
    </Grid>
</base:LecgWindow>
```

**File**: `src/Views/RenumberView.xaml.cs`

```csharp
using LECG.ViewModels;
using LECG.Views.Base;

namespace LECG.Views
{
    public partial class RenumberView : LecgWindow
    {
        public RenumberView(RenumberViewModel viewModel)
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
}
```

#### 5. Create Command

**File**: `src/Commands/RenumberCommand.cs`

```csharp
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using LECG.Core;
using LECG.Views;
using LECG.ViewModels;
using LECG.Services.Interfaces;
using System.Linq;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class RenumberCommand : RevitCommand
    {
        protected override string? TransactionName => null;

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            // 1. Select elements
            var selIds = uiDoc.Selection.GetElementIds();
            if (!selIds.Any())
            {
                TaskDialog.Show("Info", "Please select elements to renumber.");
                return;
            }

            var elements = selIds.Select(id => doc.GetElement(id)).ToList();

            // 2. Load settings
            var settings = SettingsManager.Load<RenumberViewModel>("RenumberSettings.json");

            // 3. Show dialog
            var dialog = new RenumberView(settings);
            if (dialog.ShowDialog() != true) return;

            // 4. Save settings
            SettingsManager.Save(settings, "RenumberSettings.json");

            // 5. Show log window
            ShowLogWindow("Renumber Elements");
            Log("Renumber Elements");
            Log("=================");

            // 6. Resolve service and execute
            var service = ServiceLocator.GetRequiredService<IRenumberService>();
            service.RenumberElements(doc, elements, settings.Prefix, settings.StartNumber, Log);

            Log("=== COMPLETE ===");
        }
    }
}
```

#### 6. Register in DI

**File**: `src/Core/Bootstrapper.cs`

```csharp
private static void ConfigureServices(IServiceCollection services)
{
    // ...
    services.AddSingleton<IRenumberService, RenumberService>();
}

private static void ConfigureViewModels(IServiceCollection services)
{
    // ...
    services.AddTransient<RenumberViewModel>();
}

private static void ConfigureViews(IServiceCollection services)
{
    // ...
    services.AddTransient<Views.RenumberView>();
}
```

#### 7. Add to Ribbon

**File**: `src/Core/Ribbon/RibbonService.cs`

```csharp
// In appropriate panel method:
RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
    UIConstants.ButtonRenumber_Name,
    UIConstants.ButtonRenumber_Text,
    "LECG.Commands.RenumberCommand",
    UIConstants.ButtonRenumber_Tooltip,
    AppImages.Renumber
), assemblyPath, availability);
```

**File**: `src/Configuration/UIConstants.cs`

```csharp
public const string ButtonRenumber_Name = "Renumber";
public const string ButtonRenumber_Text = "Renumber";
public const string ButtonRenumber_Tooltip = "Renumber selected elements with a custom prefix.";
```

---

## Conclusion

These patterns provide a solid foundation for implementing new features in the LECG plugin. Key takeaways:

1. **Always follow MVVM**: Commands orchestrate, Services implement, ViewModels bind, Views display
2. **Use base classes**: `RevitCommand`, `BaseViewModel`, `LecgWindow`
3. **Register in DI**: Services (Singleton), ViewModels (Transient), Views (Transient)
4. **Use callbacks**: Pass `Action<string>` and `Action<double, string>` for logging and progress
5. **Handle errors gracefully**: Try/catch per element, log warnings instead of crashing
6. **Persist settings**: Use `SettingsManager.Load/Save` for user preferences

For architectural context, see `01-architecture.md`. For component inventory, see `02-components.md`. For organization, see `03-organization.md`.
