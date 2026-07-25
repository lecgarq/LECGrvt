# Revit API Semantics

`members.txt` says what exists. This says what corrupts a model, crashes Revit, or runs 100× too slow — the rules a signature cannot encode.

Everything version-specific here was verified against the 2026.4.10 reference assemblies. Re-verify after a Revit version bump.

## Transactions

- **One open `Transaction` per `Document`.** Starting a second while one is open throws. Nest with `SubTransaction`, never with `Transaction`.
- Any model write outside a started transaction throws. Reads need none.
- `Commit()` or `RollBack()` explicitly. Disposing an uncommitted transaction rolls back — quietly. A missing `Commit()` is a silent no-op, not an error.
- `SubTransaction` — must sit inside a `Transaction`. No name, no failure handling. Use it to make part of a larger operation revertible.
- `TransactionGroup` — spans several transactions:
  - `Commit()` keeps each transaction as its own undo entry
  - `Assimilate()` collapses them into **one** undo entry — what you want for a user-facing batch
  - `RollBack()` undoes the whole group
- **Regeneration happens at commit.** If you need to read geometry your own edit just produced, call `Document.Regenerate()` mid-transaction. It is expensive — never inside a loop.
- Failure handling: `Transaction.SetFailureHandlingOptions(...)` with an `IFailuresPreprocessor` to resolve or swallow warnings. For batch work set `SetForcedModalHandling(false)` so a dialog cannot block an unattended run.

In this repo, writes go through `ITransactionService` (~71 call sites). Only 7 raw `new Transaction(` sites exist, all inside `Services/Infrastructure`. Follow that boundary.

## API context and threading

- **The Revit API is single-threaded, main thread only.** No `Task.Run`, no `Parallel.ForEach`, no background thread touching `Document`. Reading looks like it works right up until it doesn't.
- Valid contexts: `IExternalCommand.Execute`, `IExternalEventHandler.Execute`, registered DB/UI event handlers, `Idling`.
- **`ExternalEvent.Raise()` is a request, not a call.** It queues; Revit runs the handler when next idle. Code after `Raise()` continues immediately and is *not* in API context. Anything needing the result belongs inside the handler.
- Modeless WPF must never call the API from a click handler — route through `ExternalEventCommand<T>`.
- `ExternalEvent.CreateJournalable(...)` exists if the action must be replayable from a journal; plain `Create(...)` otherwise.

## Documents

- `Document` is the model; `UIDocument` is the UI wrapper (selection, active view). `UIApplication.ActiveUIDocument` **can be null** — at startup and with no document open. Check it.
- `Document.IsFamilyDocument` distinguishes family from project. This repo gates that with `ProjectDocumentAvailability` / `FamilyDocumentAvailability`.
- `Document.GetElement(ElementId)` is a cheap hash lookup. Prefer it over a collector whenever you already hold the id.
- Links: `RevitLinkInstance.GetLinkDocument()` returns **null** when the link is unloaded — always check. Transform link geometry with `GetTotalTransform()` before comparing against host coordinates.
- Batch-opening other files: `Application.OpenDocumentFile` with `OpenOptions` (detached for read-only sweeps). Every opened document must be `Close(false)`d or Revit leaks it for the session.

## Batch processing

The failure modes here are performance and partial-failure, not compilation.

- **One transaction per batch, not per element.** Transaction overhead dominates at scale. But an unbounded single transaction loses everything on failure — chunk into transactions inside a `TransactionGroup` and `Assimilate()`.
- **Never construct a `FilteredElementCollector` inside a loop.** This is the single most common Revit performance bug. Collect once, materialize, then iterate.
- Quick filters (`OfClass`, `OfCategory`, `WhereElementIsNotElementType`) resolve in the database. Slow filters (`ElementParameterFilter`, geometric intersects) expand every candidate element. Order quick-before-slow.
- **Materialize ids before deleting or modifying.** Mutating the model while enumerating a live collector invalidates it — call `.ToElementIds()` first.
- `Document.Regenerate()` inside a batch loop turns minutes into hours. Let commit handle it.
- No built-in progress or cancellation. Check your own cancellation flag between chunks.

## Elements and parameters

- **`ElementId.Value` is `Int64` in 2026. `IntegerValue` no longer exists** — verified absent from the assembly. Code using it will not compile.
- Check `StorageType` before any `AsDouble`/`AsString`/`AsInteger`/`AsElementId`, and `IsReadOnly` before `Set`. Wrong-type access throws.
- `LookupParameter(name)` is a linear, localization-dependent name match. Prefer `get_Parameter(BuiltInParameter)` — faster and language-proof.
- Type vs instance parameters are different objects. Setting one does not affect the other.

## Units

- Internal units are **feet, radians, square feet, cubic feet** — always, regardless of project display units.
- `UnitUtils.ConvertToInternalUnits(double, ForgeTypeId)` / `ConvertFromInternalUnits`. **`DisplayUnitType` is gone** — zero occurrences in 2026. `ForgeTypeId` and `UnitTypeId` only.
- `AssetPropertyDistance.Value` is in `GetUnitTypeId()` units — **inches**, not feet. Already cost debugging time here; see `docs/ai/revit-protocol.md`.

## Geometry

- Tolerances come from the application, not from constants you invent: `Application.ShortCurveTolerance` (≈ 1/256 ft), `VertexTolerance`, `AngleTolerance`.
- **Never `==` on `double` or `XYZ`.** Use `XYZ.IsAlmostEqualTo(other)` or the overload taking an explicit tolerance.
- Curves shorter than `ShortCurveTolerance` cannot be created — Revit throws. Validate before constructing.
- `Element.get_Geometry(Options)` — set `ComputeReferences` only when you need references (it costs), and pick `DetailLevel` deliberately.
- A `GeometryInstance` holds no geometry directly: call `GetInstanceGeometry()` for world coordinates or `GetSymbolGeometry()` for symbol space. Mixing them silently misplaces everything.
