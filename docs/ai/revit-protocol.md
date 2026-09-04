# Revit Protocol

Read this when a change touches the Revit API, WPF views, or deployment. Skip it for pure `LECG.Core` / test-only work.

Repo facts live in `docs/ai/repo-context.md`. This file is the rules; that file is the evidence.

## Look the API up — do not recall it

The full 2026.4.10 API surface is indexed at `docs/ai/revit-api/` — 33,089 members, flat text, greppable in milliseconds. No model has that memorized, and a plausible-but-nonexistent overload costs a build cycle or a runtime crash.

```bash
rg "^Autodesk\.Revit\.DB\.TransactionGroup\." docs/ai/revit-api/members.txt
```

Check it before using any type not already used in this repo, any overload you are not certain of, and anything version-sensitive (`ForgeTypeId`, unit APIs, anything deprecated around 2021–2024). **Absence is evidence** — it indexes the exact assemblies the build compiles against, so a missing member does not exist.

`docs/ai/revit-api/SEMANTICS.md` covers what signatures cannot: transaction nesting and regeneration, valid API context, batch performance, unit and geometry rules. Read it before writing anything that mutates a document in bulk.

Never hand the user an API call you did not either find in the index or find already working in `src/`.

## Risk check

Address every applicable row in the plan. Write "N/A — reason" for the rest.

| Risk | Check |
|------|-------|
| Transaction safety | Every document write goes through `ITransactionService`. No nested or orphaned transactions. Rollback path on failure. |
| API context | No API calls off the main thread or outside a valid context. Commands subclass `RevitCommand`; modeless subclasses `ExternalEventCommand<T>`. |
| Modeless WPF | Document changes from a modeless window marshal through `ExternalEvent` — never called directly from a WPF event handler. |
| Units | Internal units are feet. `AssetPropertyDistance.Value` is in `GetUnitTypeId()` units (inches), not feet. Use repo helpers, never hand-rolled factors. |
| Geometry tolerance | Use the repo's tolerance constants (Revit short-curve tolerance ≈ 1/256 ft). No raw `==` on doubles or `XYZ`. |
| Linked models | Link geometry transformed via `GetTotalTransform()`. Never mix link-space and host-space coordinates. |
| Parameters | Check `StorageType` and `IsReadOnly` before get/set. Type vs instance matters. |
| Collectors | Quick filters (`OfClass`, `OfCategory`) before slow filters or LINQ. No collectors constructed inside loops. |
| XAML binding | Binding paths match ViewModel property names; `INotifyPropertyChanged` raised; `DataContext` set. Compilation does **not** prove bindings work. |
| Deployment | Do not touch `.addin` files, `.csproj`, or target framework unless the task requires it — and say so in the plan if it does. |

## Validation levels

Report these separately. Never blur them.

