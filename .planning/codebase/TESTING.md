# Testing Patterns

**Analysis Date:** 2026-07-04

## Test Framework

**Test Runner:**
- Framework: xUnit 2.6.5
- Config: `LECG.Tests/LECG.Tests.csproj:14-15`
- Implicit using: `using Xunit` (global project-level import in `LECG.Tests/LECG.Tests.csproj:23`)
- Test discovery: standard xUnit attribute-based (Fact/Theory)
- Test isolation: one test class per subject file; no shared state across tests

**Assertion Library:**
- Library: FluentAssertions 6.12.0
- Config: `LECG.Tests/LECG.Tests.csproj:17`
- API: Fluent method chains ending with assertions (`.Should().Be()`, `.Should().Contain()`, etc.)
- Example patterns: `src/LECG.Tests/Services/ElementLabelServiceTests.cs:15`, `:23`, `:39`

**Mocking Framework:**
- Library: NSubstitute 5.1.0
- Config: `LECG.Tests/LECG.Tests.csproj:18`
- Usage: Limited in observed test suite; when used, follows NSubstitute patterns (`Substitute.For<T>()`)
- Revit API caveat: Revit API types (Document, FamilyInstance, etc.) cannot be proxied by NSubstitute without full Revit API DLLs at test-run time; therefore tests either mock non-Revit dependencies or use reflection for null-guard verification (example: `LECG.Tests/Services/BatchRenameExecutionServiceTests.cs:288-312`)

**Code Coverage:**
- Tool: coverlet.collector 6.0.2
- Config: `LECG.Tests/LECG.Tests.csproj:19`
- Requirements: None enforced (no explicit coverage % gates found)
- View: `dotnet test --collect:"XPlat Code Coverage"` (inferred; not yet verified by GSD run)

**Run Commands:**

```bash
# Run all tests (project root)
dotnet test

# Watch mode (interactive development)
dotnet watch test

# Coverage report
dotnet test --collect:"XPlat Code Coverage" --logger "console;verbosity=detailed"

# Filter by category (xUnit traits)
dotnet test --filter "Category=Unit"

# Verbose output
dotnet test -v n  # or --verbosity normal/detailed/diag
```

**Revit API in Tests:**
- Nice3point reference assemblies (Revit 2026.4.10) compiled into LECG.Tests.csproj (lines 35-36)
- DLLs **not** available at test-run time (reference-only compilation)
- Tests that would invoke Revit are **skip-gated** with `[Fact(Skip = "...")]` or omitted entirely
- Example: FormulaAutoGroupingCommand integration tests (transaction commit, ReplaceParameter) are manual-only (see `LECG.Tests/Commands/FormulaAutoGroupingCommandTests.cs:8-11`)

## Test File Organization

**Location Strategy:**
- Tests co-located with source folder structure
- `src/Commands/` → `LECG.Tests/Commands/`
- `src/Services/` → `LECG.Tests/Services/`
- `src/ViewModels/` → `LECG.Tests/ViewModels/`
- Utilities: `LECG.Tests/Utils/`

**File Naming:**
- Pattern: `[SubjectClass]Tests.cs`
- Examples:
  - `src/Services/ElementLabelService.cs` → `LECG.Tests/Services/ElementLabelServiceTests.cs`
  - `src/Commands/FormulaAutoGroupingCommand.cs` → `LECG.Tests/Commands/FormulaAutoGroupingCommandTests.cs`

**Namespace Mapping:**
- `src/Services/X.cs` (namespace `LECG.Services`) → `LECG.Tests/Services/XTests.cs` (namespace `LECG.Tests.Services`)
- File-scoped namespaces used (example: `LECG.Tests.Services;` in `ElementLabelServiceTests.cs:6`)

## Test Structure

**Test Suite Organization:**

```csharp
// File: LECG.Tests/Services/ElementLabelServiceTests.cs
using FluentAssertions;
using LECG.Services;

namespace LECG.Tests.Services;

public class ElementLabelServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void GetLabelsFromRaw_returns_synthetic_name_when_raw_name_is_whitespace_or_empty()
    {
        // Arrange
        var (name, _) = ElementLabelService.GetLabelsFromRaw("", "Walls", "WallType", 12345);
        
        // Act (implicit in single-line test)
        
        // Assert
        name.Should().Be("<WallType 12345>");
    }
}
```

