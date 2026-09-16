using System.Text.Json;
using Autodesk.Revit.DB;

namespace LECG.RevitCopilot.Revit;

// Reviewed native adapters, not generated-code execution. These operations are read-only:
// no transaction is opened; invalid/stale inputs and API exceptions use the executor's error envelope.
internal sealed partial class ToolExecutor
{
    private static bool ReadOptionalBoolean(JsonElement args, string name, bool fallback)
    {
        if (!args.TryGetProperty(name, out var value)) return fallback;
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new ArgumentException($"'{name}' must be true or false.");
        return value.GetBoolean();
    }

    // campaign-v5 candidate 742369128674aae8a9b74352; exact FindInserts flag semantics from RevitAPI.xml.
    private static object HostInserts(Document doc, JsonElement args)
    {
        var host = RequireElement(doc, RequireString(args, "unique_id")) as HostObject
            ?? throw new ArgumentException("Select a HostObject, such as a wall or floor, in the active document.");
        var ids = host.FindInserts(ReadOptionalBoolean(args, "include_rectangular_openings", true),
            ReadOptionalBoolean(args, "include_shadows", false), ReadOptionalBoolean(args, "include_embedded_walls", false),
            ReadOptionalBoolean(args, "include_shared_embedded_inserts", false));
        return new { host_unique_id = host.UniqueId, inserts = Page(ids.OrderBy(id => id.Value), args, id =>
        {
            var element = doc.GetElement(id) ?? throw new InvalidOperationException("An insert is no longer available; query the host again.");
            return new { id = id.Value, unique_id = element.UniqueId, name = SafeElementName(element), category = element.Category?.Name };
        }) };
    }

    // campaign-v5 candidate e86aa1a1f66f14a32497fead; end is constrained to the documented 0/1 pair.
    private static object CurveJoinNeighbors(Document doc, JsonElement args)
    {
        Element element = RequireElement(doc, RequireString(args, "unique_id"));
        var location = element.Location as LocationCurve
            ?? throw new ArgumentException("Select an element with a curve-based location in the active document.");
        var neighbors = Enumerable.Range(0, 2).SelectMany(end => location.get_ElementsAtJoin(end).Cast<Element>()
            .Select(joined => (End: end, Element: joined))).OrderBy(item => item.End).ThenBy(item => item.Element.Id.Value);
        return new { element_unique_id = element.UniqueId, neighbors = Page(neighbors, args, item => new
        {
            end = item.End, id = item.Element.Id.Value, unique_id = item.Element.UniqueId,
            name = SafeElementName(item.Element), category = item.Element.Category?.Name
        }) };
    }

    // campaign-v5 candidate 041f324808ec9567d9d3da5b; the API defines this for an electrical panel's MEP model.
    private static object AssignedElectricalSystems(Document doc, JsonElement args)
    {
        var instance = RequireElement(doc, RequireString(args, "unique_id")) as FamilyInstance
            ?? throw new ArgumentException("Select a family instance in the active document.");
        MEPModel model = instance.MEPModel
            ?? throw new ArgumentException("The selected family instance has no MEP model.");
        return new { element_unique_id = instance.UniqueId, systems = Page(model.GetAssignedElectricalSystems()
            .OrderBy(system => system.Id.Value), args, system => new
            {
                id = system.Id.Value, unique_id = system.UniqueId,
                name = SafeElementName(system), category = system.Category?.Name
            }) };
    }

    // campaign-v5 candidates c0f89183090ce961dfd436a2 and d4094fcbdb6666f262bea8aa; coordinates are explicit millimeters.
    private static object SpatialContainsPoint(Document doc, JsonElement args)
    {
        Element element = RequireElement(doc, RequireString(args, "unique_id"));
        var point = new XYZ(Feet(Number(args, "x_mm")), Feet(Number(args, "y_mm")), Feet(Number(args, "z_mm")));
        return element switch
        {
            Autodesk.Revit.DB.Architecture.Room room => new { element_unique_id = room.UniqueId,
                spatial_kind = "room", contains_point = room.IsPointInRoom(point) },
            Autodesk.Revit.DB.Mechanical.Space space => new { element_unique_id = space.UniqueId,
                spatial_kind = "space", contains_point = space.IsPointInSpace(point) },
            _ => throw new ArgumentException("Select a room or MEP space in the active document.")
        };
    }

