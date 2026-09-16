using System.Text.Json;
using LECG.RevitCopilot.Agent;

internal static class SetterInventory
{
    internal static void Write(string destination)
        => Write(destination, "change");

    internal static void WriteAll(string destination)
        => Write(destination, null);

    private static void Write(string destination, string? kind)
    {
        var rows = RevitApiCatalog.All.Values.Where(b => kind is null || b.Kind == kind).OrderBy(b => b.Operation).Select(b => new
        {
            operation = b.Operation, declaring_type = b.Property.DeclaringType!.FullName, property = b.Property.Name,
            value_type = b.Property.PropertyType.FullName, is_enum = b.Property.PropertyType.IsEnum,
            enum_values = b.Property.PropertyType.IsEnum ? Enum.GetNames(b.Property.PropertyType) : null,
            summary = b.Summary, schema = b.Capability.Arguments
        }).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        File.WriteAllText(destination, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(new { accessors = rows.Length, types = rows.GroupBy(r => r.value_type).Select(g => new { type = g.Key, count = g.Count() }), destination }));
    }
}
