using System.IO;
using System.Text.Json;
using NUnit.Framework;

namespace LECG.SetterValidationProbe;

public sealed class NonElementBatch : SetterHarness
{
    protected override string ManifestName => "non-element-manifest.json";
    protected override string PreregistrationName => "non-element-preregistration.md";
    protected override string RunKind => "non-element-runs";

    [TestCase("Analysis.HVACLoadBuildingType.ClosingTime")]
    [TestCase("Analysis.HVACLoadBuildingType.OpeningTime")]
    [TestCase("Analysis.MassLevelData.ConceptualConstructionIsByEnergyData")]
    [TestCase("Analysis.PathOfTravel.PathEnd")]
    [TestCase("Analysis.PathOfTravel.PathStart")]
    [TestCase("ColorFillLegend.Origin")]
    [TestCase("Electrical.CableTray.CurveNormal")]
    [TestCase("Family.StructuralCodeName")]
    [TestCase("Family.StructuralFamilyNameKey")]
    [TestCase("FamilyInstance.IsWorkPlaneFlipped")]
    [TestCase("ImageInstance.EnableSnaps")]
    [TestCase("ReferencePlane.FreeEnd")]
    [TestCase("SiteLocation.PlaceName")]
    [TestCase("Structure.ReinforcementSettings.RebarVaryingLengthNumberSuffix")]
    public void RunNonElement(string property)
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
        File.WriteAllText(Path.Combine(CaseDirectory(property), "case-summary.json"),
            JsonSerializer.Serialize(new {
                operation = "api.set:Autodesk.Revit.DB." + property, attempts,
                models_planned = plan.GetProperty("models").GetArrayLength(),
                models_attempted = attempts.Count
            }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
