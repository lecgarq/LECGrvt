using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Electrical;

namespace LECG.RevitCopilot.Revit;

internal sealed partial class ToolExecutor
{
    private static string? ContextReadRestriction(string operation, Element target)
    {
        if (target is RailingType railing)
        {
            if (operation is "api.get:Autodesk.Revit.DB.Architecture.RailingType.PrimaryHandrailHeight" or "api.get:Autodesk.Revit.DB.Architecture.RailingType.PrimaryHandrailLateralOffset"
                && railing.PrimaryHandrailType == ElementId.InvalidElementId) return "No primary handrail is assigned to this railing type.";
            if (operation is "api.get:Autodesk.Revit.DB.Architecture.RailingType.SecondaryHandrailHeight" or "api.get:Autodesk.Revit.DB.Architecture.RailingType.SecondaryHandrailLateralOffset"
                && railing.SecondaryHandrailType == ElementId.InvalidElementId) return "No secondary handrail is assigned to this railing type.";
        }
        if (target is FamilyInstance instance)
        {
            if (operation == "api.get:Autodesk.Revit.DB.FamilyInstance.IsWorkPlaneFlipped" && !instance.CanFlipWorkPlane)
                return "This instance does not support flipping its work plane.";
            if (operation == "api.get:Autodesk.Revit.DB.FamilyInstance.HostParameter" &&
                (instance.Host?.Category?.Id.Value != (long)BuiltInCategory.OST_Walls || instance.Symbol.Family.get_Parameter(BuiltInParameter.FAMILY_WORK_PLANE_BASED)?.AsInteger() == 1))
                return "HostParameter requires a wall-hosted instance whose family is not work-plane based.";
        }
        if (target is SpatialElementTag tag)
        {
            if (operation is "api.get:Autodesk.Revit.DB.SpatialElementTag.HasElbow" or "api.get:Autodesk.Revit.DB.SpatialElementTag.LeaderEnd" or "api.get:Autodesk.Revit.DB.SpatialElementTag.LeaderElbow"
                && !tag.HasLeader) return "This spatial tag has no leader.";
            if (operation == "api.get:Autodesk.Revit.DB.SpatialElementTag.LeaderElbow" && !tag.HasElbow)
                return "A straight tag leader has no elbow point.";
        }
        if (target is Dimension dimension)
        {
            if (operation is "api.get:Autodesk.Revit.DB.Dimension.TextPosition" or "api.get:Autodesk.Revit.DB.Dimension.LeaderEndPosition"
                && !dimension.AreReferencesAvailable) return "The dimension references are unavailable in the current view context.";
            if (operation == "api.get:Autodesk.Revit.DB.Dimension.LeaderEndPosition" && !dimension.HasLeader)
                return "This dimension has no leader.";
            if (operation is "api.get:Autodesk.Revit.DB.Dimension.Origin" or "api.get:Autodesk.Revit.DB.Dimension.IsLocked"
                && dimension.NumberOfSegments > 1) return "Use individual segments for this multi-segment dimension.";
            if (operation is "api.get:Autodesk.Revit.DB.Dimension.TextPosition" or "api.get:Autodesk.Revit.DB.Dimension.LeaderEndPosition"
                && (dimension.NumberOfSegments > 1 || !dimension.IsTextPositionAdjustable())) return "This dimension style or multi-segment dimension does not expose an adjustable text/leader position.";
            if (operation is "api.get:Autodesk.Revit.DB.SpotDimension.LeaderHasShoulder" or "api.get:Autodesk.Revit.DB.SpotDimension.LeaderShoulderPosition"
                && !dimension.HasLeader) return "This spot dimension has no leader.";
            if (operation is "api.get:Autodesk.Revit.DB.SpotDimension.LeaderHasShoulder" or "api.get:Autodesk.Revit.DB.SpotDimension.LeaderShoulderPosition"
                && dimension.DimensionType.StyleType == DimensionStyleType.SpotSlope) return "Spot slope dimensions do not expose leader shoulders.";
            if (operation == "api.get:Autodesk.Revit.DB.SpotDimension.LeaderShoulderPosition" && target is SpotDimension spot && !spot.LeaderHasShoulder)
                return "This spot dimension leader has no shoulder.";
        }
        if (operation == "api.get:Autodesk.Revit.DB.Architecture.RoomTag.TaggedLocalRoomId" && target is RoomTag roomTag && roomTag.Room is null)
            return "The tagged room is unavailable, for example because its link is unloaded or the tag is orphaned.";
        if (operation == "api.get:Autodesk.Revit.DB.ViewSchedule.KeyScheduleParameterName" && target is ViewSchedule schedule && !schedule.Definition.IsKeySchedule)
            return "This schedule is not a key schedule.";
        if (operation == "api.get:Autodesk.Revit.DB.Electrical.ElectricalSystem.Length" && target is ElectricalSystem system && system.BaseEquipment is null)
            return "Circuit length cannot be calculated until the system is connected to a panel.";
        if (target is View3D { IsTemplate: true } && operation is "api.get:Autodesk.Revit.DB.View3D.IsLocked" or "api.get:Autodesk.Revit.DB.View3D.IsPerspective"
            or "api.get:Autodesk.Revit.DB.View3D.IsSectionBoxActive" or "api.get:Autodesk.Revit.DB.View3D.ProjectGridsOnSectionBox")
            return "This property requires a 3D view instance rather than a view template.";
        return null;
    }

    private static string? DocumentedReadFailure(string operation, string message)
    {
        if (operation == "api.get:Autodesk.Revit.DB.Dimension.TextPosition" && message == "Can't get text position."
            || operation == "api.get:Autodesk.Revit.DB.Dimension.LeaderEndPosition" && message == "Can't get leader position.")
            return "Revit cannot resolve this dimension position in its current reference/view context. Inspect the owning view; no position was inferred.";
        if (operation is "api.get:Autodesk.Revit.DB.Mechanical.Space.CalculatedCoolingLoad" or "api.get:Autodesk.Revit.DB.Mechanical.Space.CalculatedHeatingLoad"
            or "api.get:Autodesk.Revit.DB.Mechanical.Space.CalculatedSupplyAirflow" && message.Contains("Not Computed", StringComparison.OrdinalIgnoreCase))
            return "Heating/cooling analysis has not produced this calculated space value.";
        if (operation is "api.get:Autodesk.Revit.DB.MEPCurve.Height" or "api.get:Autodesk.Revit.DB.MEPCurve.Width"
            && message.Contains("not rectangular or oval", StringComparison.OrdinalIgnoreCase))
            return "Width and height require a rectangular or oval connector profile; use Diameter for round profiles.";
        if (operation == "api.get:Autodesk.Revit.DB.RoofBase.FasciaDepth" && message.Contains("FasciaDepth' is disabled", StringComparison.Ordinal))
            return "Fascia depth is disabled for this roof configuration.";
        return null;
    }
}