**Key Patterns:**

**[Fact] for single test cases:**
- No parameters
- One scenario per test
- Example: `LECG.Tests/Services/ElementLabelServiceTests.cs:10-16`

**[Theory] + [InlineData] for parameterized tests:**
```csharp
[Theory]
[InlineData(1, new[] { 1 })]
[InlineData(3, new[] { 1, 2, 3 })]
[InlineData(0, new int[0])]
public void GetPasses_ReturnsCorrectSequence(int input, int[] expected)
{
    // Act
    var result = PurgeSequence.GetPasses(input);
    
    // Assert
    result.Should().BeEquivalentTo(expected);
}
```
- Example: `LECG.Tests/Services/PurgeSequenceTests.cs:9-20`
- Combines multiple scenarios in one parameterized test body

**Trait Categorization:**
```csharp
[Trait("Category", "Unit")]
[Trait("Category", "Renaming")]
[Trait("Category", "FormulaGrouping")]
```
- Standardized category names observed: "Unit", "Renaming", "FormulaGrouping"
- Enables filtering: `dotnet test --filter "Category=Unit"`

**Test Naming Convention:**
- Format: `[MethodName]_[Condition]_[Expected]`
- Examples:
  - `GetLabelsFromRaw_returns_synthetic_name_when_raw_name_is_whitespace_or_empty`
  - `UpdateFormula_WhenNameMatchesToken_ReplacesName`
  - `EvaluateFamilyParamSkipReason_BuiltInParam_ReturnsBuiltInReason`
- Pattern is self-documenting; reading test name tells expected behavior

**Skip-Gating for Incomplete Work:**
```csharp
[Fact(DisplayName = "anchor — fixture exists; behavioural tests skip-gated by implementing plan")]
public void Fixture_Anchor_Exists()
{
    Assert.NotNull(typeof(BatchRenameExecutionService));
}

// Commented-out tests with skip markers:
// [Fact(Skip = "Plan 05-02 — wave 2 coverage")]
// public void EvaluateFamilyParamSkipReason_...() { }
```
- Example: `LECG.Tests/Services/BatchRenameExecutionServiceTests.cs:28-32`
- Anchor test keeps `--filter` discovery working even when all behavioral tests skipped
- DisplayName documents reason and owning plan

## Test Structure (Detailed)

**Arrange-Act-Assert Implicit:**
- No explicit // Arrange, // Act, // Assert comments
- Implied by structure:
  1. Setup (local variables, test data)
  2. Single call/invocation
  3. Assertion(s)

**Example with Complex Setup:**
```csharp
[Fact]
public void GroupCheckedFamilyParameterItems_FiltersUnchecked_PreservesOrder()
{
    // Arrange
    var checked1 = new ElementRowViewModel { Id = 10, Name = "Param1", ... };
    var checked2 = new ElementRowViewModel { Id = 10, Name = "Param2", ... };
    var uncheckedRow = new ElementRowViewModel { Id = 20, Name = "Param3", ... };
    var items = new List<ElementRowViewModel> { checked1, checked2, uncheckedRow };
    
    // Act
    var result = BatchRenameExecutionService.GroupCheckedFamilyParameterItemsForTest(items);
    
    // Assert
    result.Should().HaveCount(1, because: "only family Id=10 has checked items");
    result[10].Should().HaveCount(2, because: "two checked items belong to family Id=10");
}
```
- Example: `LECG.Tests/Services/BatchRenameExecutionServiceTests.cs:259-275`
- `because:` clauses document **why** assertions matter (fluent diagnostic)

## Mocking

**Strategy:** Minimal mocking of Revit API; mock non-Revit dependencies only

**NSubstitute Pattern (when applicable):**
```csharp
var mockService = Substitute.For<IMyService>();
mockService.DoSomething(Arg.Any<string>()).Returns("result");
```
- Not heavily used in observed test suite due to Revit API limitations
- Example of explicit reflection-based testing instead: `LECG.Tests/Services/BatchRenameExecutionServiceTests.cs:294-307`

