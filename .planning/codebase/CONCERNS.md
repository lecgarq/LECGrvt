# Codebase Concerns

**Analysis Date:** 2026-07-04 · **Debt review:** 2026-07-27 (v1.0 hardening close audit — `.planning/archive/v1.0-hardening/AUDIT.md`)

## Accepted debt (v1.0 hardening closed as-is, 2026-07-27)

The v1.0 hardening milestone was closed with 16 of 32 requirements unmet and 9 partial. Its planning process (GSD) was retired mid-milestone; rename safety, the CI/test fix, deploy guardrails, and the CategoryChanger error message shipped, the rest is accepted debt. Accepted because the milestone tracking was stale, the suite is green (211 tests), and remaining items are better re-scoped into future milestones than executed from a dead roadmap. Outstanding, in the audit's wording:

- **Reentrancy:** no `ExternalEvent.IsPending` guard in `ExternalEventCommand` (`src/Core/ExternalEventCommand.cs:19-27`); static handler lifecycle undocumented.
- **Error handling:** no `LogAndIgnore` helper; 31 silent catch blocks remain across 20 files (down from 54); both temp-file cleanup services still swallow deletion failures; no CA1031/RCS1075 analyzers.
- **UX:** no uniform COMPLETED/ROLLED BACK status reporting (rollback does throw and surface an error via `RevitCommand`); the 8 Align/Distribute pulldown sub-buttons pass an empty availability string (`src/Core/Ribbon/RibbonService.cs:246-247`).
- **Security:** path sanitization only in `LinkedModelExportService`; no parent-dir rejection or containment checks in `CadFamilySaveService`, `FamilyEditorService`, `SettingsManager`; `LinkedModelExportService.cs:83` still logs a full path.
- **Dependency isolation:** no startup version logging for Clipper2/DI.Abstractions; no `ManifestSettings` isolation in the manifest; pack:// post-isolation check moot until isolation exists.
- **Tests:** no command-level tests for Purge/AlignEdges/FixPoints/CategoryChanger; no alignment-geometry tests.
- **Perf:** no profiling of BatchRename/Purge/Alignment ever done.
- **Deployment/docs:** no manifest presence validation at startup; unit-conversion / StorageType / link-transform conventions still open.
- **Runtime validation:** DialogWhitelist's five entries still unverified in live Revit; smoke-test checklist never executed end-to-end (2026-07-25 run covered theme-scoping subset only).

## Tech Debt

**FormulaAutoGroupingCommand static state management:** — RESOLVED (verified 2026-07-27: flag reset in job `Dispose()`, guaranteed by `RevitIdlingRunner.cs:59-65` `finally` on both complete and exception paths)
- Issue: `s_projectRunActive` (static bool) tracks whether a project-wide formula auto-grouping pass is running. If an exception occurs during execution but BEFORE Dispose() is called, the flag remains true and blocks subsequent runs.
- Files: `src/Commands/FormulaAutoGroupingCommand.cs:27,135,157,252`
- Impact: User cannot retry a failed project-wide formula grouping operation without restarting Revit. RevitIdlingRunner's exception handling SHOULD guarantee Dispose is called (line 63 in finally block), but the pattern is fragile — state flag should be reset in a more defensive way.
- Fix approach: Consider using a try-finally within ExecuteInProjectDocument, or promote the flag to a disposable resource that resets on Dispose regardless of exception path.

**DialogWhitelist low confidence:**
- Issue: The hardcoded dialog ID whitelist (`src/Core/DialogWhitelist.cs:44-67`) contains five entries marked "confidence: low — unverified". Comments explicitly state "STATUS: 06-DIALOG-DISCOVERY.md status=blocked — all entries below are LOW-confidence research guesses, NOT runtime-confirmed."
- Files: `src/Core/DialogWhitelist.cs:40-66` (5 TaskDialog entries)
- Impact: If a dialog ID is incorrect or the result code is wrong, auto-dismiss will fail or dismiss with the wrong button, causing unexpected behavior in purge/conversion operations that rely on these dialogs. Users may experience stuck workflows if a modal dialog is not auto-dismissed as expected.
- Fix approach: Execute a formal Revit runtime discovery pass (referenced in comments as `06-DIALOG-DISCOVERY.md`) to verify each DialogId and result code against actual Revit 2026 behavior. Update the whitelist only after confirming with an interactive Revit session.

