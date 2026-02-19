# Research: Triple Purge Implementation (Iterative Cleaning)

## The Need for Iteration
In Revit, elements often have dependencies that form a chain.
Example:
- A `Level` is used by a `WallType`.
- The `WallType` is used by a `Wall` element.
- The `Wall` element might be deleted, but the `WallType` and `Level` remain.
- A single purge pass might find the `WallType` is unused and delete it.
- Only *after* the `WallType` is deleted does the `Level` potentially become unused.

## Transaction Strategy
- **Single Transaction**: If we delete `WallType` in a transaction, the `Level`'s "referenced by" count might not update within the same transaction's `FilteredElementCollector` or parameter checks until the transaction is committed or the document is "regenerated".
- **Multiple Transactions**: Committing between passes is the most reliable way to ensure Revit's database state is fully updated for subsequent scans.

## Current Implementation Analysis
- `PurgePassSequenceService` returns `1, 2, 3`.
- `PurgeExecutionCoordinatorService` runs all 3 passes inside a **single** transaction.
- **Potential Issue**: Pass 2 and 3 might be redundant if the document doesn't regenerate or if the dependency checks rely on committed data.

## Proposed Strategy
1. **Configurable Passes**: Update UI to allow "Quick" (1 pass) vs "Deep" (3 passes).
2. **Transaction per Pass**: Run each pass in its own transaction (if possible) or at least call `doc.Regenerate()` between passes.
3. **UI Feedback**: Show a progress bar that reflects the current pass (e.g. "Pass 2/3: Checking Materials...").

## Constraint Considerations
- `PurgeAll` is a blocking operation. Multiple transactions will take longer but are more thorough.
- Levels are especially sensitive; the existing logic defaults them to `false` which is correct.
