using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using PublicApiGenerator;

// Offline metadata inspection only; this is not a Revit test runner.
try
{
string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.."));
string apiPath = @"C:\Program Files\Autodesk\Revit 2026\RevitAPI.dll";
string reportPath = Path.Combine(root, "docs/review/unvalidated-setters-12c46a90-96ae-46dc-b14d-01bf45655121.json");
string ledgerPath = Path.Combine(root, "RevitCopilot/Agent/Knowledge/revit2026-validation.json");
string destination = Path.Combine(root, "docs/review/setter-validation-gate");
using var input = JsonDocument.Parse(File.ReadAllText(reportPath));
using var ledger = JsonDocument.Parse(File.ReadAllText(ledgerPath));
var phase1 = input.RootElement.GetProperty("entries").EnumerateArray()
    .Where(e => e.GetProperty("state").GetString() == "same_value_only")
    .Select(e => e.GetProperty("operation").GetString()!).ToHashSet(StringComparer.Ordinal);
string[] operations = ledger.RootElement.GetProperty("entries").EnumerateArray()
    .Select(e => e.GetProperty("operation").GetString()!).Where(o => o.StartsWith("api.set:", StringComparison.Ordinal))
    .Order(StringComparer.Ordinal).ToArray();
if (phase1.Count != 70 || operations.Distinct().Count() != operations.Length)
    throw new InvalidOperationException("Input scope/count or operation uniqueness changed; review before regenerating.");
// Unrelated optional database assemblies are absent from the main Revit directory.
// Resolve every reported property type explicitly below instead of requiring the entire assembly graph.
var resolver = new UniversalAssemblyResolver(apiPath, false, ".NETCoreApp,Version=v10.0");
var decompiler = new CSharpDecompiler(apiPath, resolver, new DecompilerSettings { ThrowOnAssemblyResolveErrors = false });
var xml = XDocument.Load(Path.ChangeExtension(apiPath, ".xml")).Descendants("member")
    .Where(e => e.Attribute("name") is not null).GroupBy(e => (string)e.Attribute("name")!)
    .ToDictionary(g => g.Key, g => string.Join("\n", g.Select(e => e.ToString())));
var rows = new List<object>();
var typeNames = new HashSet<string>(StringComparer.Ordinal);
foreach (string operation in operations)
{
    string member = operation[8..];
    int separator = member.LastIndexOf('.');
    string typeName = member[..separator], propertyName = member[(separator + 1)..];
    var type = decompiler.TypeSystem.MainModule.GetTypeDefinition(new FullTypeName(typeName))
        ?? throw new InvalidOperationException($"Unresolved Autodesk metadata type: {typeName}");
    var property = type.Properties.SingleOrDefault(p => p.Name == propertyName && p.Parameters.Count == 0)
        ?? throw new InvalidOperationException($"Unresolved/non-unique declared property: {operation}");
    if (property.Setter?.Accessibility != Accessibility.Public)
        throw new InvalidOperationException($"No public setter: {operation}");
    if (property.ReturnType.Kind == TypeKind.Unknown || property.ReturnType.GetDefinition() is null)
        throw new InvalidOperationException($"Unresolved property type: {operation}");
    typeNames.Add(typeName);
    rows.Add(new { operation, declaring_type = typeName, property_type = property.ReturnType.FullName,
        kind = property.ReturnType.Kind.ToString(), metadata_token = $"0x{MetadataTokens.GetToken(property.MetadataToken):X8}",
        phase1 = phase1.Contains(operation),
        api_documentation = phase1.Contains(operation) ? xml.GetValueOrDefault("P:" + member) : null });
}
AssemblyLoadContext.Default.Resolving += (_, name) => {
    string candidate = Path.Combine(Path.GetDirectoryName(apiPath)!, name.Name + ".dll");
    return File.Exists(candidate) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate) : null;
};
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(apiPath);
Type[] types = typeNames.Order(StringComparer.Ordinal).Select(n => assembly.GetType(n, throwOnError: true)!).ToArray();
string surface = types.GeneratePublicApi(new ApiGeneratorOptions { IncludeAssemblyAttributes = false });
var git = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD") {
    WorkingDirectory = root, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!;
string revision = git.StandardOutput.ReadToEnd().Trim(); git.WaitForExit();
if (git.ExitCode != 0) throw new InvalidOperationException("Cannot establish repository revision.");
Directory.CreateDirectory(destination);
string baseline = Path.Combine(destination, "setter-types.publicapi.txt");
if (File.Exists(baseline) && File.ReadAllText(baseline) != surface)
    throw new InvalidOperationException("Public API baseline drift: review explicitly; baseline was not overwritten.");
if (!File.Exists(baseline)) File.WriteAllText(baseline, surface);
File.WriteAllText(Path.Combine(destination, "setter-surface.json"), JsonSerializer.Serialize(new {
    revision, api = apiPath, api_sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(apiPath))),
    ledger_sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ledgerPath))),
    input_sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(reportPath))),
    api_xml_sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.ChangeExtension(apiPath, ".xml")))),
    public_api_sha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(surface))),
    generator = "PublicApiGenerator 11.5.4", resolver = "ICSharpCode.Decompiler 9.1.0.7988 (ILSpy)",
    resolution_policy = "Unrelated missing references tolerated; every reported property return type must resolve.",
    denominator_scope = "Distinct existing ledger setter operations resolved to public setters in installed Autodesk metadata; not all Revit API setters.",
    denominator = operations.Length, phase1_count = phase1.Count, declaring_types = types.Length, entries = rows
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(new { denominator = operations.Length, phase1_count = phase1.Count,
    declaring_types = types.Length, public_api_bytes = System.Text.Encoding.UTF8.GetByteCount(surface), destination }));
}
catch (Exception error)
{
    Console.Error.WriteLine(JsonSerializer.Serialize(new { error = error.GetType().FullName, reason = error.Message }));
    Environment.ExitCode = 1;
}
