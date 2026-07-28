# Phase 1 Plan — Warnings command end to end

## Goal

The Warnings button exists in the Project Health panel and opens a modal view listing all `Document.GetWarnings()` warnings grouped by description with working select/show/isolate actions (R1–R7). Live Revit proof is Phase 2.

## Evidence

- `docs/ai/revit-api/members.txt:12136` — `Document.GetWarnings() -> IList<FailureMessage>` exists.
- `docs/ai/revit-api/members.txt:15444,15447,15448` — `FailureMessage.GetFailingElements()`, `GetSeverity()`, `GetDescriptionText()` exist.
- `docs/ai/revit-api/members.txt:32770,33030,29940` — `Selection.SetElementIds(ICollection<ElementId>)`, `UIDocument.ShowElements(ICollection<ElementId>)`, `View.IsolateElementsTemporary(ICollection<ElementId>)` exist.
- `docs/ai/revit-api/members.txt:13491,13493` — `ElementId..ctor(Int64)` and `ElementId.Value -> Int64` — Core can carry plain `long` ids.
- `src/Commands/ChangeLevelCommand.cs:18-37` — the modal command template: resolve VM, `CreateWith<View>(vm)`, `view.Initialize(uidoc)`, `ShowDialog()`.
- `src/Views/ChangeLevelView.xaml:8-15` — every view merges `LecgTheme.xaml` in its own XAML (ui-guide trap).
- `src/Views/Base/LecgWindow.cs:116-151` — `Initialize(UIDocument)` + `BindClose(vm)` are the base hooks.
- `src/Core/Ribbon/RibbonService.cs:161-197` — `CreateHealthPanel` is where the button goes; command class referenced by name string `"LECG.Commands.WarningsCommand"`.
- `src/Core/Bootstrapper.cs:154,214,242` — registration pattern: service singleton, VM transient, View transient.
- `src/Configuration/UIConstants.cs:120-122` — button constant naming pattern.
- `src/Utilities/AppImages.cs` — no warning icon asset exists; placeholder reuse is established precedent (`ConvertShared`, `FilterCopy`).
- `LECG.Tests/Services/PurgeSequenceTests.cs` et al. — Core policy tests live in `LECG.Tests/Services/`.
- Modal decision and no-transaction constraint: `.planning/phases/01-warnings-command/CONTEXT.md` (isolate is temporary view mode — no document write, so no transaction anywhere).

## Files changing

New:
- `LECG.Core/Warnings/WarningGroupingPolicy.cs` — `WarningItem` / `WarningGroup` records + static grouping policy
- `src/Services/Health/WarningsService.cs` — read warnings (skip-and-continue), select/show/isolate
- `src/ViewModels/WarningsViewModel.cs`
- `src/Views/WarningsView.xaml` + `.xaml.cs`
- `src/Commands/WarningsCommand.cs`
- `LECG.Tests/Services/WarningGroupingPolicyTests.cs`
- `LECG.Tests/Services/WarningsServiceTests.cs`
- `LECG.Tests/ViewModels/WarningsViewModelTests.cs`

Edited:
- `src/Core/Bootstrapper.cs` — 3 registrations
- `src/Core/Ribbon/RibbonService.cs` — button in `CreateHealthPanel`
- `src/Configuration/UIConstants.cs` — `ButtonWarnings_*` constants

Anything outside this list is a deviation.

## Design

- **Core policy (R2, R3):** `WarningItem(string Description, string Severity, IReadOnlyList<long> ElementIds)`; `WarningGroupingPolicy.Group(items)` returns `WarningGroup(Description, Severity, Count, ElementIds)` list — grouped by description (ordinal), ordered count desc then description asc, ids distinct per group. Total count = sum of group counts, computed in the VM (one line).
- **Service (R4–R7):** `WarningsService(ILogger)`. `ReadWarnings(Document)` delegates to `ReadAll<T>(IEnumerable<T> source, Func<T, WarningItem> map)` — per-item try/catch, log warning, continue (R7, testable without Revit). `Select/Show/Isolate(UIDocument, IEnumerable<long>)` are one-line API calls; isolate targets `uidoc.ActiveGraphicalView` temporary mode. No `ITransactionService` anywhere (R6).
- **VM:** `WarningsViewModel(WarningsService)`; `Initialize(UIDocument)` reads + groups; `Load(IEnumerable<WarningItem>)` is the Revit-free seam tests use. `RelayCommand<WarningGroup>` for Select/Show/Isolate, each wrapped in try/catch → `LecgDialog.Show` (a throwing button inside a modal must not take down Revit). `TotalCount`, empty-state text.
- **View:** `LecgWindow` subclass, merges theme in XAML, `ItemsControl` of groups (description, severity, count, three buttons with `CommandParameter="{Binding}"`), Close button via `BindClose`. Modal (`ShowDialog`) — actions run on Revit's UI thread inside `Execute`'s stack: valid API context, no ExternalEvent.
- **Command:** `WarningsCommand : RevitCommand`, mirrors `ChangeLevelCommand`. Ribbon: Health panel, `ProjectDocumentAvailability`, placeholder icon (`AppImages.Eraser`).

## Steps

1. Core: `WarningGroupingPolicy.cs` + `WarningGroupingPolicyTests` (grouping, ordering, distinct ids, empty input). Commit `feat(core): warning grouping policy`.
2. Service: `WarningsService` + `WarningsServiceTests` (skip-and-continue: one bad item logged and skipped, rest survive). Commit `feat(health): warnings reading service with skip-and-continue`.
3. VM: `WarningsViewModel` + `WarningsViewModelTests` (Load → groups ordered, TotalCount correct). Commit `feat(health): warnings view model`.
4. View: `WarningsView.xaml/.cs`. Commit `feat(health): warnings view`.
5. Wire-up: `WarningsCommand`, Bootstrapper registrations, `UIConstants`, `RibbonService` Health button. Commit `feat(health): warnings command and ribbon button`.
6. Validate full suite, patch MAP.md edges, update STATE.md.

Each step builds green (`-p:SkipRevitDeploy=true`) before its commit.

## Risks (revit-protocol rows)

| Risk | Check |
|---|---|
| Transaction safety | N/A — read-only by design; no writes, no `ITransactionService` in any new type (R6 holds by construction). |
| API context | All Revit calls happen inside `Execute`'s call stack (modal `ShowDialog` nested message loop on Revit's UI thread). |
| Modeless WPF | N/A — modal by decision (CONTEXT.md). |
| Units / tolerance / linked models / parameters | N/A — no geometry, no parameters, host-document warnings only. |
| Collectors | N/A — `GetWarnings()` is the only enumeration. |
| XAML binding | Bindings to record properties are one-way reads; theme merged in view XAML per ui-guide. Build proves markup only — bindings are Phase 2 eyes-on. |
| Deployment | `.addin`/`.csproj` untouched. |
| Action throws in modal (e.g. isolate on a schedule view) | VM command handlers catch and show `LecgDialog` instead of crashing Revit. |
| `ShowElements` behind modal dialog | Known open question — Phase 2 smoke test; fallback documented in CONTEXT.md. |

## Validation

- `dotnet build -p:SkipRevitDeploy=true`
- `dotnet test -c Debug -p:SkipRevitDeploy=true` (baseline 211 pass / 5 skip)
- Revit runtime validation: **not executed** — Phase 2 (R8).
