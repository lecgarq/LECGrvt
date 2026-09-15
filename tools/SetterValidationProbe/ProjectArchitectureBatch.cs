using NUnit.Framework;

namespace LECG.SetterValidationProbe;

public sealed class ProjectArchitectureBatch : PlannedSetterBatch
{
    protected override string ManifestName => "project-architecture-manifest.json";
    protected override string PreregistrationName => "project-architecture-preregistration.md";
    protected override string RunKind => "project-architecture-runs";

    [TestCase("AssemblyInstance.NamingCategoryId")]
    [TestCase("Architecture.StairsLanding.BaseElevation")]
    [TestCase("Architecture.StairsRun.BaseElevation")]
    [TestCase("Architecture.StairsRun.TopElevation")]
    public void RunProjectArchitecture(string property) => RunPlanned(property);
}