| Level | Proves | How |
|-------|--------|-----|
| Build | Compiles | `dotnet build -p:SkipRevitDeploy=true` — **always use this flag**; a plain build overwrites the live Revit 2026 add-in folder |
| Tests | Logic that runs without Revit | `dotnet test -c Debug -p:SkipRevitDeploy=true` — the flag is mandatory here too; `dotnet test` builds the add-in and the deploy step fails with MSB3027 while Revit is open |
| XAML compile | Markup parses. **Not** bindings. | Included in build |
| Manifest / deploy | Output lands where Revit loads it | Inspect `LECG.csproj:61-72` + `%APPDATA%\Autodesk\Revit\Addins\2026\` |
| Revit runtime | It actually works | Only claimable if Revit was opened and the command run |

**Gate:** if Revit was not opened, write `Revit runtime validation: not executed` and list the pending smoke-test steps. Never phrase untested runtime behavior as confirmed. See `docs/ai/revit-smoke-test.md`.

## Dev loop

- **Hot reload** — [RevitAddInManager](https://github.com/chuongmep/RevitAddInManager) loads a rebuilt add-in without restarting Revit (F5 to reload, Esc to close). Supports 2019–2027. Removes the slowest part of the loop.
- **Inspection** — [RevitLookup](https://github.com/lookup-foundation/RevitLookup) to snoop live element parameters and geometry when the API docs are ambiguous.
- **Reference assemblies** — this repo uses `Nice3point.Revit.Api.*` 2026.4.10, which is why the test suite compiles on CI without a Revit install. Keep it that way.
- **Revit.Async** — not currently a dependency. If ever considered: it is *not* multithreaded. It is control-flow sugar over `ExternalEvent`, still serialized on Revit's main thread, and requires `RevitTask.Initialize()` in a valid context. `ExternalEventCommand<T>` already covers this repo's needs.

## Gotchas already paid for

Recorded because they cost real debugging time. Add to this list; don't repeat them.

- Bump/normal map slots must hold a plain `UnifiedBitmap`. Unlink texture transforms before writing scales, and keep the edit inside a transaction.
- PBR sample size: `AssetPropertyDistance.Value` is in inches. `UScale`/`VScale` must stay `1.0`.
- Family reload: `overwriteParameterValues` is parameterized on the factory. Category Changer and purge reloads pass `false`; CAD flows pass `true`.
- **Two manifests for one add-in are silently resolved by Revit.** With `LECG.addin` in both `%APPDATA%\...\Addins\2026` and `C:\ProgramData\...\Addins\2026`, the journal logs `Duplicate addins:` and the per-user copy wins — a build deployed to the other folder never runs, and every "fix didn't change anything" report starts here. Verified 2026-09-04; ProgramData copy retired, deploy target is `%APPDATA%`. Grep the journal for `Duplicate addins` before trusting any smoke test.
- **Temporary view modes are model modifications.** `View.IsolateElementsTemporary` (and its `Hide*Temporary` siblings) throw `Autodesk.Revit.Exceptions.ModificationOutsideTransactionException` without an open transaction, despite being "temporary" and not persisted unless the document is saved. Verified live 2026-07-27. Do not assume "temporary" or "view-only" means transaction-free.
- **A live MCP probe proves nothing about transaction requirements.** `send_code_to_revit` runs inside its own open transaction — `Document.IsModifiable` is `True` on entry. Code that needs a transaction will appear to work. To test the no-transaction path, find another open document with `IsModifiable == False` (`document.Application.Documents`) and run against a view there. Check `IsModifiable` at entry before drawing any conclusion.
- **Revit-typed interfaces cannot be injected into anything tests construct.** The test runner has only Nice3point *reference* assemblies, so taking `ITransactionService` (or any interface whose signatures name Revit types) as a constructor parameter makes the class unconstructible in tests — `FileNotFoundException: Could not load file or assembly 'RevitAPI'`. Referencing `Transaction` inside a method body instead keeps type loading lazy. Precedent: `LECG.Tests/Services/FormulaUpdateServiceTests.cs:64-68`; applied in `src/Services/Health/WarningsService.cs:83-88`.
- **A null guard does not stop the JIT from loading Revit types.** The known rule is that a
  Revit-typed interface must not be a constructor parameter. The subtler variant: any *method
  body* that references such an interface loads it when that method is JITed, which happens
  **before** the method's own `if (_service == null) return;` guard executes. So a ViewModel
  property setter that reaches a Revit-typed service is unreachable in tests even when the
  service is null. Found 2026-08-18: setting any scope flag on `SearchReplaceViewModel` threw
  `FileNotFoundException: RevitAPI` because `SetExclusiveScope` calls `RefreshScope`, whose
  body names `ISearchReplaceService`. Fix pattern: extract the testable logic into a pure
  static that takes primitives (`SearchReplaceViewModel.BuildScopeKey`), and leave the
  Revit-touching path to the smoke test.
- **Assembly version does not prove a deployed binary is fresh.** `Directory.Build.props` bumps rarely, so a
  redeployed DLL usually carries the same version as the stale one it replaced. Confirm the deploy behaviourally
  instead: check the IL byte count of the changed method, or execute a path that only exists in the new build.
  On 2026-07-28 the fixed `WarningsService.Isolate` was confirmed by its IL size (76 bytes vs ~30 for the bare call)
  and by the nested-transaction error it raised under MCP, which is only reachable once `Transaction.Start()` runs.
- A view declaring `<base:LecgWindow.Resources><ResourceDictionary>` **replaces** the dictionary the `LecgWindow` constructor populated — it does not merge into it. Every view must merge `LecgTheme.xaml` in its own XAML or `StaticResource` lookups fail at runtime. See `docs/ai/ui-guide.md`.
