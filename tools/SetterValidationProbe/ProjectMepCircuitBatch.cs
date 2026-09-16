using NUnit.Framework;

namespace LECG.SetterValidationProbe;

public sealed class ProjectMepCircuitBatch : PlannedSetterBatch
{
    protected override string ManifestName => "project-mep-circuit-manifest.json";
    protected override string PreregistrationName => "project-mep-circuit-preregistration.md";
    protected override string RunKind => "project-mep-circuit-runs";

    [TestCase("Electrical.ElectricalSystem.CircuitConnectionType")]
    public void RunProjectMepCircuit(string property) => RunPlanned(property);
}
