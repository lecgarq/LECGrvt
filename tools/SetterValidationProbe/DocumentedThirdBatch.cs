using NUnit.Framework;

namespace LECG.SetterValidationProbe;

public sealed class DocumentedThirdBatch : PlannedSetterBatch
{
    protected override string ManifestName => "documented-third-manifest.json";
    protected override string PreregistrationName => "documented-third-preregistration.md";
    protected override string RunKind => "documented-third-runs";

    [TestCase("Architecture.ContinuousRailType.EndOrTopTermination")]
    [TestCase("Architecture.ContinuousRailType.StartOrBottomTermination")]
    [TestCase("FamilyInstance.StructuralUsage")]
    [TestCase("FloorType.StructuralMaterialId")]
    [TestCase("Mechanical.MEPHiddenLineSettings.LineStyle")]
    [TestCase("Electrical.ElectricalSystem.CircuitConnectionType")]
    public void RunDocumentedThird(string property) => RunPlanned(property);
}
