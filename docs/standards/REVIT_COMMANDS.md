---
name: Revit Commands Guide
description: Best practices for implementing IExternalCommand in this framework.
---

# Revit Commands

Standard Revit API Commands (`IExternalCommand`) are the entry points for all functionality. In the LECG framework, they should be **Thin Wrappers**.

## 1. Structure

Feature logic should **NOT** exist in the Command class. The Command class exists to:

1. Satisfy the Revit API contract.
2. Initialize the DI Container (if needed, though usually done at App startup).
3. Resolve dependencies (Service/ViewModel).
4. Launch the UI or Component.

### The `RevitCommand` Base Class

All commands inherit from `LECG.Core.RevitCommand` instead of implementing `IExternalCommand` directly.

**Features of `RevitCommand`:**

- **Error Handling**: Wraps execution in `try/catch` and logs errors automatically.
- **Logging**: Provides helper `ShowLogWindow()` and `Log()`.
- **Transaction**: Optional auto-transaction via `TransactionName` property.

```csharp
[Transaction(TransactionMode.Manual)]
public class MyCommand : RevitCommand
{
    // Return null to manage transactions manually (Preferred for UI commands)
    // Return string to wrap entire Execute in one transaction.
    protected override string? TransactionName => null; 

    public override void Execute(UIDocument uiDoc, Document doc)
    {
        // 1. Resolve
        var vm = ServiceLocator.GetRequiredService<MyViewModel>();
        
        // 2. Show UI
        var view = new MyView(vm);
        if (view.ShowDialog() == true)
        {
            // 3. Execute logic (which might be in VM or Service)
        }
    }
}
```

## 2. Selection Filters (`ISelectionFilter`)

If a command requires input, create a private `ISelectionFilter` class inside the Command file to keep it encapsulated, unless it's reusable (then move to `src/Utils/Filters`).

```csharp
private class DoorFilter : ISelectionFilter
{
    public bool AllowElement(Element elem) => elem.Category.Id.Value == (long)BuiltInCategory.OST_Doors;
    public bool AllowReference(Reference referrer, XYZ pos) => true;
}
```

## 3. Transaction Mode

Always use `[Transaction(TransactionMode.Manual)]`.

- **Why**: Gives us full control to commit/rollback multiple times, show progress updates in between, and handle errors gracefully within the UI loop.
- **How**: Use `using (Transaction t = new Transaction(doc, "Name")) { t.Start(); ... t.Commit(); }` inside your Services.
