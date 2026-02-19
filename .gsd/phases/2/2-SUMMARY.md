# Plan 2.2 Summary: Purge UI and Exposure

## Objective
Expose the "Deep Purge" functionality in the UI and ensure clear feedback to the user during iterative cleaning.

## Changes
- Updated `PurgeViewModel` with `IsDeepPurge` property and an `ApplyCommand`.
- Updated `PurgeView.xaml` with a "Deep Purge (3 Passes)" checkbox and a separator for visual clarity.
- Updated `PurgeCommand` to read `IsDeepPurge` from settings and correctly configure the `passCount` before calling the service.

## Verification Results
- UI correctly displays and binds the Deep Purge option.
- Settings are persisted for the new Deep Purge property.
- Command correctly bridges UI settings to backend service logic.
