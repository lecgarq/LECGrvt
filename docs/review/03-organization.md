# Code Organization Guide

## Document Purpose

This document explains the directory structure, file organization patterns, and naming conventions in the LECG Revit plugin. Use this as a reference when adding new code or refactoring existing components.

**Target Audience**: Developers

**Last Updated**: February 2026

---

## Directory Structure Overview

```
LECG/
├── src/                           ← All source code
│   ├── Commands/                  ← Revit command implementations
│   ├── Configuration/             ← Constants and configuration
│   ├── Core/                      ← Infrastructure and base classes
│   │   └── Ribbon/                ← Ribbon UI initialization
│   ├── Services/                  ← Business logic and Revit API
│   │   ├── Interfaces/            ← Service interfaces (preferred location)
│   │   └── Logging/               ← Logging infrastructure
│   ├── Utils/                     ← Helper utilities
│   ├── ViewModels/                ← MVVM ViewModels
│   │   └── Components/            ← Reusable ViewModel components
│   ├── Views/                     ← WPF XAML views
│   │   ├── Base/                  ← Base view classes
│   │   └── Components/            ← Reusable UI components
│   ├── Resources/                 ← Resource dictionaries and images
│   │   ├── Base/                  ← Base styles and templates
│   │   └── Images/                ← Image resources
│   └── App.cs                     ← Plugin entry point
│
├── img/                           ← ⚠️ Legacy images (prefer src/Resources/Images)
├── bin/                           ← Build outputs (gitignored)
├── obj/                           ← Intermediate build files (gitignored)
├── docs/                          ← Documentation
│   └── review/                    ← This architecture review
├── LECG.csproj                    ← Project file
├── LECG.sln                       ← Solution file
└── global.json                    ← .NET SDK version specification
```

---

## Core Directories

### `src/Commands/`

**Purpose**: Entry points for user-initiated actions from the Revit ribbon.

**Contents**:
- Command classes implementing `IExternalCommand` (via `RevitCommand` base class)
- Thin orchestrators that coordinate UI, settings, and services

**Naming Convention**: `{Feature}Command.cs`

**Examples**:
- `SexyRevitCommand.cs` - View beautification
- `PurgeCommand.cs` - Purge unused elements
- `AlignCommands.cs` - Multiple alignment commands

**When to Add a New Command**:
- Adding a new ribbon button
- Implementing a new user-facing feature

**Pattern**:
```csharp
namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class MyFeatureCommand : RevitCommand
    {
        protected override string? TransactionName => "My Feature"; // or null for manual

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            // 1. Load settings
            var settings = SettingsManager.Load<MyFeatureViewModel>("MyFeatureSettings.json");

            // 2. Show dialog
            var dialog = new MyFeatureView(settings);
            if (dialog.ShowDialog() != true) return;

            // 3. Save settings
            SettingsManager.Save(settings, "MyFeatureSettings.json");

            // 4. Show log window
            ShowLogWindow("My Feature");

            // 5. Resolve service and execute
            var service = ServiceLocator.GetRequiredService<IMyFeatureService>();
            service.DoWork(doc, settings, Log, UpdateProgress);

            // 6. Log completion
            Log("=== COMPLETE ===");
        }
    }
}
```

**File Size**: Target 50-150 LOC. If larger, consider extracting logic to service.

---

### `src/Services/`

**Purpose**: Business logic and all Revit API interactions.

**Contents**:
- Service implementations (`*Service.cs`)
- Service interfaces (`Interfaces/I*Service.cs`)
- Utility services (`SettingsManager.cs`, `Logging/`)

**Naming Convention**:
- Implementation: `{Feature}Service.cs`
- Interface: `I{Feature}Service.cs`

**Examples**:
- `SexyRevitService.cs` + `Interfaces/ISexyRevitService.cs`
- `MaterialService.cs` + `Interfaces/IMaterialService.cs`

**When to Add a New Service**:
- Implementing business logic for a command
- Encapsulating a Revit API interaction pattern
- Creating reusable logic used by multiple commands

