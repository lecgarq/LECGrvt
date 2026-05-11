---
phase: 06-cross-cutting-foundation
plan: 02
subsystem: logging
tags: [di, ilogger, refactor, singleton-elimination]
dependency_graph:
  requires: [06-01]
  provides: [CROSS-01]
  affects: [all-services, all-commands, bootstrapper]
tech_stack:
  added: []
  patterns: [constructor-injection, servicelocator-static-fallback, startup-buffer-replay]
key_files:
  created: []
  modified:
    - src/Services/Infrastructure/Logging/Logger.cs
    - src/Core/Bootstrapper.cs
    - src/Core/RevitCommand.cs
    - src/App.cs
    - src/Views/LogView.xaml.cs
    - src/ViewModels/LogViewModel.cs
    - src/ViewModels/CategoryChangerViewModel.cs
    - src/ViewModels/FilterCopyViewModel.cs
    - src/Commands/ConvertFamilyCommand.cs
    - src/Commands/FormulaAutoGroupingCommand.cs
    - src/Commands/PurgeCommand.cs
    - src/Commands/SearchReplaceCommand.cs
    - src/Commands/AssignMaterialCommand.cs
    - src/Commands/CompactingStylesCommand.cs
    - src/Commands/FixPointsCommand.cs
    - src/Commands/RenderAppearanceMatchCommand.cs
    - src/Commands/SexyRevitCommand.cs
    - src/Commands/SimplifyPointsCommand.cs
    - src/Commands/TypeToLinkedModelsCommand.cs
    - src/Utilities/ExecutionTimer.cs
    - src/Services/ElementLabelService.cs
    - src/Services/CadConversion/CadCurveTessellationService.cs
    - src/Services/CadConversion/CadPolylineExtractionService.cs
    - src/Services/FamilyConversion/FamilyGeometryCopyService.cs
    - src/Services/FamilyConversion/FamilyConversionLoggingService.cs
    - src/Services/FamilyConversion/FamilyConversionExecutionService.cs
    - src/Services/FamilyConversion/FamilyEditorService.cs
    - src/Services/FamilyConversion/FamilyConversionService.cs
    - src/Services/FamilyConversion/FamilyProjectLoadService.cs
    - src/Services/FamilyConversion/FamilyTempFileCleanupService.cs
    - src/Services/FamilyConversion/FamilyParameterSetupService.cs
    - src/Services/FamilyConversion/FamilySourceDocumentService.cs
    - src/Services/FamilyConversion/FamilyTargetDocumentService.cs
    - src/Services/FamilyConversion/FamilySaveService.cs
    - src/Services/FamilyConversion/FamilyTemplatePathService.cs
    - src/Services/Renaming/BaseElementCollectionService.cs
    - src/Services/Renaming/BatchRenameExecutionService.cs
    - src/Services/RenderAppearance/RenderAppearanceSingleSyncService.cs
    - src/Services/PurgeAndCompaction/CompactionSharedHelper.cs
    - src/Services/PurgeAndCompaction/LinePatternCompactionService.cs
    - src/Services/PurgeAndCompaction/PurgeContext.cs
    - src/Services/PurgeAndCompaction/PurgeLinePatternService.cs
    - src/Services/PurgeAndCompaction/PurgeReferenceScannerService.cs
    - src/Services/Materials/MaterialBitmapPropertyService.cs
    - src/Services/Infrastructure/SettingsManager.cs
decisions:
  - "Static classes (PurgeContext, CompactionSharedHelper, ElementLabelService, SettingsManager) use ServiceLocator.GetService<ILogger>() since they cannot have injected constructors"
  - "Static dialog handlers (OnDialogShowing in Commands) resolve ILogger on-demand via ServiceLocator.GetRequiredService, acceptable since DI container is live at that point"
  - "Pre-DI startup warnings in Bootstrapper buffered in List<(string, bool)> and replayed after BuildServiceProvider via concrete Logger cast for ConfigureStructuredLogger"
  - "Static private methods that accessed _logger converted to instance methods (TryCloseFamilyDocument, TryAddCategoryPattern, ScanViewCategoryOverrides, ScanViewFilterOverrides, CompactDuplicateGroups, CompactDuplicateGroup, RewireGroupReferences, BuildCompactionIndexes, RewireReferences, BuildCategoryPatternIndex, IndexCategoryPattern, RewireCategoryReferencesFromIndex)"
metrics:
  duration: "~4 hours (split across 2 sessions)"
  completed_date: "2026-05-11"
  tasks_completed: 3
  files_modified: 45
---

# Phase 06 Plan 02: Logger.Instance Elimination Summary

