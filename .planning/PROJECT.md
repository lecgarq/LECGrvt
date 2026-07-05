# LECG Revit Add-in — Codebase Hardening

## What This Is

LECG is a production Revit 2026 add-in (C#/.NET 8, WPF MVVM, ~39 commands) providing alignment, CAD conversion, family conversion, materials/PBR, purge/compaction, renaming, and topography tools for architectural workflows. This milestone hardens the existing codebase by fixing every concern documented in `.planning/codebase/CONCERNS.md` — bugs, fragile state, silent error swallowing, security gaps, deployment risks, dependency conflicts, and missing UX feedback.

## Core Value

Every documented concern is resolved or explicitly closed with evidence — the add-in becomes reliable enough that operations either complete correctly or fail loudly with clear user feedback.

## Requirements

### Validated

<!-- Inferred from existing codebase (see .planning/codebase/ARCHITECTURE.md) -->

- ✓ 39 working commands across alignment, CAD conversion, family conversion, materials, purge/compaction, renaming, topography — existing
- ✓ DI composition root (Bootstrapper/ServiceLocator) with feature-organized domain services — existing
- ✓ Transaction discipline via ITransactionService (71+ call sites) — existing
- ✓ Structured Serilog logging to %APPDATA%\LECG\Logs — existing
- ✓ Modal (RevitCommand) and modeless (ExternalEventCommand) execution paths — existing
- ✓ Build/deploy pipeline to live add-in folder with SkipRevitDeploy guard — existing

### Active

<!-- One requirement per concern category from CONCERNS.md -->

**Tech debt**
- [ ] FormulaAutoGroupingCommand `s_projectRunActive` flag resets defensively on every exit path (try-finally/disposable), so failed runs never block retries
- [ ] DialogWhitelist entries verified against live Revit 2026 via interactive discovery pass; low-confidence entries confirmed, corrected, or removed
- [ ] BatchRenameExecutionService formula-reference and dimension-label handling implemented (plans 04-03/04-04) with tests for formula-dependent parameters
- [ ] Services touched by other fixes in this milestone are decomposed where the fix requires it (e.g. BatchRename family-handling extraction)

**Known bugs**
- [ ] FamilyEditorService category-change failure surfaces an actionable error explaining why the category cannot be changed (workaround path documented)

**Error handling**
- [ ] CadTempFileCleanupService and FamilyTempFileCleanupService log deletion failures before suppressing
- [ ] All 54 bare catch blocks audited: each either logs, re-throws with context, or carries an explicit suppress-with-reason (`LogAndIgnore` helper)

**Fragile areas**
- [ ] ExternalEventCommand static handler/event state guarded against reentrancy (busy guard + warning); limitation documented
- [ ] Modeless dialog re-invocation (CategoryChanger, ConvertCad) blocked while an operation is in progress
- [ ] Clipper2 / DI.Abstractions version conflict: startup diagnostic logs actually-loaded versions AND dependency isolated (embedded/aliased) so LECG always uses its own copy

**Security**
- [ ] User-influenced file paths sanitized (Path.GetFileName/GetFullPath normalization, parent-directory rejection) in CadFamilySaveService, FamilyEditorService, LinkedModelExportService, SettingsManager
- [ ] Logging reviewed so file paths in logs show names, not full directory structure, where sensitive

**Deployment**
- [ ] `.addin` manifest install/restore procedure scripted or documented, with App.OnStartup validation that the manifest is present and valid
- [ ] Deploy-on-build risk documented and guarded (SkipRevitDeploy conventions enforced in docs/scripts)

**Missing UX features**
- [ ] Ribbon buttons disable/enable dynamically via ProjectDocumentAvailability/FamilyDocumentAvailability
- [ ] Operations report explicit COMPLETED / ROLLED BACK status to the user on transaction rollback (TransactionService + RevitCommand exception feedback)

**Test coverage (high-risk focus)**
- [ ] Command-level tests for high-risk commands: Purge, AlignEdges, FixPoints, CategoryChanger (mock UIDocument/Document/services; availability, error handling, result reporting)
- [ ] Alignment geometry service tests (boundary-point and vertex-alignment first)
- [ ] Tests locking in each bug fix made in this milestone

**Performance (profile first)**
- [ ] Large operations (BatchRename, Purge, Alignment) profiled for FilteredElementCollector cost; only proven bottlenecks optimized, findings documented

**Documentation**
- [ ] Pack:// URI registration workaround in App.OnStartup documented with rationale + smoke check
- [ ] Open questions from CONCERNS.md (unit-converter helper, StorageType validation pattern, linked-model transforms, sanctioned `dotnet test` invocation) answered or converted to documented conventions

**Runtime validation (interactive, user-assisted)**
- [ ] Revit smoke-test checklist (docs/ai/revit-smoke-test.md) executed with user at the keyboard; results recorded

### Out of Scope

- Full decomposition of all 5 monolithic services — only services touched by other fixes get decomposed; wholesale refactoring deferred (risk outweighs benefit this milestone)
- Comprehensive test suites for topography and materials services — high-risk focus chosen; these are deferred to a future testing milestone
- Proactive collector caching / Big-O documentation without profiling evidence — profile-first policy; speculative optimization deferred
- Upgrading other add-ins' Clipper2 versions — outside LECG's control; isolation solves it from our side
- New product features — this is a hardening milestone only
- Streaming/pagination for million-element collections — no evidence of real-world need; revisit if profiling or user reports surface it

## Context

- Brownfield: full codebase map exists at `.planning/codebase/` (ARCHITECTURE, STACK, CONCERNS, CONVENTIONS, TESTING, STRUCTURE, INTEGRATIONS), analysis date 2026-07-04
- Source of scope: `.planning/codebase/CONCERNS.md` — the user's directive is "fix all concerns"
- Interactive Revit sessions available: the user will run DialogWhitelist discovery and the smoke-test checklist when a phase asks for it; code prep must be automated first
- Known build guardrail: always `dotnet build -p:SkipRevitDeploy=true` for validation; plain build overwrites the live add-in folder
- Test project (LECG.Tests) exists but CI-mode compilation of Revit-dependent tests is a known open issue
- Established patterns to preserve: `ArgumentNullException.ThrowIfNull` (633 uses), try-finally disposal, ITransactionService for all writes

## Constraints

- **Tech stack**: C#/.NET 8, Revit 2026 API (Nice3point reference assemblies), WPF — fixed; no framework changes
- **Compatibility**: Fixes must not change user-visible behavior of working commands except where a concern explicitly calls for it (error feedback, ribbon greying)
- **Environment**: Revit shares one AppDomain with other add-ins (Enscape, ModPlus, Forma) — dependency isolation must not break them
- **Validation**: Runtime-dependent fixes (dialogs, smoke tests) require interactive user sessions; phases must sequence code-first, validate-second
- **Build**: `dotnet build -p:SkipRevitDeploy=true` for all validation builds; live deploy only intentionally

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Include interactive Revit validation with user's help | DialogWhitelist and smoke tests can't be verified in code alone; user available at keyboard | — Pending |
| Decompose large services only where other fixes require it | Full decomposition of 5 services is high-risk/low-urgency; targeted extraction gets testability where it matters | — Pending |
| High-risk-focused test coverage (not comprehensive) | 39 untested commands is too broad for one milestone; Purge/AlignEdges/FixPoints/CategoryChanger + alignment geometry are highest blast radius | — Pending |
| Profile before optimizing collectors | No profiling evidence of a real bottleneck; speculative caching adds complexity | — Pending |
| Detect + isolate Clipper2/DI version conflicts | Silent geometry corruption is unacceptable; detection alone still leaves wrong results possible | — Pending |
| Include both UX features (ribbon availability + rollback feedback) | Both are documented concerns; silent rollback directly undermines trust in every other fix | — Pending |

---
*Last updated: 2026-07-05 after initialization*
