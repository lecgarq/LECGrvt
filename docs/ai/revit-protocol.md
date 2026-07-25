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
| Tests | Logic that runs without Revit | `dotnet test` |
| XAML compile | Markup parses. **Not** bindings. | Included in build |
| Manifest / deploy | Output lands where Revit loads it | Inspect `LECG.csproj:61-72` + `C:\ProgramData\Autodesk\Revit\Addins\2026\` |
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
- The live `.addin` manifest uses `<ClientId>`; the tracked template uses `<AddInId>`. The live one works. Do not unify without an interactive Revit test.
