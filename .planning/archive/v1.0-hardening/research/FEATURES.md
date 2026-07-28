# Feature Research

**Domain:** Reliability/UX hardening for a production Revit 2026 add-in (C#/.NET 8, WPF, modal + modeless commands)
**Researched:** 2026-07-05
**Confidence:** MEDIUM-HIGH (Revit API mechanics verified against Autodesk API docs and Jeremy Tammik / The Building Coder, the de-facto canonical source for Revit add-in patterns; project-specific gaps verified directly against LECG source)

## Scope Note

This is not a market-feature landscape — it's a **reliability/UX feature landscape** for hardening an existing add-in. "Table stakes" means "what a Revit add-in that claims to be production-hardened must have." "Differentiators" means "polish beyond the documented concerns." "Anti-features" means "over-engineering traps common in hardening passes."

Each feature below is cross-checked against current LECG source (not just CONCERNS.md, which is a snapshot and may be imprecise — see notes marked "Evidence:").

## Feature Landscape

### Table Stakes (A Hardened Revit Add-in Must Have These)

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Ribbon availability via `IExternalCommandAvailability`** | Standard Revit API mechanism (`PushButtonData.AvailabilityClassName`) since Revit 2011; users expect greyed-out buttons instead of a command that runs then fails with a confusing error. | LOW | Evidence: LECG already wires this — `RibbonFactory.cs:32,93` sets `buttonData.AvailabilityClassName`, and `RibbonService.cs` assigns `ProjectDocumentAvailability`/`FamilyDocumentAvailability`/empty (any) per button. Revit itself re-invokes `IsCommandAvailable` automatically on ribbon paint/context changes — **no manual document-change event subscription or caching is required** for this to work; Revit calls it fresh each time the ribbon needs to redraw. The gap is narrower than CONCERNS.md implies: it is not "not integrated," it is "integrated only for the binary project-vs-family split," not for finer per-command preconditions (e.g. requires active selection, requires specific view type). Confirm exactly which commands need finer-grained availability before adding new `IExternalCommandAvailability` classes. |
| **`ExternalEvent.IsPending` reentrancy guard** | Revit API's own documented mechanism for preventing duplicate raises of the same handler; querying it before `Raise()` is the standard idiom (Autodesk docs, Jeremy Tammik / BIM Matters). | LOW | Evidence: `ExternalEventCommand<THandler>` (`src/Core/ExternalEventCommand.cs:9-27`) has static `_handler`/`_externalEvent` fields but `RaiseExternalEvent()` does **not** check `IsPending` before raising — this is the actual gap, not a novel busy-guard invention. Add `if (_externalEvent.IsPending) { warn + return; }` before `Raise()`. This is the minimal, idiomatic fix — no custom semaphore/lock needed. |
| **COMPLETED / ROLLED BACK user feedback on transaction outcome** | Silent rollback is a known Revit add-in anti-pattern (users lose trust when "nothing visibly happened" after clicking a command). TaskDialog-based "here's what happened" feedback is the standard pattern in Building Coder examples. | LOW-MEDIUM | Evidence: `TransactionService.RunInternal` (`src/Services/Infrastructure/TransactionService.cs:99-137`) throws `InvalidOperationException` on Revit-forced rollback but the message never distinguishes "rolled back" from any other failure once it reaches `RevitCommand.Execute` (`src/Core/RevitCommand.cs:47-56`), which shows the same generic `ERROR: {detailed}` log line + log window for every exception type. Fix is to have `TransactionService` throw/signal a distinguishable rollback-specific exception or result, and have `RevitCommand` special-case it in its catch block to say "ROLLED BACK — no changes were made" vs "COMPLETED with error." |
| **`.addin` manifest presence validation at `OnStartup`** | Manifest is the single point of failure for the add-in loading at all; mature add-ins verify their own registration rather than fail silently/mysteriously. | LOW | Evidence: `App.OnStartup` (`src/App.cs:17-46`) has no manifest check — by definition it can't "check itself" once it's already running (if `OnStartup` executes, the manifest already worked for *this* session), but it CAN validate: (a) the manifest file still exists on disk for next launch, (b) assembly path in the manifest matches the running assembly location, (c) log a warning if drift is detected. This is a "detect problems before they bite next time" diagnostic, not a load-blocking gate. |
| **Path sanitization on user/parameter-influenced file writes** | Standard OWASP/`.NET` guidance: canonicalize with `Path.GetFullPath`, verify result starts with the intended base directory, reject `..`/absolute-path injection. This is baseline secure-coding practice for any desktop app that writes files from user-controllable strings (family/parameter names here, not internet input, but the same defense-in-depth applies). | LOW | Evidence: `CadFamilySaveService.Save` (`src/Services/CadConversion/CadFamilySaveService.cs:8-18`) does `Path.Combine(Path.GetTempPath(), name + ".rfa")` with **no validation** that `name` doesn't contain `..\` or path separators — a family/type name containing traversal characters would escape the temp directory. Fix pattern (verified against multiple current .NET security sources): sanitize `name` via `Path.GetFileName(name)` first (strips directory components entirely), then optionally re-verify `Path.GetFullPath(combined)` still starts with `Path.GetFullPath(baseDir)`. Same pattern applies to `FamilyEditorService.cs:177`, `LinkedModelExportService.cs:74`, `SettingsManager.cs:15,47,63`. |
| **Dialog whitelist verified against live Revit, not guessed** | `DialogBoxShowing`/`TaskDialogShowingEventArgs.OverrideResult` requires the exact `DialogId` string and correct result code (standard Windows Message-Box IDs OR Revit's custom incremental IDs starting at 1001 for custom-button task dialogs) — Autodesk API docs and Building Coder confirm these values are dialog-specific and not derivable from guesswork. | MEDIUM (requires interactive Revit session) | Evidence: `DialogWhitelist.cs:40-67` explicitly self-documents 5 entries as "LOW-confidence research guesses, NOT runtime-confirmed." This cannot be resolved by code alone — it requires the documented interactive discovery pass. Code-side prep (already good): the `Apply()` method already logs a Warning on miss and Info on hit (`DialogWhitelist.cs:74-88`), which is the correct fail-safe default (unmatched dialogs reach the user rather than being silently eaten). |
| **Bare-catch audit: log-or-rethrow-or-explicit-suppress** | Silent exception swallowing is a universally recognized anti-pattern in production software; the fix (log at minimum Warning, or an explicit named "suppress with reason" helper) is standard, not novel. | MEDIUM (54 sites to review, mechanical but not automatable — each needs a judgment call) | A `LogAndIgnore(action, logger, reason)` helper (as CONCERNS.md itself proposes) turns "was this intentional?" into a self-documenting call site instead of a bare `catch { }`. This is the standard shape used across .NET codebases for deliberate suppressions (cf. `TryXxx` pattern conventions). |

### Differentiators (Polish Beyond the Documented Concerns)

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Startup diagnostic banner logging loaded assembly versions** | Beyond just fixing Clipper2/DI.Abstractions conflicts, logging *which* versions actually resolved at runtime (via `Assembly.GetExecutingAssembly()`/`AppDomain.CurrentDomain.GetAssemblies()` reflection at `OnStartup`) gives a permanent diagnostic trail for the next AppDomain-sharing conflict with Enscape/ModPlus/Forma, without requiring a debugger session to reproduce. | LOW | This directly satisfies the CONCERNS.md ask ("startup diagnostic logs actually-loaded versions") and is cheap once the isolation mechanism itself is decided. Log at Info level once per session, not per-command. |
| **Distinguishing "ROLLED BACK" from "COMPLETED WITH WARNINGS"** | Beyond binary success/fail, Revit transactions can also complete via `SafeFailureHandler`/`WarningSwallower` (both referenced in `TransactionService.cs`) which suppress warnings but still commit. Surfacing "completed, but N warnings were suppressed" is richer than a flat COMPLETED, and closes a real gap (users currently have no visibility into what `SafeFailureHandler`/`WarningSwallower` silently absorbed). | MEDIUM | Requires `IFailuresPreprocessor` implementations to accumulate a count/list rather than just swallow, and for that list to surface through to the log window. Worth doing only if the failure handlers are already being touched for other reasons — don't refactor them solely for this. |
| **Command-level "busy" visual state on ribbon (not just reentrancy rejection)** | Beyond silently blocking a re-click, greying the button or showing a status-bar spinner while a modeless operation (CategoryChanger, ConvertCad) is in flight is a nicer UX than "click does nothing" or "click shows a warning." | MEDIUM | Ribbon button state changes from an `ExternalEvent`-driven background operation require careful thread marshaling (`RibbonItem.Enabled` must be set on the Revit UI thread, typically via Idling or another ExternalEvent). Genuinely more complex than the reentrancy guard itself — treat as a stretch goal, not baseline. |
| **Structured startup smoke-check for XAML/pack:// resource loading** | CONCERNS.md flags the `pack://` workaround as fragile and undocumented; a one-line smoke check (attempt to resolve one known resource URI and log success/failure) turns "will silently break XAML rendering" into "will loudly log at startup." | LOW | Cheap insurance for a documented fragile area; pairs naturally with the "document the workaround" requirement already in scope. |

### Anti-Features (Common Hardening Over-Engineering Traps)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|------------------|-------------|
| **Generic "operation result" framework / custom Result<T> monad across all services** | Tempting once you start distinguishing COMPLETED/ROLLED BACK/WARNING — "why not make every service return a rich result type?" | This milestone is scoped to fixing documented concerns, not architecting a new cross-cutting result-passing convention across 39 commands and ~all services. That's a rewrite disguised as hardening, and PROJECT.md explicitly scopes out "full decomposition" and "new product features." | Keep the fix local: `TransactionService` signals rollback distinguishably (e.g. a specific exception type or an out `bool wasRolledBack`), and `RevitCommand.Execute`'s existing catch block branches on it. No new pervasive abstraction. |
| **Full custom dialog-interception engine (regex/heuristic matching beyond exact `DialogId`)** | Tempting to "future proof" against dialogs whose IDs might change between Revit versions by matching on title text or partial strings. | Revit's own API guidance and Building Coder examples use exact `DialogId` string matching for a reason: heuristic matching on dialog *text* is locale-dependent (breaks on non-English Revit) and fragile across versions in the opposite direction — it silently matches the *wrong* dialog instead of failing safe. | Keep exact-match whitelist (already implemented correctly in `DialogWhitelist.cs`) with fail-safe-to-user-visible on miss. Only widen matching if a specific documented case demands it. |
| **Global command-level mutex/semaphore across ALL 39 commands** | "If reentrancy is bad for CategoryChanger/ConvertCad, why not guard every command against concurrent execution?" | Revit is fundamentally single-threaded for document modification — most commands (modal, non-`ExternalEventCommand`) literally cannot overlap because Revit blocks the UI during modal command execution. The reentrancy problem is specific to the modeless (`ExternalEventCommand`) pattern, not a general concern. Building a universal guard solves a problem that mostly doesn't exist for modal commands and adds needless state to every command. | Scope the reentrancy fix to `ExternalEventCommand<THandler>` only (where it's real), using `ExternalEvent.IsPending`. |
| **Full assembly-isolation framework (custom `AssemblyLoadContext`, binding redirects, shim generation) for dependency conflicts** | Clipper2/DI.Abstractions version conflicts are real and the natural instinct is to build a generic "any future dependency conflict" isolation framework. | Over-engineering for two known conflicts. Revit's shared-AppDomain model (.NET Framework-era assembly resolution, not a modern `AssemblyLoadContext` world — confirm this against the actual .NET 8/Revit 2026 hosting model before building anything) has a small number of documented mitigation patterns (ILRepack/merge, strong-naming + binding redirect, or embedding as an internalized/aliased reference). Building a generic multi-assembly isolation engine is speculative infrastructure for a two-dependency problem. | Solve Clipper2 and DI.Abstractions specifically (embed/alias/ILMerge), log actually-loaded versions at startup for visibility, and stop there. Revisit only if a third conflict appears. |
| **Telemetry/analytics dashboard for command usage and error rates** | "While we're improving observability, why not add usage analytics?" | Out of scope per PROJECT.md ("no new product features"); also introduces privacy/data-handling questions (this is an internal-use architectural add-in, not a SaaS product) with no stated business need. | Serilog file logging (already in place) is sufficient for a hardening milestone; revisit only if a future milestone has an explicit analytics requirement. |
| **Retry-with-backoff / auto-recovery for known Revit API limitations (e.g. FamilyEditorService category-change failure)** | Tempting to make the workaround "smarter" (loop through candidate categories automatically) instead of just explaining the failure. | CONCERNS.md's own fix approach is explicit: improve the *error message*, not build more retry logic on top of an already-fragile "reset to Generic Model then retry" workaround. Adding more automatic retcategory attempts increases the blast radius of an already-documented Revit API limitation. | Improve the actionable error message; document the manual workaround path; do not add more automated category-guessing. |

## Feature Dependencies

```
[Rollback COMPLETED/ROLLED BACK feedback]
    └──requires──> [TransactionService distinguishing rollback-caused vs other exceptions]

[ExternalEvent reentrancy guard]
    └──enhances──> [Modeless dialog re-invocation block] (CategoryChanger, ConvertCad)
                       (the IsPending check is the mechanism; the dialog-level guard is the
                        user-visible behavior built on top of it)

[Dialog whitelist verification]
    └──requires──> [Interactive Revit session] (cannot be resolved by code alone)

[Startup manifest validation]
    └──enhances──> [.addin install/restore documentation] (validation without a restore
                     procedure just logs a problem nobody can fix)

[Path sanitization]
    └──enhances──> [Log sanitization] (both concerns touch the same file-path-in-logs surface;
                     doing them together avoids re-touching the same call sites twice)

[Startup assembly-version diagnostic logging]
    └──requires──> [Dependency isolation decision] (log first to know what's actually loaded,
                     THEN decide embed/alias/ILMerge strategy — sequencing matters)
```

### Dependency Notes

- **Rollback feedback requires TransactionService changes first:** `RevitCommand.Execute`'s catch block can only say "ROLLED BACK" if something upstream (`TransactionService`) hands it a distinguishable signal. Fix the producer before the consumer.
- **Dialog whitelist verification requires an interactive session:** this is the one feature in this set that genuinely cannot be resolved by code changes alone — it must be sequenced after (or interleaved with) a user-assisted Revit session, matching the milestone's own "code-first, validate-second" constraint.
- **Manifest validation enhances (doesn't replace) the install/restore documentation:** detecting "manifest missing/mismatched" is only useful if there's a documented recovery step; do these together, not manifest-checking as a standalone deliverable.
- **Assembly diagnostic logging should land before the isolation fix, not after:** confirming which versions actually resolve at runtime today (with other add-ins loaded) informs whether embedding/aliasing/ILMerge is even necessary for each dependency, or whether one of them turns out to be a non-issue in practice.

## MVP Definition (This Milestone's "Launch")

### Must Fix (Directly Named in CONCERNS.md / PROJECT.md Active Requirements)

- [ ] `ExternalEvent.IsPending` guard in `ExternalEventCommand<THandler>.RaiseExternalEvent()` — closes the reentrancy gap with the Revit API's own idiomatic mechanism
- [ ] Modeless dialog re-invocation block (CategoryChanger, ConvertCad) built on top of the above
- [ ] TransactionService signals rollback-vs-other-failure distinguishably; RevitCommand surfaces COMPLETED / ROLLED BACK to the user
- [ ] Path sanitization (`Path.GetFileName` + `Path.GetFullPath` containment check) in the four flagged file-writing services
- [ ] Bare-catch audit across 54 sites: log, rethrow-with-context, or named `LogAndIgnore` suppression
- [ ] `.addin` manifest presence/validity check logged at `OnStartup` (detection only — restore procedure is documentation, not code)
- [ ] `FormulaAutoGroupingCommand` static flag reset via try-finally/disposable, independent of exit path

### Requires Interactive Revit Session (Sequence Alongside Code Work, Not After)

- [ ] DialogWhitelist entries verified/corrected/removed via live Revit 2026 discovery pass
- [ ] Revit smoke-test checklist executed with user at the keyboard

### Add Only If Touching the Relevant Service Anyway (Not Standalone Work This Milestone)

- [ ] Ribbon availability refinement beyond the existing project/family split (only if a specific command's current availability is proven wrong, not speculatively)
- [ ] Startup diagnostic banner for assembly versions (cheap, pairs naturally with the dependency-isolation fix — do together)

### Explicitly Deferred (Per PROJECT.md Out of Scope)

- [ ] Full decomposition of the 5 monolithic services (only extract where another fix requires it)
- [ ] Comprehensive test suites beyond the named high-risk commands (Purge, AlignEdges, FixPoints, CategoryChanger) and alignment geometry
- [ ] Collector caching/Big-O optimization without profiling evidence
- [ ] Any new user-facing product feature

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| ExternalEvent reentrancy guard | HIGH | LOW | P1 |
| COMPLETED/ROLLED BACK feedback | HIGH | LOW-MEDIUM | P1 |
| Path sanitization | MEDIUM (security, low visible daily impact) | LOW | P1 |
| Bare-catch audit | MEDIUM (diagnosability) | MEDIUM (mechanical volume) | P1 |
| Manifest validation at startup | MEDIUM | LOW | P1 |
| Dialog whitelist verification | HIGH (blocks purge/conversion reliability) | MEDIUM (needs interactive session) | P1 |
| Static flag defensive reset (FormulaAutoGrouping) | MEDIUM | LOW | P1 |
| Startup assembly-version diagnostic | MEDIUM (future-proofing, not user-facing today) | LOW | P2 |
| Finer-grained ribbon availability | LOW (no evidence current split is wrong) | MEDIUM | P3 |
| Busy visual state on ribbon buttons | LOW-MEDIUM (nice polish) | MEDIUM | P3 |

**Priority key:**
- P1: Directly closes a documented concern — must have for this milestone to be "complete"
- P2: Natural extension of a P1 fix, cheap to add while already in that code
- P3: Polish; only pursue if time remains after P1s are done and validated

## Sources

- [Ribbon Panels and Controls — Autodesk Revit API Developers Guide](https://help.autodesk.com/cloudhelp/2024/ITA/Revit-API/files/Revit_API_Developers_Guide/Introduction/Add_In_Integration/Revit_API_Revit_API_Developers_Guide_Introduction_Add_In_Integration_Ribbon_Panels_and_Controls_html.html) — MEDIUM confidence (2024-dated, mechanism unchanged through 2026 per no contradicting evidence found)
- [External Events — Autodesk Revit API Developers Guide](https://help.autodesk.com/view/RVT/2024/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Advanced_Topics_External_Events_html) — MEDIUM confidence
- [IsPending Property — RevitApiDocs](https://www.revitapidocs.com/2020/355f86f4-a0ca-da4a-e263-1a27069ce174.htm) — MEDIUM confidence, cross-referenced with Building Coder usage pattern
- [Revit API – External events & modeless dialogs — BIM Matters](https://bimmatters.wordpress.com/2018/08/05/revit-api-external-events/) — MEDIUM confidence, community source consistent with official docs
- [Idling Enhancements and External Events — The Building Coder](https://jeremytammik.github.io/tbc/a/0743_external_event.htm) — MEDIUM confidence, canonical Revit API pattern source
- [DialogBoxShowingEventArgs Class — RevitApiDocs (2027)](https://www.revitapidocs.com/2027/8b6b969f-45d2-5b90-ca6d-593348ddf8d4.htm) and [TaskDialogShowingEventArgs Class (2025.3)](https://www.revitapidocs.com/2025.3/96cc0900-708b-5a2c-8d07-b2596ec20700.htm) — MEDIUM-HIGH confidence, official API reference
- [TaskDialog vs DialogBox, Id vs HelpId, and DialogBoxShowing Event](https://spiderinnet.typepad.com/blog/2011/07/taskdialog-vs-dialogbox-id-vs-helpid-and-dialogboxshowing-event.html) — MEDIUM confidence, long-standing community reference matching official docs on result-code semantics
- [Auto-Confirm Save Using DialogBoxShowing Event — The Building Coder](https://jeremytammik.github.io/tbc/a/0151_auto_confirm_save.htm) — MEDIUM confidence
- [Add-in Registration — Autodesk Revit API Developers Guide](https://help.autodesk.com/cloudhelp/2018/ENU/Revit-API/Revit_API_Developers_Guide/Introduction/Add_In_Integration/Add_in_Registration.html) and [Create a .addin manifest file (2025)](https://help.autodesk.com/view/RVT/2025/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Appendices_Hello_World_for_VB_NET_Create_a_addin_manifest_file_html) — HIGH confidence, official docs
- [How to prevent Path Traversal in .NET — Minded Security](https://blog.mindedsecurity.com/2018/10/how-to-prevent-path-traversal-in-net.html) and [Mitigating Path Traversal Attacks in .NET — StackHawk](https://www.stackhawk.com/blog/net-path-traversal-guide-examples-and-prevention/) — MEDIUM confidence (general .NET security guidance, not Revit-specific, but directly applicable)
- **Direct source evidence** (HIGH confidence — read directly, not inferred): `src/Core/ExternalEventCommand.cs`, `src/Core/ProjectDocumentAvailability.cs`, `src/Core/DialogWhitelist.cs`, `src/Core/Ribbon/RibbonService.cs`, `src/Core/Ribbon/RibbonFactory.cs`, `src/Services/Infrastructure/TransactionService.cs`, `src/Core/RevitCommand.cs`, `src/App.cs`, `src/Services/CadConversion/CadFamilySaveService.cs`
- `.planning/codebase/CONCERNS.md` (2026-07-04 analysis) — treated as a starting hypothesis, cross-checked against live source per protocol; one discrepancy noted (ribbon availability is partially wired already, contrary to the concern's framing of "not integrated at all")

---
*Feature research for: Revit add-in reliability/UX hardening (LECG)*
*Researched: 2026-07-05*
