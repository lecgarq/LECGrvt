# CASA EUCALIPTO project corpus — frozen before execution

Use the four user-authorized CASA EUCALIPTO Revit files as read-only validation
fixtures for the 11 generator-backed setters that remain unresolved after the
completed 20260911T020232 non-ElementId run. This run supplements the Autodesk
sample corpus; it does not replace or weaken earlier evidence.

The supplied paths contained an extra `_RVT` directory and leading underscore.
The manifest records the four files actually present below the CASA EUCALIPTO
`Project Files` root, with exact SHA256, byte length, modification time and discipline.
Resolve relative paths only beneath `LECG_PROJECT_FIXTURE_ROOT`. Never accept an
absolute manifest path, `..` escape or a non-RVT file.

Every operation/model attempt copies the source to the evidence directory, opens
only that disposable copy detached from central, unloads Revit/CAD links through
TransmissionData, performs at most one generated write, commits and reads fresh,
then rolls the transaction group back and closes without saving. Re-hash the source
and unsaved copy at cleanup. Never edit, save, synchronize, transmit or compact the
ACCDocs original. Abort the entire batch after any isolation, source-hash, rollback,
cleanup, manifest or binary failure.

Use the existing documented candidate rules without modification. Model order is
discipline-directed and frozen per case. Stop after the first validated model;
otherwise exhaust all four. A missing target or no proven alternative is evidence,
not permission to create fixtures or change related properties.

| Property (`Autodesk.Revit.DB.` omitted) | Frozen model order |
| --- | --- |
| Analysis.MassLevelData.ConceptualConstructionIsByEnergyData | architecture, topography, structure, MEP |
| ImageInstance.EnableSnaps | architecture, topography, structure, MEP |
| Electrical.CircuitNamingSchemeSettings.CircuitNamingSchemeId | MEP, architecture, structure, topography |
| Electrical.ElectricalSystem.CircuitConnectionType | MEP, architecture, structure, topography |
| Part.OriginalCategoryId | architecture, topography, structure, MEP |
| Structure.StructuralConnectionHandler.ApprovalTypeId | structure, architecture, MEP, topography |
| View.AnalysisDisplayStyleId | architecture, topography, structure, MEP |
| ViewSheet.SheetCollectionId | architecture, structure, MEP, topography |
| ViewSheetSet.IsAutomatic | architecture, structure, MEP, topography |
| ViewSheetSet.SheetOrganizationId | architecture, structure, MEP, topography |
| ViewSheetSet.ViewOrganizationId | architecture, structure, MEP, topography |

`ViewSheetSet.IsAutomatic` is a classification check only: prior installed-metadata
and live evidence show a proxy-local setter that did not persist through a fresh
wrapper, and `ViewSheetSetting.Save()` failed in the tested context. It may record
same-value or rejection evidence but cannot receive standalone setter credit unless
fresh read-back equals the requested value under the existing general protocol.

The manifest generator must assert exactly 11 project cases. The completed
non-ElementId run accounts for 14 cases, 12 of which validated; these project cases
are the 11 generator-backed unresolved identities remaining after those successes.
Preserve the 805 denominator and reconcile by
distinct source-model hash. Project validation establishes only the named binary,
revision, model hash and context—not universal correctness or MCP permission.
