using System.IO;
using System.Text.Json;
using NUnit.Framework;

namespace LECG.SetterValidationProbe;

public sealed class DedicatedCollectionBatch : SetterHarness
{
    protected override string ManifestName => "dedicated-writable-manifest.json";
    protected override string PreregistrationName => "dedicated-preregistration.md";
    protected override string RunKind => "dedicated-runs";

    [TestCase("Analysis.MassLevelData.ConceptualConstructionId")]
    [TestCase("Architecture.StairsRunType.NosingProfile")]
    [TestCase("Architecture.StairsRunType.RiserProfile")]
    [TestCase("Architecture.StairsRunType.TreadProfile")]
    [TestCase("Electrical.CableType.ConductorMaterial")]
    [TestCase("Electrical.CableType.InsulationMaterial")]
    [TestCase("Electrical.CableType.TemperatureRating")]
    [TestCase("Electrical.ElectricalSystem.CableSize")]
    [TestCase("Electrical.WireType.Insulation")]
    [TestCase("Electrical.WireType.TemperatureRating")]
    [TestCase("Electrical.WireType.WireMaterial")]
    [TestCase("Part.OriginalCategoryId")]
    [TestCase("Structure.FabricArea.TagViewId")]
    [TestCase("Structure.StructuralConnectionHandler.ApprovalTypeId")]
    public void RunDedicated(string property)
    {
        var plan = Manifest.RootElement.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("property").GetString() == property);
        var attempts = new List<object>();
        foreach (var model in plan.GetProperty("models").EnumerateArray())
        {
            UseModel(model.GetString()!);
            Record(property);
            attempts.Add(new { model = model.GetString(), status = LastReceipt["status"], reason = LastReceipt.GetValueOrDefault("reason") });
            if ((string)LastReceipt["status"]! == "validated") break;
        }
        File.WriteAllText(Path.Combine(CaseDirectory(property), "case-summary.json"), JsonSerializer.Serialize(new {
            operation = "api.set:Autodesk.Revit.DB." + property, attempts,
            historical_reachable_models = plan.GetProperty("models").GetArrayLength(), models_attempted = attempts.Count
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed class AutomaticPersistenceProbe : SetterHarness
{
    protected override string ManifestName => "dedicated-manifest.json";
    protected override string PreregistrationName => "dedicated-preregistration.md";
    protected override string RunKind => "automatic-runs";
    protected override bool PersistenceProbe => true;

    [Test]
    public void ProbePersistence() => Record("ViewSheetSet.IsAutomatic");
}

public sealed class RestorationDiagnostic : SetterHarness
{
    protected override string ManifestName => "dedicated-writable-manifest.json";
    protected override string PreregistrationName => "dedicated-preregistration.md";
    protected override string RunKind => "restoration-runs";
    protected override long? RestorationWatchId => 1462965;

    [Test]
    public void InspectFailedRestoration() => Run(false);

    [Test]
    public void InspectNoWriteRestoration() => Run(true);

    [Test]
    public void InspectParameterReadOnly()
    {
        InspectReadOnly = true;
        try { Run(true); }
        finally { InspectReadOnly = false; }
    }

    private void Run(bool noWrite)
    {
        NoWriteControl = noWrite;
        var plan = Manifest.RootElement.GetProperty("cases").EnumerateArray()
            .Single(c => c.GetProperty("property").GetString() == "Analysis.MassLevelData.ConceptualConstructionId");
        UseModel(plan.GetProperty("models")[0].GetString()!);
        Record("Analysis.MassLevelData.ConceptualConstructionId");
    }
}
