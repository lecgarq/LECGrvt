using NUnit.Framework;

namespace LECG.SetterValidationProbe;

public sealed class DocumentedNextBatch : PlannedSetterBatch
{
    protected override string ManifestName => "documented-next-manifest.json";
    protected override string PreregistrationName => "documented-next-preregistration.md";
    protected override string RunKind => "documented-next-runs";

    [TestCase("Architecture.StairsLanding.BaseElevation")]
    [TestCase("Architecture.StairsRun.BaseElevation")]
    [TestCase("Architecture.StairsRun.TopElevation")]
    [TestCase("ScheduleSheetInstance.SegmentIndex")]
    [TestCase("Electrical.WireType.MaxSize")]
    public void RunDocumentedNext(string property)
        => RunPlanned(property);
}
