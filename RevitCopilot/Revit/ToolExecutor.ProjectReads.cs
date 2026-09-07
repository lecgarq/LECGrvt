using System.Text.Json;
using Autodesk.Revit.DB;

namespace LECG.RevitCopilot.Revit;

internal sealed partial class ToolExecutor
{
    private static object Worksets(Document doc, JsonElement args)
    {
        object Map(Workset workset) => new { id = workset.Id.IntegerValue, name = workset.Name,
            is_open = workset.IsOpen, is_editable = workset.IsEditable };
        if (!doc.IsWorkshared) return new { is_workshared = false, worksets = Page(Array.Empty<Workset>(), args, Map) };
        using var collector = new FilteredWorksetCollector(doc);
        return new { is_workshared = true, worksets = Page(collector.OfKind(WorksetKind.UserWorkset)
            .Where(w => Matches(w.Name, args)).OrderBy(w => w.Name).ThenBy(w => w.Id.IntegerValue), args, Map) };
    }

    private static object ScheduleFields(Document doc, JsonElement args)
    {
        var schedule = RequireElement(doc, RequireString(args, "unique_id")) as ViewSchedule
            ?? throw new ArgumentException("Select a schedule in the current document.");
        ScheduleDefinition definition = schedule.Definition;
        var ids = definition.GetFieldOrder();
        return new { unique_id = schedule.UniqueId, fields = Page(ids.Select((id, index) => new { field = definition.GetField(id), index })
            .Where(item => Matches(item.field.GetName() + " " + item.field.ColumnHeading, args)), args,
            item => new { index = item.index, field_id = item.field.FieldId.IntegerValue, name = item.field.GetName(),
                heading = item.field.ColumnHeading, is_hidden = item.field.IsHidden, parameter_id = item.field.ParameterId.Value,
                field_type = item.field.FieldType.ToString() }) };
    }

    private static object ViewFilters(Document doc, JsonElement args)
    {
        var view = RequireElement(doc, RequireString(args, "unique_id")) as View
            ?? throw new ArgumentException("Select a view in the current document.");
        if (!view.AreGraphicsOverridesAllowed()) throw new ArgumentException("This view does not support graphics overrides or filters.");
        return new { unique_id = view.UniqueId, filters = ElementsPage(view.GetFilters().Select(doc.GetElement).OfType<FilterElement>(), args,
            filter => new { id = filter.Id.Value, unique_id = filter.UniqueId, name = filter.Name,
                is_visible = view.GetFilterVisibility(filter.Id), is_enabled = view.GetIsFilterEnabled(filter.Id) }) };
    }
}
