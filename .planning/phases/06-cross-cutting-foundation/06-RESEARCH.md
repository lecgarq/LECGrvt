---
phase: 6
slug: cross-cutting-foundation
milestone: v2.0
status: researched
created: 2026-05-10
---

# Phase 6: Cross-cutting Foundation — Research

**Researched:** 2026-05-10
**Domain:** C# DI / logging infrastructure, Revit DialogBoxShowing API, xUnit test architecture
**Confidence:** HIGH (all findings from direct source-code inspection)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**CROSS-01 — Logger unification:**
- Pure DI. `Logger.Instance` singleton is physically removed by end of phase.
- Every call site receives `ILogger` via constructor injection.
- No `[Obsolete]` transitional facade — singleton goes away in this phase.
- Migrate every existing `Logger.Instance` call site in this phase (30+ files).
- `Logger.cs` structured-sink wiring (`MsLoggerFactory`) kept as-is.
- Dispatcher / threading model in `Logger.cs` preserved verbatim.
- Tag taxonomy cleanup deferred (Future Requirements).

**CROSS-02 — Severity preservation:**
- `IProgressReporter` stays as an interface but impls take `ILogger` via constructor.
- Every impl forwards `LogWarning`/`LogError` to `ILogger.LogWarning`/`ILogger.LogError` with severity preserved.
- Per-call scope parameter on `ILogger` methods (`logger.LogWarning(message, scope: "Purge")`).
- No constructor-bound scope, no `ILogger<T>` generic.
- LogView already renders severity — pipe fix is sufficient; visual polish allowed, bounded.
- `Report(message, percentage)` channel stays separate from LogView.

**CROSS-03 — Dialog whitelist:**
- Single `DialogWhitelist` class, hardcoded `IReadOnlyDictionary<string, int>` (DialogId → OverrideResult).
- `DialogId`-only exact string match. No message-substring fallback.
- Entry payload: `(DialogId, OverrideResult)`. Match key is DialogId only.
- Unknown/unmatched dialogs: reach the user (no OverrideResult call) — changes current Purge cancel-all behavior.
- Every whitelist hit → `Info` log. Every reach-user dialog → `Warning` log (best-effort, before modal).
- Location of `DialogWhitelist` class: planner's call (suggested `src/Core/` near `SafeFailureHandler.cs`).

**GAPS-01:**
- Compile existing evidence only. No new xUnit runs, no new manual Revit checks.
- xUnit-only evidence is sufficient for REQ-07.
- Location: `.planning/milestones/v1.1-phases/02-formula-auto-grouping-bug-fix/02-VERIFICATION.md`
- Template shape: mirror `03-VERIFICATION.md` (section order/headings).

### Claude's Discretion

- File locations for new types (e.g., where `DialogWhitelist` lives).
- Test-class names and per-test breakdown.
- Whether to split CROSS-01 into multiple plans.
- Specific `DialogId` values on the whitelist.

### Deferred Ideas (OUT OF SCOPE)

- UI-02 (severity filter, text search, copy, collapsible scope groups) — Phase 13.
- Tag taxonomy unification — Future Requirements.
- GAPS-02 manual Revit checks — Phase 8.
- Per-command whitelist defaults — rejected.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| CROSS-01 | Unified structured `ILogger` with severity + scope tag; delete `Logger.Instance` singleton; migrate all 30+ call sites to constructor injection | Migration inventory below identifies all 30 call sites, their file paths, and proposed scope tags |
| CROSS-02 | `IProgressReporter` impls preserve LogWarning/LogError severity; entries reach LogView with correct level | Root cause confirmed in three impls; fix pattern identified; ILogger interface extension specified |
| CROSS-03 | Dialog auto-dismiss uses explicit whitelist; unmatched dialogs reach user | Existing dialog handlers reverse-engineered; partial DialogId enumeration provided; wave-0 discovery task flagged |
| GAPS-01 | Retroactive `02-VERIFICATION.md` artifact with xUnit wave-0 evidence for REQ-07 | All four source files identified; 03-VERIFICATION.md template structure documented |
</phase_requirements>

---

## Overview

Phase 6 establishes the logging and dialog-suppression substrate that every Phase 7–11 command will consume. It has four independent workstreams that share no code dependencies on each other — they can be planned as separate waves:

1. **ILogger interface extension + singleton deletion** (CROSS-01): Add `scope` parameter to all `ILogger` methods; delete `Logger.Instance`; inject `ILogger` into every constructor that currently grabs the singleton.
2. **IProgressReporter severity fix** (CROSS-02): Rewrite `SimpleProgressReporter`, `LegacyProgressReporter`, `RevitCommandProgressReporter` to accept `ILogger` via constructor and forward `LogWarning`/`LogError` with correct severity.
3. **DialogWhitelist** (CROSS-03): Create `DialogWhitelist` class; rewrite `PurgeCommand.OnDialogShowing` and `ConvertFamilyCommand.OnDialogShowing` to use it; establish wave-0 discovery task for runtime DialogId enumeration.
4. **GAPS-01 documentation artifact** (GAPS-01): Compile evidence from existing phase 02 artifacts into a new `02-VERIFICATION.md` file using the `03-VERIFICATION.md` template shape.

