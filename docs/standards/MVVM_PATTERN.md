---
name: MVVM Pattern Guide
description: Detailed guide on implementing Model-View-ViewModel in the LECG project.
---

# MVVM Pattern in LECG

The Model-View-ViewModel (MVVM) pattern is strictly enforced for all UI-driven features in this project. We use the **CommunityToolkit.Mvvm** library to reduce boilerplate.

## 1. ViewModels (`src/ViewModels`)

ViewModels are the brain of the UI. They hold the state and behavior.

### Base Class

All ViewModels must inherit from `BaseViewModel`. This provides:

- `ObservableObject`: Implementation of `INotifyPropertyChanged`.
- `CloseAction`: Action delegate to close the associated View.
- `Title`, `IsBusy`: Common properties.

### Observable Properties

Use the `[ObservableProperty]` attribute to automatically generate properties with notification support.

```csharp
public partial class MyViewModel : BaseViewModel
{
    // Generates: public string UserName { get; set; }
    // Handles PropertyChanged events automatically.
    [ObservableProperty] 
    private string _userName;

    // Generates: public bool IsEnabled { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusMessage))] // Update dependent property
    private bool _isEnabled;

    public string StatusMessage => IsEnabled ? "Active" : "Inactive";
}
```

### Commands

Use `[RelayCommand]` to generate `ICommand` properties for buttons.

```csharp
[RelayCommand]
private void Save()
{
    // Logic here
}

// Generates: public ICommand SaveCommand { get; }
```

### Dependency Injection

ViewModels should explicitly declare their dependencies (Services) in the constructor.

```csharp
public MyViewModel(IMaterialService materialService, ILogger logger)
{
    _materialService = materialService;
    _logger = logger;
}
```

## 2. Views (`src/Views`)

Views are purely for presentation. They should have almost no code-behind logic, except for:

1. Initializing the Component (`InitializeComponent`).
2. Setting the `DataContext`.
3. Handling the `CloseAction`.

### Standard Implementation template

**XAML (`MyView.xaml`):**
Inherit from `base:LecgWindow` to ensure consistent styling (Title bar, Icon, etc.).

```xml
<base:LecgWindow x:Class="LECG.Views.MyView" ...>
    ...
    <Button Command="{Binding SaveCommand}" Content="Save"/>
</base:LecgWindow>
```

**Code-Behind (`MyView.xaml.cs`):**

```csharp
public partial class MyView : LecgWindow
{
    public MyView(MyViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        
        // Wire up CloseAction to close the window
        viewModel.CloseAction = () => 
        { 
            DialogResult = true; 
            Close(); 
        };
    }
}
```

## 3. Data Binding Rules

- **TwoWay Binding**: Use for Inputs (TextBox, CheckBox). `Text="{Binding Name, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"`.
- **OneWay Binding**: Use for Read-only display (TextBlock).
- **Commands**: Bind Buttons to Commands. `Command="{Binding SaveCommand}"`.
- **Converters**: Use `BooleanToVisibilityConverter` or custom converters in `src/Converters` for UI logic.