    // campaign-v5 candidate 4ac2566755135916ee349a4b; exact lookup uses Revit's stable connector index.
    private static object MepConnectors(Document doc, JsonElement args)
    {
        Element element = RequireElement(doc, RequireString(args, "unique_id"));
        ConnectorManager? manager = element switch
        {
            MEPCurve curve => curve.ConnectorManager,
            FamilyInstance instance when instance.MEPModel is not null => instance.MEPModel.ConnectorManager,
            _ => null
        };
        if (manager is null) throw new ArgumentException("Select an MEP curve or family instance with connectors in the active document.");
        IEnumerable<Connector> connectors = manager.Connectors.Cast<Connector>();
        if (args.TryGetProperty("connector_id", out _))
        {
            int id = OptionalInt(args, "connector_id") ?? throw new ArgumentException("'connector_id' must be a 32-bit integer.");
            connectors = [manager.Lookup(id) ?? throw new ArgumentException($"Connector '{id}' is not available on the selected element.")];
        }
        return new { element_unique_id = element.UniqueId, connectors = Page(connectors.OrderBy(connector => connector.Id), args,
            connector => new { id = connector.Id, domain = connector.Domain.ToString(), connector_type = connector.ConnectorType.ToString() }) };
    }

    // campaign-v5 candidate 7082414fa9bd90b3cfb3c9ad; covers every external-file reference class known to the document.
    private static object ExternalFiles(Document doc, JsonElement args)
    {
        IEnumerable<Element> elements = ExternalFileUtils.GetAllExternalFileReferences(doc).Select(doc.GetElement)
            .OfType<Element>().Where(element => Matches(SafeElementName(element), args))
            .OrderBy(element => SafeElementName(element)).ThenBy(element => element.Id.Value);
        return new { files = Page(elements, args, element =>
        {
            ExternalFileReference reference = ExternalFileUtils.GetExternalFileReference(doc, element.Id);
            return new { id = element.Id.Value, unique_id = element.UniqueId, name = SafeElementName(element),
                category = element.Category?.Name, reference_type = reference.ExternalFileReferenceType.ToString(),
                path_type = reference.PathType.ToString(), status = reference.GetLinkedFileStatus().ToString(),
                path = ModelPathUtils.ConvertModelPathToUserVisiblePath(reference.GetPath()) };
        }) };
    }

    // campaign-v5 candidate 49ff3f43469219d7121ba283; InvalidElementId means the curtain panel has no displayed host replacement.
    private static object PanelHost(Document doc, JsonElement args)
    {
        var panel = RequireElement(doc, RequireString(args, "unique_id")) as Panel
            ?? throw new ArgumentException("Select a curtain panel in the active document.");
        Element? host = doc.GetElement(panel.FindHostPanel());
        object? hostBrief = host is null ? null : new { id = host.Id.Value, unique_id = host.UniqueId,
            name = SafeElementName(host), category = host.Category?.Name };
        return new { panel_unique_id = panel.UniqueId, host = hostBrief };
    }

    // campaign-v5 candidate 29802d8c0186b950dec8c775; association is defined by the stair boundary, not dependency traversal.
    private static object StairsAssociatedRailings(Document doc, JsonElement args)
    {
        var stairs = RequireElement(doc, RequireString(args, "unique_id")) as Autodesk.Revit.DB.Architecture.Stairs
            ?? throw new ArgumentException("Select a stair in the active document.");
        IEnumerable<Element> railings = stairs.GetAssociatedRailings().Select(doc.GetElement).OfType<Element>()
            .OrderBy(element => element.Id.Value);
        return new { stairs_unique_id = stairs.UniqueId, railings = Page(railings, args, element => new
        {
            id = element.Id.Value, unique_id = element.UniqueId,
            name = SafeElementName(element), category = element.Category?.Name
        }) };
    }