---

## Current State

### `ILogger` interface (`src/Services/Infrastructure/Logging/Logger.cs`)

The current interface has four severity methods, none accepting a scope parameter:

```csharp
void Log(string message);
void LogSuccess(string message);
void LogWarning(string message);
void LogError(string message);
```

The `Logger` class also has two extra overloads accepting `(string message, string categoryName, Exception? exception = null)` — these are not on the interface and are called directly on `Logger.Instance` in two places (`CadCurveTessellationService`, `SettingsManager`). These overloads already carry the concept of a scope/category; the new `scope` parameter unifies them into the interface.

**Singleton construction:** `private static Logger? _instance; public static Logger Instance => _instance ??= new Logger();` — a lazy singleton initialized on first access.

**DI registration (Bootstrapper.cs line 54):** `services.AddSingleton<LECG.Services.Logging.ILogger>(_ => Logger.Instance);` — this registration returns the singleton. After CROSS-01 this registration should become `services.AddSingleton<ILogger, Logger>()` with a Logger constructor that accepts its dependencies, OR the existing singleton-object approach kept as a single registered instance without the static accessor.

**Dispatcher/flush logic:** The `_uiDispatcher`, `_flushTimer`, `EnqueueVisibleEntry`, and `FlushTimerTick` members handle background-thread-safe observable updates. These are preserved verbatim.

**Structured-logger forwarding:** `ConfigureStructuredLogger(MsLoggerFactory)` and `ForwardToStructuredLogger` forward entries to `Microsoft.Extensions.Logging` using the entry's level. Already uses `categoryName` as the MS logger category. With the new `scope` parameter, `scope` will become the `categoryName` passed to `ForwardToStructuredLogger`.

### `IProgressReporter` (`src/Services/Infrastructure/IProgressReporter.cs`)

Interface exposes four methods:
- `void Report(string message, double percentage)` — progress channel
- `void Log(string message)` — plain info
- `void LogWarning(string message)` — severity-collapsing bug
- `void LogError(string message)` — severity-collapsing bug

**SimpleProgressReporter** (same file, lines 26–54): Constructor takes `Action<ProgressReport>? onReport`. `LogWarning` and `LogError` both call `_onReport?.Invoke(new ProgressReport { Message = message })` — no severity distinction.

**LegacyProgressReporter** (`src/Services/Infrastructure/LegacyProgressReporter.cs` lines 27–35): Constructor takes `Action<double, string>? progressCallback` and `Action<string>? logCallback`. `LogWarning` and `LogError` both call `_logCallback?.Invoke(message)` — same collapse bug.

**RevitCommandProgressReporter** (`src/Services/Infrastructure/RevitCommandProgressReporter.cs` lines 27–35): Constructor takes `Action<string> log` and `Action<double, string> progress`. `LogWarning` and `LogError` both call `_log(message)` — same collapse bug.

**Fix pattern:** All three impls must accept `ILogger` via constructor and call `_logger.LogWarning(message, scope: "…")` / `_logger.LogError(message, scope: "…")`. The `Report` method stays unchanged (calls `ILogger.UpdateProgress` which remains on the interface).

**`SimpleProgressReporter` note:** Its `onReport` Action callback is used by zero callers in `src/` (confirmed by grep — no `new SimpleProgressReporter(…)` with a callback). It was the original in-file impl. After CROSS-02 it is rewritten to take `ILogger`; the `Action<ProgressReport>` constructor is removed.

### `LogView` severity rendering (`src/Views/LogView.xaml` lines 34–70)

Already correct. The `DataTemplate` for `LogEntry` uses `DataTrigger` on `Binding={Binding Level}` to set `Border.Background` and `TextBlock.Foreground`:
- Default (Info): `#EDF2F7` background, `LecgTextSecondary` text
- Success: `#C6F6D5` bg / `#22543D` text
- Warning: `#FEEBC8` bg / `#744210` text
- Error: `#FED7D7` bg / `#822727` text

The pipe bug (CROSS-02) means `LogLevel.Warning`/`Error` entries are never produced by `IProgressReporter` calls today — the right `LogEntry.Level` value never reaches the collection. Once the impls are fixed, the existing XAML renders them correctly. The visual polish mentioned in CONTEXT §2.4 is bounded to contrast/palette tweaks on these existing triggers; no structural XAML change is needed for correctness.

### `PurgeCommand.OnDialogShowing` (`src/Commands/PurgeCommand.cs` lines 36–42)

Cancel-all implementation:
```csharp
private static void OnDialogShowing(object? sender, DialogBoxShowingEventArgs e)
{
    e.OverrideResult(2);   // always cancel
    string detail = e is TaskDialogShowingEventArgs td ? td.Message : e.DialogId ?? "unknown";
    Logger.Instance.Log($"  Auto-dismissed dialog during purge: {detail}");
}
```

