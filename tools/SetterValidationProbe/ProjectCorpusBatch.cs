using System.IO;
using System.Text.Json;
using NUnit.Framework;

namespace LECG.SetterValidationProbe;

public sealed class ProjectCorpusBatch : SetterHarness
{
    protected override string ManifestName => "project-corpus-manifest.json";
    protected override string PreregistrationName => "project-corpus-preregistration.md";
    protected override string RunKind => "project-corpus-runs";

    [TestCase("Analysis.MassLevelData.ConceptualConstructionIsByEnergyData")]
    [TestCase("ImageInstance.EnableSnaps")]
    [TestCase("Electrical.CircuitNamingSchemeSettings.CircuitNamingSchemeId")]
    [TestCase("Electrical.ElectricalSystem.CircuitConnectionType")]
    [TestCase("Part.OriginalCategoryId")]
    [TestCase("Structure.StructuralConnectionHandler.ApprovalTypeId")]
    [TestCase("View.AnalysisDisplayStyleId")]
    [TestCase("ViewSheet.SheetCollectionId")]
    [TestCase("ViewSheetSet.IsAutomatic")]
    [TestCase("ViewSheetSet.SheetOrganizationId")]
    [TestCase("ViewSheetSet.ViewOrganizationId")]
    public void RunProjectCorpus(string property)
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
