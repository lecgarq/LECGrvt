# Requirements — LECG Codebase Hardening

**Defined:** 2026-07-05
**Source:** `.planning/codebase/CONCERNS.md` (scope: fix all concerns) + research corrections (`.planning/research/SUMMARY.md`)

## v1 Requirements

### State & Reentrancy

- [ ] **STATE-01**: FormulaAutoGroupingCommand's `s_projectRunActive` flag resets on every exit path (try-finally/disposable pattern), so a failed run never blocks retries until Revit restart
- [ ] **STATE-02**: `ExternalEventCommand<THandler>` checks `ExternalEvent.IsPending` before `Raise()` and rejects re-invocation with a user-visible warning while an operation is in progress (covers CategoryChanger and ConvertCad modeless reentrancy — same fix pattern as STATE-01)
- [ ] **STATE-03**: `ExternalEventCommand` static handler/event lifecycle limitation is documented in the class, and handler state is initialized/validated at the start of each invocation

### Error Handling

- [ ] **ERR-01**: A `LogAndIgnore`-style helper exists (designed against `ILogger`'s scope-string requirement) making intentional exception suppression explicit and logged
- [ ] **ERR-02**: `CadTempFileCleanupService` and `FamilyTempFileCleanupService` log deletion failures at Warning level before suppressing
- [ ] **ERR-03**: All 54 bare catch blocks are audited; each one logs, re-throws with context, or carries an explicit suppress-with-reason — WITHOUT converting per-item batch-loop catches into all-or-nothing failures
- [ ] **ERR-04**: CA1031 and RCS1075 analyzers are activated via `.editorconfig` so new unlogged bare catches are flagged at build time

### Known Bugs

- [ ] **BUG-01**: FamilyEditorService category-change failure produces an actionable error message explaining why the category cannot be changed (Generic Model retry workaround documented in code)

### Rename Integrity

- [ ] **REN-01**: BatchRenameExecutionService validates and safely handles formula-referenced parameters before rename (plan 04-03 TODO closed)
- [ ] **REN-02**: BatchRenameExecutionService validates and safely handles dimension-label parameters before rename (plan 04-04 TODO closed)
- [ ] **REN-03**: Family-handling logic touched by REN-01/REN-02 is extracted into focused, testable sub-service(s) (targeted decomposition — the static pure methods make this near-mechanical)
- [ ] **REN-04**: Unit tests cover formula-dependent and dimension-labeled parameter rename scenarios

### User Feedback (UX)

- [ ] **UX-01**: Operations report explicit COMPLETED / ROLLED BACK status to the user when a transaction rolls back (surfaced through RevitCommand's existing catch-and-dialog path; ITransactionService interface change only if spike proves necessary)
- [ ] **UX-02**: The 8 Align pulldown sub-buttons get correct availability classes (closing the one real ribbon-availability gap; existing wiring verified, not rebuilt)

### Security

- [ ] **SEC-01**: User-influenced file paths are sanitized (filename normalization, parent-directory rejection, full-path containment checks) in CadFamilySaveService, FamilyEditorService, LinkedModelExportService, and SettingsManager
- [ ] **SEC-02**: Logging of file paths is reviewed; sensitive full paths reduced to file names where directory structure would be exposed

### Dependency Isolation

- [ ] **DEP-01**: App.OnStartup logs actually-loaded versions of Clipper2 and Microsoft.Extensions.DependencyInjection.Abstractions as a startup diagnostic
- [ ] **DEP-02**: LECG's dependencies are isolated via Revit 2026's native `ManifestSettings` (`UseRevitContext=False` + `ContextName`) so LECG always runs against its own Clipper2 2.0 / DI.Abstractions 8.0 copies
- [ ] **DEP-03**: The pack:// URI workaround in App.OnStartup is verified to still function after isolation is enabled (interactive smoke check) and documented with rationale

### Test Coverage (high-risk focus)

- [ ] **TEST-01**: Command-level tests exist for Purge, AlignEdges, FixPoints, and CategoryChanger (through LECG's own service abstractions — availability, error handling, result reporting)
- [ ] **TEST-02**: Alignment geometry services have unit tests, starting with boundary-point and vertex-alignment services (after DEP-02 so Clipper2 version is deterministic)
- [ ] **TEST-03**: Every bug fix in this milestone is locked in by at least one test
- [ ] **TEST-04**: A sanctioned `dotnet test` invocation is validated and documented (closes the CI-mode open question)

### Performance (profile first)

- [ ] **PERF-01**: BatchRename, Purge, and Alignment operations are profiled (dotnet-trace/Stopwatch instrumentation) for FilteredElementCollector cost; findings documented
- [ ] **PERF-02**: Only profiling-proven bottlenecks are optimized; each optimization is re-measured to confirm improvement

### Deployment

- [ ] **DEPLOY-01**: `.addin` manifest install/restore is scripted or step-by-step documented in docs/deployment, so a new machine or corrupted manifest is recoverable
- [ ] **DEPLOY-02**: App.OnStartup (or a setup script) validates the manifest is present and structurally valid, surfacing a clear warning if not
- [ ] **DEPLOY-03**: SkipRevitDeploy build guardrail conventions are enforced in docs/scripts so validation builds never overwrite the live add-in unintentionally

### Documentation & Conventions

- [ ] **DOC-01**: Open convention questions from CONCERNS.md are answered as documented conventions: unit-conversion helper (ForgeTypeId vs UnitUtils), parameter StorageType validation pattern, linked-model transform handling
- [ ] **DOC-02**: Codebase map (CONCERNS.md) corrections from research are recorded so stale items (ribbon availability status) don't mislead future work

### Runtime Validation (interactive, user-assisted)

- [ ] **VAL-01**: DialogWhitelist's five low-confidence entries are verified in a live Revit 2026 session (recording concrete DialogBoxShowingEventArgs subtype + result code per entry); entries confirmed, corrected, or removed
- [ ] **VAL-02**: The Revit smoke-test checklist (docs/ai/revit-smoke-test.md) is executed with the user at the keyboard, including the post-isolation pack:// check; results recorded

## v2 Requirements (deferred)

- **v2-REFAC-01**: Decompose remaining monolithic services (LinePatternCompactionService, MaterialBumpMapNormalizer, FillPatternCompactionService, PurgeParameterService) — only BatchRename is touched this milestone
- **v2-TEST-01**: Test suites for topography services (SplitBoundaries, FixPoints, DivideToposolid, Conversion)
- **v2-TEST-02**: Test suites for materials/PBR services (MaterialAppearanceAssetService, MaterialBitmapPropertyService)
- **v2-TEST-03**: Test stubs for the remaining ~35 commands
- **v2-PERF-01**: Collector caching convention / Big-O documentation beyond what profiling proves this milestone

## Out of Scope

| Item | Reason |
|------|--------|
| Full decomposition of all 5 monolithic services | Risk outweighs benefit; only fix-driven extraction this milestone |
| Streaming/pagination for million-element collections | No evidence of real-world need; revisit on profiling or user reports |
| Upgrading other add-ins' Clipper2 versions | Outside LECG's control; DEP-02 isolation solves it from LECG's side |
| ILRepack/Costura/AssemblyResolve isolation approaches | Research-confirmed dead ends on .NET 8 / Revit 2026 |
| Generic Result<T> framework, universal command mutex, heuristic dialog matching | Over-engineering anti-features flagged by research |
| New product features | Hardening milestone only |

## Traceability

<!-- Filled by roadmap creation -->

| Requirement | Phase |
|-------------|-------|
| (pending roadmap) | |