Migration: replace `e.OverrideResult(2)` with `DialogWhitelist.Apply(e, _logger)` (or equivalent static dispatch). The handler does NOT currently log the DialogId to a structured log — it logs the message text only. Post-migration, the whitelist logs `Info` on hits and `Warning` on reach-user events.

### `ConvertFamilyCommand.OnDialogShowing` (`src/Commands/ConvertFamilyCommand.cs` lines 25–67)

Substring-match heuristic with three branches:
1. **Safe dialogs** (contains "cannot be added", "already exists", "will be replaced", "duplicate", "overwrite", or dialogId contains "Duplicate") → `OverrideResult(1)` (accept)
2. **Dangerous dialogs** (contains "delete", "remove", "constraint", "discard", "cannot be undone") → `OverrideResult(2)` (cancel)
3. **Everything else (including non-TaskDialog)** → `OverrideResult(2)` (cancel)

Migration: entire heuristic replaced with `DialogWhitelist.Apply(e, _logger)`. The whitelist must encode the previously "safe" entries as `OverrideResult(1)` and the previously "dangerous" entries — if they have stable DialogIds — as `OverrideResult(2)`. Anything not in the whitelist reaches the user (new behavior).

---

## Migration Inventory — `Logger.Instance` Call Sites

Complete list from grep. 30 call sites across 18 files. Confidence: HIGH (direct grep output).

