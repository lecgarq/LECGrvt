# Roadmap: LECG Codebase Hardening

## Overview

This milestone hardens an existing, working Revit 2026 add-in by resolving every documented concern in `.planning/codebase/CONCERNS.md` — no new product features. The 11 phases below follow the dependency graph derived from research (`.planning/research/ARCHITECTURE.md`): shared helpers and test scaffolding land first so downstream audits aren't redone; state/reentrancy, UX, bug-fix/decomposition, and security fixes proceed as architecturally-isolated groups; dependency isolation lands before geometry tests so those tests run against a deterministic Clipper2 version; performance profiling targets the post-decomposition, post-test code shape; and the milestone closes with deployment/documentation hardening followed by interactive, user-assisted runtime validation that exercises the cumulative set of fixes in one live Revit session.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Test Scaffolding + Shared Error-Handling Helper** - Unblock `dotnet test` for Revit-dependent tests and build the `LogAndIgnore` helper the catch audit depends on
- [ ] **Phase 2: Bare-Catch Audit & Enforcement** - Classify and fix all 54 bare catch blocks; activate analyzers so regressions are caught at build time
- [ ] **Phase 3: Reentrancy Guards (State & Modeless Commands)** - Fix `s_projectRunActive` and add an `ExternalEventCommand` busy-guard covering all exit paths
- [ ] **Phase 4: Ribbon Availability + Transaction Rollback UX** - Close the Align-pulldown availability gap; surface COMPLETED/ROLLED BACK to the user
- [ ] **Phase 5: Known Bug Fixes + BatchRename Decomposition** - Fix CategoryChanger's error message and BatchRename's formula/dimension TODOs, extracting a testable sub-service in the process
- [ ] **Phase 6: Security — Path Sanitization & Log Hygiene** - Sanitize user-influenced file paths and reduce sensitive path logging
- [ ] **Phase 7: Dependency Isolation (Clipper2 / DI.Abstractions)** - Log actually-loaded versions and isolate LECG's dependencies via native Revit manifest settings
- [ ] **Phase 8: Test Coverage (High-Risk Focus)** - Command-level tests for Purge/AlignEdges/FixPoints/CategoryChanger; alignment geometry tests; lock in this milestone's bug fixes
- [ ] **Phase 9: Performance Profiling** - Profile BatchRename/Purge/Alignment; optimize only proven bottlenecks
- [ ] **Phase 10: Deployment & Documentation Hardening** - Manifest recovery procedure, deploy guardrails, and open conventions documented
- [ ] **Phase 11: Interactive Runtime Validation** - DialogWhitelist discovery, post-isolation pack:// check, and the full smoke-test checklist with the user at the keyboard

## Phase Details

### Phase 1: Test Scaffolding + Shared Error-Handling Helper
**Goal**: A sanctioned test workflow exists and a reusable suppression-logging helper is ready before the catch-block audit begins.
**Depends on**: Nothing (first phase)
**Requirements**: ERR-01, TEST-04
**Success Criteria** (what must be TRUE):
  1. Running the documented `dotnet test` invocation compiles and executes Revit-dependent tests in LECG.Tests without CI-mode errors.
  2. A `LogAndIgnore`-style helper exists, designed against `ILogger`'s scope-string requirement, and can be called from any service to suppress an exception while still logging it with a reason.
  3. The CI-mode compilation issue that previously blocked Revit-dependent test files is resolved or has a documented, sanctioned workaround.
**Plans**: TBD

Plans:
- [ ] 01-01: TBD

### Phase 2: Bare-Catch Audit & Enforcement
**Goal**: Every intentional exception suppression in the codebase is explicit, logged, and enforced going forward — without regressing batch operations to all-or-nothing.
**Depends on**: Phase 1 (LogAndIgnore helper)
**Requirements**: ERR-02, ERR-03, ERR-04
**Success Criteria** (what must be TRUE):
  1. All 54 previously bare catch blocks now log, re-throw with context, or call `LogAndIgnore` with an explicit reason.
  2. `CadTempFileCleanupService` and `FamilyTempFileCleanupService` write a Warning-level log entry before suppressing a deletion failure.
  3. Batch operations (BatchRename, Purge) still complete partially when one item fails — per-item "skip and continue" catches were not converted into operation-aborting rethrows.
  4. Introducing a new unlogged bare catch now triggers a CA1031/RCS1075 analyzer warning at build time.
**Plans**: TBD

Plans:
- [ ] 02-01: TBD

