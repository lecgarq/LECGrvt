---
name: lecg-command
description: Add a new Revit command to the add-in — command class, service, ViewModel, View, DI registration, and ribbon button, wired the way this repo already does it. Use when the user says "add a command", "new tool", "new ribbon button", "I want a command that...", or types /lecg-command. This is the most repeated task in the repo; follow the existing wiring exactly.
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
| 5 | `Bootstrapper.ConfigureServices/ViewModels/Views` | `AddSingleton<...>` | `ServiceLocator.GetRequiredService` throws at runtime |
| 6 | `RibbonService` | `RibbonFactory.CreateButton(panel, new RibbonButtonConfig(...))` | Command exists but no button appears |
| 7 | `LECG.Tests/` | Cover the service, not the command | — |

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
        if (view.ShowDialog() == true && vm.ShouldRun)
        {
            var result = service.DoThing(doc, vm.Selection);
            // report outcome to the user — never fail silently
        }
    }
}
```

- Base class is `RevitCommand`. **Never raw `IExternalCommand`** — the base supplies `Doc`/`UIDoc`, the logging scope, and exception-to-dialog handling.
- For a **modeless** window use `ExternalEventCommand<THandler>` instead, and route every document change through the handler. A modeless window that calls the API from a click handler crashes Revit.
- Gate availability with `ProjectDocumentAvailability` or `FamilyDocumentAvailability` if the command needs one kind of document.
- Pre-selection: seed via `SelectionSeedHelper.GetSelectedReferences(uiDoc, filter)` before showing the dialog, as `AlignEdgesCommand` does.

## Service shape

- All document writes through `ITransactionService` — never `new Transaction(` outside `Services/Infrastructure`.
- Return a result the command can report on. Do not show UI from a service.
- Check `docs/ai/revit-api/SEMANTICS.md` before anything that mutates in bulk or touches units, geometry tolerance, or linked models.
- Verify unfamiliar API calls against `docs/ai/revit-api/members.txt` before writing them.

## View shape

Read `docs/ai/ui-guide.md` before writing XAML. Two rules carry the weight:

- **Root element is `<base:LecgWindow>`**, never a raw `Window`. The base merges the theme into that window only.
- **Never merge anything into `Application.Current.Resources`** — that is Revit's application, and implicit styles there restyle Revit's own dialogs and every other add-in.

Use the tokens in `src/Resources/` (`Base/Colors`, `Base/Brushes`, `Base/Sizes`, `Base/Fonts`). A literal hex value or hardcoded margin in a view is a bug.

## Registration

Add to the matching `Configure*` method in `Bootstrapper` — services, ViewModels, and Views each have one. Follow the surrounding `AddSingleton<Interface, Impl>()` or `AddSingleton<Concrete>()` style already in that method.

Then add the button in `RibbonService`, copying an adjacent `CreateButton` call for panel, icon, and tooltip conventions.

## Validate

```bash
dotnet build -p:SkipRevitDeploy=true
dotnet test
```

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
