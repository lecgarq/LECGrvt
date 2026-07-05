# Coding Conventions

**Analysis Date:** 2026-07-04

## Naming Patterns

**Files:**
- Classes and interfaces: PascalCase (e.g., `CategoryChangerCommand.cs`, `ITransactionService.cs`)
- Test files: [TestSubject]Tests.cs (e.g., `ElementLabelServiceTests.cs`)
- Validators: [Subject]Validator.cs (e.g., `ConvertCadViewModelValidator.cs`)

**Classes:**
- PascalCase, no suffix for concrete classes
- All commands inherit from `RevitCommand` or `ExternalEventCommand<T>`
- All ViewModels marked `partial` (enables source generators for CommunityToolkit.Mvvm)
- Service implementations suffixed with "Service" (e.g., `TransactionService`, `AlignEdgesService`)

**Interfaces:**
- I-prefix convention: `ITransactionService`, `IAlignEdgesService`, `ILogger`
- Located in `Services/Infrastructure/` folder (e.g., `src/Services/Infrastructure/ITransactionService.cs`)

**Private Fields:**
- camelCase with leading underscore: `_viewModel`, `_sync`, `_pendingEntries`
- Marked `readonly` when not mutated (e.g., `private readonly object _sync = new object()`)
- Protected fields: same camelCase underscore convention (e.g., `protected LECG.Views.LogView? _logWindow`)

**Parameters:**
- camelCase: `uiDoc`, `doc`, `commandData`, `action`, `preprocessor`

**Properties:**
- PascalCase: `Title`, `IsBusy`, `ShouldRun`, `Doc`, `UIDoc`, `CommandData`
- Auto-properties or with backing field via `SetProperty()` (MVVM pattern)
- Nullable explicitly marked: `public Document? Doc { get; set; }`

**Methods:**
- PascalCase: `Execute()`, `GetOrCreate()`, `RunConditional()`, `ShowLogWindow()`
- Verb-first naming: `Get*`, `Set*`, `Run*`, `Add*`, `Remove*`
- Private helpers: `IsExpected*`, `Should*`, `Get*`, `Evaluate*` (e.g., `IsExpectedCategoryChangerException()`)

**Variables:**
- camelCase: `factoryCalls`, `successCount`, `failCount`
- Boolean prefixes: `is`, `has`, `should` (e.g., `isReporting`, `hasSelection`, `shouldCommit`)

## Code Style

**Formatting:**
- Tool: `.editorconfig` rule IDE0055 (warning level)
- Target: consistent formatting across all `.cs` files in `src/` and `LECG.Core/`, stricter in `LECG.Core/**/*.cs`
- Indentation: 4 spaces (inferred from code)
- Line endings: LF (Windows/.NET standard)

**Linting / Analysis:**
- Controlled opt-in via `.editorconfig` (`dotnet_analyzer_diagnostic.severity = none` base, then explicit enablement)
- Enabled rules (warning level):
  - `CA1062`: Validate arguments of public methods (null guards required)
  - `CA2000`: Dispose objects before losing scope (soft rollout via suggestion in main, warning in LECG.Core)
  - `CA2016`: Forward CancellationToken parameters to methods that accept them (suggestion in main, warning in LECG.Core)
- Suppressed (expected conflicts):
  - `CS0436`: Type conflicts from MVVM source generators — commented as expected in `Directory.Build.props:11-12`
  - `MSB3277`: Assembly version conflicts among Revit 2026 API DLLs — downgraded via `MSBuildWarningsAsMessages` in `Directory.Build.props:13-18`

**Null Handling:**
- Nullable reference types enabled project-wide (`<Nullable>enable</Nullable>`)
- Null checks use `ArgumentNullException.ThrowIfNull()` (example: `src/Core/RevitCommand.cs:107-108`)
- Null-or-whitespace checks use `ArgumentException.ThrowIfNullOrWhiteSpace()` (example: `src/Services/Infrastructure/TransactionService.cs:27`)
- Optional parameters marked with `?` explicitly (e.g., `IFailuresPreprocessor? preprocessor = null`)
- Fallback patterns when ServiceLocator may not be initialized: cast to interface, use optional chaining (example: `src/App.cs:91-92`)

## Import Organization

**Order (observed pattern):**
1. System namespaces (e.g., `using System`, `using System.IO`, `using System.Collections.Generic`)
2. Revit API namespaces (e.g., `using Autodesk.Revit.DB`, `using Autodesk.Revit.UI`)
3. External packages (e.g., `using Microsoft.Extensions.Logging`, `using FluentValidation`)
4. Local LECG namespaces (e.g., `using LECG.Core`, `using LECG.Services`)

**Aliasing:**
- Used to disambiguate: `using RevitExceptions = Autodesk.Revit.Exceptions` (example: `src/Commands/CategoryChangerCommand.cs:12`)
- Also: `using MsLogLevel = Microsoft.Extensions.Logging.LogLevel` (example: `src/Services/Infrastructure/Logging/Logger.cs:6`)

