namespace LECG.RevitCopilot.Llm;

internal static class ToolSchemas
{
    internal static readonly object[] Definitions =
    [
        Function(
            "project_info",
            "Get active Revit project, view, worksharing, and level information.",
            Schema(new { }, [])),
        Function(
            "elements_query",
            "Query model elements by BuiltInCategory and optional level, with bounded results.",
            Schema(
                new
                {
                    category = new { type = "string", description = "BuiltInCategory name such as OST_Walls." },
                    level = new { type = new[] { "string", "null" }, description = "Exact level name, or null." },
                    limit = new { type = new[] { "integer", "null" }, minimum = 1, maximum = 100, description = "Maximum returned elements, or null for 50." }
                },
                ["category", "level", "limit"])),
        Function(
            "element_get",
            "Get a comprehensive parameter dump for one element by unique_id.",
            Schema(
                new { unique_id = new { type = "string", description = "Stable Revit element unique_id." } },
                ["unique_id"])),
        Function(
            "element_set_parameter",
            "Set one writable element parameter. Numeric doubles are raw internal feet; formatted strings use project units.",
            Schema(
                new
                {
                    unique_id = new { type = "string" },
                    parameter_name = new { type = "string" },
                    new_value = new { type = new[] { "string", "number", "boolean" } }
                },
                ["unique_id", "parameter_name", "new_value"])),
        Function(
            "elements_delete",
            "Delete active-document elements by unique_id inside one Revit transaction. This is destructive.",
            Schema(
                new
                {
                    unique_ids = new
                    {
                        type = "array",
                        items = new { type = "string" },
                        minItems = 1
                    }
                },
                ["unique_ids"])),
        Function(
            "view_isolate_or_select",
            "Select and highlight active-document elements in the current Revit view.",
            Schema(
                new
                {
                    element_ids = new
                    {
                        type = "array",
                        items = new { type = "integer" },
                        minItems = 1
                    }
                },
                ["element_ids"]))
    ];

    private static object Function(string name, string description, object parameters) => new
    {
        type = "function",
        name,
        description,
        parameters,
        strict = true
    };

    private static object Schema(object properties, string[] required) => new
    {
        type = "object",
        properties,
        required,
        additionalProperties = false
    };
}
