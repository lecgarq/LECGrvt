# Developer Onboarding Guide

## Document Purpose

This guide helps new developers get started with the LECG Revit plugin, from setting up the development environment to implementing their first feature.

**Target Audience**: New developers joining the project

**Last Updated**: February 2026

---

## Prerequisites

### Required Software

1. **Visual Studio 2022** (or later)
   - Workload: ".NET desktop development"
   - Component: "WPF development"

2. **Revit 2026**
   - Standard installation
   - Path: `C:\Program Files\Autodesk\Revit 2026\`

3. **.NET 8.0 SDK**
   - Included with Visual Studio 2022
   - Verify: `dotnet --version` (should show 8.0.x)

4. **Git**
   - For version control

### Optional Tools

- **ReSharper** or **Rider** - Enhanced C# development
- **RevitLookup** - Inspect Revit elements during development
- **Postman** or similar - If working with external APIs

---

## Getting Started

### 1. Clone the Repository

```bash
git clone <repository-url>
cd LECG
```

### 2. Open Solution

1. Open `LECG.sln` in Visual Studio
2. Wait for NuGet restore (automatic)
3. Build solution: `Ctrl+Shift+B`

**Expected Output**:
```
Build succeeded.
    6 Warning(s)
    0 Error(s)
```

**Warnings**: 3 nullable reference warnings in `App.cs` (known, safe to ignore)

### 3. Deploy to Revit

**Option A: Manual Deployment**

1. Build project (Debug or Release)
2. Copy output files to Revit add-ins folder:
   ```
   Source: bin/Debug/net8.0-windows/
   Target: %AppData%\Autodesk\Revit\Addins\2026\LECG\
   ```

3. Copy files:
   - `LECG.dll`
   - `LECG.pdb` (for debugging)
   - `CommunityToolkit.Mvvm.dll`
   - Other dependencies

4. Create `.addin` manifest (if not exists):

**LECG.addin**:
```xml
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>LECG</Name>
    <Assembly>LECG\LECG.dll</Assembly>
    <FullClassName>LECG.App</FullClassName>
    <ClientId>12345678-1234-1234-1234-123456789abc</ClientId>
    <VendorId>LECG</VendorId>
    <VendorDescription>LECG Arquitectura</VendorDescription>
  </AddIn>
</RevitAddIns>
```

**Option B: Automated Post-Build** (Recommended)

Add to `LECG.csproj`:
```xml
<Target Name="PostBuild" AfterTargets="PostBuildEvent">
  <Exec Command="xcopy /Y /I &quot;$(TargetDir)*.*&quot; &quot;$(AppData)\Autodesk\Revit\Addins\2026\LECG\&quot;" />
</Target>
```

Now building automatically deploys to Revit.

### 4. Launch Revit and Test

1. Start Revit 2026
2. Open any project (or create new)
3. Look for "LECG" tab in ribbon
4. Click "Home" button → Should show tool grid
5. Try "Sexy Revit" command → Should show dialog

**Troubleshooting**:
- **No LECG tab**: Check Revit journal (`%AppData%\Autodesk\Revit\Addins\2026\Journals\`)
- **Tab appears but buttons don't work**: Check for exceptions in journal
- **Build errors**: Verify Revit 2026 is installed at expected path

---

## Understanding the Codebase

### 15-Minute Tour

**Start here** (read these files in order):

1. **`LECG.sln`** - Solution structure
2. **`LECG.csproj`** - Dependencies, target framework
3. **`src/App.cs`** - Entry point, startup flow
4. **`src/Core/Bootstrapper.cs`** - DI configuration
5. **`src/Commands/SexyRevitCommand.cs`** - Example command (simple, well-structured)
6. **`src/Services/SexyRevitService.cs`** - Example service
7. **`src/ViewModels/SexyRevitViewModel.cs`** - Example ViewModel
8. **`src/Views/SexyRevitView.xaml`** - Example view

**Key Takeaways**:
- Commands are thin orchestrators
- Services contain all Revit API logic
- ViewModels use CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`)
- Views extend `LecgWindow` base class

### Architecture Diagram

