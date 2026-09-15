using System.IO;
using System.Text.Json;

namespace LECG.SetterValidationProbe;

public abstract class PlannedSetterBatch : SetterHarness
{
    protected void RunPlanned(string property)
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