| # | File | Line(s) | Method Called | Scope Tag to Use | Notes |
|---|------|---------|---------------|-----------------|-------|
| 1 | `src/Views/LogView.xaml.cs` | 25 | `Logger.Instance` (fallback ctor) | n/a — remove fallback | `LogView()` default ctor uses `Logger.Instance` as fallback; after CROSS-01 the DI-provided `ILogger` is always available |
| 2 | `src/App.cs` | 90 | `.Log(…)` | `"App"` | WPF resource load failure |
| 3 | `src/App.cs` | 107 | `.Log(…)` | `"App"` | Dispatcher exception handler |
| 4 | `src/App.cs` | 129 | `.Log(…)` | `"App"` | Unhandled exception handler |
| 5 | `src/ViewModels/CategoryChangerViewModel.cs` | 63 | `.Log(…)` | `"CategoryChanger"` | Error in Apply |
| 6 | `src/ViewModels/LogViewModel.cs` | 30 | `Logger.Instance` (fallback) | n/a — remove `?? Logger.Instance` | Constructor already accepts `ILogger logger`; just remove fallback |
| 7 | `src/ViewModels/FilterCopyViewModel.cs` | 331 | `.Log(…)` | `"FilterCopy"` | Filter copy failure |
| 8 | `src/Core/RevitCommand.cs` | 62 | `.Log(…)` (protected helper) | `"Command"` | `protected void Log(string text)` helper delegates to singleton; migrate to injected field |
| 9 | `src/Core/RevitCommand.cs` | 120 | `.Clear()` | n/a | `PrepareCommandExecution` calls `Clear()` |
| 10 | `src/Core/RevitCommand.cs` | 121 | `.SetDispatcher(…)` | n/a | `PrepareCommandExecution` calls `SetDispatcher` |
| 11 | `src/Commands/ConvertFamilyCommand.cs` | 41, 53, 59, 65 | `.Log(…)`, `.LogWarning(…)` | `"ConvertFamily"` | Dialog handler — 4 call sites; entire method replaced by DialogWhitelist |
| 12 | `src/Commands/FormulaAutoGroupingCommand.cs` | 35 | `.Log(…)` | `"FormulaAutoGrouping"` | Dialog auto-dismiss logging |
| 13 | `src/Commands/PurgeCommand.cs` | 41 | `.Log(…)` | `"Purge"` | Dialog auto-dismiss logging; replaced by DialogWhitelist |
| 14 | `src/Commands/SearchReplaceCommand.cs` | 40 | `Logger.Instance` passed as arg | n/a — inject `ILogger` | `ExecuteBatchRename(doc, items, Logger.Instance, reporter)` — pass injected `ILogger` |
| 15 | `src/Utilities/ExecutionTimer.cs` | 20, 36 | `.Log(…)` | `"Perf"` | Start/end timing logs |
| 16 | `src/Services/ElementLabelService.cs` | 92 | `.LogWarning(…)` | `"ElementLabel"` | |
| 17 | `src/Services/CadConversion/CadCurveTessellationService.cs` | 40 | `.LogWarning(…, categoryName, ex)` | `"CadCurveTessellation"` | Already has categoryName overload |
| 18 | `src/Services/CadConversion/CadPolylineExtractionService.cs` | 30 | `.LogWarning(…)` | `"CadPolylineExtraction"` | |
| 19 | `src/Services/FamilyConversion/FamilyGeometryCopyService.cs` | 46, 58, 61 | `.Log(…)` | `"FamilyGeometryCopy"` | 3 call sites |
| 20 | `src/Services/Renaming/BaseElementCollectionService.cs` | 133, 287 | `.LogWarning(…)` | `"BaseElementCollection"` | 2 call sites |
| 21 | `src/Services/Renaming/BatchRenameExecutionService.cs` | 931, 932 | (omitted — long line) | `"BatchRename"` | 2 call sites |
| 22 | `src/Services/RenderAppearance/RenderAppearanceSingleSyncService.cs` | 39, 43, 47 | `.LogWarning(…)` | `"RenderAppearance"` | 3 call sites |
| 23 | `src/Services/FamilyConversion/FamilyConversionLoggingService.cs` | 11–23 | `.Log(…)` (5 sites) | `"FamilyConversion"` | 5 call sites; entire service migrates to injected ILogger |
| 24 | `src/Services/FamilyConversion/FamilyConversionExecutionService.cs` | 28, 41 | `.Log(…)`, `.LogWarning(…)` | `"FamilyConversionExecution"` | |
| 25 | `src/Services/FamilyConversion/FamilyEditorService.cs` | 121, 138, 285 | `.Log(…)`, `.LogWarning(…)` | `"FamilyEditor"` | 3 call sites |
| 26 | `src/Services/FamilyConversion/FamilyConversionService.cs` | 159–465 | multiple `.Log(…)`, `.LogWarning(…)` | `"FamilyConversion"` | ~15 call sites — densest file |
| 27 | `src/Services/FamilyConversion/FamilyProjectLoadService.cs` | 32, 34 | `.Log(…)` | `"FamilyConversion"` | |
| 28 | `src/Services/FamilyConversion/FamilyTempFileCleanupService.cs` | 16, 25 | `.Log(…)` | `"FamilyConversion"` | |
| 29 | `src/Services/FamilyConversion/FamilyParameterSetupService.cs` | 78, 107, 200 | `.Log(…)` | `"FamilyParameterSetup"` | |
| 30 | `src/Services/FamilyConversion/FamilySourceDocumentService.cs` | 17 | `.Log(…)` | `"FamilyConversion"` | |
| 31 | `src/Services/FamilyConversion/FamilyTargetDocumentService.cs` | 17, 24 | `.Log(…)` | `"FamilyConversion"` | |
| 32 | `src/Services/FamilyConversion/FamilySaveService.cs` | 22 | `.Log(…)` | `"FamilyConversion"` | |
| 33 | `src/Services/FamilyConversion/FamilyTemplatePathService.cs` | 55 | `.LogWarning(…)` | `"FamilyTemplatePath"` | |
| 34 | `src/Services/PurgeAndCompaction/CompactionSharedHelper.cs` | 44–51, 407 | `.LogWarning(…)` | `"CompactionShared"` | 7 call sites |
| 35 | `src/Services/PurgeAndCompaction/LinePatternCompactionService.cs` | 640, 672 | `.LogWarning(…)` | `"LinePatternCompaction"` | |
| 36 | `src/Services/PurgeAndCompaction/PurgeContext.cs` | 176–530 | `.LogWarning(…)` | `"PurgeContext"` | ~30 call sites — second densest file |
| 37 | `src/Services/PurgeAndCompaction/PurgeLinePatternService.cs` | 127–212 | `.LogWarning(…)` | `"PurgeLinePattern"` | ~8 call sites |
| 38 | `src/Services/PurgeAndCompaction/PurgeReferenceScannerService.cs` | 43 | `.LogWarning(…)` | `"PurgeReferenceScanner"` | |
| 39 | `src/Services/Materials/MaterialBitmapPropertyService.cs` | 35–308 | `.Log(…)`, `.LogWarning(…)` | `"MaterialBitmapProperty"` | ~8 call sites |
| 40 | `src/Services/Infrastructure/SettingsManager.cs` | 89 | `.LogWarning(…, categoryName)` | `"SettingsManager"` | Uses categoryName overload |
| 41 | `src/Core/Bootstrapper.cs` | 34, 51, 54 | `.LogWarning(…)`, `.ConfigureStructuredLogger(…)`, `_ => Logger.Instance` | — | Line 34: startup warning; line 51: ConfigureStructuredLogger call (moves into Logger ctor); line 54: registration replaced |

**Total distinct source files:** 18
**Total Logger.Instance call sites:** ~65 (the grep output shows ~65 lines; `PurgeContext.cs` and `FamilyConversionService.cs` together account for ~45 of them)

### Critical migration notes

- **`RevitCommand.cs` (lines 62, 120, 121):** This is the base class for all command classes. Injecting `ILogger` into `RevitCommand` is the highest-leverage single change — it gives every derived command access to the logger without individual per-command ctor changes. `RevitCommand` can receive `ILogger` from the service locator in `PrepareCommandExecution()` (since commands are resolved by Revit attribute, not by DI) or via a protected static accessor that pulls from `ServiceLocator`. The singleton pattern in `RevitCommand` is the one case where a static pull from `ServiceLocator.GetRequiredService<ILogger>()` may be justified instead of true ctor injection, because Revit instantiates commands directly.

- **`Bootstrapper.cs` line 54:** `services.AddSingleton<ILogger>(_ => Logger.Instance)` must become `services.AddSingleton<ILogger, Logger>()` (or keep the instance approach, but without the static accessor). `ConfigureStructuredLogger` (currently called on `Logger.Instance` at line 51) must be called on the registered instance — easiest to call via a startup hook after DI container is built.

