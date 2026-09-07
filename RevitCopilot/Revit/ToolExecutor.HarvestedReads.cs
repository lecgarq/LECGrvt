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