**Alphabetization:**
- Within each group, namespaces are sorted alphabetically (observable in all source files)

## Error Handling

**Pattern Matching in Catch:**
- Use `catch (Exception ex) when (IsExpectedExceptionType(ex))` for selective exception handling
- Example: `src/Commands/CategoryChangerCommand.cs:108` (`when (IsPlatformLimitException(ex))`)
- Allows nested exception handling without swallowing unexpected failures

**Exception Categorization:**
- Private static methods ending in `IsExpected*` or `IsPlatformLimit*` to classify exceptions
- Example: `IsExpectedCategoryChangerException()` checks for ArgumentException, InvalidOperationException, or Revit's InvalidOperationException
- Centralizes exception type logic; reused across try-catch blocks

**Null Guard Pattern:**
- Entry-point arguments guarded: `ArgumentNullException.ThrowIfNull(uiDoc); ArgumentNullException.ThrowIfNull(doc)`
- Pattern-matching guards before operations: `if (_viewModel == null || _service == null) return;`
- Example: `src/Commands/CategoryChangerCommand.cs:70-71`

**Nested Try-Catch for Layered Operations:**
- Outer try: main operation with general exception handling
- Inner try: specialized sub-operations with their own recovery logic
- Example: `src/Commands/CategoryChangerCommand.cs:73-156` (multiple nesting levels for family processing → category change → transplant fallback)

**Fallback / Safe Handling When Infrastructure Not Ready:**
- Check for service availability before logging: `(Core.ServiceLocator.GetService<Services.Logging.ILogger>() as Services.Logging.ILogger)?`
- Graceful degradation when Dispatcher unavailable: `System.Diagnostics.Debug.WriteLine()` as backup
- Example: `src/App.cs:91-92`

## Logging

**Framework:** Custom `ILogger` interface + forwarding to `Microsoft.Extensions.Logging.ILoggerFactory`

**Method Signatures:**
```csharp
public void Log(string message, string scope);
public void LogSuccess(string message, string scope);
public void LogWarning(string message, string scope, Exception? exception = null);
public void LogError(string message, string scope, Exception? exception = null);
```

**Scope Parameter:**
- Required (non-optional) for all logging calls
- Usually the calling class name or feature name (e.g., `scope: "App"`, `scope: GetType().Name`)
- Used as logger category when forwarding to ILoggerFactory

**When to Log:**
- Entry points (commands): `LogInformation("Executing Revit command {CommandName}", commandName)` — `src/Core/RevitCommand.cs:41`
- Completion: `LogInformation("Completed Revit command {CommandName}", commandName)` — `src/Core/RevitCommand.cs:44`
- Errors: `LogError(ex, "Revit command {CommandName} failed", commandName)` — `src/Core/RevitCommand.cs:50`
- Progress updates: `UpdateProgress(percent, status)` for UI display
- User-facing operations: via custom `ILogger.Log()` to UI + structured logging simultaneously

**Structured Logging Patterns:**
- Use structured parameters: `logger.Log(level, "{Message}", entry.Message)` instead of string interpolation
- Supports structured log analysis (JSON output in production)
- Bridge class `Logger` (custom ILogger) formats to structured logger: `src/Services/Infrastructure/Logging/Logger.cs:183-210`

**UI vs. Structured:**
- Custom `ILogger` queues messages for UI display (ObservableCollection<LogEntry>)
- Concurrently forwards to `ILoggerFactory` for structured/file logging
- Dispatcher-aware async handling for thread safety

## Comments

**When to Comment:**
- Complex branching logic (e.g., `src/Commands/CategoryChangerCommand.cs:184-203` multi-pass batch processing)
- Exception-handling rationale: `src/App.cs:21-26` explains pack:// URI workaround
- Lifecycle constraints: `src/Services/Infrastructure/Logging/Logger.cs:40-42` explains why Dispatcher is not auto-captured
- Non-obvious algorithm choices

**Avoid:**
- Restating code (e.g., "increment counter" above `i++`)
- Line-by-line explanations of obvious statements

**Summary Headers:**
- Used in large test classes to organize test groups (example: `src/LECG.Tests/Services/BatchRenameExecutionServiceTests.cs:23-26`, `:35-36`, `:111-112`)

**JSDoc/XML Comments:**
- Minimal observed usage; docstrings used mainly for public interface contracts
- Example: `src/LECG.Tests/Commands/FormulaAutoGroupingCommandTests.cs:7-11` (class-level summary)

## Function Design

**Size:**
- Typical: 20–50 lines (example: `src/Core/RevitCommand.cs` methods range 10–30 lines)
- Larger methods acceptable if single responsibility (example: `src/Commands/CategoryChangerCommand.cs:68-156` multi-pass logic, justified)
- Private helpers extracted for testability (example: `IsExpectedCategoryChangerException()` repeated 3+ times)

