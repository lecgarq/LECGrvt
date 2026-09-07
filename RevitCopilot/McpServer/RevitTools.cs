using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace LECG.RevitCopilot.McpServer;

[McpServerToolType]
internal static class RevitTools
{
    [McpServerTool(Name = "project_info", Title = "Revit project information", ReadOnly = true, OpenWorld = false)]
    [Description("Get active Revit project, view, worksharing, and level information.")]
    public static Task<string> ProjectInfo(CancellationToken cancellationToken) =>
        RevitPipeClient.CallAsync("project_info", new { }, cancellationToken);

    [McpServerTool(Name = "elements_query", Title = "Query Revit elements", ReadOnly = true, OpenWorld = false)]
    [Description("Query Revit model elements by BuiltInCategory and optional exact level name, with bounded results.")]
    public static Task<string> ElementsQuery(
        [Description("BuiltInCategory name such as OST_Walls.")] string category,
        [Description("Exact level name, or null for every level.")] string? level = null,
        [Description("Maximum returned elements from 1 through 100; defaults to 50.")] int? limit = null,
        CancellationToken cancellationToken = default) =>
        RevitPipeClient.CallAsync("elements_query", new { category, level, limit }, cancellationToken);

    [McpServerTool(Name = "element_get", Title = "Get Revit element", ReadOnly = true, OpenWorld = false)]
    [Description("Get a comprehensive Revit parameter dump for one element by stable unique_id.")]
    public static Task<string> ElementGet(
        [Description("Stable Revit element unique_id.")] string unique_id,
        CancellationToken cancellationToken = default) =>
        RevitPipeClient.CallAsync("element_get", new { unique_id }, cancellationToken);

    [McpServerTool(Name = "element_set_parameter", Title = "Set Revit parameter", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Set one writable Revit element parameter. Numeric doubles are raw internal feet; formatted strings use project units.")]
    public static Task<string> ElementSetParameter(
        [Description("Stable Revit element unique_id.")] string unique_id,
        [Description("Exact Revit parameter display name.")] string parameter_name,
        [Description("New string, number, or boolean value.")] JsonElement new_value,
        CancellationToken cancellationToken = default) =>
        RevitPipeClient.CallAsync(
            "element_set_parameter",
            new { unique_id, parameter_name, new_value },
            cancellationToken);

    [McpServerTool(Name = "elements_delete", Title = "Delete Revit elements", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Delete active-document Revit elements by unique_id inside one transaction. This is destructive.")]
    public static Task<string> ElementsDelete(
        [Description("One or more stable Revit element unique_ids.")] string[] unique_ids,
        CancellationToken cancellationToken = default) =>
        RevitPipeClient.CallAsync("elements_delete", new { unique_ids }, cancellationToken);

    [McpServerTool(Name = "view_isolate_or_select", Title = "Select Revit elements", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Select and highlight active-document Revit elements in the current view.")]
    public static Task<string> ViewIsolateOrSelect(
        [Description("One or more integer Revit element IDs.")] long[] element_ids,
        CancellationToken cancellationToken = default) =>
        RevitPipeClient.CallAsync("view_isolate_or_select", new { element_ids }, cancellationToken);
}