**Revit API Testing Workaround:**
- For services with Revit API dependencies, test **pure data logic** only
- Pure-data helpers: static methods that accept/return data structures (no Revit API objects)
- Example: `BatchRenameExecutionService.EvaluateFamilyParamSkipReason()` takes primitive types + collections, returns string or null
- Behavioral integration tests (transaction commit, element modification): skip-gated or manual-only

**Test Fixtures / Factory Objects:**
- Local nested classes for minimal test data:
```csharp
private sealed record TestWidget(string Name);

private sealed class TestWidgetValidator : AbstractValidator<TestWidget>
{
    public TestWidgetValidator()
    {
        RuleFor(widget => widget.Name).NotEmpty();
    }
}
```
- Example: `LECG.Tests/DependencyInjection/ValidatorRegistrationTests.cs:33-41`
- Self-contained; no separate factory files needed for simple cases

## Coverage Areas

**Observed Test Suites:**

**Unit Tests (Logic-Level):**
- `ElementLabelServiceTests.cs` — Label synthesis logic (6 tests)
- `FormulaNameUpdaterTests.cs` — Formula token replacement (5 tests)
- `PurgeSequenceTests.cs` — Progress sequence calculation (3 tests)

**Behavior Tests (Decision Paths):**
- `BatchRenameExecutionServiceTests.cs` — Skip-reason evaluation, log formatting, progress tracking (14+ tests)
  - Tests each branch in `EvaluateFamilyParamSkipReason()` (4 branches)
  - Tests each branch in `EvaluateStandardItemSkipReason()` (6 branches)
  - Tests `FormatSafeRenameLog()` (4 branches)

**Validation Tests:**
- `ValidatorRegistrationTests.cs` — Validator registration via DI reflection (1 test)
- Validator implementations not directly tested; assumed covered by FluentValidation framework

**Cache/Infrastructure Tests:**
- `AppMemoryCacheTests.cs` — Cache hit/miss, expiration (1 test)

**Command Tests:**
- `FormulaAutoGroupingCommandTests.cs` — Pure-logic formula parsing (3 tests)
- No behavioral tests; integration tests are manual-only

## Test Patterns

**Pattern 1: Pure Data Helper Coverage**

```csharp
[Fact]
public void EvaluateFamilyParamSkipReason_BuiltInParam_ReturnsBuiltInReason()
{
    // Built-in param: paramIdValue < 0 → built-in branch
    var result = BatchRenameExecutionService.EvaluateFamilyParamSkipReason(
        paramIdValue: -1,
        isReporting: false,
        paramName: "Width",
        newName: "PanelWidth",
        existingParamNames: Array.Empty<string>(),
        formulaReferenced: new HashSet<string>(),
        dimensionLabels: new HashSet<string>(),
        elementAssociated: new HashSet<string>());

    result.Should().NotBeNull();
    result.Should().Contain("built-in");
}
```
- Tests a single decision branch
- Passes minimal data to trigger one path
- Asserts output contains expected keyword(s)
- Example: `LECG.Tests/Services/BatchRenameExecutionServiceTests.cs:40-56`

**Pattern 2: Theory with InlineData for Branch Coverage**

```csharp
[Theory]
[InlineData(null, null)]
[InlineData("", "")]
[InlineData("   ", "   ")]
[InlineData(null, "")]
[InlineData("", null)]
public void GetLabelsFromRaw_never_returns_null_or_whitespace_for_either_field(
    string? rawName, string? rawCategory)
{
    var (name, category) = ElementLabelService.GetLabelsFromRaw(rawName!, rawCategory!, "Element", 42);
    name.Should().NotBeNullOrWhiteSpace();
    category.Should().NotBeNullOrWhiteSpace();
}
```
- Parameterized test covers multiple edge cases
- One assertion per parameter set
- Example: `LECG.Tests/Services/ElementLabelServiceTests.cs:58-71`

**Pattern 3: Constructor Null-Guard Testing via Reflection**

```csharp
[Fact]
public void Constructor_NullFormulaUpdateService_Throws()
{
    var ctor = typeof(BatchRenameExecutionService).GetConstructors()[0];
    
    Action act = () =>
    {
        try
        {
            ctor.Invoke(new object?[] { null, null, null });
        }
        catch (System.Reflection.TargetInvocationException tie)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(tie.InnerException!).Throw();
        }
    };

    act.Should().Throw<ArgumentNullException>()
        .WithParameterName("formulaUpdateService");
}
```
- Used when constructor parameter types include Revit API (cannot mock)
- Bypasses Revit DLL requirement via reflection-based invocation
- Validates null-guard by expecting ArgumentNullException
- Example: `LECG.Tests/Services/BatchRenameExecutionServiceTests.cs:287-312`

