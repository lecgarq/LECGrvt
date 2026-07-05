# Architecture Research

**Domain:** Brownfield hardening of a layered MVVM + service-oriented Revit 2026 add-in (C#/.NET 8, WPF)
**Researched:** 2026-07-05
**Confidence:** HIGH (all findings verified directly against current source in `src/`, not just codebase docs)

> **Correction to `.planning/codebase/CONCERNS.md` (evidence-based, current code beats memory):**
> Two "missing" items in CONCERNS.md/PROJECT.md are **already partially implemented** as of the current tree (commit `0ed18e5`):
> 1. `RibbonButtonConfig`/`RibbonFactory.CreateButton`/`AddItemToPulldown` already accept an `availabilityClassName` and set `PushButtonData.AvailabilityClassName` (`src/Core/Ribbon/RibbonFactory.cs:18-33,78-94`).
> 2. `RibbonService.InitializeRibbon()` already wires `LECG.Core.ProjectDocumentAvailability` or `LECG.Core.FamilyDocumentAvailability` onto most panels (Home, Toposolids, Health, Visualization, Standards, ModelOrganization — `src/Core/Ribbon/RibbonService.cs:47,61,164,202,300-301,369`).
>
> **What is still actually missing:** the `Align` pulldown panel (`CreateAlignPanel`, `src/Core/Ribbon/RibbonService.cs:230-295`) passes `availability = ""` for all 8 sub-items (AlignLeft/Center/Right/Top/Middle/Bottom, DistributeH/V) with a comment claiming "Available always due to Master availability" — but `CreatePulldownButton` (`RibbonFactory.cs:55-76`) never sets any `AvailabilityClassName` on the pulldown itself, so there is in fact **no availability restriction at all** on any Align command. These are project-only operations (they act on selected model elements) and should get `ProjectDocumentAvailability` like everything else. This reframes that concern from "build from scratch" to "close one real gap + verify the rest by inspection/runtime check."

## Standard Architecture

### System Overview

```
┌──────────────────────────────────────────────────────────────────────┐
│  Revit Host Process (shared AppDomain w/ Enscape, ModPlus, Forma)     │
├──────────────────────────────────────────────────────────────────────┤
│  App : IExternalApplication (src/App.cs)                              │
│   - OnStartup: pack:// URI registration, Bootstrapper.Initialize(),   │
│     ServiceLocator.Initialize(), IRibbonService.InitializeRibbon()    │
├──────────────────────────────────────────────────────────────────────┤
│  Commands (src/Commands/, 34+ classes)     Ribbon (src/Core/Ribbon/)  │
│  ┌───────────────┐  ┌────────────────────┐  ┌─────────────────────┐  │
│  │ RevitCommand  │  │ ExternalEventCmd<T> │  │ IExternalCommand    │  │
│  │ (modal)       │  │ (modeless, static   │  │ Availability        │  │
│  │               │  │  handler+event)     │  │ (Project/Family)    │  │
│  └──────┬────────┘  └─────────┬───────────┘  └─────────────────────┘  │
├─────────┼─────────────────────┼───────────────────────────────────────┤
│  Views & ViewModels (WPF MVVM, CommunityToolkit.Mvvm)                  │
│  ┌────────────────┐  BaseViewModel: Apply/Cancel/ShouldRun            │
│  │ IValidationService (FluentValidation) ← CanApply()                 │
│  └────────────────┘                                                   │
├─────────────────────────────────────────────────────────────────────┤
│  Domain Services (src/Services/{Feature}/)                            │
│  Alignment · CadConversion · FamilyConversion · Materials ·           │
│  PurgeAndCompaction · RenderAppearance · Topography · Renaming        │
│   - Some monolithic (BatchRenameExecutionService 1019 lines)          │
│   - 54 bare `catch` blocks scattered across 35 files                  │
├──────────────────────────────────────────────────────────────────────┤
│  Infrastructure Services (src/Services/Infrastructure/)                │
│  ITransactionService · ILogger (Serilog + UI log) · IAppMemoryCache · │
│  ISelectionCoordinator · ILinkedModelExportService                    │
├──────────────────────────────────────────────────────────────────────┤
│  Revit API (Autodesk.Revit.DB) · LECG.Core (Revit-free shared lib) ·  │
│  Third-party deps at risk: Clipper2 2.0.0 vs 1.1.1.0 (other add-ins), │
│  MS.Ext.DI.Abstractions 8.0.0 vs 9.0.0 (other add-ins)                │
└──────────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities (hardening-relevant subset)

| Component | Responsibility | Current Implementation |
|-----------|----------------|------------------------|
| `ExternalEventCommand<THandler>` (`src/Core/ExternalEventCommand.cs`) | Owns static `_handler`/`_externalEvent` per closed generic type; exposes `GetOrCreateHandler()`, `RaiseExternalEvent()` | 29 lines, no reentrancy guard today. Every `Command.Execute()` call creates a *new* View+ViewModel but calls `handler.Initialize(newViewModel, ...)` on the *same* static handler instance — a second click before the first completes overwrites the handler's active state |
| `IExternalCommandAvailability` implementations (`ProjectDocumentAvailability`, `FamilyDocumentAvailability`, `src/Core/*.cs`) | Gate command enablement by document type | Already exist and are already wired into `RibbonService` for most panels; NOT wired for the Align pulldown sub-items |
| `RibbonFactory` / `RibbonButtonConfig` (`src/Core/Ribbon/`) | Build `PushButtonData`, set `AvailabilityClassName`, wire icons/tooltips | Supports availability already; `CreatePulldownButton` does not accept/propagate an availability parameter for the pulldown button itself (only its children can be individually configured) |
| `ITransactionService` (`src/Services/Infrastructure/ITransactionService.cs`, `TransactionService.cs`) | Single point for all document writes: `Run`, `Run<T>`, `RunConditional`, `RunRollbackOnly`, `RunWithOptions`, `RunWithWarningHandler` | All methods are `void`/`T`-returning or throw; **no result type communicates COMPLETED vs ROLLED_BACK vs FAILED to the caller** — `RunInternal` throws `InvalidOperationException` on Revit-forced rollback, otherwise silently commits; callers (services/commands) get either a return value or an exception, nothing in between |
| `BatchRenameExecutionService` (`src/Services/Renaming/BatchRenameExecutionService.cs`, 1019 lines) | Batch rename orchestration: family-parameter grouping, formula/dimension handling, style swap, progress reporting | Constructor takes only 3 deps (`ITransactionService`, `IFamilyLoadOptionsFactory`, `IFormulaUpdateService`) — most of the 1019 lines are `private static`/`internal static` helper methods operating on parameters, not instance state. This is a **low-risk decomposition target**: static/pure methods can be lifted into new sibling services with minimal wiring changes |
| Bare `catch` blocks (54 across 35 files, confirmed by direct grep) | Ad hoc exception suppression, mostly cleanup/graphics/parameter paths | No shared `ILogAndIgnore`/`SafeIgnore` helper exists today. `LECG.Services.Logging.ILogger` (`src/Services/Infrastructure/Logging/Logger.cs`) requires a `scope` string on every call — any suppression helper must be an extension/wrapper around this interface, and any service currently *without* an injected `ILogger` (e.g. `CadTempFileCleanupService`) needs a constructor + Bootstrapper registration change before it can log |
| `DialogWhitelist` (`src/Core/DialogWhitelist.cs`) | Whitelist of Revit `TaskDialog` IDs auto-dismissed during batch operations | 5 of N entries marked low-confidence; requires interactive Revit session to verify, independent of all code-only fixes |

## Recommended Fix Sequencing (Dependency-Driven)

This is a **hardening milestone on an existing architecture**, not a green-field build — the "recommended structure" is the existing layering (Command → ViewModel/View → Domain Service → ITransactionService → Revit API). The research question that matters is **sequencing**, because several fix areas share files, share abstractions, or share risk surface.

### Dependency graph between fix areas

```
[0] Test scaffolding fix           [1] LogAndIgnore helper
    (CI-mode Revit-dependent           (small, no deps)
     test compile issue)                    │
         │                                  ▼
         │                    [2] Bare-catch audit (54 sites)
         │                        — depends on [1] existing first
         │                        so sites get a real helper, not
         │                        ad hoc inline logging
         │                                  │
         ▼                                  ▼
[3] Command-level tests   ←──────  [4] Bug fixes that touch services
    for high-risk commands             also touched by [2]/decomposition
    (needs [0] resolved first)         (FormulaAutoGrouping flag, Category
         │                              Changer error message, BatchRename
         │                              formula/dimension TODOs)
         │                                  │
         └──────────────┬───────────────────┘
                         ▼
         [5] Targeted service decomposition
             (BatchRenameExecutionService — ONLY because [4]
              requires touching it; do not decompose services
              untouched by other fixes)
                         │
                         ▼
         [6] Tests locking in each [4]/[5] fix

[7] ExternalEventCommand reentrancy guard   [8] Ribbon availability audit
    (Core/ExternalEventCommand.cs +             (close Align pulldown gap +
     CategoryChanger/ConvertCad callers)         verify existing wiring —
         — independent of 0-6, but                small, no shared files with
           touches same 2 command files            0-6)
           as any UX work on those commands
                         │
                         ▼
         [9] Modeless dialog re-invocation block
             (depends on [7] existing — same guard
              mechanism, same files)

[10] ITransactionService rollback-status surface
     (interface change — ripples to every call site
      that wants COMPLETED/ROLLED_BACK feedback;
      do this BEFORE wiring UI feedback in RevitCommand,
      but it can proceed in parallel with 1-9 since it
      touches different files — only true collision is
      with [4]'s CategoryChanger error-message fix, which
      should land first or be coordinated)
                         │
                         ▼
     RevitCommand exception/result-dialog update
     (surfaces COMPLETED/ROLLED BACK to user)

[11] Security: path sanitization                [12] Clipper2/DI.Abstractions
     (CadFamilySaveService, FamilyEditorService,     isolation + startup diagnostic
      LinkedModelExportService, SettingsManager)     (App.OnStartup, .csproj) —
     — independent, touches different files           fully independent, but should
     from everything else                              land BEFORE alignment/geometry
                                                        test suites ([13]) so tests run
                                                        against the guaranteed-correct
                                                        Clipper2 version

[13] Alignment geometry service tests
     (depends on [12] — testing geometry correctness
      against an ambiguous Clipper2 version is wasted
      effort)

[14] Performance profiling (BatchRename, Purge, Alignment)
     — depends on [5]/[13] existing so profiling targets
       the POST-decomposition, POST-test shape of the code,
       not code about to be restructured

[15] Deployment/manifest docs + Pack:// URI docs + DialogWhitelist
     interactive discovery + smoke-test checklist
     — fully independent of all code fixes; can run in parallel
       throughout, but DialogWhitelist/smoke-test need the user
       at the keyboard and should be scheduled toward the end so
       they validate the cumulative set of code fixes in one pass
```

### Key ordering rules distilled from the dependency graph

1. **`LogAndIgnore`/`SafeIgnore` helper before the 54-site catch audit.** Auditing 54 sites twice (once ad hoc, once to retrofit a helper) wastes a phase. Design the helper's signature against `LECG.Services.Logging.ILogger` (scope-aware) first.
2. **Test-scaffolding fix (CI-mode Revit-dependent compile issue) before command-level tests.** Writing `LECG.Tests/Commands/*Tests.cs` against a broken CI-mode build is wasted work — validate `dotnet test` actually runs Revit-dependent tests before investing in new suites.
3. **Decomposition is a *consequence* of bug fixes, not a prerequisite.** `BatchRenameExecutionService`'s formula/dimension TODOs (lines 613, 628-629) are the reason to touch the file; extract the sub-service *while* implementing those TODOs, not before. This matches the PROJECT.md decision "decompose only where another fix requires it."
4. **Reentrancy guard and modeless re-invocation block are the same mechanism.** Both concerns point at `ExternalEventCommand<THandler>` plus its two consumers (`CategoryChangerCommand`, `ConvertCadCommand`). Sequence them as one phase, not two — a single "busy" flag on the base class (set in `Execute()`/`Initialize()`, cleared when the handler's `Execute(UIApplication)` completes or the window closes) satisfies both concerns simultaneously.
5. **Clipper2 isolation before alignment/geometry tests.** `AlignEdgesService`, `FixPointsService`, `SplitBoundariesService` all depend on Clipper2. Writing "boundary-point and vertex-alignment" tests (per PROJECT.md) against a document that might load Clipper2Lib 1.1.1.0 at runtime produces tests that pass/fail non-deterministically depending on add-in load order. Diagnostic-log-then-isolate should land first, even though the isolation fix and the test-writing touch completely different files.
6. **Profile after decomposition and after tests exist**, not before. Both PROJECT.md and CONCERNS.md agree: profile first, optimize only proven bottlenecks. But profiling `BatchRenameExecutionService` before its formula/dimension TODOs are resolved (which changes its control flow) produces profile data that will be stale by the time the fix lands.
7. **Ribbon availability audit is fully independent** of the logging/testing/decomposition chain — it only touches `RibbonService.cs` (add `availability` param to `CreateAlignPanel`'s 8 buttons) and `RibbonFactory.cs` (optionally add an availability param to `CreatePulldownButton` if the pulldown itself should also be gated). It can run in any order, but is cheap and low-risk, so front-loading it removes one "already mostly done" item from later confusion.
8. **`ITransactionService` rollback-status change ripples outward** — it's an interface change (even if additive, e.g. a new `TransactionResult` return type or a new `RunAndReport` method) consumed by `RevitCommand`'s exception handler and potentially by every command that wants "COMPLETED/ROLLED BACK" UX. Land the abstraction change in one phase, then update `RevitCommand`'s dialog/log presentation in the same or immediately following phase — splitting these across unrelated phases risks a half-wired feature (data available, but not surfaced).
9. **Security path-sanitization and deployment/documentation items are architecturally isolated** — no shared files with the reentrancy/transaction/decomposition work, so they can be scheduled for engineering-capacity reasons (fill gaps) rather than dependency reasons.
10. **Interactive/runtime-validated work (DialogWhitelist discovery, smoke-test checklist) should close out the milestone**, not open it — per PROJECT.md's own "code-first, validate-second" constraint, and because the smoke test's value is highest when it validates the *cumulative* set of fixes (reentrancy guard, rollback feedback, ribbon availability, security sanitization) in one interactive pass rather than validating a half-finished codebase repeatedly.

## Architectural Patterns Relevant to This Milestone

### Pattern 1: Static-field reentrancy guard on `ExternalEventCommand<THandler>`

**What:** Add a `private static bool _isBusy` (or a small `static readonly object _gate` + boolean) per closed generic type alongside the existing `_handler`/`_externalEvent` static fields. Set busy on `GetOrCreateHandler()`/before raising, clear it when the handler's `IExternalEventHandler.Execute(UIApplication)` completes (in a `finally`).
**When to use:** Any command subclassing `ExternalEventCommand<THandler>` — currently `CategoryChangerCommand`, `ConvertCadCommand`.
**Trade-offs:** Keeps the fix in the base class (one place, benefits all current and future modeless commands) rather than duplicating a guard in each command's `Execute()`. Must be careful that the flag resets even if `Execute(UIApplication)` throws — same try-finally discipline the codebase already uses elsewhere (`RevitIdlingRunner.cs:59-65`, `RevitCommand.cs:32,59`).
**Example (illustrative, not existing code):**
```csharp
public abstract class ExternalEventCommand<THandler> : RevitCommand
    where THandler : class, IExternalEventHandler, new()
{
    private static ExternalEvent? _externalEvent;
    private static THandler? _handler;
    private static volatile bool _isBusy;

    protected bool TryBeginInvocation()
    {
        if (_isBusy) return false;
        _isBusy = true;
        return true;
    }

    protected void EndInvocation() => _isBusy = false;
    // handler.Execute(UIApplication) wraps its body in try/finally calling EndInvocation()
}
```

### Pattern 2: Static/pure-method extraction for large service decomposition

**What:** `BatchRenameExecutionService` already contains many `private static`/`internal static` methods (`GroupCheckedFamilyParameterItems`, `CollectFormulaUpdates`, `BuildDimensionLabelNames`, `BuildFormulaReferencedNames`, etc. — verified at `src/Services/Renaming/BatchRenameExecutionService.cs`). These take their inputs as parameters and have no dependency on the class's 3 injected services. This is the *cheapest possible* decomposition: move these static methods into a new class (e.g. `BatchRenameFamilyParameterService`) with **no behavior change**, register the new class in `Bootstrapper.cs` alongside the existing `services.AddSingleton<IBatchRenameExecutionService, BatchRenameExecutionService>()` registration, and have `BatchRenameExecutionService` call the new service via an injected interface.
**When to use:** Only for the services this milestone's other fixes are already touching (per PROJECT.md's explicit out-of-scope: "Full decomposition of all 5 monolithic services... deferred").
**Trade-offs:** Static-method extraction is safe (no instance state to worry about) but doesn't reduce *testing* surface unless the new class gets its own interface + tests. Since PROJECT.md requires "tests locking in each bug fix," pair each extraction with a test for the extracted class, not just the parent.

### Pattern 3: Transaction-result surfacing without breaking 71+ existing call sites

**What:** `ITransactionService`'s 6 existing methods are called from 71+ sites. Any hardening fix that adds "COMPLETED/ROLLED BACK" reporting should be **additive** (new method or new optional out-parameter/return type) rather than changing existing method signatures, to avoid a 71-site refactor as a side effect of a UX fix.
**When to use:** When implementing the "Operations report explicit COMPLETED / ROLLED BACK status" requirement.
**Trade-offs:** An additive `TransactionOutcome Run(...)`-returning overload (or a `RunAndReport` method that wraps existing `Run`) keeps the blast radius to `RevitCommand`'s exception handler and any command that opts in, rather than touching every service. The existing `RunInternal` already throws `InvalidOperationException` on Revit-forced rollback (`TransactionService.cs:122-126`) and the `catch` blocks in `RunConditional`/`RunRollbackOnly`/`RunInternal` already re-throw after rollback (`TransactionService.cs:53-61,128-136`) — the missing piece is presentation, not detection. `RevitCommand`'s existing catch-and-dialog pattern (`src/Core/RevitCommand.cs`) is the natural place to render "ROLLED BACK" vs "COMPLETED," since the exception already carries the necessary information; this may need zero `ITransactionService` interface changes at all — verify this hypothesis when the phase is planned, since it changes whether this fix area belongs at the Infrastructure layer or purely at the Command layer.

## Anti-Patterns to Avoid in This Milestone

### Anti-Pattern 1: Fixing the 54 bare catches with one giant mechanical pass before the helper exists

**What people do:** Grep all 54 `catch` sites and add `_logger.LogWarning(...)` inline at each one without a shared helper.
**Why it's wrong:** Produces 54 slightly different logging call shapes, several of which will be in services that don't currently take an `ILogger`/`Logging.ILogger` dependency (e.g., `CadTempFileCleanupService` — verified it has zero constructor parameters today) — meaning each site potentially needs its own DI wiring change, not just a code edit. Doing this ad hoc means redoing it once a `LogAndIgnore` helper exists.
**Do this instead:** Build the helper first (even a static `ExceptionLogging.LogAndIgnore(ILogger logger, string scope, Exception ex, string message)` free function is fine — no need to over-engineer), decide the DI-injection pattern for services that currently lack a logger, then run the 54-site audit against that pattern.

### Anti-Pattern 2: Treating "ribbon availability" as a from-scratch build item

**What people do:** Plan a full phase to "build IExternalCommandAvailability and wire it into RibbonService," assuming CONCERNS.md's characterization ("not integrated into the ribbon... no references in ribbon code") is current.
**Why it's wrong:** As verified above, this is already ~90% done. Planning a full build phase wastes budget and risks re-touching 30+ already-correct button registrations, increasing regression risk for no benefit.
**Do this instead:** Scope the phase as "close the Align-pulldown gap + verify/smoke-test the existing wiring," which is roughly 1/10th the size CONCERNS.md implies.

### Anti-Pattern 3: Decomposing all 5 monolithic services because "they're all listed together" in CONCERNS.md

**What people do:** Since CONCERNS.md lists `BatchRenameExecutionService`, `LinePatternCompactionService`, `MaterialBumpMapNormalizer`, `FillPatternCompactionService`, `PurgeParameterService` together under "Large monolithic services," treat them as one uniform decomposition task.
**Why it's wrong:** PROJECT.md explicitly scopes decomposition to only the services touched by *other* fixes this milestone (only `BatchRenameExecutionService`, because of its formula/dimension TODOs). The other four have no other fix pointing at them this milestone and are explicitly out of scope.
**Do this instead:** Only decompose `BatchRenameExecutionService`, and only the parts needed to land the formula/dimension-label fix cleanly with tests.

## Data Flow: Where Each Fix Area Plugs Into Existing Flows

### Modal command flow (unaffected structurally, but rollback-status and security fixes touch it)

```
Ribbon click → Command.Execute(UIDoc, Doc)     [availability audit gates this]
    → ViewModel + View.ShowDialog()             [validation via IValidationService — unaffected]
    → Service.DoWork(doc, inputs)                [security: sanitize any path inputs here]
    → _transactionService.Run(doc, name, action) [rollback-status: surface outcome here]
    → RevitCommand catches exception              [rollback-status: render COMPLETED/ROLLED BACK]
    → Result.Succeeded / Result.Failed
```

### Modeless command flow (reentrancy guard plugs in at steps 1 and 5)

```
Ribbon click → Command.Execute(UIDoc, Doc)
    [1] → GUARD: is a prior invocation of THIS closed generic type still busy?
          if yes → warn + refuse (or focus existing window), do not create 2nd handler state
    → new ViewModel/View created, view.Show() (non-blocking)
    → user clicks Apply → RequestRun() → RaiseExternalEvent()
    [5] → static handler.Execute(UIApplication) runs
          → GUARD: clear busy flag in finally, regardless of success/exception
    → results posted back via callback → ViewModel Dispatcher.Invoke()
```

### Logging/exception flow (LogAndIgnore + bare-catch audit plug in at every service boundary)

```
Service method → try { work } catch (KnownBenignException) {
    LogAndIgnore(logger, scope, ex, "reason")   [NEW helper — replaces bare `catch {}`]
} catch (Exception ex) {
    logger.LogError(...); throw;                 [re-throw with context — unaffected sites]
}
```

## Sources

- `src/App.cs`, `src/Core/ExternalEventCommand.cs`, `src/Core/Ribbon/RibbonService.cs`, `src/Core/Ribbon/RibbonFactory.cs`, `src/Core/Ribbon/RibbonButtonConfig.cs`, `src/Core/ProjectDocumentAvailability.cs`, `src/Core/FamilyDocumentAvailability.cs` — read directly, current tree
- `src/Services/Infrastructure/ITransactionService.cs`, `src/Services/Infrastructure/TransactionService.cs` — read directly, current tree
- `src/Services/Renaming/BatchRenameExecutionService.cs` (1019 lines, method inventory via grep) — read directly, current tree
- `src/Services/CadConversion/CadTempFileCleanupService.cs`, `src/Services/FamilyConversion/FamilyEditorService.cs` — read directly, current tree
- `src/Services/Infrastructure/Logging/Logger.cs` (`ILogger` interface shape) — read directly, current tree
- `src/Commands/CategoryChangerCommand.cs` (concrete reentrancy failure mode traced through code) — read directly, current tree
- `src/Core/Bootstrapper.cs` (DI registration convention, 157 `AddSingleton` calls across `Bootstrapper.cs`/`SimpleDi.cs`) — read directly, current tree
- Bare `catch` block count (54 total across 35 files) — reconfirmed via direct grep against current tree, matches `.planning/codebase/CONCERNS.md` count
- `.planning/PROJECT.md`, `.planning/codebase/ARCHITECTURE.md`, `.planning/codebase/CONCERNS.md` (2026-07-04 codebase map) — used as baseline, corrected against current source where discrepancies were found
- `git log --oneline -- src/Core/Ribbon/RibbonService.cs ...` — confirmed ribbon-availability wiring landed in commit `0ed18e5` ("feat: commit pending source so the branch compiles standalone")

---
*Architecture research for: Revit 2026 add-in hardening milestone (brownfield, layered MVVM + service-oriented)*
*Researched: 2026-07-05*