**BatchRenameExecutionService incomplete formula handling:** — RESOLVED (verified 2026-07-27: formula rewrite at `BatchRenameExecutionService.cs:321-344`, dimension-label reassignment at `:346-373`, covered by `LECG.Tests/Services/BatchRenameSafeRenameTests.cs`)
- Issue: Two TODO comments at line 613 mark incomplete work: "TODO 04-03: hand formulaReferenced set to safe-rename loop; TODO 04-04: hand dimensionLabels set to dimension reassignment loop". Current code skips formula-referenced and dimension-label checks intentionally (per comment on line 628), delegating them to unimplemented safe-rename and dimension-reassignment paths.
- Files: `src/Services/Renaming/BatchRenameExecutionService.cs:613,628-629`
- Impact: Formula references and dimension labels attached to family parameters are NOT validated before rename. A rename operation could break formulas or dimension constraints if the parameter name change propagates incorrectly. The impact depends on whether Revit's API already prevents these (untested).
- Fix approach: Implement the safe-rename loop (plan 04-03) and dimension-reassignment loop (plan 04-04) to validate and handle these cases before commit. Add unit tests for formula-dependent parameters.

**Large monolithic services (potential complexity and fragility):**
- Issue: Several service classes exceed 700+ lines, concentrating complex business logic in single classes:
  - `BatchRenameExecutionService`: 1019 lines
  - `LinePatternCompactionService`: 828 lines
  - `MaterialBumpMapNormalizer`: 733 lines
  - `FillPatternCompactionService`: 614 lines
  - `PurgeParameterService`: 568 lines
- Files: `src/Services/Renaming/BatchRenameExecutionService.cs`, `src/Services/PurgeAndCompaction/LinePatternCompactionService.cs`, `src/Services/Materials/MaterialBumpMapNormalizer.cs`, `src/Services/PurgeAndCompaction/FillPatternCompactionService.cs`, `src/Services/PurgeAndCompaction/PurgeParameterService.cs`
- Impact: Large services are harder to test, more prone to hidden interdependencies, and riskier to modify. A single bug in a 1000-line service could affect multiple code paths. Refactoring is difficult because the scope is hard to reason about.
- Fix approach: Decompose these services into smaller, focused classes by extracting logical sub-operations as separate, testable services. Example: BatchRenameExecutionService could split family-handling logic into a dedicated service.

---

## Known Bugs

**FamilyEditorService workaround for category change:**
- Symptoms: Setting FamilyCategory directly fails in some family types; Revit blocks the operation.
- Files: `src/Services/FamilyConversion/FamilyEditorService.cs:60-84`
- Trigger: Attempting to convert a model family to a different category (not in the original family template category list).
- Workaround: Reset to Generic Model category, then attempt the target category change again. Works for model families; non-model families still fail.
- Implication: This is not a bug fix but a defensive retry pattern that hides an underlying Revit API limitation. If the workaround fails, the error message is generic ("Revit refused category change") and doesn't help users understand why the category cannot be changed.

---

## Error Handling & Resilience Issues

**Silent exception swallowing in file cleanup:**
- Issue: `CadTempFileCleanupService` (lines 8-19) has a bare catch block with no logging when File.Delete fails. Errors are silently discarded.
- Files: `src/Services/CadConversion/CadTempFileCleanupService.cs:16-18`
- Impact: Temporary CAD conversion files may accumulate on disk if deletion fails (permissions, file locks, disk space). Users have no visibility into cleanup failures and cannot diagnose the issue.
- Fix approach: Log the failure (at least at Warning level) before swallowing the exception. Example: `_logger.Log($"Failed to delete temp file {path}: {ex.Message}", LogLevel.Warning)`.

