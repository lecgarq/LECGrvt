# Phase 2 Plan — Live Revit validation

## Goal

Prove the Warnings command against a real model with the user at the keyboard (R8): group counts match Revit's Review Warnings dialog, select/show/isolate act on the correct elements, isolation is confirmed temporary, and the run is recorded in `docs/ai/revit-smoke-test.md` Run History.

## Evidence

- `docs/ai/revit-api/members.txt:29940,29928,29948` — `View.IsolateElementsTemporary`, `DisableTemporaryViewMode`, `IsTemporaryHideIsolateActive` all exist as used.
- Live MCP probe, 2026-07-27, model `LECG_RVT_ARQUITECTURA`, active view `3D View 1` (ThreeD): `GetWarnings()` = **10** warnings, **1** distinct description (`"Highlighted toposolids overlap."`), severity `Warning`, **20** failing-element references resolving to **11 distinct** ids, all 11 valid Toposolid elements.
- Live MCP probe: `IsolateElementsTemporary` succeeded and `DisableTemporaryViewMode(TemporaryHideIsolate)` restored the view — **but** `entry_IsModifiable=True`, i.e. the MCP harness held an open transaction, so this does **not** prove the call works with no transaction open. Unresolved; see Risks.
- `src/Services/Health/WarningsService.cs:75` — `Isolate` calls `IsolateElementsTemporary` with no transaction. Only call site in the repo, so no working precedent to lean on.
- `src/ViewModels/WarningsViewModel.cs:59-73` — every action is wrapped in try/catch → `LecgDialog.Show`, so an action that throws surfaces as a dialog, not a Revit crash. Makes the smoke test safe to run.
- `src/Views/WarningsView.xaml:26,37,44,45,56,58,59,82` — binding paths (`Summary`, `HasWarnings`, `Groups`, `Description`, `Count`, `Severity`, `CancelCommand`) all resolve against `WarningsViewModel` / `WarningGroup`. Statically checked; runtime binding still unproven.
- `src/Core/Ribbon/RibbonService.cs:198-204` — button in the **Project Health** panel, label `Warnings`, class `LECG.Commands.WarningsCommand`, availability `LECG.Core.ProjectDocumentAvailability`.
- Deployed DLL `C:\ProgramData\...\Addins\2026\LECG\LECG.dll` is dated **2026-07-25 10:14** — predates the entire Warnings feature. The running Revit session does **not** contain this command.
- `docs/ai/repo-context.md:99-100` — plain `dotnet build` deploys; Revit must be closed or the copy fails with MSB3027.

## Files changing

No source changes planned. Documentation only:

- `docs/ai/revit-smoke-test.md` — new Run History entry + a Warnings-targeted section
- `docs/ai/repo-context.md` — runtime-validation entry, once observed
- `.planning/STATE.md` — phase status
- `.planning/codebase/MAP.md` — only if an edge changes

Source edits happen **only if the smoke test fails**; that is a deviation and gets recorded here.

## Blocking precondition

Revit is running (PID 31000) with `LECG_RVT_ARQUITECTURA` open and `IsModified=True` — unsaved user work. The deploying build cannot run while Revit holds the DLLs, and closing Revit is the user's call, not the agent's. Deploy is therefore a user-driven step.

## Steps

1. **User saves and closes Revit.** (Agent must not do this — unsaved changes.)
2. **Deploy:** `dotnet build -c Release` with Revit closed. Confirm `LECG.dll` timestamp in `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG\` updates to today.
3. **User reopens Revit 2026** and the same model, so the numbers below apply.
4. **Run the checklist** (predictions from the live probe — each is falsifiable):

   | # | Check | Expected |
   |---|---|---|
   | 1 | Startup | No add-in load error dialog |
   | 2 | Ribbon | **Project Health** panel shows a **Warnings** button (eraser placeholder icon) |
   | 3 | Availability | With no document open, the button is greyed out (`ProjectDocumentAvailability`) |
   | 4 | Open dialog | Header reads **"10 warnings"**; one row |
   | 5 | Group row | `Highlighted toposolids overlap.` · **10×** · `Warning` |
   | 6 | Count parity | Revit **Manage → Warnings** lists **10** warnings — same number |
   | 7 | **Select** | Status bar / properties show **11** elements selected — *not* 10 and *not* 20 (proves distinct-id grouping, R3) |
   | 8 | **Show** | View zooms to the failing elements; note whether it works behind the modal dialog |
   | 9 | **Isolate** | View isolates the 11 toposolids — **the highest-risk check**, see Risks |
   | 10 | Isolate is temporary | Blue temporary-isolate border appears; Revit's own *Reset Temporary Hide/Isolate* clears it |
   | 11 | Read-only | After Close, **no new Undo entry** for select/show/isolate beyond what Revit itself adds |

5. **Record** the run in `docs/ai/revit-smoke-test.md` Run History (date, Revit build, per-step pass/fail, exact failure text) and note it in `docs/ai/worklog.md`.
6. **Update** `.planning/STATE.md`; add a runtime-validation entry to `docs/ai/repo-context.md`.

## Risks

| Risk | Check |
|---|---|
| **`IsolateElementsTemporary` may require an open transaction** | Unresolved — the MCP probe ran inside the harness's transaction (`IsModifiable=True`), so it proved nothing about the shipped no-transaction path. If step 9 raises an `Isolate failed` dialog saying the document is not modifiable, the fix is to wrap the call in `ITransactionService` — which **contradicts the R6 "no `ITransactionService` in the dependency graph" claim** and means R6 must be restated as "no persistent model writes". Decide with the user before editing. |
| Action throws inside modal | Contained: `WarningsViewModel.cs:59-73` catches and shows a dialog; Revit will not crash. |
| `ShowElements` behind a modal dialog | Step 8. Fallback per Phase 1 CONTEXT: close the dialog before showing. |
| Group ordering (R3) unprovable on this model | This model has exactly **1** group, so count-descending ordering cannot be demonstrated. Covered by `WarningGroupingPolicyTests`; note the gap rather than claim it observed. |
| Stale deploy | Verify the DLL timestamp after step 2 — the current one is from 2026-07-25 and has no Warnings command. |
| Deploy overwrites live add-in while Revit runs | MSB3027, per `repo-context.md:99-100`. Revit must be closed first. |
| Transaction / units / geometry / linked models / parameters / collectors | N/A — read-only listing, no geometry, no parameters, host document only. |

## Validation

- `dotnet build -p:SkipRevitDeploy=true`
- `dotnet test -c Debug -p:SkipRevitDeploy=true` (baseline 222 pass / 5 skip)
- Live MCP (already run): read path exercised in the real session — counts above.
- Revit runtime: steps 1–11 above, user-observed. Not claimable until then.

## Backward check

If steps 1–11 pass, R8 is met: counts match Revit's own dialog (6), actions hit the correct elements (7–9), isolation is confirmed temporary (10), read-only holds (11), and the run is recorded (5). Goal achieved.
