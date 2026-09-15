# CASA EUCALIPTO remaining project-gap batch

## Purpose

Add previously unmeasured project/model pairs for three remaining setters whose
value generators are based on Autodesk API eligibility data. No existing project
receipt is repeated in this plan.

| Setter | Disposable project copies, in order |
| --- | --- |
| `FloorType.StructuralMaterialId` | architecture, topography, MEP |
| `Mechanical.MEPHiddenLineSettings.LineStyle` | architecture, topography, structure |
| `View.ViewPositionId` | architecture, topography |

Expected yield before execution: **0–3 validations**. Existing-model availability
does not prove that an alternative eligible value exists, so zero is admissible.

## Isolation

- Each attempt uses a fresh detached `disposable.rvt` copy under its evidence folder.
- A source model is never opened as the writable test document.
- The harness rolls back, verifies restoration, closes without save, deletes the
  copy, and verifies the frozen source hash after every attempt.
- The run aborts on any isolation or cleanup failure. Family documents remain
  outside the contract.

Only committed changed read-back with verified rollback can increase 668/805.