- **`LogViewModel.cs` line 30:** `_logger = logger ?? Logger.Instance;` — remove the `?? Logger.Instance` fallback. `LogViewModel` is always resolved from DI with an `ILogger` registered; the fallback is dead code after CROSS-01.

- **`LogView.xaml.cs` line 25:** `ServiceLocator.GetService<LogViewModel>() ?? new LogViewModel(Logger.Instance)` — remove `Logger.Instance` fallback; always use `ServiceLocator.GetRequiredService<LogViewModel>()`.

---

## IProgressReporter Consumers

### Concrete implementations (to rewrite for CROSS-02)

| Class | File | Current ctor signature | CROSS-02 ctor |
|-------|------|----------------------|---------------|
| `SimpleProgressReporter` | `src/Services/Infrastructure/IProgressReporter.cs` | `Action<ProgressReport>? onReport` | `ILogger logger` |
| `LegacyProgressReporter` | `src/Services/Infrastructure/LegacyProgressReporter.cs` | `Action<double, string>?, Action<string>?` | `ILogger logger` |
| `RevitCommandProgressReporter` | `src/Services/Infrastructure/RevitCommandProgressReporter.cs` | `Action<string> log, Action<double, string> progress` | `ILogger logger` |

**Breaking change risk:** The constructor signature changes break all call sites that `new` these classes directly. Audit needed:

| Impl | Current call sites |
|------|-------------------|
| `RevitCommandProgressReporter` | `PurgeCommand.cs` line 55: `new RevitCommandProgressReporter(Log, UpdateProgress)`; `ConvertFamilyCommand.cs` line 129: same pattern |
| `LegacyProgressReporter` | No `new LegacyProgressReporter` found in `src/` (used as DI-resolved type? or dead?) |
| `SimpleProgressReporter` | No `new SimpleProgressReporter` found in `src/` |

`RevitCommandProgressReporter` is instantiated in `PurgeCommand` and `ConvertFamilyCommand` using `Log` and `UpdateProgress` from `RevitCommand`. After CROSS-02 the new ctor takes `ILogger` — `PurgeCommand` and `ConvertFamilyCommand` (as `RevitCommand` subclasses) will have `ILogger` available after the CROSS-01 migration, so the new construction becomes `new RevitCommandProgressReporter(_logger, UpdateProgress)`. The `Report` method still needs the `UpdateProgress` action channel, so the ctor signature becomes `(ILogger logger, Action<double, string> progress)`.

### Services that accept `IProgressReporter` as a parameter (not breaking, no ctor change needed)

These services receive `IProgressReporter` as a method parameter — they call `reporter.Log()`, `reporter.LogWarning()`, `reporter.LogError()` on whatever impl is passed in. Because the interface contract is unchanged (method signatures stay the same), these services compile without modification. The severity fix is entirely inside the impl classes.

Key services affected (non-exhaustive):
- `DeepPurgeService`, `PurgeService`, `PurgeExecutionCoordinatorService`, `PurgePassExecutionService`, `PurgeSummaryService`, `PurgePassMessagingService`
- `FillPatternCompactionService`, `LinePatternCompactionService`, `LineStyleCompactionService`, `TextStyleCompactionService`
- `FamilyConversionService`
- `ConversionService`, `SplitBoundariesService`, `SimplifyPointsService`, `FixPointsService`, `DivideToposolidService`
- `MaterialAssignmentExecutionService`, `RenderAppearanceSingleSyncService`, `RenderAppearanceBatchSyncService`
- `CadConversionService` and its 10+ subordinate services
- `BatchRenameExecutionService`, `SearchReplaceService`

**Confidence: HIGH** — None of these services need changes. Their signatures, implementations, and tests are unaffected by the ctor-level fix.

---

## DialogId Enumeration

### Current behavior summary

**Purge:** Cancel-all (every dialog gets `OverrideResult(2)`). The comments cite "Extrusion is too thin", "Base sketch for extrusion is invalid" as motivation — these are `FailuresProcessing`-pipeline failures that appear as TaskDialogs during `LoadFamily`. No `DialogId` is currently logged.

**ConvertFamily:** Substring match. "Safe" dialogs accepted (OverrideResult 1); "dangerous" and unknown dialogs cancelled (OverrideResult 2).

### Proposed whitelist entries

Based on Revit API documentation knowledge and the code comments, the following entries are reasonable candidates. Confidence is marked LOW where no runtime observation exists.

| DialogId | Proposed OverrideResult | Rationale | Confidence |
|----------|------------------------|-----------|------------|
| `TaskDialog_ExtrusionTooThin` | 2 (cancel / preserve geometry) | Cited in PurgeCommand comment as the specific dialog motivating cancel-all | LOW — inferred from comment, not observed |
| `TaskDialog_BaseSketchInvalid` | 2 (cancel) | Also cited in PurgeCommand comment | LOW |
| `TaskDialog_LoadFamily` (generic load confirm) | 1 (accept) | LoadFamily during deep purge triggers a confirm dialog when family already loaded | LOW |
| `TaskDialog_Overwrite` | 1 (accept) | ConvertFamily "already exists / will be replaced" case | LOW |
| `TaskDialog_DuplicateFamily` | 1 (accept) | ConvertFamily duplicate check | LOW |