**Widespread bare catch blocks without logging:**
- Issue: 54 bare `catch` blocks across the codebase (grep found). Many are intentional (e.g., RevitIdlingRunner gracefully handles job failures), but some silently hide important errors:
  - `ElementLabelService.cs:59,77,97` — label extraction failures
  - `SettingsManager.cs:50,74` — settings I/O failures
  - `SexyGraphicsApplyService.cs:33`, `SexyCategoryVisibilityService.cs:52`, `SexySectionBoxVisibilityService.cs:34` — graphics operation failures
- Files: Multiple across `src/Services/`, `src/Core/`
- Impact: Failures in UI graphics, settings persistence, and element labeling may occur silently, leaving the add-in in an inconsistent state without diagnostic information.
- Fix approach: Audit each bare catch block. Add logging (or re-throw with context) unless the catch is guarding a truly non-critical operation. Consider creating a helper `LogAndIgnore()` method for intentional suppressions.

---

## Test Coverage Gaps

**Command-level tests missing (39 commands, 1 test):**
- Issue: 39 command classes in `src/Commands/` (IExternalCommand implementations), but only 1 has any test coverage (`FormulaAutoGroupingCommandTests`).
- Files: `src/Commands/*.cs` — 39 classes; `LECG.Tests/Commands/FormulaAutoGroupingCommandTests.cs` — 1 test file
- Impact: Commands are the primary entry points to the add-in. Any bugs in command logic (e.g., document availability checks, modeless event handling, UI coordination) are only caught at runtime in Revit. Examples of untested commands: AlignEdgesCommand, AlignElementsCommand, FixPointsCommand, PurgeCommand, CategoryChangerCommand, ConvertCadCommand.
- Fix approach: Create command test stubs for at least the high-risk/high-usage commands. Mock UIDocument, Document, and Services, then verify command availability, error handling, and result reporting.

**Alignment service tests missing:**
- Issue: `AlignEdgesService`, `AlignElementsService`, and all their sub-services (`AlignEdgesBoundaryPointService`, `AlignEdgesVertexAlignmentService`, etc.) have NO tests.
- Files: `src/Services/Alignment/` — 8+ service classes; zero test coverage
- Impact: Alignment is geometry-heavy with complex math (Clipper2 intersection, vertex classification, raycast). Bugs in vertex alignment logic or overlap region calculation can produce incorrect geometry silently. No test suite means geometry correctness is only validated by manual Revit testing.
- Fix approach: Create test suite for alignment services with mock geometry inputs and expected vertex/point outputs. Start with boundary-point service and vertex-alignment service.

**Topography service tests missing:**
- Issue: Toposolid/contour manipulation services (`SplitBoundariesService`, `FixPointsService`, `DivideToposolidService`, `ConversionService`, `SplitBoundariesService`) have NO tests.
- Files: `src/Services/Topography/` — 5+ service classes; zero test coverage
- Impact: Toposolid operations modify surface geometry in ways that can cause corruption if boundary/contour logic is wrong. Bugs may manifest only with specific surface shapes or point distributions, making them hard to reproduce.
- Fix approach: Create geometry-focused test suite with mock toposolid data and verify boundary/contour operations preserve surface integrity.

**Material/PBR system under-tested:**
- Issue: `MaterialBumpMapNormalizer` (733 lines) has a test file (`MaterialBumpMapNormalizerTests.cs`), but texture path and asset property handling is complex. Material appearance asset setup (`MaterialAppearanceAssetService`, `MaterialBitmapPropertyService`) has no dedicated tests.
- Files: `src/Services/Materials/MaterialAppearanceAssetService.cs`, `src/Services/Materials/MaterialBitmapPropertyService.cs`
- Impact: PBR texture setup (diffuse, normal, roughness) is error-prone. Incorrect asset properties or missing texture bindings result in materials rendering incorrectly in Revit without clear error feedback to the user.
- Fix approach: Add tests for MaterialAppearanceAssetService verifying that asset properties are created/updated correctly, and that SetupBumpBitmapProperty correctly sets bump-map-specific flags.

---

## Performance Bottlenecks

