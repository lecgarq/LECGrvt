using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace LECG.RevitCopilot.Agent;

internal sealed record ApiPropertyBinding(string Operation, string Kind, PropertyInfo Property, string Summary)
{
    internal Capability Capability => new(Operation, Kind,
        $"{Kind} {Property.DeclaringType!.FullName}.{Property.Name}: {Summary}",
        Kind == "read" ? "{unique_ids:string[1..50]}" :
        $"{{unique_ids:string[1..50],value:{Property.PropertyType.FullName},units?:revit_internal}}; ElementId input: {{unique_id:string}} or {{builtin_id:negative integer}}; XYZ: {{x:number,y:number,z:number}}; Color: {{red:byte,green:byte,blue:byte}}. Double/Single/XYZ writes require units=revit_internal.");
}

// The catalog binds actual public API accessors, not invented aliases or arbitrary method names.
// No static access, constructors, code evaluation, indexed getters, or document/session operations.
internal static class RevitApiCatalog
{
    private static readonly Lazy<IReadOnlyDictionary<string, ApiPropertyBinding>> Cache = new(Build);
    internal static IReadOnlyDictionary<string, ApiPropertyBinding> All => Cache.Value;
    internal static bool IsApiOperation(string name) => name.StartsWith("api.get:", StringComparison.Ordinal) || name.StartsWith("api.set:", StringComparison.Ordinal);
    internal static ApiPropertyBinding Require(string name, string? kind = null) =>
        All.TryGetValue(name, out var binding) && (kind is null || binding.Kind == kind) ? binding :
            throw new ArgumentException($"Unsupported Revit API operation '{name}'. Search api_search for an exact supported signature.");

    internal static object Search(string query = "", string? kind = null, string? elementType = null, int limit = 8, int offset = 0)
    {
        if (kind is not null and not "read" and not "change") throw new ArgumentException("kind must be read or change.");
        string[] terms = Regex.Split(query, @"[\s._:]+", RegexOptions.CultureInvariant).Where(t => t.Length > 0).Take(12).ToArray();
        string[] reviewedTerms = ReviewedDiscovery.Terms(query);
        var matches = All.Values.Where(b => kind is null || b.Kind == kind)
            .Where(b => elementType is null || AppliesTo(b.Property.DeclaringType!, elementType))
            .Select(b => new { Binding = b, Score = terms.Sum(t =>
                (b.Property.Name.Contains(t, StringComparison.OrdinalIgnoreCase) ? 4 : 0) +
                (b.Property.DeclaringType!.FullName!.Contains(t, StringComparison.OrdinalIgnoreCase) ? 2 : 0) +
                (b.Summary.Contains(t, StringComparison.OrdinalIgnoreCase) ? 1 : 0)) + ReviewedDiscovery.Score(reviewedTerms, b.Operation) })
            .Where(b => terms.Length == 0 || b.Score > 0).OrderByDescending(b => b.Score).ThenBy(b => b.Binding.Operation, StringComparer.Ordinal).ToArray();
        offset = Math.Clamp(offset, 0, All.Count);
        limit = Math.Clamp(limit, 1, 30);
        return new { total_bound_functions = All.Count, matched = matches.Length, offset,
            next_offset = offset + limit < matches.Length ? (int?)(offset + limit) : null,
            evidence = "public_api_accessor_bound; not a claim of successful execution for every project or subtype",
            items = matches.Skip(offset).Take(limit).Select(m => new
            {
                m.Binding.Capability.Name, m.Binding.Capability.Kind, m.Binding.Capability.Description, m.Binding.Capability.Arguments,
                value_type = m.Binding.Property.PropertyType.FullName,
                validation = ApiValidationEvidence.For(m.Binding.Operation),
                enum_values = m.Binding.Property.PropertyType.IsEnum ? Enum.GetNames(m.Binding.Property.PropertyType) : null
            }),
            instructions = "Reads: agent_read or agent_read_batch. Changes: agent_preview or agent_preview_batch then agent_apply. Validation describes disposable sample tests, not universal correctness; same_value_only is not a validated edit. Supply current-document UniqueIds. Revit enforces project-specific constraints. Numeric values use native Revit API units; never guess their physical meaning." };
    }

    private static bool AppliesTo(Type declaring, string name)
    {
        Type? target = declaring.Assembly.GetType(name.StartsWith("Autodesk.", StringComparison.Ordinal) ? name : "Autodesk.Revit.DB." + name);
        return target is not null && declaring.IsAssignableFrom(target);
    }

    private static IReadOnlyDictionary<string, ApiPropertyBinding> Build()
    {
        Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "RevitAPI")
            ?? Assembly.LoadFrom(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Autodesk", "Revit 2026", "RevitAPI.dll"));
        if (assembly.GetName().Version?.Major != 26) throw new InvalidOperationException("Only Revit 2026 API bindings are supported.");
        Type element = assembly.GetType("Autodesk.Revit.DB.Element", true)!;
        var docs = ReadSummaries(Path.ChangeExtension(assembly.Location, ".xml"));
        Dictionary<string, ApiPropertyBinding> result = new(StringComparer.Ordinal);
        foreach (Type type in assembly.GetExportedTypes().Where(t => element.IsAssignableFrom(t) && !t.IsDefined(typeof(ObsoleteAttribute), false)))
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (property.GetIndexParameters().Length != 0 || property.GetMethod?.IsPublic != true ||
                property.IsDefined(typeof(ObsoleteAttribute), false) || !SupportedValue(property.PropertyType)) continue;
            string member = type.FullName + "." + property.Name;
            string summary = docs.GetValueOrDefault("P:" + member, "See Revit 2026 API documentation for applicability and units.");
            result.Add("api.get:" + member, new("api.get:" + member, "read", property, summary));
            if (property.SetMethod?.IsPublic == true)
                result.Add("api.set:" + member, new("api.set:" + member, "change", property, summary));
        }
        return result;
    }

    private static bool SupportedValue(Type type) => type.IsEnum || type.FullName is
        "System.String" or "System.Boolean" or "System.Int32" or "System.Int64" or "System.Double" or "System.Single" or "System.Byte" or "System.Int16" or
        "Autodesk.Revit.DB.ElementId" or "Autodesk.Revit.DB.XYZ" or "Autodesk.Revit.DB.Color";

    private static Dictionary<string, string> ReadSummaries(string path)
    {
        if (!File.Exists(path)) return [];
        return XDocument.Load(path).Descendants("member").Where(m => ((string?)m.Attribute("name"))?.StartsWith("P:", StringComparison.Ordinal) == true)
            .GroupBy(m => (string)m.Attribute("name")!).ToDictionary(g => g.Key, g =>
            {
                string summary = Regex.Replace(g.First().Element("summary")?.Value ?? "", @"\s+", " ").Trim();
                return summary.Length > 600 ? summary[..600] : summary;
            }, StringComparer.Ordinal);
    }
}
