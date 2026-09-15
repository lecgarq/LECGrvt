using NUnit.Framework;

namespace LECG.SetterValidationProbe;

public sealed class DocumentedNextRetryBatch : PlannedSetterBatch
{
    protected override string ManifestName => "documented-next-retry-manifest.json";
    protected override string PreregistrationName => "documented-next-retry-preregistration.md";
    protected override string RunKind => "documented-next-retry-runs";

    [TestCase("Architecture.StairsLanding.BaseElevation")]
    [TestCase("Architecture.StairsRun.BaseElevation")]
    [TestCase("Architecture.StairsRun.TopElevation")]
    [TestCase("ScheduleSheetInstance.SegmentIndex")]
    [TestCase("Electrical.WireType.MaxSize")]
    public void RunDocumentedNextRetry(string property)
        => RunPlanned(property);
}
