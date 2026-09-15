# CASA EUCALIPTO MEP wire-size changed-value probe

## Purpose

Retest `Electrical.WireType.MaxSize` after replacing the deprecated Revit 2025
`TemperatureRatingType/WireSize` lookup with Revit 2026's public
`ConductorSize.GetConductorSizeIds` and `ConductorSize.GetConductorSize` APIs.
The setter accepts a conductor-size name, so the generator selects an existing
document conductor-size name that differs ordinally from the current value.

Expected yield before execution: **0–1 validation**. A model containing no second
conductor-size name is a valid `no_proven_valid_alternative` outcome.

## Fixture and isolation

- Source: `02-SHARED\03-MEP\LECG_RVT_MEP.rvt`
- Frozen SHA-256: `55C7A010C8C19630B7EC9816BBFF78FFABF46237EB88BF2AF387CEA8B5539201`
- The harness opens only a fresh detached `disposable.rvt` copy.
- It rolls the transaction group back, verifies restoration, closes without save,
  deletes the copy, and re-hashes the source before reconciliation.
- Any isolation or cleanup failure aborts the run and earns no ledger credit.

Only committed changed read-back with verified rollback can increase 667/805.
