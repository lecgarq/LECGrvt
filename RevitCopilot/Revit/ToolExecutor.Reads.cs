using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using LECG.RevitCopilot.Agent;

namespace LECG.RevitCopilot.Revit;

internal sealed partial class ToolExecutor
{
    private static object AgentRead(UIDocument ui, Document doc, string operation, JsonElement args)
    {
        CapabilityCatalog.Require(operation, "read");
        if (RevitApiCatalog.IsApiOperation(operation)) return ExecuteApiProperty(doc, operation, args, change: false);
        IEnumerable<T> Collect<T>() where T : Element => new FilteredElementCollector(doc).OfClass(typeof(T)).Cast<T>();
        object Brief(Element e) => new { id = e.Id.Value, unique_id = e.UniqueId, name = SafeElementName(e), category = e.Category?.Name };
        return operation switch
        {
            "knowledge_search" => KnowledgeLibrary.Search(OptionalString(args, "query") ?? "", OptionalInt(args, "limit") ?? 3, OptionalString(args, "kind") ?? "all"),
            "knowledge_get" => KnowledgeLibrary.Get(RequireString(args, "reference_id"), OptionalInt(args, "offset") ?? 0, OptionalInt(args, "limit") ?? 2000),
            "element_dependents" => ElementsPage(RequireElement(doc, RequireString(args, "unique_id")).GetDependentElements(null).Select(doc.GetElement).OfType<Element>(), args, Brief),
            "element_valid_types" => ElementsPage(RequireElement(doc, RequireString(args, "unique_id")).GetValidTypes().Select(doc.GetElement).OfType<ElementType>(), args, Brief),
            "phases_list" => Page(doc.Phases.Cast<Phase>().Where(p => Matches(p.Name, args)), args, Brief),
            "design_options_list" => ElementsPage(Collect<DesignOption>(), args, e => new { element = Brief(e), is_primary = ((DesignOption)e).IsPrimary,
                option_set_id = e.get_Parameter(BuiltInParameter.OPTION_SET_ID)?.AsElementId().Value }),
            "worksets_list" => Worksets(doc, args),
            "schedule_fields" => ScheduleFields(doc, args),
            "view_filters" => ViewFilters(doc, args),
            "element_action_checks" => ElementActionChecks(doc, args),
            "elements_joined" => ElementsJoined(doc, args),
            "type_compound_layers" => TypeCompoundLayers(doc, args),
            "instance_transform" => InstanceTransform(doc, args),
            "elements_find" => FindApiElements(doc, args),
            "categories_list" => Page(doc.Settings.Categories.Cast<Category>().Where(c => c.CategoryType == CategoryType.Model)
                .Where(c => Matches(c.Name, args)).OrderBy(c => c.Name), args,
                c => new { id = c.Id.Value, c.Name, built_in_category = Enum.GetName(typeof(BuiltInCategory), (int)c.Id.Value) }),
            "levels_list" => ElementsPage(Collect<Level>(), args, e => new { element = Brief(e), elevation_mm = Mm(((Level)e).Elevation) }),
            "grids_list" => ElementsPage(Collect<Grid>(), args, Brief),
            "views_list" => ElementsPage(Collect<View>(), args, e => new { element = Brief(e), kind = ((View)e).ViewType.ToString(), is_template = ((View)e).IsTemplate, scale = ((View)e).Scale }),
            "sheets_list" => ElementsPage(Collect<ViewSheet>(), args, e => new { element = Brief(e), number = ((ViewSheet)e).SheetNumber, view_count = ((ViewSheet)e).GetAllPlacedViews().Count }),
            "schedules_list" => ElementsPage(Collect<ViewSchedule>(), args, e => new { element = Brief(e), rows = ((ViewSchedule)e).GetTableData().GetSectionData(SectionType.Body).NumberOfRows }),
            "materials_list" => ElementsPage(Collect<Material>(), args, e => new { element = Brief(e), rgb = new[] { ((Material)e).Color.Red, ((Material)e).Color.Green, ((Material)e).Color.Blue }, transparency = ((Material)e).Transparency }),
            "types_list" => ElementsPage(new FilteredElementCollector(doc).OfCategory(ReadCategory(args)).WhereElementIsElementType(), args, Brief),
            "families_list" => ElementsPage(Collect<Family>(), args, e => new { element = Brief(e), symbol_count = ((Family)e).GetFamilySymbolIds().Count }),
            "rooms_list" => ElementsPage(new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType(), args,
                e => new { element = Brief(e), number = ((Room)e).Number, level = GetElementLevelName(doc, e), area_m2 = UnitUtils.ConvertFromInternalUnits(((Room)e).Area, UnitTypeId.SquareMeters) }),
            "warnings_list" => Page(doc.GetWarnings().Where(w => Matches(w.GetDescriptionText(), args)), args,
                w => new { description = w.GetDescriptionText(), severity = w.GetSeverity().ToString(), element_ids = w.GetFailingElements().Select(id => id.Value).Take(50) }),
            "links_list" => ElementsPage(Collect<RevitLinkInstance>(), args, e => new { element = Brief(e), loaded = ((RevitLinkInstance)e).GetLinkDocument() is not null }),
            "selection_get" => Page(ui.Selection.GetElementIds().Select(doc.GetElement).OfType<Element>(), args, Brief),
            "parameters_get" => AgentParameters(doc, args),
            "elements_count" => new { category = ReadCategory(args).ToString(), count = new FilteredElementCollector(doc).OfCategory(ReadCategory(args)).WhereElementIsNotElementType().GetElementCount() },
            "sheet_views" => Page((RequireElement(doc, RequireString(args, "unique_id")) as ViewSheet ?? throw new ArgumentException("Select a sheet."))
                .GetAllPlacedViews().Select(doc.GetElement).OfType<Element>(), args, Brief),
            "element_materials" => ElementMaterials(doc, args),
            "element_bounds" => ElementBounds(doc, args),
            "host_inserts" => HostInserts(doc, args),
            "element_phase_status" => ElementPhaseStatus(doc, args),
            "host_bottom_faces" => HostBottomFaces(doc, args),
            _ => throw new ArgumentException("Unsupported read operation.")
        };
    }

