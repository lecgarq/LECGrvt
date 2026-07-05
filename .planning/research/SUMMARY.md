# Project Research Summary

**Project:** LECG Revit Add-in — Codebase Hardening
**Domain:** Brownfield reliability/security hardening of a production Revit 2026 add-in (C#/.NET 8, WPF MVVM, ~39 commands)
**Researched:** 2026-07-05
**Confidence:** MEDIUM-HIGH

## Executive Summary

This is not a green-field build — it is a hardening pass on an existing, working Revit 2026 add-in, and all four research tracks converge on the same conclusion: **almost every fix has a narrow, well-precedented solution that requires zero framework changes**, and the biggest risk in this milestone is not "what to build" but "sequencing and scope creep." Revit 2026 ships a first-party manifest setting (`ManifestSettings/UseRevitContext`) that solves the Clipper2/DI.Abstractions version-conflict problem natively; the existing xUnit/FluentAssertions/NSubstitute stack already supports the wrap-and-mock pattern needed for command-level tests once a narrow interface is extracted around Revit types; `dotnet-trace` + PerfView can profile the live Revit.exe process with zero code changes; and two already-referenced analyzer packages (`Microsoft.CodeAnalysis.NetAnalyzers` CA1031, `Roslynator.Analyzers` RCS1075) just need `.editorconfig` entries to enforce the catch-block audit permanently. No new dependencies are required anywhere in this milestone.

Architecture research also corrected two of the codebase own concern-tracking documents against live source: ribbon availability (`IExternalCommandAvailability`) is already ~90% wired — only the Align pulldown 8 sub-items are missing it — and several "monolithic service" candidates for decomposition should NOT be touched this milestone; only `BatchRenameExecutionService` has another fix (formula/dimension TODOs) that justifies opening the file. This "verify against current code before planning a phase" discipline should carry into roadmap phase design: several CONCERNS.md line items are smaller than they appear once cross-checked against source.

The key risks are: (1) architectural coupling between fix areas that share files or mechanisms — the `ExternalEventCommand` reentrancy guard and the modeless dialog re-invocation block are the *same* underlying mechanism and must be built together, not sequentially, and the `ITransactionService` rollback-status change ripples to `RevitCommand` exception handling and should land as one coordinated change, not split across phases; (2) mechanical fixes that quietly change behavior — a blanket "log and rethrow" pass over the 54 bare-catch sites will regress batch operations (BatchRename, Purge) from partial-success to all-or-nothing unless each catch is classified operation-level vs. per-item-level first; (3) fixes that only fully validate in a live, multi-add-in Revit session — dependency isolation, DialogWhitelist result codes, and the smoke-test checklist cannot be resolved by code review alone and must be sequenced toward the end of the milestone, run against the *full* add-in complement (Enscape, ModPlus, Forma), not an isolated Revit profile.

## Key Findings

### Recommended Stack

No new runtime dependencies. The hardening tooling is: (1) Revit 2026 native `ManifestSettings/UseRevitContext=False` manifest isolation for dependency conflicts (zero code changes, first-party Autodesk feature); (2) the existing xUnit + FluentAssertions + NSubstitute stack combined with a "wrap Revit types behind a narrow interface, mock the interface" pattern for command-level tests (root cause: NSubstitute/Castle DynamicProxy cannot mock sealed/no-public-ctor Revit types directly, but mocks interfaces fine); (3) `dotnet-trace` (global CLI tool, attach-by-PID) + PerfView for profiling the live Revit.exe process, backed by cheap `Stopwatch` checks for a first pass; (4) `.editorconfig` activation of already-referenced `CA1031` and `RCS1075` analyzer rules for the catch-block audit, with `SonarAnalyzer.CSharp` `S2486` (scoped narrowly) as an optional refinement.

**Core technologies:**
- Native manifest `ManifestSettings` (Revit 2026) — dependency isolation — first-party, zero code change, solves both Clipper2 and DI.Abstractions conflicts in one edit
- xUnit/FluentAssertions/NSubstitute (already in place) + wrap-and-mock pattern — command-level test coverage — no new framework, closes the actual blocker (Revit types aren't mockable directly)
- `dotnet-trace` + PerfView — profiling — attaches to already-running Revit.exe by PID, matches the "profile first" policy
- CA1031 + RCS1075 via `.editorconfig` — catch-block audit enforcement — both packages already referenced, currently inert, one-line activation each

**Explicitly rejected:** AppDomain-based isolation (unsupported on .NET 6+), ILRepack/ILMerge and Costura.Fody (known failure modes with WPF/reflection), third-party ALC-isolation libraries (would require rewriting command base classes), Moq (no advantage over NSubstitute here), Revit-hosted test frameworks as the *primary* CI strategy (breaks the deliberate no-Revit CI design).

### Expected Features

This is a reliability/UX feature landscape, not a market landscape. "Table stakes" = what a hardened Revit add-in must have; most gaps are narrower than `CONCERNS.md` implies once cross-checked against source.

**Must have (table stakes / P1, all directly named in PROJECT.md):**
- `ExternalEvent.IsPending` reentrancy guard in `ExternalEventCommand<THandler>.RaiseExternalEvent()` — the actual gap; static fields already exist, the check does not
- Modeless dialog re-invocation block (CategoryChanger, ConvertCad) built on the same guard
- TransactionService signals rollback-vs-other-failure distinguishably; RevitCommand surfaces COMPLETED / ROLLED BACK
- Path sanitization (`Path.GetFileName` + `Path.GetFullPath` containment check) in 4 flagged file-writing services
- Bare-catch audit across 54 sites, classified operation-level vs. per-item, using a new `LogAndIgnore` helper
- `.addin` manifest presence/validity check logged at `OnStartup` (detection only; restore procedure is documentation)
- `FormulaAutoGroupingCommand` static flag reset via try-finally/disposable scoped to the *whole* async operation, not just `Execute()`
- Dialog whitelist entries verified via live Revit 2026 interactive discovery pass (cannot be resolved by code alone)

**Should have (P2, cheap extensions of a P1 fix, do together):**
- Startup diagnostic banner logging actually-loaded Clipper2/DI.Abstractions versions — pairs with the isolation fix, should land *before* it (diagnose, then isolate)
- Structured startup smoke-check for XAML/pack:// resource loading

**Defer / anti-features (do not build this milestone):**
- Generic `Result<T>` monad across all services — keep the transaction-outcome fix local and additive
- Full custom dialog-interception engine with heuristic/regex matching — exact `DialogId` matching is correct and already implemented
- Global reentrancy mutex across all 39 commands — Revit is single-threaded for modal commands; the problem is specific to `ExternalEventCommand`
- Full generic assembly-isolation framework — solve Clipper2/DI.Abstractions specifically, stop there
- Telemetry/analytics dashboard, retry-with-backoff auto-recovery for category-change — out of scope per PROJECT.md
- Finer-grained ribbon availability beyond project/family split, busy visual state on ribbon — P3 polish only if time remains

### Architecture Approach

The existing layered architecture (Command -> ViewModel/View -> Domain Service -> `ITransactionService` -> Revit API) is retained unchanged; this milestone real architectural question is **fix sequencing**, because several fix areas share files, share mechanisms, or share risk surface. The dependency-driven order derived from research: build the `LogAndIgnore` helper before the 54-site catch audit; fix the CI-mode test-scaffolding issue before writing new command-level tests; treat service decomposition (`BatchRenameExecutionService` only) as a *consequence* of the formula/dimension bug fix, not a standalone task; treat the `ExternalEventCommand` reentrancy guard and the modeless dialog re-invocation block as one phase (same mechanism, same two consumer commands); land the `ITransactionService` rollback-status change and its `RevitCommand` UI presentation together, not split; sequence Clipper2/DI.Abstractions diagnostic logging before isolation, and isolation before alignment-geometry tests (so tests run against a guaranteed-correct Clipper2 version); profile only after decomposition and tests exist (so profiling targets the final code shape); and close the milestone with the interactive, user-assisted work (DialogWhitelist discovery, full smoke-test checklist) so it validates the cumulative set of fixes in one pass.

**Major components (hardening-relevant):**
1. `ExternalEventCommand<THandler>` (`src/Core/ExternalEventCommand.cs`) — owns static handler/event state; needs a busy-guard cleared from the WPF window `Closed` event (all close paths), not just the success path
2. `ITransactionService` (`src/Services/Infrastructure/TransactionService.cs`) — single point for all document writes (71+ call sites); needs an *additive* rollback-status signal, not a breaking signature change
3. `RibbonService`/`RibbonFactory` (`src/Core/Ribbon/`) — availability wiring already ~90% done; only the Align pulldown 8 sub-items and `CreatePulldownButton` own availability parameter are missing
4. `BatchRenameExecutionService` (1019 lines) — the one service to decompose this milestone, and only because its formula/dimension TODOs require touching it; static/pure helper methods are the cheap extraction target
5. 54 bare `catch` blocks across 35 files — need classification (operation-level vs. per-item-level) before any fix, backed by a shared `LogAndIgnore`/`SafeIgnore` helper

### Critical Pitfalls

1. **Wiring `IExternalCommandAvailability` to document/view-change events instead of Revit pull-based re-evaluation** — Revit already calls `IsCommandAvailable` on-demand; a cached/event-pushed flag can get permanently stuck disabled. Keep the method pure, fast, exception-safe, and handle the null-active-document case explicitly.
2. **Reentrancy/busy-flag reset scoped to the wrong lifetime** — resetting inside `Execute()` rather than the disposal of the object scoping the *entire* async operation reopens the exact bug being fixed (`s_projectRunActive`, `ExternalEventCommand`). Use a disposable "run token" cleared in `finally`/`Closed`, covering abnormal exits (X button, Alt+F4, document close), not just the happy path.
3. **Catch-block audit converting per-item "skip and continue" suppressions into operation-aborting rethrows** — a mechanical "log and rethrow when in doubt" pass will turn a 500-element batch rename into an all-or-nothing operation the first time one element fails. Classify scope (operation-level vs. per-item) before changing any catch block, and require a partial-success regression test for each batch command touched.
4. **Dependency isolation validated only against LECG own commands** — the failure mode this pitfall causes (another vendor add-in breaking) is invisible unless the smoke test runs with the full normal add-in complement (Enscape, ModPlus, Forma) installed, not an isolated Revit profile. Diagnose (log actually-loaded versions) as a separate, earlier, lower-risk deliverable before attempting isolation.
5. **DialogWhitelist result codes chosen without confirming the concrete `DialogBoxShowingEventArgs` subtype** — `TaskDialog`/`MessageBox`/generic `DialogBox` dialogs each need different result-code schemes; guessing produces silent no-ops or wrong-button behavior, not exceptions. The interactive discovery pass must record the observed subtype per entry, not just the dialog ID and code.
6. **Path sanitization over-correcting and breaking legitimate UNC/long paths** — generic web-style sanitization advice doesn fit a desktop app network-drive/deep-folder conventions; classify each of the 4 flagged call sites as "bare filename" vs. "full path chosen by user" before writing the sanitizer, and test against real production path patterns, not just attack-style inputs.

## Implications for Roadmap

Based on research, suggested phase structure (10 phases, ordered by the dependency graph derived from architecture + pitfalls research):

### Phase 1: Test Scaffolding + Shared Helpers
**Rationale:** Everything downstream (catch audit, command-level tests) depends on this being unblocked or built first; doing it later means redoing earlier work.
**Delivers:** CI-mode Revit-dependent test compile issue diagnosed/fixed; `LogAndIgnore`/`SafeIgnore` helper built against `LECG.Services.Logging.ILogger` scope-aware signature.
**Addresses:** Foundational prerequisite for "Test coverage" and "Error handling" requirements in PROJECT.md.
**Avoids:** Pitfall of auditing 54 catch sites twice (once ad hoc, once retrofitted to a helper).

### Phase 2: Bare-Catch Audit (54 sites)
**Rationale:** Depends on Phase 1 helper; independent of everything else architecturally.
**Delivers:** All 54 sites classified operation-level vs. per-item-level, each logging/rethrowing/explicitly suppressing via `LogAndIgnore`.
**Addresses:** "Error handling" requirement (CadTempFileCleanupService, FamilyTempFileCleanupService, and 52 others).
**Avoids:** Pitfall 4 (batch operations regressed to all-or-nothing) — requires a partial-success regression test per batch command touched.

### Phase 3: Reentrancy Guards (ExternalEventCommand + s_projectRunActive)
**Rationale:** Same class of bug in two locations; fixing one without the other leaves an inconsistent pattern. Independent of Phases 1-2 files.
**Delivers:** Busy-guard on `ExternalEventCommand<THandler>` cleared via WPF `Closed` event (all exit paths); `FormulaAutoGroupingCommand` flag reset scoped to the whole async operation via disposable run-token.
**Addresses:** "Fragile areas" and "Tech debt" requirements (ExternalEventCommand reentrancy, s_projectRunActive, modeless dialog re-invocation block).
**Avoids:** Pitfalls 2 and 3 (wrong-scope flag reset; guard never releasing on abnormal window close).

### Phase 4: Ribbon Availability + Transaction Rollback UX
**Rationale:** Both are small, well-understood, and architecturally isolated from Phases 1-3; group together as "user-facing feedback" fixes. Ribbon fix is cheap (~1/10th the size CONCERNS.md implies since 90% is already wired).
**Delivers:** Align pulldown 8 sub-items gain `ProjectDocumentAvailability`; `ITransactionService` gains an additive rollback-status signal; `RevitCommand` exception handler renders COMPLETED vs. ROLLED BACK.
**Addresses:** "Missing UX features" requirement.
**Avoids:** Pitfall 1 (event-driven ribbon availability instead of pull-based) and the anti-pattern of treating ribbon availability as a from-scratch build.

### Phase 5: Known Bug Fixes + Targeted Decomposition
**Rationale:** BatchRenameExecutionService formula/dimension TODOs are the *reason* to touch the file; decomposition follows the fix, not the other way around.
**Delivers:** FamilyEditorService category-change actionable error message; BatchRenameExecutionService formula-reference/dimension-label handling implemented; static/pure helper methods extracted to a sibling service with tests.
**Addresses:** "Known bugs" and part of "Tech debt" requirements.
**Avoids:** Anti-pattern of decomposing all 5 "monolithic services" just because CONCERNS.md lists them together — only this one has another fix pointing at it.

### Phase 6: Security — Path Sanitization
**Rationale:** Architecturally isolated (no shared files with Phases 1-5); scheduled for engineering-capacity reasons rather than dependency reasons.
**Delivers:** `Path.GetFileName`/`Path.GetFullPath` containment checks in CadFamilySaveService, FamilyEditorService, LinkedModelExportService, SettingsManager, classified per-call-site as "bare filename" vs. "full path"; log-path sanitization (show names, not full directory structure).
**Addresses:** "Security" requirement.
**Avoids:** Pitfall 6/8 (over-aggressive sanitizer breaking legitimate UNC/long paths) — requires a realistic-path smoke test, not just attack-style input tests.

### Phase 7: Dependency Isolation (Clipper2 / DI.Abstractions)
**Rationale:** Must precede alignment-geometry tests (Phase 8) so those tests run against a guaranteed-correct Clipper2 version. Split into diagnose-first, isolate-second per PROJECT.md own decision.
**Delivers:** Startup diagnostic logging of actually-loaded Clipper2/DI.Abstractions versions; `ManifestSettings/UseRevitContext=False` manifest isolation.
**Uses:** Native Revit 2026 manifest feature (STACK.md) — zero new dependencies.
**Avoids:** Pitfall 5 (isolation validated only against LECG own commands, missing the "broke another add-in" failure mode) — smoke test must run with Enscape/ModPlus/Forma installed. Also must explicitly verify interaction with LECG existing `pack://` WPF workaround.

### Phase 8: Test Coverage (High-Risk Focus)
**Rationale:** Depends on Phase 1 (scaffolding) and Phase 7 (stable Clipper2 version for geometry tests); can otherwise proceed once those land.
**Delivers:** Command-level tests for Purge, AlignEdges, FixPoints, CategoryChanger via wrap-and-mock pattern; alignment geometry service tests (boundary-point, vertex-alignment); tests locking in each Phase 2-6 bug fix.
**Addresses:** "Test coverage (high-risk focus)" requirement.
**Avoids:** Pitfall 6 (tests that silently pass without exercising real Revit-type logic) — must document which logic is unit-testable vs. requires the live smoke test, and must not treat "test count" as the success metric.

### Phase 9: Performance Profiling
**Rationale:** Depends on Phases 5 and 8 existing, so profiling targets the post-decomposition, post-test code shape rather than code about to change.
**Delivers:** `dotnet-trace`/PerfView profiles of BatchRename, Purge, Alignment against `FilteredElementCollector` cost; only proven bottlenecks optimized, findings documented.
**Uses:** `dotnet-trace` + PerfView (STACK.md), attach-by-PID against live Revit.exe.
**Avoids:** Performance-trap of profiling with Stopwatch without accounting for Revit regeneration mode; avoids speculative collector caching without profiling evidence.

### Phase 10: Deployment, Documentation & Interactive Validation
**Rationale:** Fully independent of code fixes architecturally, but validates the *cumulative* set of prior phases — must close, not open, the milestone per PROJECT.md "code-first, validate-second" constraint.
**Delivers:** `.addin` manifest presence/validity check at `OnStartup`; deploy-on-build risk documented; pack:// URI workaround documented with smoke check; DialogWhitelist entries verified via live Revit 2026 discovery pass (recording concrete event-args subtype per entry); Revit smoke-test checklist executed with user at the keyboard.
**Addresses:** "Deployment," "Documentation," and "Runtime validation" requirements.
**Avoids:** Pitfall 7 (wrong result-code type for the dialog subtype encountered) — requires recording the observed subtype, not just confirm/reject.

### Phase Ordering Rationale

- **Foundational helpers before the work that uses them** (Phase 1 before Phase 2) avoids redoing the 54-site audit twice.
- **Bug fixes drive decomposition, not the reverse** (Phase 5) — matches PROJECT.md explicit scoping decision and avoids a wholesale-refactor trap.
- **Diagnose before isolate, isolate before geometry tests** (Phase 7 before Phase 8) — sequencing matters because untested assumptions about which Clipper2 version is loaded would make geometry tests non-deterministic.
- **Profile last among code-fix phases** (Phase 9) — profiling code that about to be restructured (Phase 5) or untested (Phase 8) produces stale data.
- **Interactive/runtime-validated work closes the milestone** (Phase 10) — its value is highest when validating the cumulative set of fixes in one pass, and two of its deliverables (DialogWhitelist, smoke test) cannot be resolved by code review at all.
- **Reentrancy and rollback-UX phases are grouped by shared mechanism**, not merely by file proximity — splitting the `ExternalEventCommand` guard from the modeless dialog block, or splitting `ITransactionService` rollback signal from `RevitCommand` presentation of it, risks a half-wired feature.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 7 (Dependency Isolation):** Needs verification of the `ManifestSettings` deduplication caveat and its interaction with LECG existing `pack://` WPF workaround — STACK.md flags this as MEDIUM confidence, requiring live-session testing before considering the isolation "done."
- **Phase 8 (Test Coverage):** The wrap-and-mock pattern is verified in principle, but each of the four target commands (Purge, AlignEdges, FixPoints, CategoryChanger) needs its own narrow interface design — this is per-command judgment work, not a single reusable recipe.
- **Phase 10 (DialogWhitelist discovery):** Requires interactive Revit access and careful recording of `DialogBoxShowingEventArgs` concrete subtypes per entry; no further desk research can resolve this, but the discovery protocol itself may need a short methodology note before the interactive session.

Phases with standard patterns (skip research-phase):
- **Phase 1 (Test scaffolding + LogAndIgnore):** Standard .NET helper pattern, no unknowns.
- **Phase 2 (Catch audit):** Mechanical once classified; pattern is well-documented in ARCHITECTURE.md and PITFALLS.md.
- **Phase 3 (Reentrancy guards):** Standard `ExternalEvent.IsPending` idiom, well-documented against official Revit API docs and Building Coder.
- **Phase 4 (Ribbon + rollback UX):** Ribbon availability mechanism already 90% implemented; rollback signal is an additive, well-scoped interface change.
- **Phase 6 (Path sanitization):** Standard .NET security pattern (`Path.GetFileName`/`GetFullPath` containment), well-documented.
- **Phase 9 (Profiling):** `dotnet-trace`/PerfView usage is directly documented against current Microsoft Learn docs.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH for isolation/profiling/analyzers; MEDIUM for testing frameworks | Isolation and profiling verified against official Autodesk/Microsoft docs read directly; testing-framework version-support claims (ricaun.RevitTest, Nice3point.TUnit.Revit) are WebSearch-sourced and not directly relevant since neither is recommended for adoption |
| Features | MEDIUM-HIGH | Revit API mechanics (availability, ExternalEvent, dialogs) verified against official docs + Building Coder; project-specific gaps verified directly against live LECG source, correcting CONCERNS.md in places |
| Architecture | HIGH | All findings verified directly against current source (`src/`), not just codebase docs; corrected two CONCERNS.md items with direct evidence |
| Pitfalls | MEDIUM | Revit API behavior (availability re-evaluation, dialog result codes, ExternalEvent semantics) is HIGH confidence via official docs and long-running community sources; assembly-isolation-in-shared-AppDomain findings are MEDIUM (unsupported scenario, community-reported only) |

**Overall confidence:** MEDIUM-HIGH

### Gaps to Address

- **`ManifestSettings` isolation vs. `pack://` WPF workaround interaction:** genuinely unresolvable by further desk research — must be verified in Phase 7 interactive smoke test before considering isolation complete.
- **DialogWhitelist result codes and concrete event-args subtypes:** by definition require a live Revit session; treat every existing whitelist entry as unverified until Phase 10 discovery pass records the observed subtype.
- **CI-mode Revit-dependent test compile issue (root cause):** research did not diagnose why this is currently broken, only that it must be fixed before Phase 8 test-writing work — flag for direct investigation at the start of Phase 1.
- **SonarAnalyzer `S2486` "comment counts as compliant" nuance:** MEDIUM confidence, sourced via WebSearch summary rather than a live SonarQube check; this analyzer is optional (CA1031 + RCS1075 already satisfy the core requirement), so treat as low-priority validation, not a blocker.
- **`ITransactionService` rollback surfacing — interface change vs. command-layer-only fix:** ARCHITECTURE.md flags a live hypothesis that this may need *zero* `ITransactionService` changes (the exception already carries what needed; only `RevitCommand` catch block needs to branch). Verify this hypothesis when Phase 4 is planned, since it changes whether the fix lands at the Infrastructure layer or purely at the Command layer.

## Sources

### Primary (HIGH confidence)
- Autodesk Help — `RevitAddInManifestSettings.UseRevitContext` Property, `RevitAddInManifestSettings` Class (Revit 2026 API docs)
- Microsoft Learn — `dotnet-trace` diagnostic tool docs (fetched directly, dated 2026-06-10), CA1031 rule page
- Autodesk Revit API Developers Guide — External Events, Add-in Registration, Ribbon Panels and Controls
- LECG own repository (`src/App.cs`, `src/Core/ExternalEventCommand.cs`, `src/Core/Ribbon/*`, `src/Services/Infrastructure/TransactionService.cs`, `src/Services/Renaming/BatchRenameExecutionService.cs`, `LECG.csproj`, `LECG.Tests.csproj`, `.editorconfig`) — read directly during research
- `.planning/PROJECT.md`, `.planning/codebase/ARCHITECTURE.md`, `.planning/codebase/CONCERNS.md` — used as baseline, corrected against current source where discrepancies were found

### Secondary (MEDIUM confidence)
- Autodesk Community Forum threads — Context Isolation in Revit 2026; Revit 2027 ManifestSettings deduplication; ILMerge/ILRepack failure reports; DLL conflict discussions
- Jeremy Tammik "The Building Coder" and BIM Matters — External events/modeless dialogs, zero-doc-state ribbon, dialog override patterns, performance profiling
- archi-lab — Costura.Fody / "dll hell is real" post-mortems, dismissing Revit pop-ups
- Roslynator docs and GitHub issue #386 (RCS1075 behavior)
- JetBrains dotTrace documentation (workflow exists, not verified against a live Revit.exe attach)

### Tertiary (LOW confidence)
- sharpbim — AppDomains for DLL conflicts (pre-.NET-6 pattern, cited only to explain exclusion)
- ricaun.RevitTest / Nice3point.TUnit.Revit version-support claims via WebSearch summary (not adopted this milestone regardless)
- SonarSource S2486 rule text via WebSearch summary (optional addition only)

---
*Research completed: 2026-07-05*
*Ready for roadmap: yes*
