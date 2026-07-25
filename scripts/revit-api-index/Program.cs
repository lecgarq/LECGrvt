using System.Reflection;
using System.Text;

// Emits a flat, greppable index of the public Revit API surface.
// One line per member so `rg` can answer "does this exist / what is the signature"
// without loading anything into a context window.
//
// Usage: dotnet run -- <output-dir> <assembly.dll> [more.dll ...] [probe-dir ...]
//
// A .dll argument is indexed. A directory argument is used for resolution only —
// pass the Revit install dir so types from AdWindows.dll and friends decode.

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: dotnet run -- <output-dir> <assembly.dll> [...] [probe-dir ...]");
    return 1;
}

var outDir = args[0];
var assemblies = args.Skip(1).Where(a => !Directory.Exists(a)).ToArray();
var probeDirs = args.Skip(1).Where(Directory.Exists).ToArray();

if (assemblies.Length == 0)
{
    Console.Error.WriteLine("error: no assemblies given to index");
    return 1;
}

var missing = assemblies.Where(a => !File.Exists(a)).ToArray();
if (missing.Length > 0)
{
    foreach (var m in missing) Console.Error.WriteLine($"error: not found: {m}");
    return 1;
}

Directory.CreateDirectory(outDir);

// Resolve against the assemblies themselves plus every installed shared framework,
// so base types resolve without executing any Revit code. RevitAPIUI references WPF
// (PresentationFramework), which lives in Microsoft.WindowsDesktop.App — not in the
// base runtime dir — so the whole `shared` root gets scanned.
var paths = new List<string>(assemblies);
var coreDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
paths.AddRange(Directory.GetFiles(coreDir, "*.dll"));

var sharedRoot = Directory.GetParent(coreDir)?.Parent;
if (sharedRoot is { Exists: true } && sharedRoot.Name == "shared")
    paths.AddRange(Directory.GetFiles(sharedRoot.FullName, "*.dll", SearchOption.AllDirectories));

foreach (var dir in assemblies.Select(a => Path.GetDirectoryName(a)!).Distinct())
    paths.AddRange(Directory.GetFiles(dir, "*.dll"));

// Extra probe dirs (e.g. the Revit install) resolve types the NuGet reference
// packages do not carry — AdWindows.dll, ReCap interop, and similar.
foreach (var dir in probeDirs)
    paths.AddRange(Directory.GetFiles(dir, "*.dll"));

// First path wins on duplicate simple names; keep the Revit assemblies at the front.
paths = paths.GroupBy(Path.GetFileNameWithoutExtension).Select(g => g.First()).ToList();

using var mlc = new MetadataLoadContext(new PathAssemblyResolver(paths.Distinct()));

var members = new StringBuilder();
var types = new StringBuilder();
int typeCount = 0, memberCount = 0, skipped = 0, degraded = 0;

foreach (var path in assemblies)
{
    var asm = mlc.LoadFromAssemblyPath(Path.GetFullPath(path));

    foreach (var t in asm.GetExportedTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
    {
        typeCount++;
        var kind = t.IsEnum ? "enum" : t.IsInterface ? "interface" : t.IsValueType ? "struct" : "class";
        var bases = new List<string>();
        try
        {
            if (t.BaseType is { } bt && bt.FullName != "System.Object") bases.Add(Sig.Short(bt));
            bases.AddRange(t.GetInterfaces().Select(Sig.Short));
        }
        catch { /* unresolvable base in a C++/CLI type; the type itself still indexes */ }
        var inherits = bases.Count > 0 ? " : " + string.Join(", ", bases) : "";
        types.AppendLine($"{kind} {t.FullName}{inherits}");

        MemberInfo[] declared;
        try
        {
            declared = t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic
                                    | BindingFlags.Instance | BindingFlags.Static
                                    | BindingFlags.DeclaredOnly);
        }
        catch { skipped++; continue; }

        foreach (var m in declared)
        {
            string? line;
            try
            {
                line = Sig.Describe(t, m);
            }
            catch (Exception ex)
            {
                // Usually a referenced assembly missing from the probe set (pass the Revit
                // install dir to fix). Emit the member anyway with its signature marked
                // unknown — dropping it would break the index's guarantee that absence
                // proves a member does not exist.
                if (!IsVisibleMember(m)) continue;
                members.AppendLine($"{t.FullName}.{m.Name}(?) -> ? [{Kind(m)}, signature-undecodable: {ex.GetType().Name}]");
                memberCount++;
                degraded++;
                continue;
            }
            if (line is null) continue;
            members.AppendLine(line);
            memberCount++;
        }
    }
}