### Phase 3: Reentrancy Guards (State & Modeless Commands)
**Goal**: Users cannot corrupt state or trigger overlapping operations by re-invoking a modeless command or restarting a run that failed partway.
**Depends on**: Nothing (architecturally independent of Phases 1-2)
**Requirements**: STATE-01, STATE-02, STATE-03
**Success Criteria** (what must be TRUE):
  1. Clicking CategoryChanger or ConvertCad a second time while the first invocation is still running shows a user-visible warning instead of starting a second overlapping operation.
  2. After `FormulaAutoGroupingCommand` fails partway through a run, the user can immediately retry without restarting Revit.
  3. Closing the CategoryChanger/ConvertCad window through any exit path (X button, Alt+F4, failed operation) still clears the busy state so the next invocation works normally.
  4. `ExternalEventCommand`'s static handler/event lifecycle limitation is documented in the class itself.
**Plans**: TBD

Plans:
- [ ] 03-01: TBD

### Phase 4: Ribbon Availability + Transaction Rollback UX
**Goal**: Users always see accurate command availability and know whether an operation actually completed or was rolled back.
**Depends on**: Nothing (architecturally independent of Phases 1-3)
**Requirements**: UX-01, UX-02
**Success Criteria** (what must be TRUE):
  1. Each of the 8 Align pulldown sub-buttons (AlignLeft/Center/Right/Top/Middle/Bottom, DistributeH/V) greys out when no project document is active.
  2. When a transaction rolls back (Revit-forced or otherwise), the user sees an explicit "ROLLED BACK" message rather than silence or a generic error.
  3. When an operation completes normally, the user sees explicit confirmation distinguishing it from a rollback.
**Plans**: TBD

Plans:
- [ ] 04-01: TBD

### Phase 5: Known Bug Fixes + BatchRename Decomposition
**Goal**: Category-change failures are explainable, and batch rename correctly handles formula-referenced and dimension-labeled parameters.
**Depends on**: Nothing (architecturally independent; decomposition is a consequence of these fixes, not a prerequisite)
**Requirements**: BUG-01, REN-01, REN-02, REN-03, REN-04
**Success Criteria** (what must be TRUE):
  1. Attempting a category change that fails shows an actionable error explaining why, including the documented Generic Model retry workaround.
  2. Renaming a family type with formula-referenced parameters no longer corrupts or silently skips those parameters.
  3. Renaming a family type with dimension-label parameters handles them correctly instead of failing silently.
  4. The formula/dimension-handling logic is callable and testable independent of the full `BatchRenameExecutionService` (extracted to a sibling service).
  5. Automated tests cover both formula-dependent and dimension-labeled rename scenarios and pass.
**Plans**: TBD

Plans:
- [ ] 05-01: TBD

### Phase 6: Security — Path Sanitization & Log Hygiene
**Goal**: User-influenced file paths cannot escape their intended directory, and logs don't unnecessarily expose full directory structure.
**Depends on**: Nothing (architecturally isolated, no shared files with other phases)
**Requirements**: SEC-01, SEC-02
**Success Criteria** (what must be TRUE):
  1. Supplying a path with parent-directory traversal to `CadFamilySaveService`, `FamilyEditorService`, `LinkedModelExportService`, or `SettingsManager` is rejected or normalized rather than written outside the intended folder.
  2. Legitimate UNC paths and deep folder structures used in production still work after sanitization (no regression on real path patterns).
  3. Log entries show file names instead of full paths where directory structure would otherwise be exposed.
**Plans**: TBD

Plans:
- [ ] 06-01: TBD

### Phase 7: Dependency Isolation (Clipper2 / DI.Abstractions)
**Goal**: LECG always runs against its own Clipper2 and DI.Abstractions versions regardless of what other add-ins in the shared AppDomain load.
**Depends on**: Nothing (architecturally independent), but must complete before Phase 8's geometry tests
**Requirements**: DEP-01, DEP-02
**Success Criteria** (what must be TRUE):
  1. Revit startup log shows the actually-loaded Clipper2 and Microsoft.Extensions.DependencyInjection.Abstractions versions as a diagnostic line.
  2. `ManifestSettings` isolation (`UseRevitContext=False` + `ContextName`) is configured so LECG loads its own Clipper2 2.0 / DI.Abstractions 8.0 copies independent of other add-ins.
  3. Alignment/geometry services (AlignEdges, FixPoints, SplitBoundaries) run against a deterministic Clipper2 version, ready for Phase 8 tests to depend on. (Live multi-add-in confirmation happens in Phase 11.)
**Plans**: TBD

Plans:
- [ ] 07-01: TBD

