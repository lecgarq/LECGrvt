---
name: lecg-command
description: Add a new Revit command to the add-in — command class, service, ViewModel, View, DI registration, and ribbon button, wired the way this repo already does it. Use when the user says "add a command", "new ribbon button", "add a button to the X panel", "I want a command that...", "new dialog for...", or types /lecg-command. Not for changing an existing command's behaviour — that is a plain edit or /lecg-debug. This is the most repeated task in the repo; follow the existing wiring exactly.
---

# New command

Seven wiring points. Miss one and it fails in a specific, recognizable way — the failure column tells you which.

Read one existing command end to end first and copy its shape. `src/Commands/AlignEdgesCommand.cs` is a good full example (selection seeding + dialog + service). Pick a simpler sibling if the new command has no dialog.

| # | Where | What | If you skip it |
|---|-------|------|----------------|
| 1 | `src/Commands/<Name>Command.cs` | `[Transaction(TransactionMode.Manual)]`, `: RevitCommand` | Nothing to bind the button to |
| 2 | `src/Services/<Name>Service.cs` + `Services/Interfaces/` | The actual work | Revit logic leaks into the command |
| 3 | `src/ViewModels/<Name>ViewModel.cs` | `: BaseViewModel`, `partial` | — |
| 4 | `src/Views/<Name>View.xaml` (+ `.cs`) | Root must be `<base:LecgWindow>`, tokens from `src/Resources/` | Raw `Window` is unstyled and unscoped |
| 5 | `Bootstrapper.ConfigureServices/ViewModels/Views` | Services `AddSingleton<...>`; ViewModels and Views `AddTransient<...>` | `ServiceLocator.GetRequiredService` throws at runtime; a singleton VM shows last run's state |
| 6 | `RibbonService` | `RibbonFactory.CreateButton(panel, new RibbonButtonConfig(...), assemblyPath, availabilityClassName)` | Command exists but no button appears |
| 7 | `LECG.Tests/` | Cover the service, not the command | CI coverage gate drops if the logic landed in `LECG.Core` |

Skip 3 and 4 for a command with no UI. Never skip 5 or 6.

## Command shape

Commands are thin. They resolve, they show, they call the service, they report. No Revit geometry work in a command.

```csharp
[Transaction(TransactionMode.Manual)]
public class ThingCommand : RevitCommand
{
    public override void Execute(UIDocument uiDoc, Document doc)
    {
        ArgumentNullException.ThrowIfNull(uiDoc);
        ArgumentNullException.ThrowIfNull(doc);

        var service = ServiceLocator.GetRequiredService<ThingService>();
        var vm = ServiceLocator.GetRequiredService<ThingViewModel>();

        var view = ServiceLocator.CreateWith<ThingView>(vm);
        view.Initialize(uiDoc);
        if (view.ShowDialog() == true && vm.ShouldRun)
        {
            var result = service.DoThing(doc, vm.Selection);
            LecgDialog.Show("Thing", BuildCompletionMessage(result)); // never fail silently
        }
    }
}
```

- Base class is `RevitCommand`. **Never raw `IExternalCommand`** — the base supplies `Doc`/`UIDoc`, the logging scope, and exception-to-dialog handling.
- For a **modeless** window use `ExternalEventCommand<THandler>` instead, and route every document change through the handler. A modeless window that calls the API from a click handler crashes Revit.
- Gate availability with `ProjectDocumentAvailability` or `FamilyDocumentAvailability` if the command needs one kind of document.
- Pre-selection: seed via `SelectionSeedHelper.GetSelectedReferences(uiDoc, filter)` before showing the dialog, as `AlignEdgesCommand` does.

## Service shape

- All document writes through `ITransactionService` — never `new Transaction(` outside `Services/Infrastructure`. One legacy violation exists (`FormulaAutoGroupingCommand`); do not add a second.
- **Never take a Revit-typed interface as a constructor parameter.** `ITransactionService` — or any interface whose signatures name Revit types — in a ctor makes the class unconstructible in tests: `FileNotFoundException: Could not load file or assembly 'RevitAPI'`. Resolve it inside the method body instead; type loading stays lazy. Precedent: `src/Services/Health/WarningsService.cs:83-88`.
- Return a result the command can report on. Do not show UI from a service.
- Read `docs/ai/revit-api/SEMANTICS.md` before anything that mutates in bulk or touches units, geometry tolerance, links, or parameters. The rules that bite here: one transaction per batch (not per element), never a `FilteredElementCollector` inside a loop, quick filters before slow ones, `StorageType` and `IsReadOnly` checked before parameter access, internal units are feet, no `==` on `double` or `XYZ`.
- **Look the API up; never recall it.** Any type or overload not already used in `src/`:

