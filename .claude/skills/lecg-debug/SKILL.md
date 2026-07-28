---
name: lecg-debug
description: Track down a bug systematically — reproduce, isolate, find the root cause, fix it once where all callers route through. Use when the user reports something broken, says "it crashes", "it doesn't work", "why is this happening", "debug this", pastes a stack trace or Revit error dialog, or types /lecg-debug.
---

# Debug

A bug report names a symptom. The fix goes at the root, not where the symptom surfaced.

Most Revit bugs are one of five things. Check the table before theorising — it usually ends the investigation in one step.

| Symptom | Almost always |
|---------|---------------|
| "Attempting to modify the model outside of transaction" | Write outside `ITransactionService`, or a transaction rolled back by an early return / uncommitted dispose |
| Crash or freeze from a modeless window | Revit API called from a WPF handler instead of through `ExternalEventCommand<T>` |
| Dialog opens blank, or a control never updates | Binding path does not match the ViewModel property, or `INotifyPropertyChanged` never fires. **XAML compiled fine** — bindings fail only at runtime |
| Geometry off by a small constant, or in the wrong place | Unit conversion (internal units are feet; `AssetPropertyDistance` is inches), or link geometry not passed through `GetTotalTransform()` |
| Works on a small model, unusable on a real one | `FilteredElementCollector` built inside a loop, slow filters before quick ones, or `Regenerate()` in a loop |
| Revit's own dialogs or another add-in look wrong | Something merged into `Application.Current.Resources` — that is Revit's application, and implicit styles there go process-wide. See `docs/ai/ui-guide.md` |

## Steps

**1. Reproduce.** Get the exact trigger — which command, which selection, which document, project or family. A bug you cannot reproduce is a bug you cannot verify fixed. If it only happens in Revit, say so and get the steps from the user.

**2. Read the evidence.** Full stack trace, not the top line. `Autodesk.Revit.Exceptions.*` types are specific and name the cause. Check the Serilog output — this repo logs through a command-scoped logger.

**3. Locate.** Use `/lecg-map` for blast radius rather than grepping blind. Read the failing path end to end before forming a hypothesis.

**4. One hypothesis at a time.** State it, then test it. "I think X because Y — checking Z." Never change three things and re-run; you learn nothing from a passing test after a shotgun edit.

**5. Find the root.** Before editing, grep every caller of the function you are about to touch. If siblings call it the same way, they have the same bug. One guard in the shared function beats a guard in each caller — and patching only the reported path leaves the rest broken.

**6. Fix and prove it.** Smallest change at the root. Then a test that **fails without the fix** — a test that passes either way proves nothing. If the bug is only observable inside Revit, say so and give the exact smoke-test steps.

**7. Record it.** If it cost real time, append to *Gotchas already paid for* in `docs/ai/revit-protocol.md`. That is how it stays fixed.

## Rules

- Verify API assumptions against `docs/ai/revit-api/members.txt` — a signature you misremember can be the bug.
- Never claim a fix works in Revit without opening Revit. If the `mcp-server-for-revit` tools are connected, reproducing and re-running the failing path through them in the live session counts.
- If you cannot reproduce it, say so. Do not ship a speculative fix and call it done.
- No debug scaffolding left behind. Remove the logging you added to find it, unless it earns its place.