**Parameters:**
- Favor explicit parameters over long argument lists (example: `BatchRenameExecutionService.EvaluateFamilyParamSkipReason()` takes ~8 params but well-structured)
- Use records for complex multi-value returns: `AlignEdgesSourceResult` — a data-only record type
- Nullable parameters spelled out: `Action<FailureHandlingOptions>? configureOptions`

**Return Values:**
- Explicit return types (no implicit void assumption)
- Boolean returns for success/failure: `bool RunConditional()`, `bool IsPlatformLimitException()`
- Generic type returns for flexible use: `T Run<T>(Document doc, string name, Func<Document, T> action)`
- Nullable returns where absence is valid: `Family? RecreateAs()`, `string? GetUnsupportedReason()`

**Early Exit:**
- Guard clauses at top: `if (_viewModel == null || _service == null) return;`
- Avoids deep nesting

## Module Design

**Exports:**
- Interfaces marked `public interface I*`
- Implementation classes marked `public class *Service : I*`
- Private helpers remain internal: `private static bool IsExpected*()`

**Barrel Files:**
- Not observed as a standard pattern in this codebase
- Namespace organization relied on instead (`using LECG.Services.Interfaces`)

**Service Injection:**
- All services resolved via `ServiceLocator.GetRequiredService<T>()` or `ServiceLocator.GetService<T>()`
- Never `new` services directly (enforced by DI setup)
- Example: `src/Commands/CategoryChangerCommand.cs:24-26`

**Partial Classes:**
- ViewModels use `partial` to support CommunityToolkit.Mvvm source generators
- Example: `src/ViewModels/BaseViewModel.cs:10` (`public abstract partial class BaseViewModel`)
- Do not artificially split implementation; used only for generated-code interop

---

## Practice Candidates

| Tag | Scope | Evidence | Why It Matters | Preserve By |
|-----|-------|----------|----------------|-------------|
| [Observed] | Project | `src/Core/RevitCommand.cs:107-108`, `src/Services/Infrastructure/TransactionService.cs:27` | null safety enforced; reduces NullReferenceException surface | Always use ArgumentNullException.ThrowIfNull()/WhiteSpace() for public/external entry points; mark new service params with `?` if truly optional |
| [Observed] | Project | `src/App.cs:91-92`, `src/Services/Infrastructure/Logging/Logger.cs:70-71` | Graceful degradation when DI not ready | When bootstrapping or in app-lifecycle code, check ServiceLocator results; provide fallback (e.g., Debug.WriteLine) |
| [Observed] | Project | `.editorconfig:3-19`, `Directory.Build.props:11-18` | Enforced consistency + expected conflicts documented | Maintain .editorconfig IDE0055 warning; don't remove CS0436/MSB3277 suppressions without justification (both are documented as expected) |
| [Observed] | Project | `src/Core/RevitCommand.cs:30-61` | Uniform error handling + logging scope; exception dialog shown to user | New commands inherit RevitCommand; never raw IExternalCommand; use RevitCommand.Execute(UIDocument, Document) override |
| [Observed] | Folder (Commands) | `src/Commands/CategoryChangerCommand.cs:108`, `src/Commands/*.cs` | Allows selective exception handling without masking unexpected failures | Use catch (Exception ex) when (IsExpected*()) pattern; keep categorization logic in private static helpers |
| [Observed] | Folder (Services) | `src/Services/Infrastructure/TransactionService.cs:8-138` | Centralized transaction management; commit/rollback guaranteed | All document writes go through ITransactionService; raw Transaction() only inside infrastructure services with comment |
| [Observed] | Service | `src/Services/Infrastructure/Logging/Logger.cs:14,40-42` | ILogger scope required; testable sync behavior; Dispatcher-aware | Always pass scope parameter to Log/LogWarning/LogError; call SetDispatcher() in UI contexts only |
| [Observed] | Folder (ViewModels) | `src/ViewModels/BaseViewModel.cs:10,23-27` | CommunityToolkit.Mvvm source generation; ObservableObject notification | New ViewModels: mark partial, inherit BaseViewModel, use RelayCommand properties (lazy-init with ??=) |
| [Inferred] | Folder (Services) | `src/Services/Infrastructure/ITransactionService.cs:1-20`, `src/Services/Alignment/AlignEdgesService.cs:1-20` | Interface-first design; testability via DI/mocking | Services defined as interfaces in Infrastructure/; implementations in their feature folders; no service instantiated via new |
| [Concern] | Project | `src/Commands/CategoryChangerCommand.cs:108,126-127` | Nested try-catch can hide failures if exception filters too broad | Use specific when-guards; don't catch bare Exception then when() without understanding all call paths |
