using NUnit.Framework;

namespace LECG.SetterValidationProbe;

public sealed class ProjectGapBatch : PlannedSetterBatch
{
    protected override string ManifestName => "project-gaps-manifest.json";
    protected override string PreregistrationName => "project-gaps-preregistration.md";
    protected override string RunKind => "project-gaps-runs";

    [TestCase("FloorType.StructuralMaterialId")]
    [TestCase("Mechanical.MEPHiddenLineSettings.LineStyle")]
    [TestCase("View.ViewPositionId")]
    public void RunProjectGaps(string property) => RunPlanned(property);
}
