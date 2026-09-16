using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.DB.Analysis;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;

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
            case "Analysis.EnergyAnalysisDetailModel.ExportCategory":
            case "Analysis.EnergyDataSettings.ExportCategory":
                return Alternative(new[] {
                    new ElementId(BuiltInCategory.OST_Rooms),
                    new ElementId(BuiltInCategory.OST_MEPSpaces)
                }.Where(EnergyDataSettings.CheckExportCategory), before);
            case "AssemblyInstance.NamingCategoryId":
                var assembly = (AssemblyInstance)target;
                var members = assembly.GetMemberIds();
                return Alternative(members.Select(id => doc.GetElement(id)?.Category?.Id).OfType<ElementId>()
                    .DistinctBy(id => id.Value)
                    .Where(id => AssemblyInstance.IsValidNamingCategory(doc, id, members)), before);
            case "Structure.LoadCase.SubcategoryId":
                var loadCase = (LoadCase)target;
                return Alternative(doc.Settings.Categories.get_Item(BuiltInCategory.OST_LoadCases).SubCategories
                    .Cast<Category>().Select(category => category.Id).Where(loadCase.IsLoadCaseSubcategoryId), before);
            case "Architecture.ContinuousRailType.EndOrTopTermination":
            case "Architecture.ContinuousRailType.StartOrBottomTermination":
                return Alternative(new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_RailingTermination)
                    .Cast<FamilySymbol>().Select(symbol => symbol.Id), before);
            case "FamilyInstance.StructuralUsage":
                var familyInstance = (FamilyInstance)target;
                if ((StructuralInstanceUsage)before != StructuralInstanceUsage.Other)
                    return StructuralInstanceUsage.Other;
                return familyInstance.StructuralType switch
                {
                    StructuralType.Beam => StructuralInstanceUsage.Girder,
                    StructuralType.Brace => StructuralInstanceUsage.Brace,
                    StructuralType.Column => StructuralInstanceUsage.Column,
                    _ => null
                };
            case "FloorType.StructuralMaterialId":
                return Alternative(Collect<Material>(doc)
                    .Where(material => material.StructuralAssetId != ElementId.InvalidElementId)
                    .Select(material => material.Id), before);
            case "Mechanical.MEPHiddenLineSettings.LineStyle":
                if (doc.GetElement((ElementId)before) is not GraphicsStyle currentStyle
                    || currentStyle.GraphicsStyleCategory.Parent is not Category lineCategory) return null;
                return Alternative(lineCategory.SubCategories.Cast<Category>()
                    .Select(category => category.GetGraphicsStyle(GraphicsStyleType.Projection)?.Id)
                    .OfType<ElementId>(), before);
            case "Architecture.StairsLanding.BaseElevation":
                var landing = (StairsLanding)target;
                return StairElevation((double)before, landing.GetStairs().ActualRiserHeight, 30000.0);
            case "Architecture.StairsRun.BaseElevation":
                var baseRun = (StairsRun)target;
                double baseRiser = baseRun.GetStairs().ActualRiserHeight;
                double raisedBase = (double)before + baseRiser;
                if (ValidStep(baseRiser) && raisedBase <= 30000.0
                    && baseRun.TopElevation - raisedBase >= baseRiser) return raisedBase;
                double loweredBase = (double)before - baseRiser;
                return ValidStep(baseRiser) && loweredBase >= 0.0 ? loweredBase : null;
            case "Architecture.StairsRun.TopElevation":
                var topRun = (StairsRun)target;
                if (topRun.StairsRunStyle == StairsRunStyle.Sketched) return null;
                return StairElevation((double)before, topRun.GetStairs().ActualRiserHeight, 30000.0);
            case "ScheduleSheetInstance.SegmentIndex":
                var instance = (ScheduleSheetInstance)target;
                if (doc.GetElement(instance.ScheduleId) is not ViewSchedule schedule || !schedule.IsSplit()) return null;
                return Enumerable.Range(0, schedule.GetSegmentCount()).Cast<int?>()
                    .FirstOrDefault(index => index != (int)before);
            case "Electrical.WireType.MaxSize":
                return AlternativeConductorSizeName(doc, (string)before);
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
                return ((View)target).AllowsAnalysisDisplay()
                    ? Alternative(Collect<AnalysisDisplayStyle>(doc).Select(e => e.Id), before) : null;
            case "View.ViewPositionId":
                return Alternative(Collect<ViewPosition>(doc).Select(position => position.Id), before);
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
            case "ViewSheetSet.IsAutomatic": return (bool)before ? false : null;
            case "Electrical.ElectricalSystem.CircuitConnectionType":
                var circuit = (ElectricalSystem)target;
                if (circuit.BaseEquipment is null)
                    return (CircuitConnectionType)before == CircuitConnectionType.NotApplicable
                        ? null : CircuitConnectionType.NotApplicable;
                if ((CircuitConnectionType)before == CircuitConnectionType.FeedThruLugs) return CircuitConnectionType.Breaker;
                return null;
            case "Analysis.HVACLoadBuildingType.ClosingTime":
                return (string)before == "16:30" ? "04:30" : "16:30";
            case "Analysis.HVACLoadBuildingType.OpeningTime":
                return (string)before == "04:30" ? "16:30" : "04:30";
            case "Analysis.MassLevelData.ConceptualConstructionIsByEnergyData":
                return !(bool)before;
            case "Analysis.PathOfTravel.PathEnd":
                return PathPoint((PathOfTravel)target, (XYZ)before, ((PathOfTravel)target).PathStart);
            case "Analysis.PathOfTravel.PathStart":
                return PathPoint((PathOfTravel)target, (XYZ)before, ((PathOfTravel)target).PathEnd);
            case "ColorFillLegend.Origin":
                var view = doc.GetElement(target.OwnerViewId) as View;
                return view is null ? null : CheckedPoint((XYZ)before + view.RightDirection, (XYZ)before);
            case "Electrical.CableTray.CurveNormal":
                var normal = (XYZ)before;
                return normal.IsZeroLength() ? null : normal.Negate();
            case "Family.StructuralCodeName":
            case "Family.StructuralFamilyNameKey":
            case "SiteLocation.PlaceName":
                return (string)before + " LECG";
            case "FamilyInstance.IsWorkPlaneFlipped":
                return ((FamilyInstance)target).CanFlipWorkPlane ? !(bool)before : null;
            case "ImageInstance.EnableSnaps":
                return ((ImageInstance)target).CanHaveSnaps ? !(bool)before : null;
            case "ReferencePlane.FreeEnd":
                var referencePlane = (ReferencePlane)target;
                return CheckedPoint((XYZ)before + ((XYZ)before - referencePlane.BubbleEnd), (XYZ)before);
            case "Structure.ReinforcementSettings.RebarVaryingLengthNumberSuffix":
                return (string)before == "A" ? "B" : "A";
            default: throw new InvalidOperationException("Property is outside the preregistered cases.");
        }
    }

    private static double? StairElevation(double before, double riser, double limit)
    {
        double candidate = before + riser;
        return ValidStep(riser) && Math.Abs(candidate) <= limit ? candidate : null;
    }

    private static bool ValidStep(double riser) => double.IsFinite(riser) && riser > 0.0;

    private static string? AlternativeConductorSizeName(Document doc, string before)
    {
        foreach (var id in ConductorSize.GetConductorSizeIds(doc))
        {
            using var size = ConductorSize.GetConductorSize(doc, id);
            if (size.Name != before) return size.Name;
        }
        return null;
    }

    private static XYZ? PathPoint(PathOfTravel path, XYZ before, XYZ opposite)
    {
        if (path.GroupId != ElementId.InvalidElementId) return null;
        var point = new XYZ(before.X + before.X - opposite.X,
            before.Y + before.Y - opposite.Y, before.Z);
        return CheckedPoint(point, before);
    }

    private static XYZ? CheckedPoint(XYZ point, XYZ before) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z)
        && XYZ.IsWithinLengthLimits(point) && !point.IsAlmostEqualTo(before) ? point : null;

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
