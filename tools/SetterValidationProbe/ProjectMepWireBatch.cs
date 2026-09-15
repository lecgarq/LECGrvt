using NUnit.Framework;

namespace LECG.SetterValidationProbe;

public sealed class ProjectMepWireBatch : PlannedSetterBatch
{
    protected override string ManifestName => "project-mep-wire-manifest.json";
    protected override string PreregistrationName => "project-mep-wire-preregistration.md";
    protected override string RunKind => "project-mep-wire-runs";

    [TestCase("Electrical.WireType.MaxSize")]
    public void RunProjectMepWire(string property) => RunPlanned(property);
}