    private static object Page<T>(IEnumerable<T> source, JsonElement args, Func<T, object> map)
    {
        int offset = Math.Clamp(OptionalInt(args, "offset") ?? 0, 0, 100000);
        int limit = Math.Clamp(OptionalInt(args, "limit") ?? 10, 1, 50);
        T[] all = source.ToArray();
        return new { total_count = all.Length, offset, next_offset = offset + limit < all.Length ? (int?)(offset + limit) : null,
            items = all.Skip(offset).Take(limit).Select(map).ToArray() };
    }

    private static object ElementsPage(IEnumerable<Element> source, JsonElement args, Func<Element, object> map) =>
        Page(source.Where(e => Matches(SafeElementName(e), args)).OrderBy(e => SafeElementName(e)).ThenBy(e => e.Id.Value), args, map);

    private static bool Matches(string text, JsonElement args) => text.Contains(OptionalString(args, "search") ?? "", StringComparison.OrdinalIgnoreCase);
    private static double Mm(double feet) => UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Millimeters);
    private static double Feet(double millimeters) => UnitUtils.ConvertToInternalUnits(millimeters, UnitTypeId.Millimeters);
    private static BuiltInCategory ReadCategory(JsonElement args) => Enum.TryParse(RequireString(args, "category"), true, out BuiltInCategory category) && Enum.IsDefined(category)
        ? category : throw new ArgumentException("Use a valid BuiltInCategory name, such as OST_Walls.");
    private static Element RequireElement(Document doc, string uniqueId) => doc.GetElement(uniqueId) ?? throw new ArgumentException($"Element '{uniqueId}' is not in the active document.");

    private static Element[] ReadElements(Document doc, JsonElement args, int maximum = 50)
    {
        string[] ids = RequireProperty(args, "unique_ids").EnumerateArray().Select(v => v.GetString() ?? "").Distinct().ToArray();
        if (ids.Length < 1 || ids.Length > maximum) throw new ArgumentException($"Supply 1 to {maximum} unique_ids.");
        return ids.Select(id => RequireElement(doc, id)).ToArray();
    }

    private static Parameter ExactParameter(Element element, string name)
    {
        var matches = element.GetParameters(name);
        if (matches.Count != 1) throw new ArgumentException($"Parameter '{name}' on {element.Id.Value} is missing or ambiguous ({matches.Count} matches).");
        return matches[0];
    }

    private static object AgentParameters(Document doc, JsonElement args)
    {
        string[] names = RequireProperty(args, "parameter_names").EnumerateArray().Select(v => v.GetString() ?? "").Distinct().ToArray();
        if (names.Length < 1 || names.Length > 20) throw new ArgumentException("Supply 1 to 20 parameter names.");
        return new { items = ReadElements(doc, args, 20).Select(e => new { unique_id = e.UniqueId, id = e.Id.Value,
            parameters = names.Select(name => SerializeParameter(doc, ExactParameter(e, name))).ToArray() }).ToArray() };
    }

    private static object ElementMaterials(Document doc, JsonElement args)
    {
        Element element = RequireElement(doc, RequireString(args, "unique_id"));
        return Page(element.GetMaterialIds(false), args, id => new { id = id.Value, unique_id = doc.GetElement(id)?.UniqueId,
            name = doc.GetElement(id)?.Name, area_m2 = UnitUtils.ConvertFromInternalUnits(element.GetMaterialArea(id, false), UnitTypeId.SquareMeters),
            volume_m3 = UnitUtils.ConvertFromInternalUnits(element.GetMaterialVolume(id), UnitTypeId.CubicMeters) });
    }

    private static object ElementBounds(Document doc, JsonElement args)
    {
        Element element = RequireElement(doc, RequireString(args, "unique_id"));
        BoundingBoxXYZ box = element.get_BoundingBox(null) ?? throw new ArgumentException("Element has no model bounding box.");
        return new { unique_id = element.UniqueId, min_mm = new[] { Mm(box.Min.X), Mm(box.Min.Y), Mm(box.Min.Z) },
            max_mm = new[] { Mm(box.Max.X), Mm(box.Max.Y), Mm(box.Max.Z) }, min_feet = new[] { box.Min.X, box.Min.Y, box.Min.Z }, max_feet = new[] { box.Max.X, box.Max.Y, box.Max.Z } };
    }
}