**One-liner:** Eliminated all 684 CS0618 warnings by migrating every `Logger.Instance` call site (~45 files) to constructor-injected `ILogger`, then deleting the singleton accessor and `[Obsolete]` shims, rewiring Bootstrapper DI to `AddSingleton<ILogger, Logger>()`.

## Objective

Remove the `Logger.Instance` singleton and all `[Obsolete]` transitional overloads introduced in Wave 1 (plan 06-01), completing the full ILogger migration across all service, command, and UI layers.

## Tasks Completed

| Task | Description | Commit |
|------|-------------|--------|
| 1 | Migrate UI layer: App.cs, LogViewModel, LogView, CategoryChangerVM, FilterCopyVM | `3a86714` |
| 1b | Migrate Utilities (ExecutionTimer) + CadConversion services | `0dbe50c` |
| 2 | Migrate FamilyConversion services (10 files) | `d2f1895` |
| 2b | Migrate PurgeAndCompaction services (5 files incl. PurgeContext static class) | `e7fc34f` |
| 2c | Migrate Renaming, RenderAppearance, Materials, SettingsManager | `83fb8f4` |
| 2d | Migrate RevitCommand base class + all Command classes (10 files) | `507e078` |
| 3 | Delete Logger.Instance + [Obsolete] overloads + rewire Bootstrapper | `0b19ab1` |

## Success Criteria Verification

- [x] Zero `Logger.Instance` references in `src/` — confirmed via grep
- [x] Zero CS0618 warnings — confirmed via build output
- [x] `dotnet build LECG.sln -c Debug` — 0 errors
- [x] `dotnet test LECG.Tests` — 190 passed / 5 skipped / 0 failed

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Static methods accessing instance field `_logger`**
- **Found during:** Task 3 (build verification after Logger.Instance deletion)
- **Issue:** 12 static private methods across 3 services had been migrated to use `_logger` calls but remained `static`, causing CS0120 errors when the instance-only `Logger.Instance` fallback was removed
- **Fix:** Converted all offending static methods to instance methods. Affected: `TryCloseFamilyDocument` (FamilyEditorService), `TryAddCategoryPattern`/`ScanViewCategoryOverrides`/`ScanViewFilterOverrides` (PurgeLinePatternService), and full call chain in LinePatternCompactionService (`CompactDuplicateGroups`, `CompactDuplicateGroup`, `RewireGroupReferences`, `BuildCompactionIndexes`, `RewireReferences`, `BuildCategoryPatternIndex`, `IndexCategoryPattern`, `RewireCategoryReferencesFromIndex`)
- **Files modified:** `FamilyEditorService.cs`, `PurgeLinePatternService.cs`, `LinePatternCompactionService.cs`
- **Commit:** `0b19ab1`

**2. [Rule 1 - Bug] `ConfigureStructuredLogger` not on `ILogger` interface**
- **Found during:** Task 3 (build of Bootstrapper.cs)
- **Issue:** After changing DI registration to `AddSingleton<ILogger, Logger>()`, the resolved type is `ILogger` which doesn't expose `ConfigureStructuredLogger` (concrete-only method)
- **Fix:** Used concrete cast `if (logger is Logger concreteLogger) concreteLogger.ConfigureStructuredLogger(...)` in post-build code
- **Files modified:** `Bootstrapper.cs`
- **Commit:** `0b19ab1`

**3. [Rule 2 - Missing functionality] FamilyConversionService static-to-instance conversion**
- **Found during:** Task 2 migration of FamilyConversionService
- **Issue:** Three methods (`ValidatePreFlight`, `TryPlaceReplacementInstance`, `ApplyCapturedInstanceData`) were `static` and used Logger.Instance; converting to `_logger.X(...)` required removing `static`
- **Fix:** Converted all three methods to instance methods
- **Files modified:** `FamilyConversionService.cs`
- **Commit:** `d2f1895`

**4. [Rule 2 - Missing functionality] MaterialBitmapPropertyService static diagnostic methods**
- **Found during:** Task 2c migration
- **Issue:** `LogBumpAssetDiagnostics` and `LogScaleDiag` were static and used Logger.Instance
- **Fix:** Converted to instance methods
- **Files modified:** `MaterialBitmapPropertyService.cs`
- **Commit:** `83fb8f4`

## Self-Check: PASSED

- FOUND: `06-02-SUMMARY.md`
- FOUND commits: `3a86714`, `0dbe50c`, `d2f1895`, `e7fc34f`, `83fb8f4`, `507e078`, `0b19ab1`
- Zero `Logger.Instance` refs confirmed in `src/`
- 0 CS0618 warnings confirmed in build output
- 190 passed / 5 skipped / 0 failed in test suite