**Pattern 4: Collection Filtering & Order Verification**

```csharp
[Fact]
public void GroupCheckedFamilyParameterItems_FiltersUnchecked_PreservesOrder()
{
    var checked1 = new ElementRowViewModel { Id = 10, ... };
    var checked2 = new ElementRowViewModel { Id = 10, ... };
    var uncheckedRow = new ElementRowViewModel { Id = 20, ... };
    var items = new List<ElementRowViewModel> { checked1, checked2, uncheckedRow };

    var result = BatchRenameExecutionService.GroupCheckedFamilyParameterItemsForTest(items);

    result.Should().HaveCount(1, because: "only family Id=10 has checked items");
    result[10].Should().HaveCount(2, because: "two checked items belong to family Id=10");
    result[10][0].Should().BeSameAs(checked1, because: "order preserved");
    result[10][1].Should().BeSameAs(checked2, because: "order preserved");
    result.Should().NotContainKey(20, because: "unchecked row is excluded");
}
```
- Tests filtering logic (inclusion/exclusion criteria)
- Verifies ordering preservation
- Asserts collection cardinality
- Example: `LECG.Tests/Services/BatchRenameExecutionServiceTests.cs:259-275`

**Pattern 5: Logical Composition (Multiple Independent Tests)**

```csharp
[Fact]
public void UpdateFormula_WhenNameMatchesToken_ReplacesName()
{
    var result = FormulaNameUpdater.UpdateFormula("Width + Height", "Width", "PanelWidth");
    result.Should().Be("PanelWidth + Height");
}

[Fact]
public void UpdateFormula_WhenNameAppearsMultipleTimes_ReplacesAllTokenMatches()
{
    var result = FormulaNameUpdater.UpdateFormula("Width + Width / 2", "Width", "PanelWidth");
    result.Should().Be("PanelWidth + PanelWidth / 2");
}

[Fact]
public void UpdateFormula_WhenNameIsPartialToken_DoesNotReplace()
{
    var result = FormulaNameUpdater.UpdateFormula("A + A1 + Length_A", "A", "B");
    result.Should().Be("B + A1 + Length_A");
}
```
- Each test validates one logical condition
- Easy to identify which condition fails
- Example: `LECG.Tests/Services/FormulaNameUpdaterTests.cs:10-32`

## Test Execution

**Build Integration:**
- `LECG.Tests` compiles with `Nice3point.Revit.Api.RevitAPI 2026.4.10` reference assembly (no local Revit install required)
- Build output: `bin/x64/[Configuration]/net8.0-windows/LECG.Tests.dll`
- Tests run on CI runners (GitHub Actions) without Revit present

**Local Execution (Development):**
```bash
# Run all tests
cd C:\LECG\RevitAddins\LECG
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~ElementLabelServiceTests"

# Watch mode (auto-rerun on file change)
dotnet watch test

# Filter by trait
dotnet test --filter "Category=Unit"
```

**Known Limitations:**
- No Revit instance required (tests compile against reference-only DLLs)
- Revit-dependent behavioral tests cannot run headless (skip-gated)
- Manual testing required for command lifecycle, transaction commit/rollback, modeless UI flows

## Test Coverage Gaps

**Areas Without Automated Tests:**
1. **Command Execution** — RevitCommand.Execute() full path (Revit API interaction)
   - Why: Requires live Revit instance; tests compile against reference-only DLLs
   - Risk: Logic errors in command entry points discovered only at runtime in Revit
   - Path to coverage: Manual smoke test (checklist in `docs/ai/revit-smoke-test.md`)

2. **Transaction Behavior** — ITransactionService.Run() with live Document
   - Why: Revit Document object not available in test runner
   - Risk: Commit/rollback edge cases, failure handler behavior
   - Path to coverage: Revit integration test (manual or future Revit test framework)