**Critical caveat:** Revit's `DialogId` values are not documented by Autodesk. The strings above are plausible conventions but are NOT confirmed. The actual values emitted at runtime may differ (e.g., `TaskDialog_ExtrusionTooThin` may actually be `Revit_TaskDialog_Extrusion_Is_Too_Thin` or a numeric GUID-like ID).

### Wave-0 discovery task (REQUIRED)

**The planner MUST include a wave-0 task:** Before any whitelist is committed, run both commands against a representative model with a temporary logging-only handler that logs `DialogId` and `Message` for every `DialogBoxShowing` event without calling `OverrideResult`. Capture all emitted DialogIds. The wave-0 task produces the definitive whitelist entries with confirmed string values. Only after this observation can the whitelist be written with HIGH confidence.

**Logging-only discovery handler pattern:**
```csharp
private static void OnDialogShowingDiscovery(object? sender, DialogBoxShowingEventArgs e)
{
    string dialogId = e.DialogId ?? "null";
    string message = e is TaskDialogShowingEventArgs td ? td.Message : "(non-task)";
    _logger.LogWarning($"[DISCOVERY] DialogId='{dialogId}' Message='{message}'", scope: "DialogDiscovery");
    // No OverrideResult call — dialog reaches the user
}
```

### `FormulaAutoGroupingCommand` dialog suppression

`FormulaAutoGroupingCommand.cs` line 31–36 also has an `OnDialogShowing` handler with cancel-all behavior. This command is NOT in the CROSS-03 scope (CONTEXT §7 lists only Purge and Convert Family). The planner should note this as a future CROSS-03 extension point but NOT migrate it in Phase 6.

---

## GAPS-01 Source Material

### Artifact location
`.planning/milestones/v1.1-phases/02-formula-auto-grouping-bug-fix/`

### Available files

| File | Content | Maps to VERIFICATION section |
|------|---------|------------------------------|
| `02-01-SUMMARY.md` | Plan 1 accomplishments: SubTransaction loop, EnsureCurrentType, in-transaction check removal, EnsureParametersPersistInGroup refactor; test scaffold created; commit hash `efc110e` + `36481c3` | Observable Truths #1–4, Required Artifacts (FormulaAutoGroupingCommand.cs + test file), Key Link Verification |
| `02-02-SUMMARY.md` | Plan 2 accomplishments: clear-replace-restore sequence in TryReplaceSharedParameterGroup; commit hash `049904e`; deviation: null→string.Empty fix | Observable Truths #5, Required Artifacts, Anti-Patterns Found |
| `02-VALIDATION.md` | Wave map: task 2-01-01/02 mapped to `dotnet test LECG.Tests --filter "FormulaAutoGroup"`; Wave 0 gap was `FormulaAutoGroupingCommandTests.cs` | Test & Build Status (the wave-0 gap was closed — file was created in Plan 01) |
| `02-UAT.md` | UAT result: Test #1 (xUnit green) = PASS; Tests #2–5 = SKIPPED (trust-based); evidence: "Category=FormulaGrouping: 4 passed, 0 failed; Full suite: 79 passed, 0 failed; timestamp 2026-05-09T04:38:00Z" | Test & Build Status, Human Verification Required |

### Template structure (from `03-VERIFICATION.md`)

The Phase 03 VERIFICATION.md has these top-level sections:
1. Frontmatter (phase, verified, status, score, re_verification)
2. Phase Goal block
3. `## Goal Achievement` → `### Observable Truths` table + `### Required Artifacts` table + `### Key Link Verification` table + `### Requirements Coverage` table + `### Test & Build Status` table + `### Anti-Patterns Found` + `### Human Verification Required`
4. `### Summary` paragraph

### REQ-07 evidence mapping

| Evidence item | Source | VERIFICATION content |
|--------------|--------|---------------------|
| 4 xUnit tests under `Category=FormulaGrouping` green | `02-UAT.md` UAT Test #1 | Observable Truth: xUnit filter green |
| `FormulaAutoGroupingCommandTests.cs` exists at `LECG.Tests/Commands/` | `02-01-SUMMARY.md` key-files.created | Required Artifacts row |
| 4 structural fixes in `FormulaAutoGroupingCommand.cs` | `02-01-SUMMARY.md` accomplishments | Observable Truths rows for each fix |
| Clear-replace-restore sequence | `02-02-SUMMARY.md` accomplishments | Observable Truth row |
| Full suite 79 GREEN at closure | `02-UAT.md` Test #1 evidence | Test & Build Status |
| Manual Revit checks SKIPPED (trust-based) | `02-UAT.md` Tests #2–5 | Human Verification Required (explicit note: live Revit observation not performed; GAPS-02 if needed) |