**Pattern**:
```csharp
namespace LECG.Services
{
    public class MyFeatureService : IMyFeatureService
    {
        public void DoWork(Document doc, MyFeatureViewModel settings,
                          Action<string>? log = null,
                          Action<double, string>? progress = null)
        {
            log?.Invoke("Starting work...");
            progress?.Invoke(0, "Initializing");

            using (Transaction t = new Transaction(doc, "My Feature"))
            {
                t.Start();

                // Revit API operations here
                // ...

                t.Commit();
            }

            progress?.Invoke(100, "Complete");
            log?.Invoke("Work completed successfully.");
        }
    }
}
```

**File Size**: Target 100-250 LOC. If larger than 400 LOC, consider decomposition.

**Registration**: Add to `Bootstrapper.ConfigureServices()`:
```csharp
services.AddSingleton<IMyFeatureService, MyFeatureService>();
```

---

### `src/ViewModels/`

**Purpose**: Presentation logic, data binding, and UI state management.

**Contents**:
- ViewModel classes (`*ViewModel.cs` or `*VM.cs`)
- Component ViewModels (`Components/*ViewModel.cs`)

**Naming Convention**: `{Feature}ViewModel.cs` or `{Feature}VM.cs`

**Examples**:
- `SexyRevitViewModel.cs` - View beautification settings
- `OffsetElevationsVM.cs` - Offset settings
- `Components/SelectionViewModel.cs` - Reusable selection component

**When to Add a New ViewModel**:
- Creating a new WPF dialog
- Managing complex UI state
- Serializing user settings