### Phase 8: Test Coverage (High-Risk Focus)
**Goal**: The highest-blast-radius commands and alignment geometry logic have automated regression coverage, and this milestone's bug fixes are locked in.
**Depends on**: Phase 1 (test scaffolding), Phase 7 (deterministic Clipper2 version for geometry tests)
**Requirements**: TEST-01, TEST-02, TEST-03
**Success Criteria** (what must be TRUE):
  1. Purge, AlignEdges, FixPoints, and CategoryChanger each have command-level tests (via mocked service abstractions) covering availability, error handling, and result reporting.
  2. Boundary-point and vertex-alignment geometry services have unit tests that pass deterministically.
  3. Every bug fixed elsewhere in this milestone (Phases 2-6) has at least one test that would fail if the fix were reverted.
  4. `dotnet test` runs the full expanded suite without CI-mode compile errors.
**Plans**: TBD

Plans:
- [ ] 08-01: TBD

### Phase 9: Performance Profiling
**Goal**: Performance work is evidence-driven — only proven bottlenecks get optimized, targeting the final (post-decomposition, post-test) code shape.
**Depends on**: Phase 5 (decomposition), Phase 8 (tests exist to catch regressions from any optimization)
**Requirements**: PERF-01, PERF-02
**Success Criteria** (what must be TRUE):
  1. BatchRename, Purge, and Alignment operations have been profiled (dotnet-trace/Stopwatch) against realistic data, with `FilteredElementCollector` cost documented in findings.
  2. Any optimization made is backed by a before/after measurement showing improvement; no speculative changes shipped without profiling evidence.
**Plans**: TBD

Plans:
- [ ] 09-01: TBD

### Phase 10: Deployment & Documentation Hardening
**Goal**: The add-in's manifest and deployment story is recoverable and documented, and open conventions are settled.
**Depends on**: Nothing (architecturally independent of all code-fix phases)
**Requirements**: DEPLOY-01, DEPLOY-02, DEPLOY-03, DOC-01, DOC-02
**Success Criteria** (what must be TRUE):
  1. A new machine or a corrupted `.addin` manifest can be restored by following a documented script/procedure in docs/deployment.
  2. App startup (or a setup script) surfaces a clear warning if the manifest is missing or structurally invalid.
  3. SkipRevitDeploy conventions are documented/enforced so validation builds never overwrite the live add-in unintentionally.
  4. Unit-conversion (ForgeTypeId vs UnitUtils), parameter StorageType validation, and linked-model-transform conventions are documented.
  5. CONCERNS.md reflects the research corrections (e.g. ribbon availability status) so future work isn't misled by stale findings.
**Plans**: TBD

Plans:
- [ ] 10-01: TBD

### Phase 11: Interactive Runtime Validation
**Goal**: The cumulative set of fixes from Phases 1-10 is validated in a live, multi-add-in Revit session with the user at the keyboard — this closes the milestone.
**Depends on**: Phases 1-10 (validates the cumulative set of fixes; DEP-03 specifically requires Phase 7's isolation to be in place)
**Requirements**: DEP-03, VAL-01, VAL-02
**Success Criteria** (what must be TRUE):
  1. The pack:// URI workaround still functions correctly after dependency isolation is enabled, confirmed in a live Revit 2026 session.
  2. Each of DialogWhitelist's five low-confidence entries has a recorded concrete `DialogBoxShowingEventArgs` subtype and result code, and is confirmed, corrected, or removed accordingly.
  3. The full Revit smoke-test checklist (docs/ai/revit-smoke-test.md) has been executed with the user at the keyboard, with the full add-in complement (Enscape, ModPlus, Forma) installed, and results recorded.
**Plans**: TBD

Plans:
- [ ] 11-01: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10 → 11

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Test Scaffolding + Shared Error-Handling Helper | 0/TBD | Not started | - |
| 2. Bare-Catch Audit & Enforcement | 0/TBD | Not started | - |
| 3. Reentrancy Guards (State & Modeless Commands) | 0/TBD | Not started | - |
| 4. Ribbon Availability + Transaction Rollback UX | 0/TBD | Not started | - |
| 5. Known Bug Fixes + BatchRename Decomposition | 0/TBD | Not started | - |
| 6. Security — Path Sanitization & Log Hygiene | 0/TBD | Not started | - |
| 7. Dependency Isolation (Clipper2 / DI.Abstractions) | 0/TBD | Not started | - |
| 8. Test Coverage (High-Risk Focus) | 0/TBD | Not started | - |
| 9. Performance Profiling | 0/TBD | Not started | - |
| 10. Deployment & Documentation Hardening | 0/TBD | Not started | - |
| 11. Interactive Runtime Validation | 0/TBD | Not started | - |
