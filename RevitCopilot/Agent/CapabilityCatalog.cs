namespace LECG.RevitCopilot.Agent;

internal sealed record Capability(string Name, string Kind, string Description, string Arguments);

internal static class CapabilityCatalog
{
    internal static readonly Capability[] All =
    [
        new("knowledge_search", "read", "Search 3,000 shared Revit 2026 source references; distinguish installed adapters from documentation-only methods", "{query?:string,kind?:all|read|change|reference,limit?:int}"),
        new("knowledge_get", "read", "Fetch one source reference and its available adapter; documentation cannot execute code; source text supports character pagination", "{reference_id:string,offset?:int,limit?:int}"),
        new("element_dependents", "read", "Inspect logical dependent elements before cleanup; deletion preview remains authoritative for actual deleted IDs", "{unique_id:string,search?:string,limit?:int,offset?:int}"),
        new("element_valid_types", "read", "Find types currently valid for one element before a type change", "{unique_id:string,search?:string,limit?:int,offset?:int}"),
        new("phases_list", "read", "Project phases in chronological Revit order", "{search?:string,limit?:int,offset?:int}"),
        new("design_options_list", "read", "Design options, primary status and option set IDs in the current document", "{search?:string,limit?:int,offset?:int}"),
        new("worksets_list", "read", "User worksets and open/editable status; returns an empty list for non-workshared projects", "{search?:string,limit?:int,offset?:int}"),
        new("schedule_fields", "read", "Ordered schedule columns, headings, parameter IDs and hidden flags; bounded output", "{unique_id:string,search?:string,limit?:int,offset?:int}"),
        new("view_filters", "read", "Filters applied to a view, with visibility and enabled flags", "{unique_id:string,search?:string,limit?:int,offset?:int}"),
        new("element_action_checks", "read", "Read API feasibility checks for deletion, mirroring, parts creation and phase edits; checks do not grant approval or replace previews", "{unique_ids:string[1..20]}"),
        new("elements_joined", "read", "Check whether two explicit current-document elements have a Revit geometry join", "{first_unique_id:string,second_unique_id:string}"),
        new("type_compound_layers", "read", "Inspect ordered wall/floor/roof type layers, material identities and widths in millimeters; flags vertically compound structures", "{unique_id:string,limit?:int,offset?:int}"),
        new("instance_transform", "read", "Read an instance total transform including true-north effects where applicable; origin in feet and millimeters, dimensionless basis vectors", "{unique_id:string}"),
        new("elements_find", "read", "Find model instances or types with current UniqueIds and exact runtime API class; bounded page without a full sort or exact count", "{category?:string,element_type?:string,kind?:instances|types|all,search?:string,limit?:int,offset?:int}"),
        new("workflow_change_batch", "change", "Execute up to 12 changes in one atomic preview/commit; earlier result references use $step:stepId.field", "{steps:[{id:string,operation:string,arguments:object}]}"),
        new("categories_list", "read", "Available model categories", "{search?:string,limit?:int,offset?:int}"),
        new("levels_list", "read", "Levels and elevations in millimeters", "{search?:string,limit?:int,offset?:int}"),
        new("grids_list", "read", "Grid names and identifiers", "{search?:string,limit?:int,offset?:int}"),
        new("views_list", "read", "Views, templates and scales", "{search?:string,limit?:int,offset?:int}"),
        new("sheets_list", "read", "Sheet numbers, names and view counts", "{search?:string,limit?:int,offset?:int}"),
        new("schedules_list", "read", "Schedule names and row counts", "{search?:string,limit?:int,offset?:int}"),
        new("materials_list", "read", "Materials, graphics color and transparency", "{search?:string,limit?:int,offset?:int}"),
        new("types_list", "read", "Element types in a BuiltInCategory", "{category:string,search?:string,limit?:int,offset?:int}"),
        new("families_list", "read", "Loaded families and symbol counts", "{search?:string,limit?:int,offset?:int}"),
        new("rooms_list", "read", "Rooms, numbers, level and area in square meters", "{search?:string,limit?:int,offset?:int}"),
        new("warnings_list", "read", "Model warnings with failing element IDs", "{search?:string,limit?:int,offset?:int}"),
        new("links_list", "read", "Revit link instances and loaded status", "{search?:string,limit?:int,offset?:int}"),
        new("selection_get", "read", "Current selection with stable unique IDs", "{limit?:int,offset?:int}"),
        new("parameters_get", "read", "Only requested parameters, avoiding full dumps", "{unique_ids:string[1..20],parameter_names:string[1..20]}"),
        new("elements_count", "read", "Count instances in a category without listing every element", "{category:string}"),
        new("sheet_views", "read", "Views placed on a sheet", "{unique_id:string,limit?:int,offset?:int}"),
        new("element_materials", "read", "Materials assigned to one element, area and volume", "{unique_id:string,limit?:int,offset?:int}"),
        new("element_bounds", "read", "Model bounding box in internal feet and millimeters", "{unique_id:string}"),
        new("host_inserts", "read", "List inserts in a wall/floor/other HostObject, with optional rectangular openings, shadows and embedded inserts; no model changes", "{unique_id:string,include_rectangular_openings?:bool,include_shadows?:bool,include_embedded_walls?:bool,include_shared_embedded_inserts?:bool,limit?:int,offset?:int}"),
        new("element_phase_status", "read", "Check whether created/demolished phase properties are modifiable and read their current phase IDs; not a guarantee a proposed phase change is valid", "{unique_ids:string[1..50]}"),
        new("host_bottom_faces", "read", "Read bottom-face references and areas in square meters for a supported floor, roof or ceiling; unsupported hosts return an error", "{unique_id:string,limit?:int,offset?:int}"),
        new("rename_elements", "change", "Rename types, views, sheets, levels, grids or materials using existing LECG rename rules", "{unique_ids:string[1..50],find?:string,replace?:string,prefix?:string,suffix?:string,case?:lower|upper|title|capitalize}"),
        new("set_parameters", "change", "Batch-set one exact parameter on explicit elements", "{unique_ids:string[1..50],parameter_name:string,new_value:string|number|bool}"),
        new("set_pinned", "change", "Pin or unpin explicit model elements", "{unique_ids:string[1..50],pinned:bool}"),
        new("set_type", "change", "Change explicit instances to a compatible element type", "{unique_ids:string[1..50],type_unique_id:string}"),
        new("move_elements", "change", "Translate explicit elements in millimeters", "{unique_ids:string[1..50],x_mm:number,y_mm:number,z_mm:number}"),
        new("rotate_elements", "change", "Rotate elements around a vertical axis", "{unique_ids:string[1..50],origin_x_mm:number,origin_y_mm:number,angle_degrees:number}"),
        new("delete_elements", "change", "Delete explicit elements with dependent deletions included in preview", "{unique_ids:string[1..50]}"),
        new("create_level", "change", "Create a named level at a millimeter elevation", "{name:string,elevation_mm:number}"),
        new("duplicate_type", "change", "Duplicate an element type with a new name", "{unique_id:string,name:string}"),
        new("duplicate_view", "change", "Duplicate a view; supports detailing or dependent copy", "{unique_id:string,name:string,mode:Duplicate|WithDetailing|AsDependent}"),
        new("material_color", "change", "Set material graphics RGB and transparency", "{unique_ids:string[1..50],red:int,green:int,blue:int,transparency:int}"),
        new("assign_material", "change", "Assign a material through an exact writable material parameter", "{unique_ids:string[1..50],parameter_name:string,material_unique_id:string}"),
        new("view_scale", "change", "Set a view scale denominator on explicit views", "{unique_ids:string[1..50],scale:int}"),
        new("material_graphics_solid", "change", "Reuse LECG solid-fill graphics application for materials", "{unique_ids:string[1..50],red:int,green:int,blue:int}"),
        new("slab_offset", "change", "Reuse LECG floor/slab height-offset function, expressed in millimeters", "{unique_ids:string[1..50],offset_mm:number}"),
        new("slab_reset", "change", "Reuse LECG shape reset for floors and toposolids; removes shape edits", "{unique_ids:string[1..50]}"),
    ];

    internal static Capability Require(string name, string? kind = null) => RevitApiCatalog.IsApiOperation(name) ? RevitApiCatalog.Require(name, kind).Capability : All.FirstOrDefault(c => c.Name == name && (kind is null || c.Kind == kind))
        ?? throw new ArgumentException($"Unknown {kind ?? "agent"} operation '{name}'. Search the capability catalog.");

    internal static object Search(string query, int limit = 6)
    {
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var reviewedTerms = ReviewedDiscovery.Terms(query);
        var matches = All.Select(c => new { capability = c, score = terms.Count(t => (c.Name + " " + c.Description).Contains(t, StringComparison.OrdinalIgnoreCase)) + ReviewedDiscovery.Score(reviewedTerms, c.Name) })
            .Where(c => terms.Length == 0 || c.score > 0).OrderByDescending(c => c.score).ThenBy(c => c.capability.Name).ToArray();
        return new { total_capabilities = All.Length, matched = matches.Length, items = matches.Take(Math.Clamp(limit, 1, 40)).Select(c => c.capability),
            instructions = "read: agent_read. change: agent_preview then agent_apply; Revit asks local confirmation. List operations default to 10 rows and support offset. Preview IDs are temporary; do not reuse element IDs from other documents." };
    }
}
