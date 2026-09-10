using System.IO;
using System.Text.Json;
using NUnit.Framework;

namespace LECG.SetterValidationProbe;

public sealed class ClassCollectorBatch : SetterHarness
{
    protected override string ManifestName => "class-collector-manifest.json";
    protected override string PreregistrationName => "class-collector-preregistration.md";
    protected override string RunKind => "class-collector-runs";

    [TestCase("Analysis.MassLevelData.MaterialId")]
    [TestCase("Electrical.CircuitNamingSchemeSettings.CircuitNamingSchemeId")]
    [TestCase("FilledRegionType.BackgroundPatternId")]
    [TestCase("MEPSystemType.FillPatternId")]
    [TestCase("MEPSystemType.LinePatternId")]
    [TestCase("MEPSystemType.MaterialId")]
    [TestCase("Material.CutBackgroundPatternId")]
    [TestCase("Material.SurfaceBackgroundPatternId")]
    [TestCase("MultiReferenceAnnotationType.DimensionStyleId")]
    [TestCase("Structure.FabricSheetType.Material")]
    [TestCase("Structure.RebarBendingDetailType.AngularDimensionTypeId")]
    [TestCase("Structure.RebarBendingDetailType.DiameterDimensionTypeId")]
    [TestCase("Structure.RebarBendingDetailType.RadialDimensionTypeId")]
    [TestCase("Structure.RebarBendingDetailType.SegmentLengthDimensionTypeId")]
    [TestCase("View.AnalysisDisplayStyleId")]
    [TestCase("ViewSheet.SheetCollectionId")]
    [TestCase("ViewSheetSet.SheetOrganizationId")]
    [TestCase("ViewSheetSet.ViewOrganizationId")]
    public void RunClassCollector(string property)
    {
        var plan = Manifest.RootElement.GetProperty("cases").EnumerateArray()
            .Single(c => c.GetProperty("property").GetString() == property);
        var attempts = new List<object>();
        foreach (var model in plan.GetProperty("models").EnumerateArray())
        {
            UseModel(model.GetString()!);
            Record(property);
            attempts.Add(new { model = model.GetString(), status = LastReceipt["status"],
                reason = LastReceipt.GetValueOrDefault("reason") });
            if ((string)LastReceipt["status"]! == "validated") break;
        }
        File.WriteAllText(Path.Combine(CaseDirectory(property), "case-summary.json"), JsonSerializer.Serialize(new {
            operation = "api.set:Autodesk.Revit.DB." + property, attempts,
            models_planned = plan.GetProperty("models").GetArrayLength(), models_attempted = attempts.Count
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
