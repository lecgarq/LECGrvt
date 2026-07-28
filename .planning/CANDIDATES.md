# Milestone candidates

Shaped, not scheduled. `/lecg-milestone-new` reads this; delete an entry when it gets roadmapped or rejected.

## Purge view templates + view filters

**Status:** shaped 2026-07-27 (idea from EF-Tools, GPL/pyRevit — concept only, no code)

**Problem:** PurgeService covers materials, line styles/patterns, fill patterns, levels, parameters, extended elements — but unused view templates and view filters are among the worst real-world model bloat and are untouched.

**Fit:** two new services in the existing purge pass architecture (`PurgeViewTemplateService`, `PurgeViewFilterService`), registered like the six existing `Purge*Service` siblings, wired into `PurgePassSequenceService` + two checkboxes in PurgeView/PurgeDialogSettings. No new patterns.

**Sketch:** unused view template = not assigned to any view and not referenced by view-creation defaults; unused filter = not referenced by any view or view template. Both deletable via the existing `PurgeDeleteElementService` path. Core sequencing logic goes in `LECG.Core/Purge` where it is testable without Revit.

**Out of scope:** purging views themselves.

## Warnings review command

**Status:** shaped 2026-07-27 (idea from EF-Tools, GPL/pyRevit — concept only, no code)

**Problem:** Nothing in the add-in touches `Document.GetWarnings()`. Reviewing model warnings in Revit's own dialog is painful, and warning count is the health metric users actually watch.

**Fit:** Project Health panel. Standard command shape — `WarningsCommand : RevitCommand`, `WarningsService` (read-only, no ITransactionService), `WarningsView` grouping warnings by description with element select/isolate/show actions. Grouping logic testable in LECG.Core.

**Sketch:** collect `doc.GetWarnings()`, group by `GetDescriptionText()`, show counts, let the user select or isolate the failing elements per group (isolate = temporary view isolation, no document write).

**Out of scope:** auto-fixing warnings.
