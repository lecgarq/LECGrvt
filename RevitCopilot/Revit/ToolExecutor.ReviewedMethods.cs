using System.Text.Json;
using Autodesk.Revit.DB;

namespace LECG.RevitCopilot.Revit;

internal sealed partial class ToolExecutor
{
    private static object ElementActionChecks(Document doc, JsonElement args) => new
    {
        items = ReadElements(doc, args, 20).Select(e => new { unique_id = e.UniqueId,
            can_delete = DocumentValidation.CanDeleteElement(doc, e.Id),
            can_mirror = ElementTransformUtils.CanMirrorElement(doc, e.Id),
            can_create_parts = PartUtils.AreElementsValidForCreateParts(doc, new[] { e.Id }),
            phases_modifiable = e.ArePhasesModifiable() }).ToArray(),
        note = "These are API feasibility checks, not permission or a guarantee of a later mutation. Preview remains required."
    };

    private static object ElementsJoined(Document doc, JsonElement args)
    {
        var first = RequireElement(doc, RequireString(args, "first_unique_id"));
        var second = RequireElement(doc, RequireString(args, "second_unique_id"));
        if (first.Id == second.Id) throw new ArgumentException("Supply two different elements.");
        return new { first_unique_id = first.UniqueId, second_unique_id = second.UniqueId,
            joined = JoinGeometryUtils.AreElementsJoined(doc, first, second) };
    }

    private static object TypeCompoundLayers(Document doc, JsonElement args)
    {
        var type = RequireElement(doc, RequireString(args, "unique_id")) as HostObjAttributes
            ?? throw new ArgumentException("Supply a wall, floor, roof or ceiling TYPE UniqueId, not an instance.");
        using var structure = type.GetCompoundStructure() ?? throw new ArgumentException("This type has no compound structure.");
        var layers = structure.GetLayers();
        return new { type_unique_id = type.UniqueId, is_vertically_compound = structure.IsVerticallyCompound,
            structural_material_index = structure.StructuralMaterialIndex,
            layers = Page(layers.Select((layer, index) => new { layer, index }), args, item => new
            {
                index = item.index, width_mm = Mm(item.layer.Width), function = item.layer.Function.ToString(),
                material_id = item.layer.MaterialId.Value, material_unique_id = doc.GetElement(item.layer.MaterialId)?.UniqueId,
                material_name = doc.GetElement(item.layer.MaterialId)?.Name
            }),
            note = "Layer order is preserved. Vertically compound walls can vary by height; these widths are not a complete sectional geometry analysis."
        };
    }

    private static object InstanceTransform(Document doc, JsonElement args)
    {
        var instance = RequireElement(doc, RequireString(args, "unique_id")) as Instance
            ?? throw new ArgumentException("Supply a FamilyInstance, link instance or import instance.");
        using Transform transform = instance.GetTotalTransform();
        object Vector(XYZ v) => new { x = v.X, y = v.Y, z = v.Z };
        return new { unique_id = instance.UniqueId, origin_feet = Vector(transform.Origin),
            origin_mm = new { x = Mm(transform.Origin.X), y = Mm(transform.Origin.Y), z = Mm(transform.Origin.Z) },
            basis_x = Vector(transform.BasisX), basis_y = Vector(transform.BasisY), basis_z = Vector(transform.BasisZ),
            has_reflection = transform.HasReflection, is_conformal = transform.IsConformal,
            note = "Maps instance-local coordinates to document coordinates; total transform includes true north where Revit applies it. No linked document is opened." };
    }
}