File.WriteAllText(Path.Combine(outDir, "types.txt"), types.ToString());
File.WriteAllText(Path.Combine(outDir, "members.txt"), members.ToString());
Console.WriteLine($"{typeCount} types, {memberCount} members " +
                  $"({degraded} with undecodable signatures, {skipped} types unreadable) -> {outDir}");
return 0;

// Name and member kind come from metadata directly and never need signature decoding,
// so these still work for the members that fail full description.
static string Kind(MemberInfo m) => m switch
{
    MethodInfo => "method",
    ConstructorInfo => "ctor",
    PropertyInfo => "property",
    FieldInfo => "field",
    EventInfo => "event",
    _ => "member",
};

static bool IsVisibleMember(MemberInfo m) => m switch
{
    MethodBase mb => mb.IsPublic || mb.IsFamily || mb.IsFamilyOrAssembly,
    FieldInfo f => f.IsPublic || f.IsFamily,
    // Property/event visibility lives on the accessors, which are what failed to decode.
    // Include them: a findable name beats a silent hole.
    _ => true,
};

static class Sig
{
    // Short name so signatures stay readable: Autodesk.Revit.DB.Element -> Element.
    public static string Short(Type t)
    {
        if (t.IsByRef) return "ref " + Short(t.GetElementType()!);
        if (t.IsArray) return Short(t.GetElementType()!) + "[]";
        if (!t.IsGenericType) return t.Name;
        var args = string.Join(", ", t.GetGenericArguments().Select(Short));
        var name = t.Name.Contains('`') ? t.Name[..t.Name.IndexOf('`')] : t.Name;
        return $"{name}<{args}>";
    }

    static string Params(ParameterInfo[] ps) =>
        string.Join(", ", ps.Select(p => $"{Short(p.ParameterType)} {p.Name}"));

    public static string? Describe(Type t, MemberInfo m)
    {
        switch (m)
        {
            case MethodInfo mi when IsVisible(mi) && !mi.IsSpecialName:
                var stat = mi.IsStatic ? "static " : "";
                return $"{t.FullName}.{mi.Name}({Params(mi.GetParameters())}) -> {Short(mi.ReturnType)} [{stat}method]";

            case ConstructorInfo ci when IsVisible(ci):
                return $"{t.FullName}..ctor({Params(ci.GetParameters())}) [ctor]";

            case PropertyInfo pi:
                var acc = pi.GetAccessors(nonPublic: true).Where(IsVisible).ToArray();
                if (acc.Length == 0) return null;
                var rw = (pi.CanRead ? "get" : "") + (pi.CanWrite ? (pi.CanRead ? "/set" : "set") : "");
                return $"{t.FullName}.{pi.Name} -> {Short(pi.PropertyType)} [{rw} property]";

            case FieldInfo fi when fi.IsPublic || fi.IsFamily:
                // Enum members carry their literal value; it is often the thing being looked up.
                var val = t.IsEnum && fi.IsLiteral ? $" = {fi.GetRawConstantValue()}" : "";
                return $"{t.FullName}.{fi.Name}{val} -> {Short(fi.FieldType)} [field]";

            case EventInfo ei when ei.AddMethod is { } am && IsVisible(am):
                return $"{t.FullName}.{ei.Name} -> {Short(ei.EventHandlerType!)} [event]";

            default:
                return null;
        }
    }

    static bool IsVisible(MethodBase m) => m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly;
}
