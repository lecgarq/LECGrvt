using NUnit.Framework;

namespace LECG.SetterValidationProbe;

public sealed class DocumentedFourthBatch : PlannedSetterBatch
{
    protected override string ManifestName => "documented-fourth-manifest.json";
    protected override string PreregistrationName => "documented-fourth-preregistration.md";
    protected override string RunKind => "documented-fourth-runs";

    [TestCase("View.AnalysisDisplayStyleId")]
    [TestCase("View.ViewPositionId")]
    [TestCase("ViewSheet.SheetCollectionId")]
    [TestCase("ViewSheetSet.IsAutomatic")]
    [TestCase("ViewSheetSet.SheetOrganizationId")]
    [TestCase("ViewSheetSet.ViewOrganizationId")]
    public void RunDocumentedFourth(string property) => RunPlanned(property);
}