```
User clicks ribbon button
    ↓
Revit instantiates Command (via reflection)
    ↓
Command.Execute()
    - Load settings (SettingsManager)
    - Show dialog (View + ViewModel)
    - Resolve service (ServiceLocator)
    - Call service method (with callbacks)
    ↓
Service.Method()
    - Create transaction
    - Call Revit API
    - Log progress
    - Commit transaction
    ↓
Return to Revit
```

---

## Your First Feature

### Tutorial: Add "Hello Revit" Command

This tutorial walks you through adding a complete feature from scratch.

#### Step 1: Create the Service

**File**: `src/Services/Interfaces/IHelloService.cs`

```csharp
using Autodesk.Revit.DB;
using System;

namespace LECG.Services.Interfaces
{
    public interface IHelloService
    {
        void SayHello(Document doc, string name, Action<string>? log = null);
    }
}
```

**File**: `src/Services/HelloService.cs`

```csharp
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Services.Interfaces;
using System;

namespace LECG.Services
{
    public class HelloService : IHelloService
    {
        public void SayHello(Document doc, string name, Action<string>? log = null)
        {
            var logFunc = log ?? (_ => { });

            logFunc($"Hello, {name}!");
            logFunc($"Current document: {doc.Title}");
            logFunc($"Number of elements: {new FilteredElementCollector(doc).WhereElementIsNotElementType().GetElementCount()}");

            TaskDialog.Show("Hello Revit", $"Hello, {name}!\n\nWorking in: {doc.Title}");
        }
    }
}
```

#### Step 2: Create the ViewModel

**File**: `src/ViewModels/HelloViewModel.cs`

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LECG.ViewModels
{
    public partial class HelloViewModel : BaseViewModel
    {
        [ObservableProperty]
        private string _userName = "";

        public HelloViewModel()
        {
            Title = "Hello Revit";
        }

        [RelayCommand]
        protected override void Apply()
        {
            if (string.IsNullOrWhiteSpace(UserName))
            {
                TaskDialog.Show("Error", "Please enter your name.");
                return;
            }

            CloseAction?.Invoke();
        }
    }
}
```

#### Step 3: Create the View

**File**: `src/Views/HelloView.xaml`

```xml
<base:LecgWindow x:Class="LECG.Views.HelloView"
                 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 xmlns:base="clr-namespace:LECG.Views.Base"
                 Title="{Binding Title}" Width="350" Height="150">
    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <StackPanel Grid.Row="0">
            <TextBlock Text="Enter your name:" FontWeight="Bold"/>
            <TextBox Text="{Binding UserName, UpdateSourceTrigger=PropertyChanged}"
                     Margin="0,5,0,0"/>
        </StackPanel>

        <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Right"
                    Margin="0,10,0,0">
            <Button Content="OK" Command="{Binding ApplyCommand}"
                    Width="80" Margin="0,0,10,0" IsDefault="True"/>
            <Button Content="Cancel" Command="{Binding CancelCommand}"
                    Width="80" IsCancel="True"/>
        </StackPanel>
    </Grid>
</base:LecgWindow>
```

**File**: `src/Views/HelloView.xaml.cs`

```csharp
using LECG.ViewModels;
using LECG.Views.Base;

