---
name: Service Architecture Guide
description: Detailed guide on Dependency Injection and Service patterns.
---

# Service Architecture

The LECG project uses **Dependency Injection (DI)** (via `Microsoft.Extensions.DependencyInjection`) to manage dependencies. This ensures testability and modularity.

## 1. The Core Principle

**"Separation of Concerns"**:

- **Commands** (`src/Commands`) handle *User Initiation* (click).
- **ViewModels** (`src/ViewModels`) handle *User Interaction* (input).
- **Services** (`src/Services`) handle *Business Logic* (Revit API, Algorithms, IO).

## 2. Creating a Service

### Step 1: Define Interface

Create an interface in `src/Interfaces`. This contracts *what* the service does, not *how*.

```csharp
// src/Interfaces/IMyService.cs
public interface IMyService
{
    void DoWork(Document doc);
    string GetData();
}
```

### Step 2: Implement Service

Create the implementation in `src/Services`.

```csharp
// src/Services/MyService.cs
public class MyService : IMyService
{
    public void DoWork(Document doc)
    {
        // ... Revit API calls ...
    }
}
```

## 3. Registering Services (`Bootstrapper.cs`)

All services and ViewModels must be registered in `src/Core/Bootstrapper.cs`.

```csharp
private static void ConfigureServices(IServiceCollection services)
{
    // Singleton: Created once, shared everywhere (Stateless services)
    services.AddSingleton<IMyService, MyService>();
    
    // Transient: Created every time it's requested (Stateful, or ViewModels)
    services.AddTransient<MyViewModel>();
}
```

## 4. Consuming Services

Use Constructor Injection to get the service you need.

```csharp
public class MyCommand : RevitCommand
{
    public override void Execute(...)
    {
        // In Commands (Entry Point), use ServiceLocator
        var service = ServiceLocator.GetRequiredService<IMyService>();
        service.DoWork();
    }
}

public class MyViewModel : BaseViewModel
{
    private readonly IMyService _service;

    // In ViewModels, use Constructor Injection
    public MyViewModel(IMyService service)
    {
        _service = service;
    }
}
```

## 5. Service Responsibilities

- **Revit API**: Services accessing Revit API should generally accept `Document` or `UIDocument` as method parameters, rather than storing them.
- **Logging**: Inject `ILogger` or use `Logger.Instance` to log operations.
- **Transactions**: Services *can* manage transactions, but prefer fine-grained transactions inside specific methods or let the Command orchestrate if it's a simple one-shot.
