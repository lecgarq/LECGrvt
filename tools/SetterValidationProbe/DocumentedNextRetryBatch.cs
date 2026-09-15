using System.IO;
using System.Text.Json;
using NUnit.Framework;

namespace LECG.SetterValidationProbe;

public sealed class DocumentedNextRetryBatch : SetterHarness
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
    {
        var plan = Manifest.RootElement.GetProperty("cases").EnumerateArray()
            .Single(item => item.GetProperty("property").GetString() == property);
        var attempts = new List<object>();
        foreach (var model in plan.GetProperty("models").EnumerateArray())
        {
            UseModel(model.GetString()!);
            Record(property);
            attempts.Add(new { model = model.GetString(), status = LastReceipt["status"],
                reason = LastReceipt.GetValueOrDefault("reason") });
            if ((string)LastReceipt["status"]! == "validated") break;
        }
        File.WriteAllText(Path.Combine(CaseDirectory(property), "case-summary.json"),
            JsonSerializer.Serialize(new {
                operation = "api.set:Autodesk.Revit.DB." + property, attempts,
                models_planned = plan.GetProperty("models").GetArrayLength(),
                models_attempted = attempts.Count
            }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
