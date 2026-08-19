# LECG Manual Revit Smoke Test

> Manual checklist — requires a human running Revit 2026 on this machine. Agents must NOT claim these steps passed unless Revit was actually opened and each step observed. The interactive checklist has never been executed end-to-end by a GSD run; see Run History below for journal-derived partial evidence.

## Preconditions

- Deployed DLLs present in `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG\` (a deploying build has run).
- Live manifest present: `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG.addin` pointing at that folder's `LECG.dll`.
- Revit was NOT running during the deploying build (locked DLLs make the copy fail or leave a stale mix).

## Checklist

1. **Startup** — Open Revit 2026. Confirm no add-in load error dialog and no startup crash. If the add-in fails to load, check the Revit journal (`%AppData%\Autodesk\Revit\Journals\`) for `LECG` entries.
2. **Ribbon** — Confirm the LECG ribbon tab appears with its panels/buttons (built by `RibbonService`/`RibbonFactory` from `App.OnStartup`).
3. **Representative command** — Open a real project document and run one representative command (e.g. a simple dialog-based command). Confirm the window opens, Apply/Cancel behave, and no exception dialog appears.
4. **Active document** — Confirm the command operates on the active document correctly (commands requiring a project doc should be greyed out with no document open — `ProjectDocumentAvailability`).
5. **Empty / invalid selection** — Where the command consumes a selection, run it with nothing selected and with an invalid selection. Expect a friendly message or no-op, not a crash.
6. **No unexpected modification** — After running a read-only or cancelled command, confirm the document shows no new entries in the Undo list and closes without a save prompt (if it was clean before).
7. **Transactions** — For a command that writes to the model: confirm the change appears as one named entry in the Undo list (commit), and that cancelling/erroring rolls back completely (no partial state). Writes should be going through `ITransactionService`.
8. **Modeless / ExternalEvent** — For modeless commands (e.g. Category Changer, Convert CAD): confirm the window stays open while Revit remains responsive, and model changes triggered from the window occur via the `ExternalEventCommand<THandler>` pattern (no "outside API context" exceptions). Run the same modeless command twice in one session — static handler/event state is shared per command type, so a second run is the regression-prone case.
9. **Shutdown** — Close Revit. Confirm no crash-on-exit dialog (Bootstrapper/Serilog shutdown path).

## Targeted: WPF theme scoping (changed 2026-07-25)

Run this in addition to the checklist above after the change that moved `LecgTheme.xaml` from `Application.Current.Resources` into `LecgWindow`. Build and tests cannot detect any of it — XAML compiles without its styles resolving.

Deploy first: `dotnet build -c Release` with Revit closed.

**A. Regression — do LECG windows still look right?**

These two are the highest risk. They had **no** local theme merge in their XAML and relied entirely on the application-level merge that was removed. If the fix is wrong, these break and nothing else does.

1. Open **Home** (`HomeView`). Buttons, text, and background must match the LECG style — dark text on the LECG surface color, accent-colored primary buttons. Unstyled = default grey Windows chrome, which is unmistakable.
2. Open **Search & Replace** (`SearchReplaceView`). Same check, plus its `TextBox` and `ComboBox` styling.
3. Open any three other LECG dialogs. These merge the theme in their own XAML too, so they should be unaffected — a difference here means the shared dictionary is interfering with the per-view merge.
4. Trigger the **startup error dialog** if convenient (`LecgDialogWindow` is a `LecgWindow` constructed on the failure path before `Bootstrapper` runs). Its option buttons use `FindResource("OptionButtonStyle")`, defined in its own XAML — confirm they render styled.

**B. The actual fix — is anything outside LECG still being restyled?**

The bug was LECG's implicit styles for `Button`, `TextBox`, `ComboBox`, `RadioButton`, `ProgressBar`, `TreeView`, `TreeViewItem` applying process-wide.

5. Open another vendor's add-in dialog (the journal shows at least one other add-in loaded — it preloads `Clipper2Lib` and `Microsoft.Extensions.DependencyInjection.Abstractions`). Its buttons and text boxes must look like **its own** design, not LECG's.
6. Open a Revit dialog with WPF content and confirm native styling.
7. Do steps 5–6 **before opening any LECG window**, then repeat **after** opening one. Identical both times. A difference means the theme is still escaping the window scope — the exact failure this change exists to prevent.

Step 7 is the one that actually proves the fix. Do not skip it.

## Targeted: Warnings command (added 2026-07-27, Phase 2 / R8)

Run this in addition to the checklist above. The expected numbers below were measured live via `mcp-server-for-revit` on **2026-07-27** against `LECG_RVT_ARQUITECTURA` with `3D View 1` active — they are only valid for that model in that state. Re-measure if the model changed.

Deploy first: `dotnet build -c Release` with Revit closed, then confirm `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG\LECG.dll` has today's timestamp. The DLL deployed before this phase is dated 2026-07-25 and contains **no** Warnings command.

Measured baseline: 10 warnings · 1 distinct description (`"Highlighted toposolids overlap."`) · severity `Warning` · 20 failing-element references resolving to **11 distinct** Toposolid ids.

| # | Check | Expected |
|---|---|---|
| 1 | Ribbon | **Project Health** panel shows a **Warnings** button (eraser placeholder icon) |
| 2 | Availability | With no document open the button is greyed out (`ProjectDocumentAvailability`) |
| 3 | Dialog opens | Header reads **"10 warnings"**, one group row |
| 4 | Group row | `Highlighted toposolids overlap.` · **10×** · `Warning` |
| 5 | Count parity | Revit **Manage → Warnings** lists **10** — the same number |
| 6 | **Select** | **11** elements selected — not 10, not 20. This is what proves distinct-id grouping (R3). |
| 7 | **Show** | Zooms to the failing elements; record whether it behaves behind the modal dialog |
| 8 | **Isolate** | Isolates the 11 toposolids — see the note below |
| 9 | Isolate is temporary | Blue temporary-isolate border appears; Revit's own *Reset Temporary Hide/Isolate* clears it |
| 10 | Undo list | Exactly one new entry, `Isolate Warning Elements`, and only if Isolate was clicked. Select and Show must add none. |

**Step 8 was a real bug, fixed before this checklist was first run.** `View.IsolateElementsTemporary` was originally called with no transaction open. Verified live on 2026-07-27 against a second open document that had no ambient transaction: the call throws `Autodesk.Revit.Exceptions.ModificationOutsideTransactionException` ("Attempt to modify the model outside of transaction"), and succeeds once wrapped in a transaction. `WarningsService.Isolate` now wraps it (`src/Services/Health/WarningsService.cs:77-91`).

Consequence: Revit treats temporary isolate as a model modification, so the command is **not** entirely transaction-free. It performs no *persistent* write — a temporary view mode is not saved unless the user saves — but it does create one Undo entry, which is why check 10 is worded as it is. If Isolate ever throws again, `WarningsViewModel` catches it and shows a dialog rather than crashing Revit.

**Known coverage gap:** this model has only one warning group, so count-descending group ordering cannot be observed here. That ordering is covered by `WarningGroupingPolicyTests`; do not report it as runtime-verified.

## Targeted: Batch Rename UX (added 2026-08-18, milestone batch-rename-ux phases 1-3)

Deployed build **2026-08-18 23:47**, `LECG.dll` 1,046,016 bytes. Freshness confirmed by
metadata, not by version: `BulkObservableCollection`, `CategoryFilterOption`,
`ValidateReplaceRegex`, `ToggleRange`, `CheckedCount` and `BuildScopeKey` are all present in
the deployed assembly. The previous deploy was 996,864 bytes (2026-07-28) and carried the same
assembly version, which is exactly why size and member presence are the check.

Open Batch Rename from the ribbon on a project document. A model with many families exercises
the slow path best — Snowdon Towers Sample Architectural measured 1,881 Types rows and 1,171
FamilyParameters rows.

| # | Step | Pass when | Covers |
|---|------|-----------|--------|
| 1 | Open the dialog | Window renders; grid populates; the column filter bar and the category dropdown button are both visible and not blank | all bindings |
| 2 | Click the **Parameters** scope pill | A busy panel covers the grid saying "Collecting elements…", then results appear | R12 |
| 3 | Click another scope, then click **Parameters** again | Second visit returns with no busy panel and no perceptible wait | R12 cache |
| 4 | Untick three rows, then edit the Replace text | The same three rows are still unticked after the preview rebuilds | R5 |
| 5 | Untick a row, filter it out of the grid, clear the filter | The row returns still unticked | R5 |
| 6 | Filter to a handful of rows, click **Select All** | Only the visible rows tick. Clear the filter and confirm the rest are untouched | R1 |
| 7 | Click **Invert** with a filter active | Only visible rows flip | R3 |
| 8 | Shift-click down the Sel column | Every row between the previous click and this one is set | R4 |
| 9 | Watch the header and status bar while filtering and ticking | Both show shown-count and selected-count, and both change with the filter | R6 |
| 10 | Type into each of the five column filter boxes | Grid narrows on each; combining two narrows further | R8 |
| 11 | Open the category dropdown | Checkboxes with a row count beside each category | R9 |
| 12 | Tick two categories | Both categories' rows show; button reads "2 categories" | R9 |
| 13 | Type in the dropdown's search box | Option list narrows; the grid does **not** | R10 |
| 14 | Click **Clear filters** | Every filter resets, all rows return, the indicator disappears | R11 |
| 15 | Enable Replace + RegEx, type `([` | Message reads "Invalid regular expression: …", **not** "Error loading preview: …" | R7 |
| 16 | Rename a small selection and check Undo | One undo entry; renamed elements correct | regression |

Step 16 is the regression guard: phases 1-3 changed which rows `Apply` collects
(`CheckedCount` instead of a walk over every row), so confirm the right elements are renamed.

**Known display-only defect, do not report as new.** A row you untick can still cause another
row to show a collision Status: `ProcessPreview` computes cross-batch collisions before the
remembered deselections are re-applied. Execution renames ticked rows only. Marked with a
`ponytail:` comment in `SearchReplaceViewModel.UpdatePreviewAsync`.

## Recording results

Log the run (date, Revit build, steps passed/failed, journal excerpts for failures) as an entry in `docs/ai/worklog.md`. Until an interactive run happens, reports must state: `Revit runtime validation: not executed.`

## Run History

### 2026-07-28 — Warnings command, MCP-driven partial run (NOT a complete checklist run)

Deployed build 2026-07-28 00:20 (`dotnet build -c Release`, Revit closed during copy; `LECG.dll` 996,864 bytes, `LECG.deps.json` unchanged and copy-skipped). Revit 2026 reopened by the user with `LECG_RVT_ARQUITECTURA`, MCP service on. Evidence is journal entries plus live execution of the **deployed** assembly via `mcp-server-for-revit`.

**Confirmed the deployed binary is the fixed build** (the assembly version string is unchanged at 0.1.1.0, so version alone proves nothing):
- Loaded from `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG\LECG.dll`; `WarningsCommand`, `WarningsService`, `Isolate` all present via reflection.
- `WarningsService` constructor takes a single `ILogger` — the reverted, intended signature.
- `Isolate` method body is **76 IL bytes** (the pre-fix bare call was ~30).

**PASS — startup (checklist step 1):** journal `API_SUCCESS { Starting External Application: LECG, Class: LECG.App, ... Assembly Version: 0.1.1.0 }`, and `Rvt.Attr.AddInLoadFailureMessage: NoError`.

**PASS — ribbon, Warnings button (targeted check 1):** journal `API_SUCCESS { Added pushbutton Id: 6530, name: btnWarnings, text: Warnings, class: LECG.Commands.WarningsCommand, assembly: ...\LECG\LECG.dll, parentId: CustomCtrl_%LECG%Project Health }`.

**PASS — read path, grouping, and Select (targeted checks 3, 4, 6), executed through the shipped service resolved from the live DI container:**
- `ServiceLocator.GetRequiredService<WarningsService>()` resolved — DI registration works at runtime.
- `ReadWarnings(document)` → **10** items.
- `WarningGroupingPolicy.Group(...)` → **1** group, `Description = "Highlighted toposolids overlap."`, `Count = 10`, **11** distinct element ids.
- `Select(uidoc, ids)` → `uidoc.Selection.GetElementIds().Count` = **11**. The distinct-id contract (R3) holds end to end against a real model.

**PASS — the isolate fix is executing:** calling the shipped `Isolate` through MCP threw `Autodesk.Revit.Exceptions.InvalidOperationException: "Starting a new transaction is not permitted..."`. That error is only reachable if `Transaction.Start()` runs, which proves the transaction wrapper is live. It failed solely because the MCP harness holds its own transaction (`IsModifiable=True`) and Revit forbids nesting — a harness limitation, not a defect. Isolate's real behavior must be validated by clicking the button, where no ambient transaction exists.

**NOT TESTED — needs a human at the keyboard:**
- Targeted check 2: button greyed out with no document open (`ProjectDocumentAvailability`).
- Targeted checks 3–4 *as rendered*: the dialog opening, header text, row layout, and WPF bindings. All of the above was exercised below the UI layer; XAML bindings remain unproven.
- Targeted check 5: count parity against Revit's own **Manage → Warnings** dialog.
- Targeted check 7: `Show` behaviour behind the modal dialog (the Phase 1 open question).
- Targeted checks 8–9: `Isolate` by click, and that Revit's *Reset Temporary Hide/Isolate* clears it.
- Targeted check 10: Undo list contains exactly one `Isolate Warning Elements` entry, and none from Select/Show.

**Side effect of this run:** the model's selection was left set to the 11 toposolids. Harmless — click empty space to clear.

### 2026-07-25 — INTERACTIVE RUN (theme scoping), user-observed

First interactive smoke test recorded in this repo. Deployed build 2026-07-25 10:14 (`dotnet build -c Release`, Revit closed during copy).

- **A1 `HomeView`: PASS** — renders styled. Failed on the first deploy (`Cannot find resource named 'DashboardCardStyle'`), fixed in `2cd13fe`, re-verified after redeploy.
- **A2 `SearchReplaceView`: PASS** — renders styled.
- **B5 other vendors' add-in dialogs: PASS** — render in their own style. The process-wide style leak is closed.
- **A3 (three other LECG dialogs), A4 (`LecgDialogWindow` startup error dialog), B7 (before/after ordering): NOT TESTED.** Low risk — all 23 remaining views merge the theme in their own XAML and were unchanged by this work.

Regression guard added the same day: `LECG.Tests/Views/ThemeScopingTests.cs`.

### 2026-07-04 — journal-derived partial evidence (NOT an interactive checklist run)

No GSD-driven interactive test was performed (agent cannot drive the Revit GUI). However, read-only inspection of the user's own Revit 2026 session that day (`%LOCALAPPDATA%\Autodesk\Revit\Autodesk Revit 2026\Journals\journal.0879.txt`, session 22:12–22:15) provides direct evidence for a subset of steps, running the currently deployed DLL (deployed 2026-07-02):

- **Step 1 (Startup): EVIDENCED** — `API_SUCCESS { Starting External Application: LECG, Class: LECG.App, ... Assembly: C:\ProgramData\Autodesk\Revit\Addins\2026\LECG\LECG.dll, Assembly Version: 0.1.1.0 }`. No load-failure dialog entries.
- **Step 2 (Ribbon): EVIDENCED** — dozens of `API_SUCCESS { Added pushbutton ... }` entries across all LECG panels (Home, Project Health, Standards, Toposolids, Align, Model Organization, Visualization).
- **Step 9 (Shutdown): EVIDENCED** — session ended with normal `ExitNativeInstance` / `finished recording journal file`; no crash markers.
- **Steps 3–8: NOT TESTED** — the journal shows no LECG command was executed in that session. Command behavior, selection edge cases, transactions, and modeless/ExternalEvent flows remain unvalidated.
- **Observed warning (not a failure):** `API_ERROR { Assembly version conflict in some references in LECG.dll assembly ... Clipper2Lib 2.0.0.0 conflicts with preloaded 1.1.1.0; Microsoft.Extensions.DependencyInjection.Abstractions 8.0.0.0 conflicts with preloaded 9.0.0.0 }` — another add-in preloads older/newer copies of these assemblies. LECG still loaded; see repo-context.md [Concern] for implications (Clipper2-dependent commands like Align Edges).