**No FilteredElementCollector caching observed:**
- Issue: 104 uses of `FilteredElementCollector` across `src/` with no visible caching strategy. Each collector is created fresh in-place, potentially re-scanning the document model.
- Files: `src/Services/` (distributed)
- Impact: In large projects with thousands of elements, repeated collectors for the same criteria (e.g., all walls, all rooms, all parameters with a specific name) could cause cumulative slowdown. Batch operations (rename, purge, category change) may perform O(n²) or worse scanning.
- Fix approach: Profile collector usage in the largest services (BatchRename, Purge, Alignment). Consider a lightweight collector cache scoped to a transaction or command execution, or use ElementFilter optimization where applicable.

**No documented search/sort algorithm complexity:**
- Issue: Complex searches and sorts in `BatchRenameExecutionService`, `SearchReplaceService`, and compaction services have no documented complexity or performance characteristics.
- Files: Multiple service classes
- Impact: Unknown scaling behavior means adding features or handling larger documents is risky. A quadratic search could be hiding inside a seemingly linear operation.
- Fix approach: Add Big-O complexity comments to search/sort loops, especially in services with nested iteration over elements and parameters.

---

## Fragile Areas

**ExternalEventCommand static handler/event state:**
- Issue: `ExternalEventCommand<THandler>` (src/Core/ExternalEventCommand.cs:9-16) holds handler and ExternalEvent in static fields per closed generic type. State is shared across all invocations.
- Files: `src/Core/ExternalEventCommand.cs:9-16`; used by `src/Commands/CategoryChangerCommand.cs`, `src/Commands/ConvertCadCommand.cs`
- Impact: If a handler becomes corrupted or holds stale state from a previous run, all subsequent invocations of that command type will inherit the bad state. Multiple rapid clicks on the same command button could queue multiple events before the first completes, leading to race conditions.
- Fix approach: Document this limitation clearly in the class and handler implementation. Consider resetting or validating handler state at the start of each invocation. Add a guard to prevent multiple simultaneous raises.

**Assembly version conflicts with other add-ins:**
- Issue: Revit's shared AppDomain loads multiple add-ins. LECG's `Clipper2Lib 2.0.0.0` conflicts with a preloaded `1.1.1.0` (from other add-ins like Enscape/ModPlus/Forma). Similarly, `Microsoft.Extensions.DependencyInjection.Abstractions 8.0.0.0` conflicts with preloaded `9.0.0.0`.
- Files: Known from Revit journal (Revit runtime); LECG.csproj references `Clipper2` Version="2.0.0" (line 29)
- Impact: Depending on add-in load order, Clipper2-dependent code (AlignEdges, FixPoints, SplitBoundaries) may silently run against Clipper2Lib 1.1.1.0 instead of 2.0.0.0. Geometry algorithms may produce wrong results due to API differences between versions.
- Fix approach: (1) Verify which Clipper2 version is actually loaded at runtime via a diagnostic check in App.OnStartup. (2) Consider embedding Clipper2 as a private strong-named assembly or using AppDomain/AssemblyResolve more aggressively to force version resolution. (3) Document version constraints in deployment README.

**Modeless dialog + static event handler reentrancy:**
- Issue: Commands that use ExternalEventCommand (CategoryChanger, ConvertCad) create modeless dialogs that raise ExternalEvents. If a dialog is shown modeless and the same command is invoked again, the static handler and event are shared.
- Files: `src/Commands/CategoryChangerCommand.cs:55-62`, `src/Commands/ConvertCadCommand.cs:64-71`, `src/Core/ExternalEventCommand.cs`
- Impact: User clicks CategoryChanger button → modeless dialog opens → user clicks CategoryChanger again before closing the dialog → both invocations share the same ExternalEventHandler → second event overwrites state set by the first event → undefined behavior.
- Fix approach: Add a guard in the dialog or command to prevent re-invocation while an operation is in progress. Log and show a warning if reentrancy is detected.

---

## Security Considerations

**File path construction without validation:**
- Issue: Multiple services construct file paths using user input or parameters without explicit validation:
  - `CadFamilySaveService.cs:13` — constructs temp path using provided name
  - `FamilyEditorService.cs:177` — constructs temp path
  - `LinkedModelExportService.cs:74` — constructs output path
  - `SettingsManager.cs:15,47,63` — constructs AppData paths
