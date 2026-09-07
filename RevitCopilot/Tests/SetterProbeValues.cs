using System.Globalization;
using Autodesk.Revit.DB;
using LECG.RevitCopilot.Agent;

namespace LECG.RevitCopilot.SmokeTests;

// Test-only value policy. It never becomes an agent tool or a production default.
internal static class SetterProbeValues
{
    internal static IEnumerable<object> Alternatives(Document doc, Element target, ApiPropertyBinding binding, object? before)
    {
        Type type = binding.Property.PropertyType;
        string name = binding.Property.Name;
        if (name is "CreatedPhaseId" or "DemolishedPhaseId")
        {
            foreach (Phase phase in doc.Phases)
                if (name == "CreatedPhaseId" ? target.IsPhaseCreatedValid(phase.Id) : target.IsPhaseDemolishedValid(phase.Id)) yield return phase.Id;
            yield break;
        }
        if (before is Color { IsValid: false }) { yield return new Color(80, 100, 120); yield break; }
        if (before is null && type == typeof(string)) { yield return "LECG_PROBE_" + Guid.NewGuid().ToString("N")[..8]; yield break; }
        if (before is bool flag) { yield return !flag; yield break; }
        if (type.IsEnum)
        {
            foreach (object value in Enum.GetValues(type).Cast<object>().Distinct().Where(v => !v.Equals(before)).Take(3)) yield return value;
            yield break;
        }
        if (before is Color color && color.IsValid)
        {
            yield return new Color((byte)(color.Red == 127 ? 128 : 127), color.Green, color.Blue);
            yield break;
        }
        if (before is string text)
        {
            // These represent keys, sizes or time formats, rather than free text.
            if (name is "OpeningTime" or "ClosingTime" or "MaxSize" or "StructuralFamilyNameKey" or "StructuralCodeName" or "PlaceName") yield break;
            yield return text[..Math.Min(text.Length, 3700)] + " LECG_PROBE_" + Guid.NewGuid().ToString("N")[..8];
            yield break;
        }
        if (before is ElementId id)
        {
            Element? current = doc.GetElement(id);
            if (current is null) yield break; // Never guess meanings of negative sentinels or missing IDs.
            // Restrict candidates to the same exact API class and category; the property setter decides compatibility.
            foreach (Element other in new FilteredElementCollector(doc).WherePasses(new ElementIsElementTypeFilter(current is not ElementType)).Where(e => e.GetType() == current.GetType()
                && e.Category?.Id == current.Category?.Id && e.Id != id).Take(3)) yield return other.Id;
            yield break;
        }
        if (before is XYZ xyz)
        {
            if (xyz.GetLength() <= 1e-9) { yield return new XYZ(0.001, 0, 0); yield break; }
            // Relative perturbation retains the existing native dimensions; no guessed force/length conversion.
            if (xyz.GetLength() > 1e-9 && xyz.GetLength() < 1e9)
                yield return name.Contains("Normal", StringComparison.Ordinal) || name.Contains("Direction", StringComparison.Ordinal)
                    ? Transform.CreateRotation(XYZ.BasisZ, 0.001).OfVector(xyz) : xyz * 1.001;
            yield break;
        }
        if (before is double or float)
        {
            double value = Convert.ToDouble(before, CultureInfo.InvariantCulture);
            if (value == 0)
            {
                // Native-unit boundary probes on disposable copies only; no claim about design suitability.
                yield return Convert.ChangeType(0.001, type, CultureInfo.InvariantCulture);
                yield return Convert.ChangeType(1.0, type, CultureInfo.InvariantCulture);
                yield break;
            }
            if (double.IsFinite(value) && Math.Abs(value) > 1e-7 && Math.Abs(value) < 1e9)
            {
                yield return Convert.ChangeType(value * 1.001, type, CultureInfo.InvariantCulture);
                yield return Convert.ChangeType(value * 0.999, type, CultureInfo.InvariantCulture);
            }
            yield break;
        }
        if (before is int number)
        {
            if (binding.Property.DeclaringType!.Name == "FabricationPart" || name.EndsWith("Index", StringComparison.Ordinal) || name.EndsWith("Idx", StringComparison.Ordinal)) yield break;
            if (name is "LineWeight") { yield return number == 2 ? 3 : 2; yield break; }
            if (name is "Transparency" or "Smoothness" or "Shininess" or "ShadowIntensity" or "SunlightIntensity")
            { yield return number == 50 ? 51 : 50; yield break; }
            if (number is >= 0 and < 1000) yield return number + 1;
        }
    }

    internal static object Wire(Document doc, object value) => value switch
    {
        ElementId id when id.Value < 0 => new { builtin_id = id.Value },
        ElementId id when doc.GetElement(id) is { } element => new { unique_id = element.UniqueId },
        ElementId => throw new ArgumentException("Cannot resolve the reference in this disposable document."),
        XYZ xyz => new { x = xyz.X, y = xyz.Y, z = xyz.Z },
        Color c when c.IsValid => new { red = c.Red, green = c.Green, blue = c.Blue },
        Color => throw new ArgumentException("Invalid color has no wire representation."),
        Enum e => e.ToString(),
        _ => value
    };

    internal static bool Equal(object? left, object? right) => (left, right) switch
    {
        (double a, double b) => Math.Abs(a - b) <= Math.Max(1e-9, Math.Abs(a) * 1e-9),
        (float a, float b) => Math.Abs(a - b) <= Math.Max(1e-6, Math.Abs(a) * 1e-6),
        (XYZ a, XYZ b) => a.IsAlmostEqualTo(b, 1e-8),
        (Color a, Color b) => a.IsValid == b.IsValid && (!a.IsValid || (a.Red == b.Red && a.Green == b.Green && a.Blue == b.Blue)),
        (ElementId a, ElementId b) => a == b,
        _ => Equals(left, right)
    };
}