```bash
rg "^Autodesk\.Revit\.DB\.Wall\.Create" docs/ai/revit-api/members.txt
```

  Absence is evidence — the index is generated from the assemblies the build compiles against, so a member that is not there does not exist in 2026 and will not compile. For what a member actually *does*, grep the NuGet XML docs rather than the web: `rg -A4 "M:Autodesk.Revit.DB.Wall.Create" "$HOME/.nuget/packages/nice3point.revit.api.revitapi/2026.4.10/ref/net8.0-windows7.0/RevitAPI.xml"`.

## View shape

Read `docs/ai/ui-guide.md` before writing XAML. Two rules carry the weight:

- **Root element is `<base:LecgWindow>`**, never a raw `Window`. The base merges the theme into that window only.
- **Never merge anything into `Application.Current.Resources`** — that is Revit's application, and implicit styles there restyle Revit's own dialogs and every other add-in.
- **A view that declares a `<base:LecgWindow.Resources>` block must merge `LecgTheme.xaml` itself.** A declared `Resources` dictionary *replaces* the one the `LecgWindow` constructor populated; it does not merge into it. Every `StaticResource` lookup then fails at runtime with `Cannot find resource named '...'` — invisible to both the compiler and the test suite. All 25 existing views do the merge; copy one.

Use the tokens in `src/Resources/` (`Base/Colors`, `Base/Brushes`, `Base/Sizes`, `Base/Fonts`). A literal hex value or hardcoded margin in a view is a bug.

## Registration

Add to the matching `Configure*` method in `Bootstrapper` — services, ViewModels, and Views each have one. Lifetimes differ and it matters: `ConfigureServices` uses `AddSingleton<Interface, Impl>()` / `AddSingleton<Concrete>()`; `ConfigureViewModels` and `ConfigureViews` use `AddTransient<>()` so every dialog opens fresh. Copy the surrounding line.

Then add the button in `RibbonService`, copying an adjacent `CreateButton` call for panel, icon, and tooltip conventions. Button text and tooltip strings live in `src/Configuration/UIConstants.cs`, panel names in `AppConstants.cs`, icons in `AppImages` — add there, not inline. The fourth argument is the availability class **name as a string** (`"LECG.Core.ProjectDocumentAvailability"`), `""` for always-on.

## Validate

```bash
dotnet build -p:SkipRevitDeploy=true
dotnet test -c Debug -p:SkipRevitDeploy=true
```

Both flags are mandatory. A plain `dotnet build` — and `dotnet test`, which builds — copies output into `%APPDATA%\Autodesk\Revit\Addins\2026\LECG\`, overwriting the live add-in, and fails with MSB3027 while Revit holds the DLLs.

Put the testable logic where a test can reach it. The runner has only Nice3point *reference* assemblies: anything whose constructor, field, or method body names a Revit type is unconstructible in tests (`FileNotFoundException: 'RevitAPI'`). Pure logic goes in `LECG.Core` or a static that takes primitives; the Revit-touching shell stays thin. CI enforces 90 % line coverage on `LECG.Core`, so Core code without a test fails the build — run the coverage command in `/lecg-phase` step 4 if you touched Core.

Build and tests prove wiring compiles and service logic works. They prove **nothing** about whether the button appears or the dialog binds — XAML compiles without its bindings resolving. That needs Revit:

1. `dotnet build -c Release` to deploy
2. Open Revit, confirm the ribbon button is present
3. Run it with a valid selection, with an empty selection, and cancel out of the dialog
4. Confirm the change is one undo step

Report per level. If the `mcp-server-for-revit` tools are connected, verify the service path and resulting elements through them and say which of steps 2–4 that covered — button presence and dialog binding still need eyes on Revit. If Revit was not opened at all, say `Revit runtime validation: not executed` and list what remains.

## Rules

- Copy the neighbouring command's shape before inventing one.
- Thin command, fat service. Any Revit geometry work in a command file is misplaced.
- Registration and ribbon are not optional — a command missing either compiles and silently does not exist.
- Never fail silently. Every failure path surfaces to the user.