3. **External Event Handlers** — ExternalEventCommand<T> lifecycle
   - Why: ExternalEvent requires Revit UIApplication context
   - Risk: State leakage between invocations of same command type
   - Path to coverage: Revit integration test with modeless UI

4. **WPF/UI Logic** — View/ViewModel bindings, XAML parsing
   - Why: No WPF test framework present; XAML not validated at compile-time
   - Risk: Binding errors, layout issues discovered only when view shown
   - Path to coverage: Revit smoke test + manual UI inspection

5. **Service Infrastructure** — Logging dispatcher handling, cache thread-safety
   - Why: Complexity of async/threading requires live multi-threaded environment
   - Risk: Race conditions, dispatcher deadlocks
   - Path to coverage: Stress tests (not yet implemented)

---

## Practice Candidates

| Tag | Scope | Evidence | Why It Matters | Preserve By |
|-----|-------|----------|----------------|-------------|
| [Observed] | Project | `LECG.Tests/LECG.Tests.csproj:14-20` | xUnit 2.6.5 + FluentAssertions 6.12.0 is the standard test stack | Use [Fact]/[Theory] for new tests; chain assertions with .Should(); don't introduce other assertion libraries |
| [Observed] | Project | `LECG.Tests/LECG.Tests.csproj:23` (global using) | Implicit xunit namespace reduces boilerplate | Maintain global using; don't add test-specific types to it |
| [Observed] | Project | `LECG.Tests/Services/ElementLabelServiceTests.cs:10-12` | [Trait("Category", "Unit")] standard categorization | Tag all tests with [Trait("Category", ...)]; use consistent category names ("Unit", "Renaming", etc.) for filtering |
| [Observed] | Project | `.Tests folder structure mirrors src/` | Tests discoverable by feature; easy to add related tests | Keep folder/file mapping: src/Services/ ↔ LECG.Tests/Services/; src/Commands/ ↔ LECG.Tests/Commands/ |
| [Observed] | Test Suite | `LECG.Tests/Services/ElementLabelServiceTests.cs:1-6,8` | [Method]_[Condition]_[Expected] naming; file-scoped namespaces | New test files: use pattern; add file-scoped namespace (no braces); import only what's needed |
| [Observed] | Test Suite | `LECG.Tests/Services/BatchRenameExecutionServiceTests.cs:40-56` | Pure data helper testing: isolated branch → assertion on output | Tests calling static methods on services (not services requiring Revit API) test one branch per [Fact]; validate output contains expected keyword(s) or exact match |
| [Observed] | Test Suite | `LECG.Tests/Services/ElementLabelServiceTests.cs:58-71` | [Theory] + [InlineData] for parameter variations | Use [Theory] to test multiple edge cases (null, empty, whitespace) in one test body; one [InlineData] row per scenario |
| [Observed] | Test Suite | `LECG.Tests/Services/BatchRenameExecutionServiceTests.cs:287-312` | Reflection-based ctor null-guard testing when Revit API types involved | Use reflection Invoke + ExceptionDispatchInfo.Capture for ctor tests with Revit API deps; validates ArgumentNullException by ParameterName |
| [Observed] | Test Suite | `LECG.Tests/Services/BatchRenameExecutionServiceTests.cs:259-275` | FluentAssertions `because:` clauses explain assertion intent | Add `because: "reason"` clauses to complex assertions; improves failure diagnostics |
| [Concern] | Project | `LECG.Tests/Services/BatchRenameExecutionServiceTests.cs:28-32` (anchor test) | Skip-gated tests must have an anchor [Fact] to keep test discovery working | When hiding tests with skip/DisplayName, include an anchor test with simple Assert.NotNull(typeof(Subject)); allows --filter to still report that test class exists |
| [Inferred] | Project | `LECG.Tests/DependencyInjection/ValidatorRegistrationTests.cs:33-41` | Local nested test fixture classes for self-contained test data | For simple test doubles, define sealed record/class locally in test file; don't create separate fixture files |
| [Concern] | Project | `LECG.Tests/Commands/FormulaAutoGroupingCommandTests.cs:7-11` | Behavioral integration tests (transaction commit, Revit API calls) skip-gated | Document in test class summary why integration tests are skipped; link to manual smoke-test checklist; note the Revit runtime requirement |
