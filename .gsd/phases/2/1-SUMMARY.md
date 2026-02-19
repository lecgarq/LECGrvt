# Plan 2.1 Summary: Purge Iteration Logic

## Objective
Update the core purge services to support configurable pass counts and ensure maximum effectiveness through document regeneration.

## Changes
- Updated `IPurgePassSequenceService` and `PurgePassSequenceService` to accept `passCount`.
- Updated `IPurgeExecutionCoordinatorService` and `PurgeExecutionCoordinatorService` to accept `passCount` and call `doc.Regenerate()` between passes.
- Updated `IPurgeService` and `PurgeService` to propagate `passCount` to the coordinator.

## Verification Results
- Service layer correctly handles variable pass counts.
- Document regeneration is integrated into the purge loop.
