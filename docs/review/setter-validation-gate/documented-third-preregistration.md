# Documented changed-value batch 3 preregistration

## Scope and expected yield

Six remaining `same_value_only` setters with alternatives derived from Revit 2026 metadata or implementation. Expected yield before execution: **2-6 validated of 6**. No rejected or unchanged case counts as validated.

## Generators

- Continuous-rail terminations: choose a different `FamilySymbol` from `BuiltInCategory.OST_RailingTermination`, the API category for railing termination families.
- `FamilyInstance.StructuralUsage`: change any non-`Other` value to `Other`, which Revit's setter accepts for every structural type. From `Other`, use only the implementation-supported value for Beam, Brace, or Column.
- `FloorType.StructuralMaterialId`: choose another `Material` whose `StructuralAssetId` is valid.
- `MEPHiddenLineSettings.LineStyle`: choose another projection `GraphicsStyle` from the current line style's parent category.
- `ElectricalSystem.CircuitConnectionType`: when there is no base panel, choose the API-required `NotApplicable`; retain the proven Breaker/FeedThruLugs rules for connected panels.

## Corpus and isolation

Installed Autodesk samples are tried first. The CASA EUCALIPTO structural and MEP files are final fallbacks through their frozen relative paths. The known slow architecture project is excluded. Every attempt uses a detached disposable copy, rollback, close-without-save, source SHA-256 verification, and physical copy removal. Family-document writes remain outside the production contract.
