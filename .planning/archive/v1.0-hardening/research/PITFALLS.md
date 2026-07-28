# Pitfalls Research

**Domain:** Hardening a production Revit 2026 add-in (C#/.NET 8, WPF, Nice3point toolkit, ~39 commands, shared-AppDomain with other vendors' add-ins)
**Researched:** 2026-07-05
**Confidence:** MEDIUM — Revit API behavior (IExternalCommandAvailability re-evaluation, DialogBoxShowing result codes, ExternalEvent semantics) is HIGH confidence (official docs + multiple independent long-running community sources: Jeremy Tammik's "The Building Coder", BIM Matters, Autodesk forums). Assembly-isolation-in-shared-AppDomain findings are MEDIUM (community-reported, not officially documented by Autodesk since it's an unsupported scenario by definition). No single source addresses this exact milestone (fixing all of CONCERNS.md at once without regressions), so pitfalls below are synthesized from general Revit add-in engineering pitfalls applied to the specific fixes this milestone requires.

## Critical Pitfalls

### Pitfall 1: IExternalCommandAvailability wired to document/view-change events instead of relying on Revit's automatic re-evaluation

**What goes wrong:**
CONCERNS.md's fix approach for the ribbon-availability gap literally says "update it on document/view change events" — but `IsCommandAvailable()` is called by Revit itself, synchronously, every time the ribbon needs to repaint (roughly every UI interaction and Idling tick), not just on document/view change. Adding `ViewActivated`/`DocumentChanged` subscriptions to push a cached availability flag is redundant work that adds event-handler surface and, if the handler throws or the cached flag gets stuck (e.g., set once and never cleared on a code path that bypasses the event), buttons can end up **permanently disabled** even though the live document state would allow the command — because the ribbon now trusts a stale cache instead of asking Revit to re-evaluate.

**Why it happens:**
Developers coming from typical UI frameworks assume "when does availability change?" needs an event-driven push model. Revit's ribbon model is pull-based: `IsCommandAvailable(app, activeDoc, selectedCategories)` is invoked on-demand by the framework. Anything that short-circuits with a cached bool is solving a problem Revit already solves, and introduces a new failure mode.

**How to avoid:**
- Implement `IExternalCommandAvailability.IsCommandAvailable` as a **pure, fast, side-effect-free** check against the arguments Revit passes in (`UIApplication`, `CategorySet`). Do not read cached instance/static state that could go stale.
- Wrap the entire method body in a try/catch that returns `false` on any exception — an unhandled exception from `IsCommandAvailable` can grey the button out for the rest of the session (Revit does not retry after a fault) with no error surfaced to the user.
- Explicitly handle the zero-document state (`activeDoc == null`) — a NullReferenceException here is the single most common cause of "button permanently disabled after closing all documents."
- Do NOT add `ViewActivated`/`DocumentChanged` handlers whose only purpose is to refresh ribbon state. If per-command computation is genuinely expensive, cache only within the single `IsCommandAvailable` call (no cross-call state) or accept the (cheap) recomputation cost — Revit already throttles how often it calls this.

**Warning signs:**
- A button stays disabled after switching to a document where it should be valid, and does not re-enable until Revit restarts.
- Any `catch` missing in the `IsCommandAvailable` implementation.
- Static or instance fields read inside `IsCommandAvailable` that are written by an event handler elsewhere.

**Phase to address:**
The "Missing UX features — Ribbon buttons disable/enable dynamically" phase (PROJECT.md Active). Verification: manually open/close project and family documents, switch between views with different categories selected, and confirm buttons re-enable without restarting Revit — this must be part of the interactive smoke-test checklist, not just a code review.

---

### Pitfall 2: try-finally reentrancy guard placed around the wrong scope for ExternalEvent-driven async commands

**What goes wrong:**
This is the exact `s_projectRunActive` bug already documented in CONCERNS.md, and it recurs easily even after a "fix" if the try-finally wraps the synchronous `Execute()` method rather than the actual asynchronous completion of the underlying operation. If `FormulaAutoGroupingCommand` (or any `RevitIdlingRunner`/`ExternalEventCommand`-based command) kicks off work that continues after `Execute()` returns (idling-driven or event-driven), resetting the flag in a try-finally scoped to `Execute()` will falsely mark the operation "not active" while it is still running in the background — reopening the reentrancy hole rather than closing it. Conversely, scoping the flag reset to the wrong disposable (one that is never disposed on some exception paths, e.g., an exception thrown before the `IDisposable` is constructed) reproduces the original bug.

**Why it happens:**
Revit commands look synchronous (`Execute` returns a `Result`) but `ExternalEventCommand`/`RevitIdlingRunner` patterns exist specifically because the real work is asynchronous relative to the command's `Execute` return. Any state-reset logic written with a "normal C# try/finally around the top-level call" mental model will silently be wrong for these two patterns, and the bug is invisible until an exception occurs during the async continuation (which is the failure mode CONCERNS.md is fixing) — a naive fix looks correct in the happy path and only fails under the exact conditions the original bug report describes.

**How to avoid:**
- Reset the flag in the disposal path of the object that scopes the *entire* logical operation (start-to-finish, including async continuation), not the object that scopes the synchronous `Execute()` call.
- Make the flag itself a disposable resource (a small `IDisposable` "run token" acquired at operation start, disposed exactly once when the operation truly ends — success, failure, or cancellation) so there is one code path, not N replicated try-finally blocks.
- Add a regression test that simulates an exception thrown mid-way through the async continuation (not just inside `Execute()`) and asserts the flag is clear afterward and a second run is permitted.
- Do not "fix" this pitfall by simply removing the guard — the guard exists to prevent a second project-wide run from starting concurrently with an in-flight one, which is a real hazard (concurrent Revit API access outside a valid transaction context).

**Warning signs:**
- Any reentrancy fix that only has test coverage for the happy path (no test forces an exception during the async portion).
- Flag-reset code physically located inside the `Execute()` method body rather than in a `Dispose()`/`finally` of the object representing the whole async job.

**Phase to address:**
The "Tech debt — s_projectRunActive" fix phase. This should be paired with (not separate from) the reentrancy-guard fix for `ExternalEventCommand` (Pitfall 3) since they are the same class of bug in two locations — fixing one without the other leaves an inconsistent pattern in the codebase.

---

### Pitfall 3: ExternalEventCommand busy-guard prevents reentrancy but never releases if the modeless dialog is closed without completing the workflow

**What goes wrong:**
CONCERNS.md's fix approach for CategoryChanger/ConvertCad reentrancy is "block re-invocation while an operation is in progress." If the busy flag is only cleared when the ExternalEvent handler's `Execute()` completes successfully, but the user closes the modeless WPF window via the OS "X" button, Alt+F4, or Revit document close — bypassing the handler's normal completion path — the flag never clears and the command becomes **permanently unavailable** until Revit restarts. This is a self-inflicted denial-of-service that is worse than the reentrancy bug it was meant to fix.

**Why it happens:**
Modeless WPF windows have multiple exit paths (Close button, window Closed event, owner document closing, Revit shutting down) and developers typically only wire the "happy path" button (e.g., an OK/Apply button) to clear the guard, forgetting the window's `Closing`/`Closed` event and abnormal termination paths.

**How to avoid:**
- Clear the busy flag from the WPF window's `Closed` event (fires on every close path: button, X, Alt+F4), not from the business-logic success path.
- Also subscribe to `UIApplication.Idling` or the document-closing event to force-clear stale guards if the owning document closes while the dialog is still open.
- Add a visible timeout/self-heal: if the guard has been set for an implausible duration (e.g., minutes) with no window reference, log a warning and clear it rather than requiring a Revit restart. This is a defensive backstop, not the primary fix.
- Prefer instance-scoped guard state tied to the actual `Window` object's lifetime over a bare static bool with no lifecycle linkage.

**Warning signs:**
- The busy-clearing code is only reachable from a "success" or "apply" code path.
- No handler subscribed to the WPF `Window.Closed` event.
- Manual test: open the dialog, close it via the taskbar/X button (not Cancel/OK), then try to reopen — if it fails to reopen, this pitfall has been reproduced.

**Phase to address:**
The "Fragile areas — Modeless dialog re-invocation blocked" phase. Verification must explicitly include closing the dialog via non-standard paths (X button, Esc, closing the underlying document) during the interactive smoke test — the "happy path" reopening test alone will not catch this.

---

### Pitfall 4: Catch-block audit converts an intentional "continue processing the batch" suppression into a rethrow that aborts the whole operation

**What goes wrong:**
CONCERNS.md calls for auditing all 54 bare catch blocks and having each one "log, re-throw with context, or carry an explicit suppress-with-reason." Several of these catches are almost certainly per-item failures inside a loop over many elements/families/parameters (e.g., batch rename, purge, label extraction across many elements) where the original design intent is "skip this one item, log it, keep going" — not "abort the entire batch." A mechanical audit pass that defaults to "add logging and rethrow when in doubt" will turn a partial-success batch operation into an all-or-nothing operation that now fails an entire 500-element rename because element #237 had a locked parameter — a functional regression, not a hardening improvement, and one that will only surface in production on large real-world models, not in a quick manual test with 5 elements.

**Why it happens:**
"Bare catch = bad, audit and fix" is generic advice; the domain-specific nuance is that batch/loop-scoped catches in element-processing code often exist specifically to isolate one bad element from the rest of the collection. A rethrow at that scope changes behavior from "N-1 succeeded, 1 reported" to "0 succeeded, nothing changed" (if wrapped in a transaction) or "N-1 succeeded then hard-crash mid-transaction" (if not) — both worse for the user than the current silent-skip behavior for that one item, even though the current behavior lacks visibility.

**How to avoid:**
- Before changing any catch block, first classify its scope: **operation-level** (wraps the whole command/transaction — safe to log-and-rethrow) vs. **per-item-level** (wraps one iteration of a loop over elements/files/parameters — should log-and-continue, not rethrow, unless the concern's own text calls for aborting).
- For per-item catches, the correct hardening is "log + continue + include in a final summary report to the user" (e.g., "Renamed 498/500; 2 skipped — see log"), not rethrow.
- Cross-reference each catch against CONCERNS.md's own text before changing it — e.g., `CadTempFileCleanupService`/`FamilyTempFileCleanupService` are explicitly cleanup-suppression cases (concern asks only for logging, not rethrow); `RevitIdlingRunner`'s job-failure handling is explicitly called out in CONCERNS.md itself as "gracefully handles job failures" and should NOT be converted to a rethrow.
- Write or update a test for each per-item catch that asserts "one bad item does not stop the batch" continues to hold after the change.

**Warning signs:**
- A diff that changes a catch block inside a `foreach`/loop body to rethrow, without a corresponding change to how the outer loop or transaction handles a mid-batch exception.
- Any batch-operation command (BatchRename, Purge) that previously processed partial results now fails outright on the first bad element in a test with mixed valid/invalid data.

**Phase to address:**
The "Error handling — 54 bare catch blocks audited" phase. This phase's plan should explicitly separate "operation-level catches" from "per-item catches" as two different remediation patterns, and require a regression test per batch-operation command proving partial-failure tolerance is preserved.

---

### Pitfall 5: Dependency isolation for Clipper2/DI.Abstractions breaks the *other* add-ins sharing the AppDomain, or silently doesn't actually change which assembly gets loaded

**What goes wrong:**
Revit loads all add-ins into a single AppDomain/process (AppDomains themselves are not a supported isolation mechanism under .NET 6+/Revit 2025+ builds — only `AssemblyLoadContext` is available, and Revit's own host does not create a separate ALC per add-in by default). Two failure modes are both well-documented in the community:
1. **`AppDomain.AssemblyResolve` handlers registered by one add-in do nothing** because whichever add-in's assembly loads *first* wins Revit's default probing/binding order — a handler registered later doesn't get a chance to redirect a version that's already resolved. Developers report spending significant effort on `AssemblyResolve`/binding-redirect approaches that had no effect because Revit's default resolution already completed before their code ran.
2. **ILRepack/merge-based isolation "fixes" the version conflict for LECG's own assembly but can break WPF resource loading** (pack:// URIs resolve relative to the merged assembly's identity, and merging can silently break `.baml`/resource lookups) or **break Serilog's assembly-scanning-based sinks/enrichers** if Serilog or its sinks are merged into the same assembly as application code (type-forwarding and reflection-based discovery inside Serilog can fail against a repacked assembly identity). Given LECG already has a documented `pack://` URI registration workaround in `App.OnStartup` (per CONCERNS.md), this project is already in the exact fragile zone where a merge tool is most likely to break WPF resource resolution.

**Why it happens:**
Isolation techniques designed for normal .NET application hosting (where your process controls assembly loading order) don't map cleanly onto Revit's model, where dozens of independently-authored add-ins race to load their dependencies first, and Autodesk does not guarantee load order across add-ins. Any isolation strategy must be validated against Revit's *actual* loading behavior (which assembly wins), not assumed from general .NET dependency-resolution knowledge.

**How to avoid:**
- Do not rely on `AppDomain.AssemblyResolve` alone — verify empirically (via the diagnostic logging CONCERNS.md already calls for) which Clipper2/DI.Abstractions version is actually bound at runtime, in the actual production environment with Enscape/ModPlus/Forma loaded, not just in isolation.
- Prefer `AssemblyLoadContext`-based isolation scoped only to LECG's own command execution path (load LECG's own copies of Clipper2/DI.Abstractions into a private ALC that LECG's code resolves against explicitly) over global AppDomain-wide binding redirects — this avoids affecting other add-ins' resolution at all, which is the safer failure mode.
- If considering ILRepack/merge, test WPF resource loading (every `pack://` reference, every `ResourceDictionary`) and Serilog logging output *after* merging, in a full Revit session — not just that the build succeeds. Do this before committing to the approach, since discovering it broken after other fixes are layered on top raises the cost of backing out.
- Treat "isolate LECG's dependency" and "diagnose what's actually loaded" as two separate, sequential deliverables (diagnose first, ship the diagnostic logging alone as a safe first step, then isolate) rather than one combined change — this matches the milestone's own stated risk tolerance (detection alone still leaves wrong results possible, per PROJECT.md's Key Decisions, but a botched isolation attempt is strictly worse than detection alone).
- Never assume success from "the build compiles and LECG's own commands still work" — the regression this pitfall causes appears in *other add-ins*, which will not be exercised by testing LECG alone. This requires the interactive smoke test to include launching Revit with all normally-installed add-ins present (Enscape, ModPlus, Forma), not a clean/isolated Revit profile.

**Warning signs:**
- Diagnostic logging shows the *intended* version loaded but geometry results are still wrong (indicates the diagnostic is reading LECG's own reference, not what's actually bound at the call site).
- WPF images/styles fail to load, or Serilog stops writing logs, immediately after introducing ILRepack/merge — these are the two most likely collateral failures given LECG's existing `pack://` fragility.
- Any change tested only with Revit running LECG in isolation (no other add-ins installed) — this cannot detect the "other add-in broke" failure mode at all.

**Phase to address:**
Split across two phases per PROJECT.md's own decision record: (1) "Fragile areas — startup diagnostic logs actually-loaded versions" first, as a low-risk standalone phase; (2) "dependency isolated (embedded/aliased)" as a separate, later phase with its own smoke-test requirement (full add-in suite installed, not isolated Revit).

---

### Pitfall 6: New command-level and service-level tests pass against Nice3point/Revit API reference assemblies while validating nothing about real Revit behavior

**What goes wrong:**
Revit API reference assemblies (whether Autodesk's own `RevitAPI.dll`/`RevitAPIUI.dll` referenced directly, or Nice3point's packaged equivalents) are **reference assemblies**: their method bodies are stubs that throw at runtime (typically `NotImplementedException` or similar) when actually invoked outside a running Revit process — they exist purely to let code compile against the correct API surface. Any new unit test written for this milestone that directly instantiates or calls into real Revit types (`Document`, `Element`, `FilteredElementCollector`, `Transaction`) — even indirectly, through a service under test that internally calls the real API — will either throw immediately (revealing the problem) or, worse, get wrapped by a broad catch somewhere in the test or app code and **report as a false pass** while exercising zero real logic. This directly threatens the milestone's test-coverage goal for Purge/AlignEdges/FixPoints/CategoryChanger: a test suite that "passes" but never actually calls a mocked/faked API surface gives false confidence that command-level behavior is locked in, when in fact only the parts of the code path that don't touch Revit types were exercised.

**Why it happens:**
It's tempting to write a test that constructs a command and calls `Execute()` with a real-looking `ExternalCommandData`, expecting normal C# polymorphism/mocking to work — but the Revit types themselves (many of them non-virtual, sealed, or requiring an internal Revit-only constructor) resist standard mocking frameworks (Moq cannot mock non-virtual members or classes without accessible constructors), pushing developers toward either (a) skipping the hard parts and testing only trivial logic, or (b) wrapping calls in try/catch "just to make the test green," both of which produce misleadingly-passing tests.

**How to avoid:**
- Follow the abstraction pattern already implied by LECG's architecture (services behind interfaces, DI composition root) — test business logic (validation, sanitization, decision branches, result-status computation) through the service's own interface with hand-written fakes/mocks for LECG's own abstractions (`ITransactionService`, `ILogger`, etc.), never by instantiating real `Document`/`Element` objects.
- For availability checks (`IsCommandAvailable`) and other logic that takes real Revit types as parameters, test only the branches reachable without dereferencing a live Revit object (e.g., `activeDoc == null` branch), and treat everything requiring a live Document as out of scope for unit tests — reserve it for the interactive smoke-test checklist instead.
- Explicitly document in the test project (or `.planning/codebase/TESTING.md` conventions) which categories of logic are unit-testable versus require a live Revit session, so future contributors don't assume "green test suite" implies "Revit-validated."
</br>
- Any test that calls a Revit API member and does NOT crash should be treated with suspicion until confirmed the member is either a trivial data holder (e.g., an enum, a DTO-like class with no Revit-side implementation) or has been explicitly verified safe to call outside Revit.
- Do not let "increase test count" become the milestone's implicit success metric — CONCERNS.md and PROJECT.md both frame test coverage as "high-risk focus," and a large number of tests that silently avoid the actual risky code paths (real geometry, real element mutation) defeats that purpose.

**Warning signs:**
- A test suite reports 100% pass with unusually fast execution time (real Revit API calls, if they worked at all, are comparatively slow; instantaneous "passing" tests around Document/Element interaction are a red flag they never touched a real object).
- Test code that catches `NotImplementedException` (or a bare `Exception`) around a Revit API call and treats "it didn't crash the test runner" as success.
- No corresponding entry in the interactive Revit smoke-test checklist for the same command whose "coverage" the automated suite claims.

**Phase to address:**
The "Test coverage (high-risk focus)" phase — should include an explicit statement of what the automated tests do and do NOT validate, cross-referenced against the "Runtime validation (interactive)" phase so the roadmap doesn't create a false sense that automated coverage substitutes for the smoke test.

---

### Pitfall 7: DialogWhitelist runtime discovery pass uses the wrong result-code type for the dialog class encountered

**What goes wrong:**
`DialogBoxShowingEventArgs` has multiple concrete subtypes (`TaskDialogShowingEventArgs`, `MessageBoxShowingEventArgs`), and each accepts a **different set of valid override codes**: `TaskDialog`-based dialogs need Revit's custom command-link IDs (1001, 1002, ... for custom buttons) or standard `TaskDialogResult` values depending on button configuration; `MessageBox`-based dialogs need Win32 `IDOK`/`IDCANCEL`-style IDs; and generic `DialogBox` dialogs accept any non-zero value to dismiss but the *chosen* value determines which button's action actually fires. Using the wrong code for the encountered dialog subtype can produce three distinct failure modes, not just "does nothing": (1) the override silently fails and the dialog stays open (blocking the modeless/automated flow entirely); (2) the override is accepted by the API but doesn't correspond to any real button, causing Revit to behave as though Cancel was pressed even when a "confirm" code was intended; (3) casting the event args to the wrong subtype (e.g., treating a MessageBox dialog as a TaskDialog) throws or returns null, silently skipping the override entirely for that occurrence.

**Why it happens:**
The whitelist entries are keyed by dialog ID and a result code chosen from documentation/guesswork (per CONCERNS.md, "low confidence, unverified guesses") without having observed the actual `DialogBoxShowingEventArgs` subtype Revit raises for that specific dialog in Revit 2026. The subtype and valid code range can only be confirmed by actually triggering the dialog and inspecting the event args at runtime — which is exactly why this concern requires the interactive discovery pass rather than a code-only fix.

**How to avoid:**
- During the interactive discovery pass, for each of the five low-confidence entries: log the actual runtime type of `DialogBoxShowingEventArgs` (via `e.GetType().Name` or pattern matching), the dialog ID, and the full set of valid `TaskDialogResult`/message-box IDs available for that specific dialog's button configuration, before choosing a code.
- Do not reuse a result code observed working for one dialog on a different dialog with a similar name/purpose — button layouts differ per dialog even within the same TaskDialog family.
- Verify the override actually took effect by observing the *downstream Revit behavior* (e.g., "operation proceeded as if Confirm was clicked"), not just that `OverrideResult` didn't throw — a code that's silently ignored produces no exception.
- Because some dialogs reportedly throw when `OverrideResult` is called in specific contexts (e.g., immediately after ending a transaction, per community reports), wrap each override attempt in the discovery pass with logging on both success and failure so ambiguous cases are visible rather than silently passing through un-dismissed.

**Warning signs:**
- A dialog in the whitelist that "usually works" but occasionally leaves a stuck modal during batch operations (purge/CAD conversion) — inconsistent behavior across runs is a strong signal the wrong code type is in play (works when the button layout happens to match, fails when it doesn't).
- Discovery-pass notes that don't record the concrete `DialogBoxShowingEventArgs` subtype observed, only "dialog ID X, code Y."

**Phase to address:**
The "DialogWhitelist entries verified against live Revit 2026" phase — must be executed interactively (already flagged in PROJECT.md as requiring the user at the keyboard) and should record, per entry, the observed event-args subtype alongside the dialog ID and result code, not just confirm/reject the existing guess.

---

### Pitfall 8: File-path sanitization added defensively breaks legitimate paths that were already safe (over-correction regression)

**What goes wrong:**
The security fix calls for sanitizing user-influenced paths in `CadFamilySaveService`, `FamilyEditorService`, `LinkedModelExportService`, and `SettingsManager` using `Path.GetFileName`/`Path.GetFullPath` normalization and parent-directory rejection. Because this milestone explicitly promises "fixes must not change user-visible behavior of working commands" (PROJECT.md Constraints), an overly aggressive sanitizer is a direct violation risk: rejecting valid Windows paths with legitimate characters (e.g., UNC paths `\\server\share\...` used for network-stored families/settings, paths containing legitimately embedded periods or spaces, or long paths near `MAX_PATH` requiring `\\?\` prefixing) will break existing working workflows for users who save families to network drives or deep folder structures — a regression introduced by the security fix itself, in a category (deployment/file I/O) the milestone explicitly must not regress.

**Why it happens:**
Generic path-sanitization advice (strip `..`, validate against a fixed allowed-character whitelist) is written for web-facing user input and doesn't account for legitimate desktop-application path conventions (UNC shares, drive letters, long-path prefixes) that a Revit add-in's users routinely rely on for corporate file servers.

**How to avoid:**
- Sanitize by *intent*, not by blanket character whitelisting: the goal is preventing a filename/parameter value from being interpreted as a path-traversal sequence when it's *supposed* to be just a filename (e.g., a family name, a rename target) — not restricting genuinely path-shaped inputs like a user-chosen save-as destination.
- Distinguish "this parameter is supposed to be a bare filename" (sanitize with `Path.GetFileName` + reject separators) from "this parameter is supposed to be a full path chosen via a file/folder browser" (validate with `Path.GetFullPath` + confinement check against an expected root, but don't strip UNC prefixes or reject legitimate long paths).
- Test against the existing production usage patterns first (network paths, deep folder hierarchies already in use by LECG's actual users) before locking down validation rules — this requires checking what real file locations are already exercised, not just theoretical attacker inputs.
- Add the sanitization as a targeted check with a clear, user-facing error message when rejected (not a silent failure) so any false-positive rejection is immediately visible and reportable, rather than manifesting as a mysterious "file not found" deep in a later step.

**Warning signs:**
- A path-validation change that has no test asserting a legitimate UNC or long path still passes.
- Any sanitizer applied uniformly to both "bare filename" and "full path" fields without distinguishing the two.

**Phase to address:**
The "Security — user-influenced file paths sanitized" phase. Verification should include the interactive smoke test covering at least one save/export operation to a realistic non-trivial path (network share or deep folder) to catch over-correction before it reaches users.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|-----------------|------------------|
| Blanket "log + rethrow" applied to every bare catch during the audit, without classifying operation-level vs per-item scope | Fast to execute; audit "looks complete" quickly | Regresses batch operations from partial-success to all-or-nothing (Pitfall 4) | Never — always classify scope first |
| Static bool reentrancy flags (as currently used) instead of scoped disposable "run tokens" | Minimal code change, quick to reason about in isolation | Every new command that copies the pattern reintroduces the same latch/leak risk (Pitfall 2, 3) | Acceptable only if paired with a shared, tested `IDisposable` run-token helper so the pattern isn't hand-rolled per command |
| AppDomain-wide `AssemblyResolve` redirect as the isolation strategy for Clipper2/DI.Abstractions | Looks like a complete fix without touching build tooling | May silently do nothing (load order already resolved) while giving false confidence the conflict is fixed (Pitfall 5) | Never as the sole mechanism; only acceptable as a diagnostic/logging aid, not the isolation mechanism itself |
| Writing "unit tests" that call into real Revit API types and rely on try/catch to keep them green | Test count goes up fast, satisfies a coverage metric | False confidence; real command behavior remains unvalidated (Pitfall 6) | Never — always test through LECG's own service abstractions or defer to the interactive smoke test |
| Uniform path-sanitization whitelist copy-pasted across all four flagged services | Fast, consistent-looking fix | Breaks legitimate UNC/long paths in at least one of the four different usage contexts (Pitfall 8) | Only acceptable if each of the four call sites is individually reviewed for its actual expected input shape first |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|-------------------|
| Shared Revit AppDomain (Enscape, ModPlus, Forma) | Assuming your add-in's `AssemblyResolve` handler or binding redirect will run before a conflicting version is already loaded | Diagnose actual loaded version first (log at startup); prefer a private `AssemblyLoadContext` scoped to LECG's own execution rather than AppDomain-wide redirects |
| Revit `DialogBoxShowing` event | Treating all dialogs as accepting the same override-code scheme | Cast to the concrete `TaskDialogShowingEventArgs`/`MessageBoxShowingEventArgs` subtype and use the code scheme valid for that subtype and button layout, confirmed per-dialog at runtime |
| Revit `ExternalEvent`/modeless dialog lifecycle | Clearing a busy/reentrancy guard only from the "success" code path | Clear the guard from the WPF window's `Closed` event (covers every close path) and add a defensive timeout/self-heal |
| Revit ribbon (`IExternalCommandAvailability`) | Pushing availability state via document/view-change event handlers | Let Revit call `IsCommandAvailable` on its own pull-based schedule; keep the method pure, fast, and exception-safe |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|-----------------|
| Profiling with `Stopwatch` around Revit API calls without accounting for regeneration mode | Measured times vary wildly run-to-run; optimization "wins" don't reproduce | Set/document Regeneration mode explicitly during profiling; profile with `doc.Regenerate()` calls isolated from the timed section so regeneration cost isn't misattributed to the code under test | Any profiling pass whose numbers are used to justify (or skip) an optimization in the "Performance (profile first)" phase |
| Treating `Idling`-event-based background work as "free" background time | Revit responsiveness degrades noticeably during batch operations even though no dialog is blocking | Keep Idling handler work chunked/short per tick; avoid moving expensive work into Idling purely to "get off the UI thread" without measuring perceived responsiveness | Any use of `RevitIdlingRunner` for genuinely large batches (thousands of elements) |
| Adding FilteredElementCollector caching speculatively before profiling evidence exists | Extra complexity (cache invalidation bugs) with no measured benefit; risk of stale results if cache isn't invalidated on document change | Follow the milestone's own profile-first policy; only add caching where profiling in the actual target service (BatchRename/Purge/Alignment) shows collector cost is the bottleneck | If a "since we're profiling anyway" scope-creep adds caching without dedicated invalidation testing |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Applying a single path-sanitization rule to both "bare filename" and "full path" input fields | Breaks legitimate UNC/long paths (over-correction) or under-sanitizes a field that should be filename-only | Classify each of the four flagged call sites by expected input shape before writing the sanitizer (Pitfall 8) |
| Logging full file paths for diagnostic purposes during the hardening effort itself (temporary debug logging added to investigate a concern) | Temporary logging additions during this milestone could reintroduce the exact path-exposure concern being fixed elsewhere in the same milestone | Apply the "show names, not full directory structure" logging convention to any new diagnostic logging added while investigating other concerns, not just the originally-flagged log sites |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|--------------|-------------------|
| Ribbon buttons disabled based on stale cached state | User sees a valid command greyed out with no explanation, may assume the add-in is broken and restart Revit unnecessarily | Pure, on-demand `IsCommandAvailable` (Pitfall 1); if truly unavailable, consider whether Revit's own tooltip mechanism can explain why (where supported) |
| Rollback/failure feedback added as a generic "operation failed" message | User still doesn't know whether partial changes were applied or fully rolled back, undermining the trust goal this fix is meant to restore | Explicit COMPLETED vs ROLLED BACK status text, plus (for batch operations, per Pitfall 4) a partial-success count when applicable ("Renamed 498/500; see log for 2 skipped") |
| Reentrancy guard silently blocks a second click with no message | User thinks the button is unresponsive and clicks repeatedly, or assumes Revit is frozen | Show a lightweight status (log entry at minimum, ideally a brief TaskDialog or status bar message) when a reentrant invocation is blocked, per CONCERNS.md's own fix approach ("log and show a warning") |

## "Looks Done But Isn't" Checklist

- [ ] **IExternalCommandAvailability wiring:** Often missing the null-active-document branch and an exception-safety wrapper — verify by closing all documents and switching between project/family documents repeatedly without restarting Revit.
- [ ] **Reentrancy guard fix (FormulaAutoGroupingCommand / ExternalEventCommand):** Often only tested against exceptions thrown synchronously in `Execute()` — verify by forcing a failure during the async/idling-driven continuation and confirming a subsequent run is permitted.
- [ ] **Dependency isolation (Clipper2/DI.Abstractions):** Often validated only by checking LECG's own commands still work — verify by running Revit with the full normal add-in complement (Enscape, ModPlus, Forma) installed and confirming those add-ins still function correctly too.
- [ ] **Catch-block audit:** Often applied uniformly — verify each changed batch/loop-scoped catch still allows partial success by testing with a mixed valid/invalid input set (e.g., rename batch with one locked element among many).
- [ ] **DialogWhitelist discovery pass:** Often records only "confirmed/rejected" per entry — verify each entry's write-up also states the observed `DialogBoxShowingEventArgs` concrete subtype, not just the dialog ID and code.
- [ ] **Command-level test coverage:** Often reports high pass counts — verify by checking execution speed (suspiciously instant Revit-API-touching tests) and confirming no test silently catches `NotImplementedException`/generic `Exception` around a real API call.
- [ ] **Path sanitization:** Often verified only against attack-style inputs (`../../etc`) — verify legitimate UNC paths and realistically deep folder paths used by actual current users still work.
- [ ] **Rollback/completion UX feedback:** Often verified only for the fully-successful and fully-failed cases — verify the partial-success/partial-rollback case (if applicable to a given command) shows accurate status, not a generic message.

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|----------------|-----------------|
| Ribbon button permanently greyed after `IsCommandAvailable` fix (Pitfall 1) | LOW | Add the missing null/exception guard; no data corruption risk, just requires a Revit restart to clear the stuck ribbon state during dev/test |
| Reentrancy flag permanently latched (Pitfall 2/3) | LOW–MEDIUM | Requires Revit restart for the affected user; fix is a targeted code change (move reset to correct disposal scope) plus a regression test; no data loss since the guard's purpose is preventing concurrent execution, not data integrity by itself |
| Batch operation regressed to all-or-nothing after catch-block audit (Pitfall 4) | MEDIUM | Revert the specific catch-block change to log-and-continue; re-run the batch-operation regression test suite; no data corruption, but user-visible behavior change needs a fast follow-up fix once discovered |
| Dependency isolation broke another vendor's add-in (Pitfall 5) | HIGH | Requires coordinating with the affected add-in vendor or reverting the isolation change entirely; may require an emergency rollback of the LECG deployment; this is why the diagnose-first, isolate-second sequencing (already decided in PROJECT.md) matters — recovery is much cheaper if isolation is deployed and validated separately from diagnostics |
| Over-aggressive path sanitizer rejects legitimate network/long paths (Pitfall 8) | MEDIUM | Loosen the specific validation rule that rejected the legitimate case; add the newly-discovered legitimate pattern as a regression test; user impact is a blocked save/export (recoverable, no data loss) but erodes trust in the "no behavior change" promise |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|-------------------|----------------|
| Ribbon availability wired to events instead of pull-based checks (1) | Missing UX features — ribbon availability | Manual: switch project/family docs and views repeatedly without restarting Revit; button state must track live document state |
| Reentrancy flag reset at wrong scope (2) | Tech debt — s_projectRunActive | Automated: unit test forcing an exception during async continuation; asserts flag clears and reruns are permitted |
| Modeless dialog busy-guard never clears on abnormal close (3) | Fragile areas — modeless dialog reentrancy | Manual: close dialog via X/Alt+F4/document-close, confirm command reopens |
| Catch-block audit breaks batch partial-success (4) | Error handling — 54 bare catch blocks audited | Automated: regression test per batch command with mixed valid/invalid input, asserting partial success is preserved |
| Dependency isolation breaks other add-ins or has no effect (5) | Fragile areas — Clipper2/DI.Abstractions isolation (split: diagnostic phase first, isolation phase second) | Manual: full Revit session with Enscape/ModPlus/Forma installed; diagnostic log confirms actually-bound version matches intent |
| Tests pass against non-functional Revit reference assemblies (6) | Test coverage (high-risk focus) | Review: confirm no test catches NotImplementedException/generic Exception around a live Revit API call; cross-reference against smoke-test checklist for the same command |
| Wrong result-code type used in DialogWhitelist discovery (7) | Tech debt — DialogWhitelist verification (interactive) | Manual: interactive discovery session recording concrete event-args subtype per entry, not just dialog ID/code |
| Path sanitizer over-corrects and breaks legitimate paths (8) | Security — file path sanitization | Manual: smoke test a save/export to a realistic UNC or deep folder path |

## Sources

- [Add-on not showing in the Ribbon Tab in Revit — Autodesk](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/Revit-Add-in-not-showing.html)
- [External Commands Disabled During Certain Views — RevitForum](https://www.revitforum.org/forum/revit-architecture-forum-rac/architecture-and-general-revit-questions/2468-external-commands-disabled-during-certain-views)
- [Adding contextual buttons to the ribbon — Autodesk Community](https://forums.autodesk.com/t5/revit-api-forum/adding-contextual-buttons-to-the-ribbon/td-p/7154519)
- [Issue in ExternalEvent — Autodesk Community](https://forums.autodesk.com/t5/revit-api-forum/issue-in-externalevent/td-p/8643136)
- [External Events — Revit API Developers Guide, Autodesk](https://help.autodesk.com/view/RVT/2024/ENU/?guid=Revit_API_Revit_API_Developers_Guide_Advanced_Topics_External_Events_html)
- [Idling Enhancements and External Events — The Building Coder (Jeremy Tammik)](https://jeremytammik.github.io/tbc/a/0743_external_event.htm)
- [Revit API – External events & modeless dialogs — BIM Matters](https://bimmatters.wordpress.com/2018/08/05/revit-api-external-events/)
- [How to best handle dll conflicts with revit addins — Autodesk Community](https://forums.autodesk.com/t5/revit-api-forum/how-to-best-handle-dll-conflicts-with-revit-addins/td-p/13285022)
- [Using AppDomains to Resolve DLL Conflicts in Revit Plugins — sharpbim](https://sharpbim.hashnode.dev/using-appdomains-to-resolve-dll-conflicts-in-revit-plugins)
- [Dependency Hell in Revit: My Experience — Medium (Symon Kipkemei)](https://medium.com/@symonkipkemei/dependency-hell-in-revit-my-experience-98d29b8dbeb6)
- [Major Issues with dll conflicts, Revit 2022 — McNeel Forum](https://discourse.mcneel.com/t/major-issues-with-dll-conflicts-revit-2022/148281)
- [Merging assemblies using ILRepack — Meziantou's blog](https://www.meziantou.net/merging-assemblies-using-ilrepack.htm)
- [Failed to load assembly when merging complete project — il-repack GitHub Issue #203](https://github.com/gluck/il-repack/issues/203)
- [Unable to override dialog with Ok value in DialogBoxShowing event handler — Autodesk Community](https://forums.autodesk.com/t5/revit-api-forum/unable-to-override-dialog-with-ok-value-in-dialogboxshowing/td-p/9752467)
- [OverrideResult Method — RevitAPIDocs](https://www.revitapidocs.com/2015/49ba2725-74c9-02b3-4321-eac1f2295bd3.htm)
- [DialogBoxShowing Event — RevitAPIDocs](https://www.revitapidocs.com/2019/cb46ea4c-2b80-0ec2-063f-dda6f662948a.htm)
- [dismissing Revit pop-ups — the easy and not so easy ways — archi-lab](https://archi-lab.net/dismissing-revit-pop-ups-the-easy-and-not-so-easy-ways/)
- [Enable Ribbon Items in Zero Document State — The Building Coder (Jeremy Tammik)](https://jeremytammik.github.io/tbc/a/0538_zero_doc_ribbon.htm)
- [Revit API: Enabling ribbon controls in Zero Document State — BIM Matters](https://bimmatters.wordpress.com/2016/03/26/zero-doc-state/)
- [Performance Profiling — The Building Coder (Jeremy Tammik)](http://jeremytammik.github.io/tbc/a/0332_performance_profiling.htm)
- [Timer Code for Benchmarking — The Building Coder](https://thebuildingcoder.typepad.com/blog/2012/01/timer-code-for-benchmarking.html)
- [Using the Revit API's Idle Event — House of BIM](https://www.houseofbim.com/posts/using-the-revit-apis-idle-event/)
- [Revit Add-in Unit Testing — The Building Coder](https://thebuildingcoder.typepad.com/blog/2013/07/revit-add-in-unit-testing.html)
- [GitHub — geberit/Revit.TestRunner](https://github.com/geberit/Revit.TestRunner)
- [.planning/codebase/CONCERNS.md — this repo's own documented concerns (internal source, primary basis for phase mapping)](../codebase/CONCERNS.md)

---
*Pitfalls research for: Revit 2026 add-in hardening milestone (LECG)*
*Researched: 2026-07-05*
</content>
