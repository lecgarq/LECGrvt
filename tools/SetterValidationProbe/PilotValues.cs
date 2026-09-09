using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;

namespace LECG.SetterValidationProbe;

// Only the eight preregistered policies. No generic same-class ElementId swap.
internal static class PilotValues
{
    internal static Element[] Elements(Document doc) => new FilteredElementCollector(doc)
        .WherePasses(new LogicalOrFilter(new ElementIsElementTypeFilter(false), new ElementIsElementTypeFilter(true)))
        .ToElements().OrderBy(e => e.Id.Value).ToArray();

    internal static object? Candidate(Element target, string property, object before)
    {
        Document doc = target.Document;
        switch (property)
        {
            case "Electrical.CableType.ConductorMaterial":
                return ConductorMaterial.GetConductorMaterialIds(doc).OrderBy(id => id.Value)
                    .FirstOrDefault(id => id.Value != ((ElementId)before).Value);
            case "Material.CutBackgroundPatternId":
                return new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>()
                    .Where(p => p.GetFillPattern().Target == FillPatternTarget.Drafting && p.Id.Value != ((ElementId)before).Value)
                    .OrderBy(p => p.Id.Value).Select(p => p.Id).FirstOrDefault();
            case "TextElement.Text":
                if (target is not TextNote) return null;
                string text = (string)before;
                // Preserve the existing paragraph terminator, without an equality normalization shortcut.
                int end = text.Length;
                while (end > 0 && text[end - 1] is '\r' or '\n') end--;
                return text.Insert(end, " [LECG changed-value pilot]");
            case "Plumbing.PipingSystemType.FluidTemperature":
                if (doc.GetElement(((PipingSystemType)target).FluidType) is not FluidType fluid) return null;
                var temperatures = new List<double>();
                using (var iterator = fluid.GetFluidTemperatureSetIterator())
                    while (iterator.MoveNext()) temperatures.Add(iterator.Current.Temperature);
                return temperatures.Where(double.IsFinite).Order().Cast<double?>().FirstOrDefault(v => v != (double)before);
            case "Structure.LoadCase.Number":
                var used = Elements(doc).OfType<LoadCase>().Select(c => c.Number).ToHashSet();
                for (int n = 1; n < int.MaxValue; n++) if (!used.Contains(n)) return n;
                return null;
            case "ReferencePlane.BubbleEnd":
                var plane = (ReferencePlane)target;
                XYZ point = (XYZ)before + ((XYZ)before - plane.FreeEnd);
                return XYZ.IsWithinLengthLimits(point) && !point.IsAlmostEqualTo(plane.FreeEnd)
                    && !point.IsAlmostEqualTo((XYZ)before) ? point : null;
            case "ViewSheetSet.IsAutomatic": return !(bool)before;
            case "Electrical.ElectricalSystem.CircuitConnectionType":
                var circuit = (ElectricalSystem)target;
                if (circuit.BaseEquipment is null) return null;
                if ((CircuitConnectionType)before == CircuitConnectionType.FeedThruLugs) return CircuitConnectionType.Breaker;
                if ((CircuitConnectionType)before == CircuitConnectionType.Breaker &&
                    circuit.BaseEquipment.get_Parameter(BuiltInParameter.RBS_ELEC_PANEL_FEED_THRU_LUGS_PARAM)?.AsInteger() == 1)
                    return CircuitConnectionType.FeedThruLugs;
                return null;
            default: throw new InvalidOperationException("Property is outside the eight-case pilot.");
        }
    }

    internal static bool Equal(object? a, object? b) => (a, b) switch
    {
        (ElementId x, ElementId y) => x.Value == y.Value,
        (XYZ x, XYZ y) => x.IsAlmostEqualTo(y), // Revit's geometric comparator; no invented shared epsilon.
        (double x, double y) => double.IsFinite(x) && double.IsFinite(y) && x == y,
        _ => Equals(a, b) // Ordinal string and exact bool, integer and enum identity.
    };

    internal static object Snapshot(object? value) => value switch
    {
        ElementId id => id.Value,
        XYZ xyz => new[] { xyz.X, xyz.Y, xyz.Z },
        Enum e => new { name = e.ToString(), value = Convert.ToInt64(e) },
        null => "<null>",
        _ => value
    };
}
