using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.DB.Analysis;
using Autodesk.Revit.DB.Architecture;

namespace LECG.SetterValidationProbe;

// Preregistered pilot and dedicated-collection policies. No generic same-class ID swap.
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
            case "Electrical.WireType.WireMaterial":
                return ConductorMaterial.GetConductorMaterialIds(doc).OrderBy(id => id.Value)
                    .FirstOrDefault(id => id.Value != ((ElementId)before).Value);
            case "Electrical.CableType.InsulationMaterial":
            case "Electrical.WireType.Insulation":
                return Alternative(InsulationMaterial.GetInsulationMaterialIds(doc), before);
            case "Electrical.CableType.TemperatureRating":
            case "Electrical.WireType.TemperatureRating":
                return Alternative(TemperatureRating.GetTemperatureRatingIds(doc), before);
            case "Electrical.ElectricalSystem.CableSize":
                return doc.GetElement(((ElectricalSystem)target).CableType) is CableType cable
                    ? Alternative(cable.GetUsableCableSizeIds(), before) : null;
            case "Analysis.MassLevelData.ConceptualConstructionId":
                return Alternative(ConceptualConstructionType.GetAllConceptualConstructionsForCategory(doc, new ElementId(BuiltInCategory.OST_MassFloor)), before);
            case "Architecture.StairsRunType.NosingProfile":
                return ((StairsRunType)target).HasTreads ? Alternative(FamilyUtils.GetProfileSymbols(doc, ProfileFamilyUsage.StairNosing, true), before) : null;
            case "Architecture.StairsRunType.TreadProfile":
                return ((StairsRunType)target).HasTreads ? Alternative(FamilyUtils.GetProfileSymbols(doc, ProfileFamilyUsage.StairTread, true), before) : null;
            case "Architecture.StairsRunType.RiserProfile":
                return ((StairsRunType)target).HasRisers ? Alternative(FamilyUtils.GetProfileSymbols(doc, ProfileFamilyUsage.StairRiser, true), before) : null;
            case "Part.OriginalCategoryId": return Alternative(((Part)target).GetSourceElementOriginalCategoryIds(), before);
            case "Structure.FabricArea.TagViewId": return Alternative(((FabricArea)target).GetValidViewsForTags(), before);
            case "Structure.StructuralConnectionHandler.ApprovalTypeId":
                StructuralConnectionApprovalType.GetAllStructuralConnectionApprovalTypes(doc, out var approvals);
                return Alternative(approvals, before);
            case "Analysis.MassLevelData.MaterialId":
            case "MEPSystemType.MaterialId":
            case "Structure.FabricSheetType.Material":
                return Alternative(Collect<Material>(doc).Select(e => e.Id), before);
            case "Electrical.CircuitNamingSchemeSettings.CircuitNamingSchemeId":
                return Alternative(Collect<CircuitNamingScheme>(doc)
                    .Where(e => CircuitNamingSchemeSettings.IsValidCircuitNamingSchemeId(doc, e.Id))
                    .Select(e => e.Id), before);
            case "FilledRegionType.BackgroundPatternId":
                return Alternative(Collect<FillPatternElement>(doc)
                    .Where(e => e.GetFillPattern().Target == FillPatternTarget.Drafting
                        && ((FilledRegionType)target).IsValidBackgroundPatternId(e.Id))
                    .Select(e => e.Id), before);
            case "MEPSystemType.FillPatternId":
                return Alternative(Collect<FillPatternElement>(doc).Select(e => e.Id), before);
            case "MEPSystemType.LinePatternId":
                return Alternative(Collect<LinePatternElement>(doc).Select(e => e.Id), before);
            case "Material.CutBackgroundPatternId":
            case "Material.SurfaceBackgroundPatternId":
                return Alternative(Collect<FillPatternElement>(doc)
                    .Where(p => p.GetFillPattern().Target == FillPatternTarget.Drafting).Select(p => p.Id), before);
            case "MultiReferenceAnnotationType.DimensionStyleId":
                return Alternative(Collect<DimensionType>(doc)
                    .Where(d => d.StyleType == DimensionStyleType.Linear
                        && ((MultiReferenceAnnotationType)target).IsAllowedDimensionStyle(d.Id))
                    .Select(d => d.Id), before);
            case "Structure.RebarBendingDetailType.AngularDimensionTypeId":
                return DimensionStyle(doc, before, DimensionStyleType.Angular);
            case "Structure.RebarBendingDetailType.DiameterDimensionTypeId":
                return DimensionStyle(doc, before, DimensionStyleType.Diameter);
            case "Structure.RebarBendingDetailType.RadialDimensionTypeId":
                return DimensionStyle(doc, before, DimensionStyleType.Radial);
            case "Structure.RebarBendingDetailType.SegmentLengthDimensionTypeId":
                return DimensionStyle(doc, before, DimensionStyleType.Linear);
            case "View.AnalysisDisplayStyleId":
                return Alternative(Collect<AnalysisDisplayStyle>(doc).Select(e => e.Id), before);
            case "ViewSheet.SheetCollectionId":
                return ((ViewSheet)target).AssociatedAssemblyInstanceId == ElementId.InvalidElementId
                    ? Alternative(Collect<SheetCollection>(doc).Select(e => e.Id), before) : null;
            case "ViewSheetSet.SheetOrganizationId":
                return ((ViewSheetSet)target).IsAutomatic
                    ? Alternative(Collect<BrowserOrganization>(doc)
                        .Where(e => e.Type == BrowserOrganizationType.Sheets).Select(e => e.Id), before) : null;
            case "ViewSheetSet.ViewOrganizationId":
                return ((ViewSheetSet)target).IsAutomatic
                    ? Alternative(Collect<BrowserOrganization>(doc)
                        .Where(e => e.Type == BrowserOrganizationType.Views).Select(e => e.Id), before) : null;
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
            default: throw new InvalidOperationException("Property is outside the preregistered cases.");
        }
    }

    private static ElementId? Alternative(IEnumerable<ElementId> eligible, object before) => eligible
        .Where(id => id.Value != ((ElementId)before).Value).OrderBy(id => id.Value).FirstOrDefault();

    private static T[] Collect<T>(Document doc) where T : Element => new FilteredElementCollector(doc)
        .OfClass(typeof(T)).Cast<T>().OrderBy(e => e.Id.Value).ToArray();

    private static ElementId? DimensionStyle(Document doc, object before, DimensionStyleType style) =>
        Alternative(Collect<DimensionType>(doc).Where(d => d.StyleType == style).Select(d => d.Id), before);

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
