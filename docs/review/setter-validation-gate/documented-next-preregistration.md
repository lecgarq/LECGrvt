# Documented changed-value batch 2 preregistration

## Scope

Five remaining `same_value_only` setters whose alternatives can be derived from the Revit 2026 API. Expected yield before execution: **2-5 validated of 5**. A refusal or context rejection remains evidence and does not count as validation.

## Generators

- `StairsLanding.BaseElevation`: add exactly `Stairs.ActualRiserHeight`; reject non-finite/non-positive risers and the documented 30,000-foot bound.
- `StairsRun.BaseElevation`: prefer one actual-riser step upward while retaining at least one riser below `TopElevation`; otherwise step downward only when the relative base remains non-negative.
- `StairsRun.TopElevation`: exclude `StairsRunStyle.Sketched`, then add exactly one actual riser within the documented bound.
- `ScheduleSheetInstance.SegmentIndex`: require a split `ViewSchedule`; choose another index from `0..GetSegmentCount()-1`. Never use the unsplit `0/-1` alias.
- `WireType.MaxSize`: resolve the selected `TemperatureRatingType`, then choose a different `InUse` name from its API-provided `WireSizes` set.

## Isolation

Every attempt uses a fresh detached disposable copy. The harness must roll back, close without saving, verify the source SHA-256, delete the copy, and record `cleanup_verified=true`. Family documents remain out of contract.

## Model order

Architecture cases try installed Autodesk architectural samples before the authorized CASA EUCALIPTO architecture copy. The wire case tries installed MEP/electrical samples before the authorized CASA EUCALIPTO MEP copy. Originals are never opened as writable test documents.
