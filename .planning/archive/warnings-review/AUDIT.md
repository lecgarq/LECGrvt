# Audit — Warnings Review

**Closed:** 2026-08-18 · **Decision:** close as-is; the R8 gap becomes recorded debt.

Evidence is taken from the code, not from the phase summaries. Test suite run at close:
`dotnet test -c Debug -p:SkipRevitDeploy=true` → **222 passed / 0 failed / 5 skipped** (the 5 skips are Revit-runtime tests that skip by design outside Revit).

## Requirement audit

| Req | Evidence | Verdict |
|---|---|---|
| R1 — Warnings button, standard wiring | `src/Commands/WarningsCommand.cs`; `src/Core/Ribbon/RibbonService.cs:198-204` in `CreateHealthPanel` with `ProjectDocumentAvailability` (`:163`); DI at `src/Core/Bootstrapper.cs:155,216,245` | met |
| R2 — lists `GetWarnings()` grouped by description with counts | `src/Services/Health/WarningsService.cs:29` → `ReadAll(doc.GetWarnings(), Map)`; grouping via `WarningGroupingPolicy` | met |
| R3 — grouping logic in `LECG.Core`, unit-tested without Revit | `LECG.Core/Warnings/WarningGroupingPolicy.cs`; `LECG.Tests/Services/WarningGroupingPolicyTests.cs` | met |
| R4 — select failing elements | `src/Services/Health/WarningsService.cs:64` `uidoc.Selection.SetElementIds(...)` | met |
| R5 — temporary isolate, reversible | `src/Services/Health/WarningsService.cs:77-91` `View.IsolateElementsTemporary` inside a transaction | met |
| R6 — no document writes, no `ITransactionService` | No `ITransactionService` in command, service, or ViewModel (grep clean). **But** `Isolate` opens a raw `Transaction` and adds one `Isolate Warning Elements` Undo entry | **partial** |
| R7 — one bad `FailureMessage` never aborts the listing | `src/Services/Health/WarningsService.cs:48-50` catch → `LogWarning` → continue | met |
| R8 — live Revit smoke check | Partial run recorded in `docs/ai/revit-smoke-test.md` Run History 2026-07-28 | **partial** |

**6 met · 2 partial · 0 unmet.**

## R6 — what actually happened

R6 as written ("performs no document writes") is not met and could not be met. `View.IsolateElementsTemporary` throws
`ModificationOutsideTransactionException` without an open transaction, despite being a temporary view mode — proved live
on 2026-07-27 against a second open document with `IsModifiable == False`. The requirement was restated mid-milestone to
"no *persistent* model writes", which the shipped code does satisfy: Select and Show add no Undo entry, Isolate adds
exactly one.

This was a real bug found by the milestone, not a technicality. The originally shipped `Isolate` would have failed on
every invocation.

## R8 — what is and is not proven

Confirmed live against the deployed 2026-07-28 00:20 build (journal + `mcp-server-for-revit` execution of the deployed
assembly, model `LECG_RVT_ARQUITECTURA`):

- add-in starts `NoError`; `btnWarnings` registers into the Project Health panel
- `ServiceLocator` resolves `WarningsService`
- `ReadWarnings` → 10 warnings; grouping → 1 group; `Select` → 11 distinct elements (the number that proves R3's
  distinct-id grouping)

Not proven, and the reason the phase never closed on its own terms:

- dialog rendering and WPF bindings (XAML compiles without its bindings resolving)
- availability greying with no document open
- count parity against Revit's own Manage → Warnings dialog
- `Show` behaving correctly behind the modal window
- `Isolate` by click, plus Reset Temporary Hide/Isolate — **MCP cannot cover this**: the harness holds its own
  transaction and Revit forbids nesting
- the Undo-list check (exactly one entry, named `Isolate Warning Elements`)

All six need a human at the keyboard. The checklist is `docs/ai/revit-smoke-test.md` → *Targeted: Warnings command*.

## Coverage note

The test model has a single warning group, so count-descending group ordering cannot be observed live at all. It stays
unit-test-only and that is not a gap that a smoke test can ever close on this model.

## Decision

Closed as-is on 2026-08-18. The R8 gap is recorded in `.planning/codebase/CONCERNS.md` as accepted debt with its exact
checklist, not rounded up to success. Nothing in the milestone was rolled back.
