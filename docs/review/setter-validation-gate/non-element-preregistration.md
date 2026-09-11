# Non-ElementId setter batch — frozen before execution

Run the exact 14 unresolved non-ElementId setter operations listed below against
the unchanged 12-model Autodesk Revit 2026 sample corpus, in manifest order.
Start from the lowest target ElementId in each model and stop after the first
committed changed value. A candidate refusal or setter rejection is a measured
result. It does not authorize a second write in the same model, creating a
fixture, changing a related property, or inventing an undocumented sentinel.

Each operation/model attempt uses a fresh detached disposable copy, never saves
it, and retains the qualified writable-snapshot rollback checks. Run and TRX
directories use the UTC second (`yyyyMMddTHHmmss`) without a GUID so evidence
paths remain below the Windows checkout limit. A pre-existing timestamp directory
is an infrastructure failure rather than permission to merge evidence.

| Property (`Autodesk.Revit.DB.` omitted) | Frozen candidate rule |
| --- | --- |
| Analysis.HVACLoadBuildingType.ClosingTime | Use `16:30`, or `04:30` when already `16:30`; both formats are documented as valid. |
| Analysis.HVACLoadBuildingType.OpeningTime | Use `04:30`, or `16:30` when already `04:30`; both formats are documented as valid. |
| Analysis.MassLevelData.ConceptualConstructionIsByEnergyData | Negate the existing Boolean. |
| Analysis.PathOfTravel.PathEnd | For an ungrouped path, extend the end one existing start-to-end vector length; retain the source Z coordinate and require Revit length limits. |
| Analysis.PathOfTravel.PathStart | For an ungrouped path, extend the start one existing end-to-start vector length; retain the source Z coordinate and require Revit length limits. |
| ColorFillLegend.Origin | Move one foot along the owner view's `RightDirection`, which lies on that view plane. |
| Electrical.CableTray.CurveNormal | Negate the existing unit up-direction vector. |
| Family.StructuralCodeName | Append ` LECG` to the existing non-null string. |
| Family.StructuralFamilyNameKey | Append ` LECG` to the existing non-null string. |
| FamilyInstance.IsWorkPlaneFlipped | Negate only when `CanFlipWorkPlane` is true. |
| ImageInstance.EnableSnaps | Negate only when `CanHaveSnaps` is true. |
| ReferencePlane.FreeEnd | Extend the free end one existing bubble-to-free vector length, preserving perpendicularity to the normal. |
| SiteLocation.PlaceName | Append ` LECG` to the existing non-null string. |
| Structure.ReinforcementSettings.RebarVaryingLengthNumberSuffix | Use `A`, or `B` when already `A`; both satisfy the documented one-to-six alphabetic-character rule. |

All XYZ candidates must be finite, different by Revit's geometric comparator,
and within Revit length limits. Every accepted result requires a committed value
matching the candidate, successful outer-group rollback, observable restoration,
source-hash preservation, unsaved-copy preservation, and cleanup. Abort the batch
after any isolation, manifest, binary, source-hash, restoration, or cleanup failure.

Report every attempt and refusal. Merge one strongest outcome per operation and
source-model hash. Preserve the 805-operation denominator and link the ledger
update to a reconciliation file and all receipts by SHA256.