- Files: Various in `src/Services/`
- Impact: If user input is not sanitized, path traversal attacks (e.g., "..\\..\\..\\Windows\\System32") could write or read files outside the intended directory. Current code uses Path.Combine which resists simple traversal, but complex injection is possible.
- Fix approach: (1) Sanitize user-provided file names (remove path separators, validate against a whitelist of allowed characters). (2) Use Path.GetFileName and Path.GetFullPath to normalize paths and verify they stay within the intended directory. (3) Add explicit checks for parent-directory references ("..").

**Environment variable usage (no sensitive values found):**
- Issue: The codebase does not appear to read environment variables for secrets or credentials. This is good. However, logging sometimes captures user input (e.g., file paths, parameter names) that could expose directory structure.
- Files: Various logging points in services
- Impact: Logs may expose internal directory structure or sensitive document metadata if shipped to end users or reviewed in screenshots.
- Fix approach: Review logging statements for sensitive output. Sanitize file paths in logs (show only file names, not full paths).

---

## Deployment & Build Risks

**Plain dotnet build deploys to live add-in folder:**
- Issue: `LECG.csproj:61-72` defines a `DeployToRevit` target that copies DLL/PDB/deps.json to `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG` after every build UNLESS `-p:SkipRevitDeploy=true` is passed.
- Files: `LECG.csproj:61-72`
- Impact: Running `dotnet build` without the flag will overwrite the live add-in. If Revit has the DLL locked, the build fails noisily. If Revit has exited or the DLL is not locked, the folder is silently updated, replacing a working version with a potentially broken debug build.
- Fix approach: (Already documented in repo-context.md) Always build with `dotnet build -p:SkipRevitDeploy=true` for validation. Only use plain `dotnet build` when deployment is explicitly intended and Revit is closed.

**`.addin` manifest not generated or deployed:**
- Issue: The build does NOT generate or deploy the `.addin` manifest file. It is manually installed at `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG.addin` and not tracked in the repo (only a template at `docs/deployment/LECG.addin.template`).
- Files: No `.addin` file in repo; template at `docs/deployment/LECG.addin.template`; live manifest at machine-specific `C:\ProgramData\...`
- Impact: If the live manifest is deleted, corrupted, or accidentally modified, Revit will not load LECG until it is reinstalled. The repo has no automated way to restore it. Developers who install LECG on a new machine must manually copy the manifest file.
- Fix approach: (1) Add a manifest generation/installation step to a setup script or docs (not the build target, to preserve manual control). (2) Add a validation check in App.OnStartup to verify the manifest exists and is valid. (3) Document the install procedure clearly in `docs/deployment/README.md`.

---

## Missing Critical Features

**Align pulldown sub-buttons missing availability classes:** (corrected 2026-07-27 — the original claim that availability classes have "no references in ribbon code" was stale: `RibbonService.cs:47,61,164,202,300-301` wires them for most commands)
- Issue: Only the 8 Align/Distribute pulldown sub-buttons pass an empty availability string (`src/Core/Ribbon/RibbonService.cs:246-247`, comment "NO AVAILABILITY RESTRICTION"), even though `RibbonFactory.AddItemToPulldown` supports availability wiring (`RibbonFactory.cs:91-94`).
- Impact: Those 8 buttons stay clickable with no project document active; the command then fails at execute time instead of greying out.
- Fix approach: Pass `ProjectDocumentAvailability` for the 8 sub-buttons in `RibbonService.cs:250-294`.

**No undo/rollback reporting to user:**
- Issue: When a command modifies the document and encounters an error mid-transaction, the transaction is rolled back silently. User sees no clear indication of what happened or whether changes were applied.
- Files: `src/Services/Infrastructure/TransactionService.cs:53,128` (catch blocks with no user feedback)
- Impact: User performs a rename or purge operation, sees activity but no clear result, and doesn't know if changes took effect or were rolled back.
- Fix approach: Ensure exception messages are surfaced to the user (via log window or dialog) with explicit "ROLLED BACK" or "COMPLETED" status. Improve RevitCommand exception handler to show clearer feedback.

---

## Scaling Limits