**Pattern**:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LECG.ViewModels
{
    public partial class MyFeatureViewModel : BaseViewModel
    {
        [ObservableProperty]
        private string _inputValue = "";

        [ObservableProperty]
        private bool _isOptionEnabled = true;

        [ObservableProperty]
        private double _sliderValue = 50.0;

        // Commands
        [RelayCommand]
        protected override void Apply()
        {
            // Validate input
            if (string.IsNullOrEmpty(InputValue))
            {
                // Show error or return
                return;
            }

            // Signal close
            CloseAction?.Invoke();
        }
    }
}
```

**File Size**: Target 50-150 LOC. If larger than 300 LOC, consider extracting sub-ViewModels.

**Registration** (TODO): Add to `Bootstrapper.ConfigureViewModels()`:
```csharp
services.AddTransient<MyFeatureViewModel>();
```

---

### `src/Views/`

**Purpose**: WPF XAML UI definitions and code-behind.

**Contents**:
- View XAML files (`*View.xaml`)
- View code-behind (`*View.xaml.cs`)
- Base view classes (`Base/LecgWindow.cs`)
- Reusable components (`Components/*Control.xaml`)

**Naming Convention**: `{Feature}View.xaml` + `{Feature}View.xaml.cs`

**Examples**:
- `SexyRevitView.xaml` + `SexyRevitView.xaml.cs`
- `Base/LecgWindow.cs` - Base window class
- `Components/SelectionControl.xaml` - Reusable control

**When to Add a New View**:
- Creating a dialog for a command
- Building a reusable UI component

**XAML Pattern**:
```xml
<base:LecgWindow x:Class="LECG.Views.MyFeatureView"
                 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 xmlns:base="clr-namespace:LECG.Views.Base"
                 Title="{Binding Title}" Width="400" Height="300">
    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Content -->
        <StackPanel Grid.Row="0" Spacing="10">
            <TextBlock Text="Input Value:" />
            <TextBox Text="{Binding InputValue, UpdateSourceTrigger=PropertyChanged}" />

            <CheckBox Content="Enable Option" IsChecked="{Binding IsOptionEnabled}" />

            <TextBlock Text="Slider Value:" />
            <Slider Value="{Binding SliderValue}" Minimum="0" Maximum="100" />
        </StackPanel>

        <!-- Buttons -->
        <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Right" Spacing="10">
            <Button Content="OK" Command="{Binding ApplyCommand}" Width="80" IsDefault="True"/>
            <Button Content="Cancel" Command="{Binding CancelCommand}" Width="80" IsCancel="True"/>
        </StackPanel>
    </Grid>
</base:LecgWindow>
```

**Code-Behind Pattern** (Minimal):
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

**File Size**: Code-behind should be minimal (10-30 LOC). Move logic to ViewModel.

**Registration** (TODO): Add to `Bootstrapper.ConfigureViews()`:
```csharp
services.AddTransient<Views.MyFeatureView>();
```

---

### `src/Core/`

**Purpose**: Infrastructure, base classes, and cross-cutting concerns.

**Contents**:
- `Bootstrapper.cs` - DI container setup
- `ServiceLocator.cs` - Static service provider access
- `RevitCommand.cs` - Base class for commands
- `SelectionFilters.cs` - ISelectionFilter implementations
- `ProjectDocumentAvailability.cs`, `FamilyDocumentAvailability.cs` - Availability logic
- `Ribbon/` - Ribbon initialization

**When to Add to Core**:
- Creating a new base class
- Adding infrastructure code (logging, caching, etc.)
- Implementing cross-cutting concerns (authentication, validation, etc.)

**Avoid**: Business logic (use Services instead)

---

### `src/Core/Ribbon/`

**Purpose**: Revit ribbon UI initialization.

**Contents**:
- `RibbonService.cs` - Creates ribbon tab, panels, buttons
- `IRibbonService.cs` - Interface
- `RibbonFactory.cs` - Button/panel creation helpers
- `RibbonButtonConfig.cs` - Button configuration data

**When to Modify**:
- Adding a new ribbon button
- Creating a new panel
- Changing button icons or tooltips

**Pattern** (in `RibbonService.CreateXxxPanel`):
```csharp
RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
    UIConstants.ButtonMyFeature_Name,
    UIConstants.ButtonMyFeature_Text,
    "LECG.Commands.MyFeatureCommand",
    UIConstants.ButtonMyFeature_Tooltip,
    AppImages.MyFeatureIcon
), assemblyPath, availability);
```

---

### `src/Configuration/`

**Purpose**: Centralized constants for UI, app settings, and Revit-specific values.

**Contents**:
- `AppConstants.cs` - Tab name, panel names
- `UIConstants.cs` - Button text, tooltips, names
- `RevitConstants.cs` - Revit-specific constants (categories, parameters)

**When to Add**:
- Adding ribbon button text/tooltips
- Defining app-wide constants
- Centralizing Revit category/parameter names

**Pattern** (UIConstants.cs):
```csharp
public static class UIConstants
{
    // My Feature Button
    public const string ButtonMyFeature_Name = "MyFeature";
    public const string ButtonMyFeature_Text = "My Feature";
    public const string ButtonMyFeature_Tooltip = "Perform my feature operation on selected elements.";
}
```

**Pattern** (RevitConstants.cs):
```csharp
public static class RevitConstants
{
    public static class Categories
    {
        public const string Walls = "Walls";
        public const string Floors = "Floors";
        // ...
    }

    public static class Parameters
    {
        public const string Comments = "Comments";
        public const string Mark = "Mark";
        // ...
    }
}
```

---

### `src/Utils/`

**Purpose**: General-purpose helper utilities and converters.

**Contents**:
- `ImageUtils.cs` - Image processing
- `MathConverter.cs` - WPF value converters
- `EnumBindingSource.cs` - Enum → ComboBox binding
- `FamilyInstanceFilter.cs` - ISelectionFilter implementations

**When to Add**:
- Creating reusable helper methods
- Implementing WPF value converters
- Building selection filters

**Avoid**: Business logic (use Services instead)

---

### Interface Location Rule

**Status**: **COMPLETED** - All service interfaces are in `src/Services/Interfaces/`

**Namespace Rule**:
- Every interface in `src/Services/Interfaces/` must use `namespace LECG.Services.Interfaces`
- Consume interfaces with `using LECG.Services.Interfaces;`

---

### `src/Resources/`

**Purpose**: WPF resource dictionaries and image assets.

**Contents**:
- `Base/` - Base styles and templates
- `Images/` - Image resources (icons, logos)

**When to Add**:
- Adding new icons for ribbon buttons
- Creating shared WPF styles or templates
- Adding image resources for UI

---

## Naming Conventions

### Files

| Component Type | Pattern | Example |
|----------------|---------|---------|
| **Command** | `{Feature}Command.cs` | `SexyRevitCommand.cs` |
| **Service** | `{Feature}Service.cs` | `MaterialService.cs` |
| **Service Interface** | `I{Feature}Service.cs` | `IMaterialService.cs` |
| **ViewModel** | `{Feature}ViewModel.cs` or `{Feature}VM.cs` | `SexyRevitViewModel.cs`, `OffsetElevationsVM.cs` |
| **View (XAML)** | `{Feature}View.xaml` | `SexyRevitView.xaml` |
| **View (Code-Behind)** | `{Feature}View.xaml.cs` | `SexyRevitView.xaml.cs` |
| **Base Class** | `{Purpose}.cs` (no suffix) | `RevitCommand.cs`, `BaseViewModel.cs`, `LecgWindow.cs` |
| **Utility** | `{Purpose}Utils.cs` or `{Purpose}.cs` | `ImageUtils.cs`, `MathConverter.cs` |
| **Constants** | `{Category}Constants.cs` | `UIConstants.cs`, `AppConstants.cs` |

### Namespaces

| Directory | Namespace |
|-----------|-----------|
| `src/Commands/` | `LECG.Commands` |
| `src/Services/` | `LECG.Services` |
| `src/Services/Interfaces/` | `LECG.Services.Interfaces` |
| `src/ViewModels/` | `LECG.ViewModels` |
| `src/Views/` | `LECG.Views` |
| `src/Core/` | `LECG.Core` |
| `src/Configuration/` | `LECG.Configuration` |
| `src/Utils/` | `LECG.Utils` |
| `src/Resources/` | `LECG.Resources` |

### Classes

| Type | Pattern | Example |
|------|---------|---------|
| **Command** | `{Feature}Command` | `SexyRevitCommand` |
| **Service** | `{Feature}Service` | `MaterialService` |
| **Interface** | `I{Feature}Service` | `IMaterialService` |
| **ViewModel** | `{Feature}ViewModel` or `{Feature}VM` | `SexyRevitViewModel`, `OffsetElevationsVM` |
| **View** | `{Feature}View` | `SexyRevitView` |

### Inconsistencies to Be Aware Of

- **ViewModel suffix**: Some use `ViewModel`, others use `VM` (both acceptable, but inconsistent)
- **Interface namespace rule**: Use `LECG.Services.Interfaces` for all service interfaces
- **Image location**: Split between `img/` and `src/Resources/Images/` (prefer latter)

---

## Where to Place New Code

### Adding a New Feature (Full Stack)

**Example**: Adding a "Renumber Elements" feature

#### 1. Create the Service

**Location**: `src/Services/`

**Files**:
- `src/Services/RenumberService.cs`
- `src/Services/Interfaces/IRenumberService.cs`

**Steps**:
1. Define interface in `src/Services/Interfaces/IRenumberService.cs`
2. Implement in `RenumberService.cs`
3. Register in `Bootstrapper.ConfigureServices()`:
   ```csharp
   services.AddSingleton<IRenumberService, RenumberService>();
   ```

#### 2. Create the ViewModel

**Location**: `src/ViewModels/`

**File**: `src/ViewModels/RenumberViewModel.cs`

**Steps**:
1. Extend `BaseViewModel`
2. Use `[ObservableProperty]` for bindable properties
3. Use `[RelayCommand]` for button commands
4. Override `Apply()` for OK button logic
5. Register in `Bootstrapper.ConfigureViewModels()`:
   ```csharp
   services.AddTransient<RenumberViewModel>();
   ```

#### 3. Create the View

**Location**: `src/Views/`

**Files**:
- `src/Views/RenumberView.xaml`
- `src/Views/RenumberView.xaml.cs`

**Steps**:
1. Extend `LecgWindow` in XAML
2. Bind to ViewModel properties
3. Minimal code-behind (DataContext setup, CloseAction wiring)
4. Register in `Bootstrapper.ConfigureViews()`:
   ```csharp
   services.AddTransient<Views.RenumberView>();
   ```

#### 4. Create the Command

**Location**: `src/Commands/`

**File**: `src/Commands/RenumberCommand.cs`

**Steps**:
1. Extend `RevitCommand`
2. Load/save settings with `SettingsManager`
3. Show dialog
4. Resolve service via `ServiceLocator`
5. Call service method with logging/progress callbacks

#### 5. Add to Ribbon

**Location**: `src/Core/Ribbon/RibbonService.cs`

**Steps**:
1. Add button configuration to appropriate panel (or create new panel)
2. Add button constants to `UIConstants.cs`:
   ```csharp
   public const string ButtonRenumber_Name = "Renumber";
   public const string ButtonRenumber_Text = "Renumber";
   public const string ButtonRenumber_Tooltip = "Renumber selected elements.";
   ```
3. Add icon to `src/Resources/Images/` (if needed)

#### 6. Add Icon (Optional)

**Location**: `src/Resources/Images/`

**File**: `src/Resources/Images/renumber.png` (32x32 recommended)

**Steps**:
1. Add image to `src/Resources/Images/`
2. Set Build Action to "Resource"
3. Reference in `AppImages.cs` (if exists) or directly in `RibbonService`

---

### Adding a Utility Function

**Location**: `src/Utils/`

**When**: Reusable helper that doesn't fit in a service

**Example**: Add `GeometryUtils.cs` for geometric calculations

**Pattern**:
```csharp
namespace LECG.Utils
{
    public static class GeometryUtils
    {
        public static double CalculateDistance(XYZ point1, XYZ point2)
        {
            return point1.DistanceTo(point2);
        }

        public static XYZ GetMidpoint(XYZ point1, XYZ point2)
        {
            return (point1 + point2) / 2.0;
        }
    }
}
```

---

### Adding a Constant

**Location**: `src/Configuration/`

**When**: Centralizing magic strings, numbers, or Revit-specific values

**Files**:
- `UIConstants.cs` - UI text, button labels, tooltips
- `AppConstants.cs` - App-level settings (tab name, panel names)
- `RevitConstants.cs` - Revit category names, parameter names, etc.

**Example** (RevitConstants.cs):
```csharp
public static class Parameters
{
    public const string Comments = "Comments";
    public const string Mark = "Mark";
    public const string TypeMark = "Type Mark";
}
```

---

### Adding a Base Class

**Location**: `src/Core/` or `src/ViewModels/` (for ViewModel bases) or `src/Views/Base/` (for View bases)

**When**: Creating reusable base functionality for multiple components

**Examples**:
- `RevitCommand.cs` - Base for commands
- `BaseViewModel.cs` - Base for ViewModels
- `LecgWindow.cs` - Base for views

---

### Adding a WPF Value Converter

**Location**: `src/Utils/`

**File**: `{Purpose}Converter.cs`

**Pattern**:
```csharp
using System;
using System.Globalization;
using System.Windows.Data;

namespace LECG.Utils
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
```

---

## File Organization Patterns

### Single Responsibility

**Good**:
- One command per file (e.g., `SexyRevitCommand.cs`)
- One service per file (e.g., `MaterialService.cs`)
- One ViewModel per file (e.g., `SexyRevitViewModel.cs`)

**Exception**:
- `AlignCommands.cs` contains 8 related alignment commands (acceptable for closely related commands)

### Related Files Grouping

**Pattern**: Keep related files close in the directory tree

**Example**:
```
Commands/
  SexyRevitCommand.cs
Services/
  SexyRevitService.cs
  Interfaces/
    ISexyRevitService.cs
ViewModels/
  SexyRevitViewModel.cs
Views/
  SexyRevitView.xaml
  SexyRevitView.xaml.cs
```

**Navigation**: All `SexyRevit*` files are in their respective type directories

---

## Build Outputs and Artifacts

### `bin/` Directory

**Purpose**: Build outputs (Debug/Release)

**Contents**:
- `Debug/net8.0-windows/` - Debug build
  - `LECG.dll` - Main assembly
  - `LECG.pdb` - Debug symbols
  - Dependencies (CommunityToolkit.Mvvm.dll, etc.)
- `Release/net8.0-windows/` - Release build

**Action**: Add to `.gitignore`

### `obj/` Directory

**Purpose**: Intermediate build files

**Contents**:
- `Debug/net8.0-windows/` - Intermediate files
  - Generated source files (CommunityToolkit source generators)
  - Compiler caches

**Action**: Add to `.gitignore`

---

## Git Ignore Recommendations

**Current**: Basic `.gitignore` for `bin/`, `obj/`

**Recommended Additions**:
```
# Build outputs
bin/
obj/
*.dll
*.pdb

# Build logs (currently in repository ⚠️)
build_logs/
*.log
*_err_*.txt
journal*.txt
crash*.txt
diag.log

# User-specific files
*.user
*.suo
*.userosscache
*.sln.docstates

# VS Code
.vscode/

# Rider
.idea/

# Settings (if not wanted in repo)
# %AppData%/LECG/*.json
```

---

## Deployment Structure

**Target Folder**: `%AppData%\Autodesk\Revit\Addins\2026\`

**Required Files**:
1. `LECG.dll` - Main assembly
2. `LECG.addin` - Manifest file (currently missing ⚠️)
3. Dependencies:
   - `CommunityToolkit.Mvvm.dll`
   - (Others excluded via `ExcludeAssets="runtime"`)
4. Resources (if external):
   - Icons
   - Image files

**Manifest Example** (`LECG.addin`):
```xml
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>LECG</Name>
    <Assembly>LECG.dll</Assembly>
    <FullClassName>LECG.App</FullClassName>
    <ClientId>12345678-1234-1234-1234-123456789abc</ClientId>
    <VendorId>LECG</VendorId>
    <VendorDescription>LECG Arquitectura</VendorDescription>
  </AddIn>
</RevitAddIns>
```

**Post-Build** (recommended):
```xml
<!-- In LECG.csproj -->
<Target Name="PostBuild" AfterTargets="PostBuildEvent">
  <Exec Command="xcopy /Y /I &quot;$(TargetDir)*.*&quot; &quot;$(AppData)\Autodesk\Revit\Addins\2026\LECG\&quot;" />
</Target>
```

---

## Code Style and Formatting

### C# Style

- **Braces**: Opening brace on new line (Allman style)
- **Indentation**: 4 spaces (tabs configured to spaces)
- **Nullable**: Nullable reference types enabled (`#nullable enable`)
- **Usings**: Implicit usings enabled (common namespaces auto-imported)

### XAML Style

- **Indentation**: 4 spaces
- **Attributes**: One per line for readability (when multiple)
- **Namespaces**: Use `xmlns:base`, `xmlns:vm`, etc. for clarity

### Comments

- **XML Documentation**: Use `/// <summary>` for public classes and methods
- **Inline Comments**: Use sparingly, prefer self-documenting code
- **TODO Comments**: Currently only 2 in codebase (good!)

---

## Conclusion

The LECG codebase follows a clear, layered directory structure with consistent naming conventions. When adding new code:

1. **Identify the layer**: Command, Service, ViewModel, or View?
2. **Follow the pattern**: Use existing files as templates
3. **Register in DI**: Add to `Bootstrapper` (services, VMs, views)
4. **Add to ribbon**: Update `RibbonService` and `UIConstants`
5. **Test**: Build and test in Revit

**Key Principles**:
- **Separation of Concerns**: Commands orchestrate, Services implement, ViewModels bind, Views display
- **Single Responsibility**: Each class has one job
- **Consistent Naming**: Follow established patterns
- **DI Registration**: Register all services, VMs, and views in `Bootstrapper`

For implementation patterns and code examples, see `04-patterns.md`.
