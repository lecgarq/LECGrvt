# CASA EUCALIPTO architecture changed-value batch

## Purpose

Test four remaining setter definitions against the existing architecture project
fixture. The batch uses the API-derived generators already implemented in
`PilotValues.cs`; it does not invent new value constraints.

## Frozen cases

1. `AssemblyInstance.NamingCategoryId`
2. `Architecture.StairsLanding.BaseElevation`
3. `Architecture.StairsRun.BaseElevation`
4. `Architecture.StairsRun.TopElevation`

Expected yield before execution: **0–4 validations**. Existing Autodesk sample
attempts show that stair geometry can reject elevation changes at commit, so zero
is an admissible result and must not be rationalized upward after the run.

## Fixture and isolation

- Source: `01-WIP\01-ARQ\LECG_RVT_DISEÑO (ANTEPROYECTO).rvt`
- Frozen SHA-256: `1ABDE76F3C94BB2401FB1FA537FA88759A717963D271560211FDEB6D4234B39A`
- Each operation receives a fresh detached `disposable.rvt` copy.
- The source is never opened as the writable test document and is re-hashed
  before reconciliation.
- Every inner transaction is followed by transaction-group rollback, restoration
  checks, document close without save, copy deletion, and source-hash verification.
- Any isolation or cleanup failure aborts the batch. No family document is opened.

Only a committed, changed read-back with verified rollback can increase the
667/805 numerator. Missing targets and Revit rejections remain evidence only.
