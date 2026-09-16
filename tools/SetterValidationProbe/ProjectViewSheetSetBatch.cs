using NUnit.Framework;

namespace LECG.SetterValidationProbe;

public sealed class ProjectViewSheetSetBatch : PlannedSetterBatch
{
    protected override string ManifestName => "project-view-sheet-set-manifest.json";
    protected override string PreregistrationName => "project-view-sheet-set-preregistration.md";
    protected override string RunKind => "project-view-sheet-set-runs";
    protected override bool PersistenceProbe => true;

    [TestCase("ViewSheetSet.IsAutomatic")]
    [TestCase("ViewSheetSet.SheetOrganizationId")]
    [TestCase("ViewSheetSet.ViewOrganizationId")]
    public void ClassifyPersistence(string property) => RunPlanned(property);
}
