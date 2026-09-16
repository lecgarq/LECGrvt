using NUnit.Framework;

namespace LECG.SetterValidationProbe;

public sealed class ProjectContextGapBatch : PlannedSetterBatch
{
    protected override string ManifestName => "project-context-gaps-manifest.json";
    protected override string PreregistrationName => "project-context-gaps-preregistration.md";
    protected override string RunKind => "project-context-gaps-runs";

    [TestCase("DisplacementPath.AncestorIdx")]
    [TestCase("TableView.TargetId")]
    public void RunProjectContextGaps(string property) => RunPlanned(property);
}