**Note:** The current test count on the baseline is 175 GREEN (from STATE.md). The 79 tests cited in `02-UAT.md` reflect the test suite size at Phase 2 completion (2026-04-29). The VERIFICATION artifact should cite the at-phase evidence (79 passed) not the current count, since it's a retroactive artifact for the Phase 2 period.

---

## Validation Architecture

### Test framework

| Property | Value |
|----------|-------|
| Framework | xUnit |
| Config file | `LECG.Tests/LECG.Tests.csproj` |
| Quick run | `dotnet test LECG.Tests --filter "Category=CrossCutting"` |
| Full suite | `dotnet test LECG.Tests` |
| Baseline | 175 PASSED, 5 SKIPPED — must not regress |

### Phase requirements → test map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CROSS-01 | `ILogger` interface has `scope` parameter on all methods | unit | `dotnet test LECG.Tests --filter "Category=CrossCutting"` | No — Wave 0 |
| CROSS-01 | No `Logger.Instance` references compile after deletion | build | `dotnet build` (0 errors) | n/a — verified by build |
| CROSS-02 | `RevitCommandProgressReporter.LogWarning(msg)` produces a `LogEntry` with `LogLevel.Warning` | unit | `dotnet test LECG.Tests --filter "Category=CrossCutting"` | No — Wave 0 |
| CROSS-02 | `RevitCommandProgressReporter.LogError(msg)` produces a `LogEntry` with `LogLevel.Error` | unit | `dotnet test LECG.Tests --filter "Category=CrossCutting"` | No — Wave 0 |
| CROSS-02 | `LegacyProgressReporter.LogWarning(msg)` produces `LogLevel.Warning` entry | unit | `dotnet test LECG.Tests --filter "Category=CrossCutting"` | No — Wave 0 |
| CROSS-02 | `LegacyProgressReporter.LogError(msg)` produces `LogLevel.Error` entry | unit | `dotnet test LECG.Tests --filter "Category=CrossCutting"` | No — Wave 0 |
| CROSS-03 | Whitelisted DialogId → `OverrideResult` called with correct value + `Info` log entry produced | unit (with fake ILogger) | `dotnet test LECG.Tests --filter "Category=CrossCutting"` | No — Wave 0 |
| CROSS-03 | Unknown DialogId → `OverrideResult` NOT called + `Warning` log entry produced | unit | same | No — Wave 0 |
| GAPS-01 | `02-VERIFICATION.md` file exists at correct path | file-existence check | manual or shell | No — Wave 0 |
| GAPS-01 | REQ-07 FormulaGrouping tests still GREEN | unit | `dotnet test LECG.Tests --filter "Category=FormulaGrouping"` | Yes — `FormulaAutoGroupingCommandTests.cs` |

### Sampling rate

- **Per task commit:** `dotnet test LECG.Tests --filter "Category=CrossCutting OR Category=FormulaGrouping"`
- **Per wave merge:** `dotnet test LECG.Tests`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 gaps

- [ ] `LECG.Tests/Services/Logging/LoggerSeverityTests.cs` — covers CROSS-01 scope parameter + CROSS-02 severity preservation for all three reporter impls
- [ ] `LECG.Tests/Core/DialogWhitelistTests.cs` — covers CROSS-03 hit/miss/log behavior

*(GAPS-01 has no Wave 0 test gap — it is a documentation artifact, validated by file existence and content review)*

---

## Risks and Open Questions

### Risk 1: `RevitCommand` cannot use true constructor injection

**What:** Revit instantiates `IExternalCommand` implementations directly (no DI container in the call chain). `RevitCommand.Log()` currently delegates to `Logger.Instance`; after deletion, `RevitCommand` must get `ILogger` from somewhere.

**Options:**
1. Pull from `ServiceLocator.GetRequiredService<ILogger>()` inside `PrepareCommandExecution()` — acceptable given `RevitCommand` is always a leaf command class, not a testable service unit.
2. Accept `ILogger` via an `Init(ILogger)` method called from `PrepareCommandExecution()`.
3. Cache it as a protected field populated from ServiceLocator on first use.

**Recommended:** Option 1 (ServiceLocator pull in `PrepareCommandExecution`). The `RevitCommand.Log()` helper becomes `protected void Log(string text) => _logger.Log(text, scope: CommandName)`. This avoids changing every derived command's constructor while still removing `Logger.Instance`.

**Note:** `Bootstrapper.cs` already uses `ServiceLocator.Initialize(_provider)` pattern. ServiceLocator is established infrastructure.

### Risk 2: `Bootstrapper.cs` initialization ordering

**What:** `Logger.Instance.ConfigureStructuredLogger(loggerFactory)` (line 51) and `Logger.Instance.LogWarning(...)` (line 34) run during `Initialize()` before the DI container is built. After singleton deletion, `Logger` must be constructable with a default state (no loggerFactory) and have `ConfigureStructuredLogger` called post-build.

**Resolution:** Keep `Logger` constructable without arguments (as it is today). After `services.BuildServiceProvider()`, resolve `ILogger` and call `.ConfigureStructuredLogger(loggerFactory)` on it. The existing Bootstrapper flow can do this with a one-line addition after `ServiceLocator.Initialize(_provider)`.