    // campaign-v5 candidate ffdc7e1ebf434480ba699ae0; Revit rejects detail groups, so require the model-group category explicitly.
    private static object GroupAttachedDetailTypes(Document doc, JsonElement args)
    {
        var group = RequireElement(doc, RequireString(args, "unique_id")) as Group
            ?? throw new ArgumentException("Select a model group in the active document.");
        if (group.Category?.Id.Value != (long)BuiltInCategory.OST_IOSModelGroups)
            throw new ArgumentException("Select a model group; detail groups cannot own attached detail groups.");
        IEnumerable<Element> types = group.GetAvailableAttachedDetailGroupTypeIds().Select(doc.GetElement)
            .OfType<Element>().OrderBy(element => element.Id.Value);
        return new { group_unique_id = group.UniqueId, detail_group_types = Page(types, args, element => new
        {
            id = element.Id.Value, unique_id = element.UniqueId,
            name = SafeElementName(element), category = element.Category?.Name
        }) };
    }

    // campaign-v5 candidate 4d2491374a2d57c261855471; pair it with Revit's corresponding survey-point accessor.
    private static object BasePoints(Document doc)
    {
        object Point(XYZ point) => new { x_mm = Mm(point.X), y_mm = Mm(point.Y), z_mm = Mm(point.Z) };
        object Brief(string kind, BasePoint point) => new { kind, id = point.Id.Value, unique_id = point.UniqueId,
            position_mm = Point(point.Position), shared_position_mm = Point(point.SharedPosition),
            point.Clipped, point.IsShared };
        return new { points = new[] { Brief("project", BasePoint.GetProjectBasePoint(doc)),
            Brief("survey", BasePoint.GetSurveyPoint(doc)) } };
    }

    // campaign-v5 candidate 3c567e7f95e186ae02c6c02a; expose bounded loop summaries instead of unbounded curve geometry.
    private static object FaceSplitBoundaries(Document doc, JsonElement args)
    {
        var splitter = RequireElement(doc, RequireString(args, "unique_id")) as FaceSplitter
            ?? throw new ArgumentException("Select a face splitter in the active document.");
        Element? splitElement = doc.GetElement(splitter.SplitElementId);
        var loops = splitter.GetBoundaries().Select((loop, index) => (Loop: loop, Index: index));
        return new { splitter_unique_id = splitter.UniqueId,
            split_element = splitElement is null ? null : new { id = splitElement.Id.Value, unique_id = splitElement.UniqueId,
                name = SafeElementName(splitElement), category = splitElement.Category?.Name },
            boundaries = Page(loops, args, item => new { index = item.Index, segment_count = item.Loop.Count(),
                approximate_length_mm = item.Loop.Sum(curve => Mm(curve.ApproximateLength)) }) };
    }

    // campaign-v5 candidate da314384a8346ca50371fffc. Modifiable does not imply that any proposed phase is valid.
    private static object ElementPhaseStatus(Document doc, JsonElement args) => new
    {
        items = ReadElements(doc, args).Select(element => new
        {
            unique_id = element.UniqueId, phases_modifiable = element.ArePhasesModifiable(),
            created_phase_id = element.CreatedPhaseId.Value, demolished_phase_id = element.DemolishedPhaseId.Value,
            created_phase_unique_id = doc.GetElement(element.CreatedPhaseId)?.UniqueId,
            demolished_phase_unique_id = doc.GetElement(element.DemolishedPhaseId)?.UniqueId
        }).ToArray()
    };

    // campaign-v5 candidate 2c0d92d0a287c6d4189043f2. Docs limit this to hosts supporting compound-structure bottom faces.
    private static object HostBottomFaces(Document doc, JsonElement args)
    {
        var host = RequireElement(doc, RequireString(args, "unique_id")) as HostObject
            ?? throw new ArgumentException("Select a supported floor, roof or ceiling in the active document.");
        var references = HostObjectUtils.GetBottomFaces(host);
        return new { host_unique_id = host.UniqueId, faces = Page(references, args, reference =>
        {
            var face = host.GetGeometryObjectFromReference(reference) as Face
                ?? throw new InvalidOperationException("A bottom face is unavailable; query the current model again.");
            return new { stable_reference = reference.ConvertToStableRepresentation(doc), area_m2 = UnitUtils.ConvertFromInternalUnits(face.Area, UnitTypeId.SquareMeters) };
        }), reference_scope = "current document; re-query after geometry edits" };
    }
}