**No pagination/streaming for large element collections:**
- Issue: Services that enumerate all elements of a type use FilteredElementCollector().ToList() to materialize the entire collection into memory. No streaming or pagination observed.
- Files: Multiple services; example `src/Services/Alignment/AlignEdgesBoundaryCollectionService.cs:31`
- Limit: In a project with millions of elements (rare but possible in infrastructure/GIS imports), a single ToList() could exhaust memory or cause severe slowdown.
- Scaling path: For very large operations, implement a streaming/batching pattern that processes elements in chunks, yielding results or committing sub-transactions.

---

## Dependencies at Risk

**Clipper2 version conflict:**
- Risk: LECG references Clipper2 2.0.0, but other add-ins (Enscape, ModPlus, Forma) may load Clipper2 1.1.1.0 first. Revit's AppDomain will use the first-loaded version.
- Impact: Geometry operations (union, intersection, point-in-polygon) may behave differently or incorrectly if using the wrong version's implementation.
- Migration plan: (1) Strongly-name LECG's Clipper2 dependency to force version resolution. (2) Or embed Clipper2 as a private assembly. (3) Or upgrade other add-ins to Clipper2 2.0.0 (requires coordination outside LECG).

**Microsoft.Extensions.DependencyInjection.Abstractions version conflict:**
- Risk: LECG references version 8.0.0, but other add-ins load 9.0.0. Abstractions may have API changes between versions.
- Impact: DI interface compatibility issues; services may fail to resolve if the wrong version's interface is used.
- Migration plan: Same as Clipper2 — strongly-name or embed, or upgrade all add-ins to 9.0.0.

---

## Documentation & Clarity Gaps

