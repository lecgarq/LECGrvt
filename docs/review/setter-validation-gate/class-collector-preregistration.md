# Class-collector setter batch — frozen before execution

Run the exact 18 `collector_by_class` operations from
`elementid-classification.csv`. `Material.CutBackgroundPatternId` is the existing
changed-value positive control; it may confirm the harness but cannot add a new
ledger identity. The other 17 are the complete unvalidated class-collector scope.

For each operation, inspect the unchanged 12-model Autodesk sample corpus in the
manifest order. Start from the lowest target ElementId and choose the lowest
different eligible candidate ElementId. Stop after the first validated write. A
candidate generator refusal is a measured result and does not authorize creating a
fixture, changing a related property, trying undocumented sentinels, or repeatedly
probing writes. Each operation/model attempt uses a fresh detached disposable copy,
never saves it, and retains the writable-snapshot rollback checks already qualified.

Candidate sets are fixed as follows:

| Property (`Autodesk.Revit.DB.` omitted) | Eligible candidate set |
| --- | --- |
| Analysis.MassLevelData.MaterialId | Existing `Material` elements |
| Electrical.CircuitNamingSchemeSettings.CircuitNamingSchemeId | Existing `CircuitNamingScheme` elements accepted by `IsValidCircuitNamingSchemeId` |
| FilledRegionType.BackgroundPatternId | Existing drafting `FillPatternElement` values accepted by `IsValidBackgroundPatternId` |
| MEPSystemType.FillPatternId | Existing `FillPatternElement` values |
| MEPSystemType.LinePatternId | Existing `LinePatternElement` values |
| MEPSystemType.MaterialId | Existing `Material` elements |
| Material.CutBackgroundPatternId | Existing drafting `FillPatternElement` values |
| Material.SurfaceBackgroundPatternId | Existing drafting `FillPatternElement` values |
| MultiReferenceAnnotationType.DimensionStyleId | Existing linear `DimensionType` values accepted by `IsAllowedDimensionStyle` |
| Structure.FabricSheetType.Material | Existing `Material` elements |
| Structure.RebarBendingDetailType.AngularDimensionTypeId | Existing angular `DimensionType` values |
| Structure.RebarBendingDetailType.DiameterDimensionTypeId | Existing diameter `DimensionType` values |
| Structure.RebarBendingDetailType.RadialDimensionTypeId | Existing radial `DimensionType` values |
| Structure.RebarBendingDetailType.SegmentLengthDimensionTypeId | Existing linear `DimensionType` values |
| View.AnalysisDisplayStyleId | Existing `AnalysisDisplayStyle` elements; view applicability remains a live setter gate |
| ViewSheet.SheetCollectionId | Existing `SheetCollection` elements; exclude assembly sheets |
| ViewSheetSet.SheetOrganizationId | Existing sheet `BrowserOrganization` elements; require target `IsAutomatic == true` |
| ViewSheetSet.ViewOrganizationId | Existing view `BrowserOrganization` elements; require target `IsAutomatic == true` |

Every accepted result requires a committed changed value matching the requested
ElementId, followed by successful outer-group rollback and observable restoration.
The restoration scope and its stated limits remain exactly those in
`writable-snapshot-amendment.md`. Abort the remaining batch after any isolation,
cleanup, source-hash, manifest, binary, or restoration failure.

Report actual attempts, refusals, and yield after execution. Merge one strongest
outcome per operation and source-model hash. Keep all historical receipts unchanged,
retain the 805 denominator, and link the reconciliation from the ledger by SHA256.