namespace LECG.Views
{
    public partial class HelloView : LecgWindow
    {
        public HelloView(HelloViewModel viewModel)
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

#### Step 4: Create the Command

**File**: `src/Commands/HelloCommand.cs`

```csharp
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Views;
using LECG.ViewModels;
using LECG.Services.Interfaces;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class HelloCommand : RevitCommand
    {
        protected override string? TransactionName => null; // Read-only command

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            // 1. Create ViewModel
            var vm = new HelloViewModel();

            // 2. Show Dialog
            var dialog = new HelloView(vm);
            if (dialog.ShowDialog() != true) return;

            // 3. Show Log Window
            ShowLogWindow("Hello Revit");

            // 4. Resolve Service and Execute
            var service = ServiceLocator.GetRequiredService<IHelloService>();
            service.SayHello(doc, vm.UserName, Log);

            Log("=== COMPLETE ===");
        }
    }
}
```

#### Step 5: Register in DI

**File**: `src/Core/Bootstrapper.cs`

Add to `ConfigureServices`:
```csharp
services.AddSingleton<IHelloService, HelloService>();
```

(Optional) Add to `ConfigureViewModels`:
```csharp
services.AddTransient<HelloViewModel>();
```

(Optional) Add to `ConfigureViews`:
```csharp
services.AddTransient<Views.HelloView>();
```

#### Step 6: Add to Ribbon

**File**: `src/Core/Ribbon/RibbonService.cs`

Add to an existing panel (e.g., `CreateHomePanel`):
```csharp
RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
    "Hello",
    "Hello",
    "LECG.Commands.HelloCommand",
    "Say hello to Revit",
    AppImages.Home // Or use custom icon
), assemblyPath, availability);
```

**Or create a new panel**:
```csharp
private void CreateTutorialPanel(UIControlledApplication app, string tabName, string assemblyPath)
{
    RibbonPanel panel = GetOrCreatePanel(app, tabName, "Tutorial");
    string availability = "LECG.Core.ProjectDocumentAvailability";

    RibbonFactory.CreateButton(panel, new RibbonButtonConfig(
        "Hello",
        "Hello",
        "LECG.Commands.HelloCommand",
        "Say hello to Revit",
        AppImages.Home
    ), assemblyPath, availability);
}
```

And call it in `InitializeRibbon`:
```csharp
CreateTutorialPanel(app, tabName, assemblyPath);
```

#### Step 7: Build and Test

1. **Build**: `Ctrl+Shift+B`
2. **Deploy**: Files copied automatically (if post-build configured)
3. **Launch Revit**: Restart if already open
4. **Test**:
   - Open any project
   - Click "Hello" button in ribbon
   - Enter your name → Click OK
   - Verify dialog shows greeting
   - Check log window for messages

**Congratulations!** You've added your first feature. 🎉

---

## Debugging

### Attach to Revit Process

1. Start Revit 2026
2. In Visual Studio: `Debug > Attach to Process` (or `Ctrl+Alt+P`)
3. Find "Revit.exe" in process list
4. Click "Attach"

**Now you can**:
- Set breakpoints in code
- Step through execution (`F10`, `F11`)
- Inspect variables
- Evaluate expressions in Watch window

### Reading Revit Journal Files

**Location**: `%AppData%\Autodesk\Revit\Addins\2026\Journals\`

**File**: `journal.XXXX.txt` (latest date)

**Look for**:
- Plugin load errors: Search for "LECG"
- Exceptions: Search for "Exception" or "Error"
- Command execution: Search for command class name

**Example Error**:
```
Exception: System.InvalidOperationException: Service of type IMyService could not be resolved.
   at LECG.Core.ServiceLocator.GetRequiredService[T]()
   at LECG.Commands.MyCommand.Execute(UIDocument uiDoc, Document doc)
```

### Common Debugging Scenarios

**Scenario 1: Command Not Appearing in Ribbon**

**Check**:
1. Is ribbon initialization code correct?
2. Is command class name correct in `RibbonFactory.CreateButton`?
3. Check journal for ribbon creation errors

**Scenario 2: Command Throws Exception**

**Check**:
1. Is service registered in `Bootstrapper`?
2. Are all dependencies resolved?
3. Check journal for exception details
4. Set breakpoint in command, attach debugger, step through

**Scenario 3: UI Not Updating**

**Check**:
1. Is property using `[ObservableProperty]`?
2. Is `UpdateSourceTrigger=PropertyChanged` set in XAML binding?
3. Is `INotifyPropertyChanged` working? (Check BaseViewModel inheritance)

---

## Best Practices

### Code Style

1. **Follow existing patterns**: Use `SexyRevitCommand` as a template
2. **Use base classes**: Extend `RevitCommand`, `BaseViewModel`, `LecgWindow`
3. **Register in DI**: All services, ViewModels, Views
4. **Interface-based services**: Always create `I*Service` interface
5. **Minimal code-behind**: Views should have <30 lines of code-behind

### Naming Conventions

| Type | Pattern | Example |
|------|---------|---------|
| Command | `{Feature}Command` | `HelloCommand` |
| Service | `{Feature}Service` | `HelloService` |
| Interface | `I{Feature}Service` | `IHelloService` |
| ViewModel | `{Feature}ViewModel` | `HelloViewModel` |
| View | `{Feature}View` | `HelloView` |

### Git Workflow

1. **Create feature branch**: `git checkout -b feature/hello-command`
2. **Make changes**: Implement feature
3. **Commit often**: Small, focused commits
4. **Push to remote**: `git push origin feature/hello-command`
5. **Create pull request**: For code review
6. **Merge after approval**

**Commit Message Format**:
```
Add Hello Revit command

- Created HelloService for greeting logic
- Added HelloViewModel with user name input
- Created HelloView with simple input dialog
- Registered service in Bootstrapper
- Added button to Tutorial ribbon panel
```

### Testing Checklist

Before submitting code:
- [ ] Code builds without errors
- [ ] No new warnings (except known nullable warnings)
- [ ] Command appears in ribbon
- [ ] Dialog shows and closes properly
- [ ] Service logic works as expected
- [ ] Log window shows appropriate messages
- [ ] No exceptions in Revit journal
- [ ] Tested in clean Revit session (restart Revit)

---

## Common Pitfalls

### 1. Forgetting Transaction

**Error**: "Cannot modify model outside transaction"

**Fix**: Wrap Revit API modifications in `Transaction`:
```csharp
using (Transaction t = new Transaction(doc, "My Operation"))
{
    t.Start();
    element.get_Parameter(param).Set(value);
    t.Commit();
}
```

### 2. Not Registering Service

**Error**: "Service of type X could not be resolved"

**Fix**: Add to `Bootstrapper.ConfigureServices()`:
```csharp
services.AddSingleton<IMyService, MyService>();
```

### 3. Wrong Namespace in Ribbon

**Error**: Command doesn't load, ribbon button does nothing

**Fix**: Use **full namespace** in `RibbonFactory.CreateButton`:
```csharp
"LECG.Commands.HelloCommand" // ✅ Full namespace
"HelloCommand"                 // ❌ Wrong
```

### 4. Modifying Read-Only Parameter

**Error**: "Parameter is read-only"

**Fix**: Check before setting:
```csharp
if (!param.IsReadOnly)
{
    param.Set(value);
}
```

### 5. Not Restarting Revit

**Issue**: Changes not reflected after rebuild

**Fix**: Revit locks DLLs, must restart to reload plugin

---

## Learning Resources

### Internal Documentation

- **Architecture**: `docs/review/01-architecture.md`
- **Components**: `docs/review/02-components.md`
- **Organization**: `docs/review/03-organization.md`
- **Patterns**: `docs/review/04-patterns.md`
- **Revit API**: `docs/review/05-revit-api.md`
- **DI Guide**: `docs/review/06-dependency-injection.md`

### External Resources

- **Revit API Docs**: https://www.revitapidocs.com/2026/
- **The Building Coder**: https://thebuildingcoder.typepad.com/
- **CommunityToolkit.Mvvm**: https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/
- **WPF Tutorial**: https://wpf-tutorial.com/

---

## Getting Help

### Internal Team

1. **Code Review**: Create PR, tag team members
2. **Questions**: Ask in team chat/Slack
3. **Pair Programming**: Schedule session with senior dev

### External Resources

1. **Revit API Forum**: https://forums.autodesk.com/t5/revit-api-forum/bd-p/160
2. **Stack Overflow**: Tag `revit-api`
3. **GitHub Issues**: (if repo is public)

---

## Next Steps

After completing the tutorial:

1. **Read the documentation** in `docs/review/`
2. **Explore existing commands**: Pick a complex one (e.g., `ConvertCadCommand`) and understand it
3. **Try modifying an existing feature**: Add a new option to "Sexy Revit"
4. **Pick a real task**: Ask team lead for a small bug fix or feature
5. **Contribute**: Submit your first PR!

---

## Welcome to the Team!

You now have everything you need to contribute to the LECG Revit plugin. Don't hesitate to ask questions - we're here to help you succeed.

Happy coding! 🚀