### Risk 3: `PurgeContext.cs` and `FamilyConversionService.cs` are the heaviest migration files

**What:** These two files contain ~30 and ~15 `Logger.Instance` calls respectively — almost all `LogWarning` in exception catch blocks. The pattern is uniform (the scope tag is already embedded in the string as `[PurgeContext]`, `[FamilyConversionService]`), so migration is mechanical but the file size means high diff volume.

**Recommendation:** Plan these as a separate migration sub-task for the sweep, not mixed with the interface-extension work.

### Risk 4: `SimpleProgressReporter` constructor breaking change

**What:** `SimpleProgressReporter` is defined in `IProgressReporter.cs` (same file as the interface). Its `Action<ProgressReport>` constructor is public but no call sites construct it directly in `src/`. If any external consumer (test fakes, scripts) uses it, the constructor change is breaking.

**Check:** Grep for `new SimpleProgressReporter` in `LECG.Tests/` — not found in the test glob output. The only test reference is `SearchReplaceFakes.cs` which has its own `IProgressReporter` fake. Safe to change constructor.

### Risk 5: DialogId values are unconfirmed without runtime observation

**What:** Revit does not publicly document `DialogId` string values. The strings emitted by `TaskDialog` are set by internal Revit code and may differ between Revit versions (this plugin targets Revit 2026 only, which reduces variability but does not eliminate it).

**Resolution:** The wave-0 discovery task (logging-only run with both commands against a test model) is mandatory before writing the whitelist. The planner should place this task in Wave 1 with a gate: whitelist values must be confirmed before Phase 3 tasks that implement the whitelist handler.

### Open Question 1: Where does `DialogWhitelist` live?

CONTEXT suggests `src/Core/` near `SafeFailureHandler.cs`. This is reasonable: `SafeFailureHandler` is the `IFailuresPreprocessor` analog (handles failures in transaction pipeline); `DialogWhitelist` handles the `DialogBoxShowing` pipeline. They are both "Revit interception" concerns. Planner's call.

### Open Question 2: Should `ILogger` scope parameter be nullable?

The per-call scope design (`logger.LogWarning(message, scope: "Purge")`) needs a decision on whether `scope` is `string?` or `string`. Making it `string?` with a default of `null` (meaning "use the caller's namespace as category") is safest for the 65 existing call sites that currently omit scope. Making it `string` with a required value forces the migration to be explicit but produces ~65 compiler errors until all sites are updated. Given the locked decision to migrate all sites in this phase, `string` (required, non-nullable) is consistent with the intent — missing scope = compile error = migration is enforced.

### Open Question 3: `BatchRenameExecutionService.cs` lines 931–932

The grep output showed `[Omitted long matching line]` for these two lines, suggesting the Logger.Instance call is inside a very long line (possibly a lambda or ternary). The planner should read these lines directly before tasking the migration.

---

## Sources

### Primary (HIGH confidence)
All findings from direct source-code inspection of the LECG repository. No external sources required — the domain is entirely internal code.

- `src/Services/Infrastructure/Logging/Logger.cs` — ILogger interface, Logger class, singleton pattern, MS.Extensions forwarding
- `src/Services/Infrastructure/IProgressReporter.cs` — interface + SimpleProgressReporter
- `src/Services/Infrastructure/LegacyProgressReporter.cs` — severity-collapse bug confirmed
- `src/Services/Infrastructure/RevitCommandProgressReporter.cs` — severity-collapse bug confirmed
- `src/Views/LogView.xaml` lines 34–70 — severity rendering already correct
- `src/Commands/PurgeCommand.cs` lines 36–42 — cancel-all dialog handler
- `src/Commands/ConvertFamilyCommand.cs` lines 25–67 — substring-match dialog handler
- `src/Core/RevitCommand.cs` lines 62, 120–121 — Logger.Instance usage in base class
- `src/Core/Bootstrapper.cs` lines 34, 51, 54 — initialization and DI registration
- `src/ViewModels/LogViewModel.cs` line 30 — Logger.Instance fallback in ctor
- `.planning/milestones/v1.1-phases/02-formula-auto-grouping-bug-fix/` (all 7 files)
- `.planning/milestones/v1.1-phases/03-grid-collection-fixes/03-VERIFICATION.md` — template reference

### Secondary (LOW confidence — runtime)
- Proposed DialogId values (`TaskDialog_ExtrusionTooThin` etc.) — LOW, inferred from code comments and Revit API conventions, NOT confirmed from runtime observation. Wave-0 discovery task required.

---

## Metadata

**Confidence breakdown:**
- Current state (Logger, IProgressReporter, LogView, dialog handlers): HIGH — direct code inspection
- Migration inventory (call site list): HIGH — direct grep, 100% enumerated
- IProgressReporter consumer list: HIGH — direct grep
- DialogId whitelist entries: LOW — no runtime observation available
- GAPS-01 source material: HIGH — all artifact files confirmed present and read
- Validation architecture: HIGH — existing test infrastructure confirmed

**Research date:** 2026-05-10
**Valid until:** Phase 6 implementation complete (stable domain — internal code only)