**Pack:// URI registration workaround not clearly documented:**
- Issue: `App.OnStartup` (lines 22-27) registers pack:// URI scheme manually with a comment referencing .NET 8 / Revit 2026 ("The URI prefix is not recognized"). This is load-bearing but fragile.
- Files: `src/App.cs:22-27`
- Impact: Removing or reordering this code will break resource loading (XAML images, styles). Future developers may not understand why it's necessary and may attempt to "clean it up."
- Fix approach: Add a detailed comment explaining why this is necessary (Revit 2026 WPF context doesn't auto-register pack scheme), and add a test/smoke test that verifies resource loading works after this line runs.

---

## Open Questions / Unknowns

- [Open] **Unit converter / ForgeTypeId helper:** Is there an intended canonical helper for unit conversion (ForgeTypeId vs UnitUtils), or is direct API usage the standard? (No shared helper class identified in `src/`; 11 ForgeTypeId vs 5 UnitUtils hits suggest no unified pattern.)

- [Open] **Parameter StorageType validation pattern:** 33 StorageType checks exist across services. Is there a canonical helper or does each site validate before access? (No shared `*ParameterHelper` class found.)

- [Open] **Linked model transform conventions:** How are coordinates from linked models transformed into project coordinates? `LinkedModelExportService` exists but no `GetTotalTransform` / `GetTransform(` calls found in src. (Need clarification on linked model handling.)

- [Open] **Sanctioned `dotnet test` invocation:** Test project builds locally and claims CI compatibility, but the exact `dotnet test` command and pass/fail expectations have never been validated by a GSD run. (Pending first CI/automated test execution.)

- [Open] **Interactive Revit command validation:** The smoke-test checklist in `docs/ai/revit-smoke-test.md` has never been executed by a human. Command-level behavior (selection handling, transaction commit/rollback, modeless dialog workflows) remains unvalidated. (Pending first interactive Revit run covering steps 3–8 of the checklist.)

---

## Practice Candidates

### [Concern] Static state tracking in long-running operations
**Scope:** Command-level, ExternalEventCommand pattern  
**Evidence paths + line refs:**
- `src/Commands/FormulaAutoGroupingCommand.cs:27,135,157,252` — `s_projectRunActive` flag
- `src/Core/ExternalEventCommand.cs:9-16` — static handler/event fields
**Why it matters:** Static flags are shared across invocations and outlive individual command executions. If exception occurs or reentrancy happens, state becomes inconsistent.  
**Guardrail:** Use try-finally or disposable patterns to guarantee state reset. Consider moving state out of static fields into instance scope or a scoped context object.

### [Concern] Silent exception swallowing in cleanup operations
**Scope:** File cleanup services  
**Evidence paths + line refs:**
- `src/Services/CadConversion/CadTempFileCleanupService.cs:16-18` — bare catch with no logging
- `src/Services/FamilyConversion/FamilyTempFileCleanupService.cs:26-29` — bare catch with comment "ignore lock errors"
**Why it matters:** When cleanup fails, resources accumulate and users have no diagnostic information.  
**Guardrail:** Always log exceptions before swallowing, even at Warning level. Consider a `SafeIgnore(ex => logger.Log(...))` helper to make intent clear.

### [Concern] Hardcoded configuration requiring runtime discovery
**Scope:** DialogWhitelist  
**Evidence paths + line refs:**
- `src/Core/DialogWhitelist.cs:40-67` — five "confidence: low" dialog entries marked unverified
**Why it matters:** Dialog IDs and result codes are brittle; wrong values break workflows silently.  
**Guardrail:** Mark all hardcoded behavior that depends on external system state (Revit dialogs, file paths, registry). Require runtime validation or explicit testing before changes. Document discovery process for future updates.

### [Observed] Comprehensive null validation in constructor/entry points
**Scope:** Project-wide  
**Evidence paths + line refs:**
- `ArgumentNullException.ThrowIfNull()` appears 633 times across src/ — consistent validation pattern
**Why it matters:** Reduces NullReferenceException surprises; makes contract explicit.  
**Preserve by:** Continue using ArgumentNullException.ThrowIfNull for all service/parameter constructors.

### [Observed] Disposable pattern with try-finally for resource cleanup
**Scope:** Services, IRevitSliceJob handlers  
**Evidence paths + line refs:**
- `src/Core/RevitIdlingRunner.cs:59-65` — finally block guarantees Dispose
- `src/Core/RevitCommand.cs:32,59` — logScope disposed in finally
- `src/Services/Infrastructure/Logging/CommandLogContext.cs:40` — _tokens disposed loop
**Why it matters:** Guarantees cleanup even on exception; thread-safe for long-running operations.  
**Preserve by:** Use try-finally consistently for resources (events, file handles, log scopes). Apply pattern to FormulaAutoGroupingCommand static flag reset.

### [Open] Large service decomposition strategy
**Scope:** BatchRenameExecutionService (1019 lines), LinePatternCompactionService (828 lines), others  
**Evidence paths + line refs:**
- `src/Services/Renaming/BatchRenameExecutionService.cs:1-1019` — monolithic rename logic
- `src/Services/PurgeAndCompaction/LinePatternCompactionService.cs:1-828` — compaction logic
**Why it matters:** Size correlates with hidden complexity, interdependencies, and test brittleness.  
**What to do:** Profile operation flow in these services; extract logical sub-operations as dedicated, testable services. Example: create `FamilyParameterBatchRenameService` as a focused sub-service of BatchRenameExecutionService.

### [Concern] FilteredElementCollector usage without caching convention
**Scope:** Project-wide  
**Evidence paths + line refs:**
- 104 FilteredElementCollector uses across `src/Services/` — no visible caching strategy
- Example: `src/Services/Alignment/AlignEdgesBoundaryCollectionService.cs:31` — in-place collector, no cache
**Why it matters:** In large projects, repeated collectors for the same criteria could cause O(n²) performance.  
**Guardrail:** Document whether per-site freshness is intended or whether a transaction-scoped collector cache should be implemented. Profile before and after any collector optimization to verify improvement.

### [Open] Test coverage for entry points (Commands)
**Scope:** Command-level testing  
**Evidence paths + line refs:**
- `src/Commands/*.cs` — 39 command classes
- `LECG.Tests/Commands/FormulaAutoGroupingCommandTests.cs` — only 1 test
**Why it matters:** Commands are the public API of the add-in; any bugs in command logic are caught only at runtime in Revit.  
**What to do:** Create test stubs for high-risk commands (AlignEdges, FixPoints, Purge, CategoryChanger). Mock UIDocument/Document/Services. Verify availability checks, error handling, result reporting.

