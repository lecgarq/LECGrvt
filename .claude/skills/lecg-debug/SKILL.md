---
name: lecg-debug
description: Track down a bug systematically — reproduce, isolate, find the root cause, fix it once where all callers route through. Use when the user reports something broken, says "it crashes", "it doesn't work", "why is this happening", "debug this", "test fails", "binding doesn't update", pastes a stack trace, a journal excerpt, or a Revit error dialog, or types /lecg-debug.
---

# Debug

A bug report names a symptom. The fix goes at the root, not where the symptom surfaced.

Almost every Revit bug here is one of the rows below. Check the table first, before step 1 — it usually ends the investigation in one step. Commands are bash (`rg` = ripgrep); in PowerShell swap `$LOCALAPPDATA/` for `$env:LOCALAPPDATA\`.

| Symptom | Almost always |
|---------|---------------|
| "Attempting to modify the model outside of transaction" | Write outside `ITransactionService`, or a transaction rolled back by an early return / uncommitted dispose |
| Crash or freeze from a modeless window | Revit API called from a WPF handler instead of through `ExternalEventCommand<T>` |
| Dialog opens blank, or a control never updates | Binding path does not match the ViewModel property, or `INotifyPropertyChanged` never fires. **XAML compiled fine** — bindings fail only at runtime |
| Geometry off by a small constant, or in the wrong place | Unit conversion (internal units are feet; `AssetPropertyDistance` is inches), or link geometry not passed through `GetTotalTransform()` |
| Works on a small model, unusable on a real one | `FilteredElementCollector` built inside a loop, slow filters before quick ones, or `Regenerate()` in a loop |
| Revit's own dialogs or another add-in look wrong | Something merged into `Application.Current.Resources` — that is Revit's application, and implicit styles there go process-wide. See `docs/ai/ui-guide.md` |
| `Cannot find resource named '...'` when a window opens | The view declared its own `<base:LecgWindow.Resources>` block, which **replaces** the constructor's dictionary instead of merging. Every view must merge `LecgTheme.xaml` in its own XAML |
| A test throws `FileNotFoundException: ... 'RevitAPI'` constructing a service | The service takes a Revit-typed interface in its constructor. Reference `Transaction` inside a method body instead — type loading stays lazy. `src/Services/Health/WarningsService.cs:83-88` |
| Reproduces only in Revit, never in tests | Check the Revit journal for `Assembly version conflict` before anything else. In Revit's shared AppDomain the first-loaded version wins — Clipper2Lib and `Microsoft.Extensions.DependencyInjection.Abstractions` both load from other add-ins, so the running code may not be the version LECG compiled against |
| Something "temporary" or "view-only" throws `ModificationOutsideTransactionException` | Temporary view modes **are** model modifications. `View.IsolateElementsTemporary` and its `Hide*Temporary` siblings need an open transaction |
| Rebuilt, redeployed, restarted Revit — behaviour unchanged | Revit is running a different `LECG.dll`. The deploy target is `%APPDATA%\Autodesk\Revit\Addins\2026\LECG`; if a second `LECG.addin` exists (e.g. `C:\ProgramData\Autodesk\Revit\Addins\2026\`), the journal logs `Duplicate addins:` and, per ribbon button, the `assembly:` path it bound. Compare that file's timestamp with your build; remove the stray manifest. Seen 2026-09-04: both present, Revit loaded `%APPDATA%` while a stale ProgramData copy still existed |
| `FileNotFoundException: 'RevitAPI'` and the constructor has **no** Revit-typed parameter | Any method body that names a Revit-typed interface loads it at JIT time — before that method's own `if (_service == null) return;` runs. Extract the testable logic into a pure static that takes primitives (`SearchReplaceViewModel.BuildScopeKey`) and leave the Revit-touching path to the smoke test |
| `A compatible .NET SDK was not found` / `Requested SDK version: 8.0.x` | `global.json` pins 8.0.x; `C:\Program Files\dotnet` is first on PATH and only has 9.x. The 8.0 SDK lives in `%LOCALAPPDATA%\Microsoft\dotnet`. Prepend it for the session — PowerShell `$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"`, bash `export PATH="$LOCALAPPDATA/Microsoft/dotnet:$PATH"` — then rerun. Do not edit `global.json` |
| CI fails `Line coverage NN% is below threshold 90%` while every test passes | The gate counts `[LECG.Core]*` lines only (`coverlet.runsettings`); new Core code without tests drags it down. Reproduce locally with the coverage command in `/lecg-phase` step 4 before pushing |

## Steps

**1. Reproduce.** Get the exact trigger — which command, which selection, which document, project or family. A bug you cannot reproduce is a bug you cannot verify fixed. If it only happens in Revit, say so and get the steps from the user.

**2. Read the evidence.** Full stack trace, not the top line. `Autodesk.Revit.Exceptions.*` types are specific and name the cause. Two files hold what the error dialog does not:

- **LECG log** — `%APPDATA%\LECG\Logs\lecg-YYYYMMDD.log` (Serilog, daily file, command-scoped). The command name is on every line; the exception and its stack are on the `[ERR]` line.
- **Revit journal** — `%LOCALAPPDATA%\Autodesk\Revit\Autodesk Revit 2026\Journals\journal.NNNN.txt`, highest number is the latest session. Read-only evidence of what Revit actually did:

```bash
rg -n "Starting External Application: LECG|Duplicate addins|assembly: .*LECG\.dll|Assembly version conflict|API_ERROR" "$LOCALAPPDATA/Autodesk/Revit/Autodesk Revit 2026/Journals/journal.NNNN.txt"
```

  That one grep answers: did LECG load, from which DLL, with which version, and what failed.

**3. Locate.** Use `/lecg-map` for blast radius rather than grepping blind. Read the failing path end to end before forming a hypothesis.

**4. One hypothesis at a time.** State it, then test it. "I think X because Y — checking Z." Never change three things and re-run; you learn nothing from a passing test after a shotgun edit.

**5. Find the root.** Before editing, grep every caller of the function you are about to touch. If siblings call it the same way, they have the same bug. One guard in the shared function beats a guard in each caller — and patching only the reported path leaves the rest broken.

**6. Fix and prove it.** Smallest change at the root. Then a test that **fails without the fix** — a test that passes either way proves nothing. If the bug is only observable inside Revit, say so and give the exact smoke-test steps.

**7. Record it.** If it cost real time, append to *Gotchas already paid for* in `docs/ai/revit-protocol.md`. That is how it stays fixed.

## Rules

- Verify API assumptions against `docs/ai/revit-api/members.txt` — a signature you misremember can be the bug. Absence is evidence: the index is built from the assemblies the build compiles against.
- Read *Gotchas already paid for* in `docs/ai/revit-protocol.md` before theorising. Someone already lost a day to it.
- Never claim a fix works in Revit without opening Revit. If the `mcp-server-for-revit` tools are connected, reproducing and re-running the failing path through them in the live session counts.
- **An MCP probe cannot answer a transaction question.** `send_code_to_revit` runs inside its own open transaction, so `Document.IsModifiable` is `True` on entry and code that needs a transaction appears to work. To exercise the no-transaction path, target another open document with `IsModifiable == False` (`document.Application.Documents`). Check `IsModifiable` at entry before drawing any conclusion.
- Rebuild with `dotnet build -p:SkipRevitDeploy=true` and `dotnet test -c Debug -p:SkipRevitDeploy=true`. Without the flag you overwrite the add-in you are debugging.
- If you cannot reproduce it, say so. Do not ship a speculative fix and call it done.
- No debug scaffolding left behind. Remove the logging you added to find it, unless it earns its place.
