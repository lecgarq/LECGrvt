using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;

const string DefaultRevitDir = @"C:\Program Files\Autodesk\Revit 2026";
const string RevitApiDll = "RevitAPI.dll";
const string RevitApiUiDll = "RevitAPIUI.dll";

string repoRoot = FindRepoRoot(AppContext.BaseDirectory);
string revitDir = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
    ? args[0]
    : Environment.GetEnvironmentVariable("REVIT_2026_DIR") ?? DefaultRevitDir;
string outputDir = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
    ? Path.GetFullPath(args[1])
    : Path.Combine(repoRoot, "docs", "review", "revit-api");
string addinAssemblyPath = Path.Combine(repoRoot, "bin", "Debug", "net8.0-windows", "LECG.dll");

string? currentPath = Environment.GetEnvironmentVariable("PATH");
if (currentPath is null || !currentPath.Split(Path.PathSeparator).Contains(revitDir, StringComparer.OrdinalIgnoreCase))
{
    Environment.SetEnvironmentVariable("PATH", string.IsNullOrWhiteSpace(currentPath) ? revitDir : $"{revitDir}{Path.PathSeparator}{currentPath}");
}

string revitApiPath = Path.Combine(revitDir, RevitApiDll);
string revitApiUiPath = Path.Combine(revitDir, RevitApiUiDll);

if (!File.Exists(revitApiPath) || !File.Exists(revitApiUiPath))
{
    Console.Error.WriteLine($"Revit API DLLs not found under '{revitDir}'.");
    return 1;
}

Directory.CreateDirectory(outputDir);

var summaries = new List<AssemblySummary>();
var failures = new List<string>();
var hostResolver = new HostAssemblyResolver(revitDir, addinAssemblyPath);
AppDomain.CurrentDomain.AssemblyResolve += (_, eventArgs) => hostResolver.Resolve(new AssemblyName(eventArgs.Name));

var loadContext = new RevitAssemblyLoadContext(revitDir, addinAssemblyPath);
try
{
    foreach (string assemblyPath in new[] { revitApiPath, revitApiUiPath })
    {
        try
        {
            Assembly assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            summaries.Add(ExportAssembly(assembly, outputDir));
        }
        catch (Exception ex)
        {
            try
            {
                summaries.Add(MetadataAssemblyExporter.Export(assemblyPath, outputDir));
                failures.Add($"{Path.GetFileName(assemblyPath)}: runtime load failed with {ex.GetType().Name}; exported via metadata fallback");
            }
            catch (Exception metadataEx)
            {
                failures.Add($"{Path.GetFileName(assemblyPath)}: {ex.GetType().Name} - {ex.Message}");
                failures.Add($"{Path.GetFileName(assemblyPath)} metadata fallback: {metadataEx.GetType().Name} - {metadataEx.Message}");
            }
        }
    }
}
finally
{
}

WriteSummary(Path.Combine(outputDir, "README.md"), revitDir, summaries, failures);
WriteLlmArtifacts(outputDir);
Console.WriteLine($"Exported Revit API metadata to '{outputDir}'.");
return 0;

static AssemblySummary ExportAssembly(Assembly assembly, string outputDir)
{
    string assemblyName = assembly.GetName().Name ?? Path.GetFileNameWithoutExtension(assembly.Location);
    string slug = assemblyName.ToLowerInvariant();
    string typesPath = Path.Combine(outputDir, $"{slug}.types.jsonl");
    string membersPath = Path.Combine(outputDir, $"{slug}.members.jsonl");
    string publicTypesPath = Path.Combine(outputDir, $"{slug}.public.types.jsonl");
    string publicMembersPath = Path.Combine(outputDir, $"{slug}.public.members.jsonl");

    int typeCount = 0;
    int publicTypeCount = 0;
    int memberCount = 0;
    int publicMemberCount = 0;
    var namespaces = new HashSet<string>(StringComparer.Ordinal);

    using var typeWriter = new StreamWriter(typesPath, false, new UTF8Encoding(false));
    using var memberWriter = new StreamWriter(membersPath, false, new UTF8Encoding(false));
    using var publicTypeWriter = new StreamWriter(publicTypesPath, false, new UTF8Encoding(false));
    using var publicMemberWriter = new StreamWriter(publicMembersPath, false, new UTF8Encoding(false));

    foreach (Type type in GetLoadableTypes(assembly).OrderBy(t => t.FullName, StringComparer.Ordinal))
    {
        string? ns = type.Namespace;
        if (!string.IsNullOrWhiteSpace(ns))
        {
            namespaces.Add(ns);
        }

        var typeRecord = new TypeRecord(
            Assembly: assemblyName,
            Namespace: ns,
            Name: type.Name,
            FullName: GetTypeDisplayName(type),
            Kind: GetTypeKind(type),
            Visibility: GetTypeVisibility(type),
            BaseType: type.BaseType is null ? null : SafeGetTypeDisplayName(() => type.BaseType),
            Interfaces: SafeGetTypeNames(type.GetInterfaces),
            GenericArguments: type.IsGenericType ? type.GetGenericArguments().Select(GetTypeDisplayName).ToArray() : Array.Empty<string>(),
            Attributes: SafeGetAttributeNames(type),
            IsAbstract: type.IsAbstract,
            IsSealed: type.IsSealed,
            IsStatic: type.IsAbstract && type.IsSealed,
            IsDisposable: SafeImplementsInterface(type, "System.IDisposable"));

        typeWriter.WriteLine(JsonSerializer.Serialize(typeRecord));
        typeCount++;
        bool isPublicType = IsTypeVisibleOutsideAssembly(type);
        if (isPublicType)
        {
            publicTypeWriter.WriteLine(JsonSerializer.Serialize(typeRecord));
            publicTypeCount++;
        }

        foreach (MemberRecord member in GetMemberRecords(assemblyName, type))
        {
            memberWriter.WriteLine(JsonSerializer.Serialize(member));
            memberCount++;

            if (isPublicType && IsVisibilityOutsideAssembly(member.Visibility))
            {
                publicMemberWriter.WriteLine(JsonSerializer.Serialize(member));
                publicMemberCount++;
            }
        }
    }

    return new AssemblySummary(assemblyName, typesPath, membersPath, publicTypesPath, publicMembersPath, typeCount, publicTypeCount, memberCount, publicMemberCount, namespaces.Count);
}

static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
{
    try
    {
        return assembly.GetTypes();
    }
    catch (ReflectionTypeLoadException ex)
    {
        return ex.Types.Where(t => t is not null)!;
    }
}

static IEnumerable<MemberRecord> GetMemberRecords(string assemblyName, Type type)
{
    const BindingFlags Flags =
        BindingFlags.Instance |
        BindingFlags.Static |
        BindingFlags.Public |
        BindingFlags.NonPublic |
        BindingFlags.DeclaredOnly;

    foreach (ConstructorInfo ctor in type.GetConstructors(Flags).OrderBy(c => c.MetadataToken))
    {
        yield return CreateMethodLikeRecord(assemblyName, type, ctor, "constructor", null);
    }

    foreach (MethodInfo method in type.GetMethods(Flags).Where(ShouldExportMethod).OrderBy(m => m.MetadataToken))
    {
        yield return CreateMethodLikeRecord(assemblyName, type, method, "method", SafeGetTypeDisplayName(() => method.ReturnType));
    }

    foreach (PropertyInfo property in type.GetProperties(Flags).OrderBy(p => p.MetadataToken))
    {
        MethodInfo? accessor = property.GetMethod ?? property.SetMethod;
        yield return new MemberRecord(
            Assembly: assemblyName,
            Namespace: type.Namespace,
            DeclaringType: GetTypeDisplayName(type),
            MemberKind: "property",
            Name: property.Name,
            Signature: $"{GetTypeDisplayName(type)}.{property.Name}",
            Visibility: accessor is null ? "unknown" : GetMethodVisibility(accessor),
            ReturnType: SafeGetTypeDisplayName(() => property.PropertyType),
            Parameters: SafeGetParametersFromGetter(() => property.GetIndexParameters()),
            Attributes: SafeGetAttributeNames(property),
            IsStatic: accessor?.IsStatic ?? false,
            IsAbstract: accessor?.IsAbstract ?? false,
            IsVirtual: accessor?.IsVirtual ?? false);
    }

    if (type.IsEnum)
    {
        foreach (FieldInfo field in type.GetFields(Flags).Where(f => f.IsLiteral).OrderBy(f => f.MetadataToken))
        {
            yield return new MemberRecord(
                Assembly: assemblyName,
                Namespace: type.Namespace,
                DeclaringType: GetTypeDisplayName(type),
                MemberKind: "enumValue",
                Name: field.Name,
                Signature: $"{GetTypeDisplayName(type)}.{field.Name}",
                Visibility: GetFieldVisibility(field),
                ReturnType: SafeGetTypeDisplayName(() => field.FieldType),
                Parameters: Array.Empty<ParameterRecord>(),
                Attributes: SafeGetAttributeNames(field),
                IsStatic: true,
                IsAbstract: false,
                IsVirtual: false,
                ConstantValue: field.GetRawConstantValue()?.ToString());
        }
    }
    else
    {
        foreach (FieldInfo field in type.GetFields(Flags).Where(f => !f.IsSpecialName).OrderBy(f => f.MetadataToken))
        {
            yield return new MemberRecord(
                Assembly: assemblyName,
                Namespace: type.Namespace,
                DeclaringType: GetTypeDisplayName(type),
                MemberKind: "field",
                Name: field.Name,
                Signature: $"{GetTypeDisplayName(type)}.{field.Name}",
                Visibility: GetFieldVisibility(field),
                ReturnType: SafeGetTypeDisplayName(() => field.FieldType),
                Parameters: Array.Empty<ParameterRecord>(),
                Attributes: SafeGetAttributeNames(field),
                IsStatic: field.IsStatic,
                IsAbstract: false,
                IsVirtual: false,
                ConstantValue: field.IsLiteral ? field.GetRawConstantValue()?.ToString() : null);
        }
    }

    foreach (EventInfo eventInfo in type.GetEvents(Flags).OrderBy(e => e.MetadataToken))
    {
        MethodInfo? addMethod = eventInfo.AddMethod ?? eventInfo.RemoveMethod;
        yield return new MemberRecord(
            Assembly: assemblyName,
            Namespace: type.Namespace,
            DeclaringType: GetTypeDisplayName(type),
            MemberKind: "event",
            Name: eventInfo.Name,
            Signature: $"{GetTypeDisplayName(type)}.{eventInfo.Name}",
            Visibility: addMethod is null ? "unknown" : GetMethodVisibility(addMethod),
            ReturnType: SafeGetTypeDisplayName(() => eventInfo.EventHandlerType ?? typeof(void)),
            Parameters: Array.Empty<ParameterRecord>(),
            Attributes: SafeGetAttributeNames(eventInfo),
            IsStatic: addMethod?.IsStatic ?? false,
            IsAbstract: addMethod?.IsAbstract ?? false,
            IsVirtual: addMethod?.IsVirtual ?? false);
    }
}

static MemberRecord CreateMethodLikeRecord(string assemblyName, Type type, MethodBase method, string memberKind, string? returnType)
{
    string signature = $"{GetTypeDisplayName(type)}.{method.Name}({string.Join(", ", SafeGetParameters(method).Select(p => p.Type))})";
    return new MemberRecord(
        Assembly: assemblyName,
        Namespace: type.Namespace,
        DeclaringType: GetTypeDisplayName(type),
        MemberKind: memberKind,
        Name: method.Name,
        Signature: signature,
        Visibility: GetMethodVisibility(method),
        ReturnType: returnType,
        Parameters: SafeGetParameters(method),
        Attributes: SafeGetAttributeNames(method),
        IsStatic: method.IsStatic,
        IsAbstract: method.IsAbstract,
        IsVirtual: method.IsVirtual);
}

static ParameterRecord CreateParameterRecord(ParameterInfo parameter)
{
    object? defaultValue = null;
    if (parameter.HasDefaultValue)
    {
        defaultValue = parameter.DefaultValue switch
        {
            null => null,
            Type missingType when missingType == typeof(Missing) => "Missing",
            _ => parameter.DefaultValue
        };
    }

    return new ParameterRecord(
        Name: parameter.Name ?? string.Empty,
        Type: SafeGetTypeDisplayName(() => parameter.ParameterType) ?? "<unresolved>",
        Position: parameter.Position,
        IsOut: parameter.IsOut,
        IsOptional: parameter.IsOptional,
        HasDefaultValue: parameter.HasDefaultValue,
        DefaultValue: defaultValue?.ToString());
}

static ParameterRecord[] SafeGetParameters(MethodBase method)
{
    try
    {
        return method.GetParameters().Select(CreateParameterRecord).ToArray();
    }
    catch (Exception ex) when (IsMetadataResolutionException(ex))
    {
        return new[] { new ParameterRecord("<unresolved>", $"<unresolved:{ex.GetType().Name}>", -1, false, false, false, null) };
    }
}

static ParameterRecord[] SafeGetParametersFromGetter(Func<ParameterInfo[]> getter)
{
    try
    {
        return getter().Select(CreateParameterRecord).ToArray();
    }
    catch (Exception ex) when (IsMetadataResolutionException(ex))
    {
        return new[] { new ParameterRecord("<unresolved>", $"<unresolved:{ex.GetType().Name}>", -1, false, false, false, null) };
    }
}

static string[] SafeGetAttributeNames(MemberInfo member)
{
    try
    {
        return member.GetCustomAttributesData()
            .Select(a => GetTypeDisplayName(a.AttributeType))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
    }
    catch (Exception ex) when (IsMetadataResolutionException(ex))
    {
        return new[] { $"<unresolved:{ex.GetType().Name}>" };
    }
}

static string[] SafeGetTypeNames(Func<Type[]> getter)
{
    try
    {
        return getter()
            .Select(GetTypeDisplayName)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
    }
    catch (Exception ex) when (IsMetadataResolutionException(ex))
    {
        return new[] { $"<unresolved:{ex.GetType().Name}>" };
    }
}

static string? SafeGetTypeDisplayName(Func<Type> getter)
{
    try
    {
        return GetTypeDisplayName(getter());
    }
    catch (Exception ex) when (IsMetadataResolutionException(ex))
    {
        return $"<unresolved:{ex.GetType().Name}>";
    }
}

static bool SafeImplementsInterface(Type type, string fullName)
{
    try
    {
        return ImplementsInterface(type, fullName);
    }
    catch (Exception) when (type is not null)
    {
        return false;
    }
}

static bool IsMetadataResolutionException(Exception ex)
{
    return ex is FileNotFoundException
        or FileLoadException
        or BadImageFormatException
        or TypeLoadException
        or ReflectionTypeLoadException;
}

static bool ShouldExportMethod(MethodInfo method)
{
    if (!method.IsSpecialName)
    {
        return true;
    }

    return method.Name.StartsWith("op_", StringComparison.Ordinal);
}

static string GetTypeKind(Type type)
{
    if (type.IsEnum)
    {
        return "enum";
    }

    if (typeof(MulticastDelegate).IsAssignableFrom(type.BaseType))
    {
        return "delegate";
    }

    if (type.IsInterface)
    {
        return "interface";
    }

    if (type.IsValueType)
    {
        return "struct";
    }

    return "class";
}

static string GetTypeVisibility(Type type)
{
    if (type.IsPublic || type.IsNestedPublic)
    {
        return "public";
    }

    if (type.IsNestedFamily)
    {
        return "protected";
    }

    if (type.IsNestedFamORAssem)
    {
        return "protected internal";
    }

    if (type.IsNestedAssembly)
    {
        return "internal";
    }

    if (type.IsNestedFamANDAssem)
    {
        return "private protected";
    }

    if (type.IsNestedPrivate)
    {
        return "private";
    }

    return type.IsNotPublic ? "internal" : "unknown";
}

static string GetMethodVisibility(MethodBase method)
{
    if (method.IsPublic)
    {
        return "public";
    }

    if (method.IsFamily)
    {
        return "protected";
    }

    if (method.IsFamilyOrAssembly)
    {
        return "protected internal";
    }

    if (method.IsAssembly)
    {
        return "internal";
    }

    if (method.IsFamilyAndAssembly)
    {
        return "private protected";
    }

    if (method.IsPrivate)
    {
        return "private";
    }

    return "unknown";
}

static string GetFieldVisibility(FieldInfo field)
{
    if (field.IsPublic)
    {
        return "public";
    }

    if (field.IsFamily)
    {
        return "protected";
    }

    if (field.IsFamilyOrAssembly)
    {
        return "protected internal";
    }

    if (field.IsAssembly)
    {
        return "internal";
    }

    if (field.IsFamilyAndAssembly)
    {
        return "private protected";
    }

    if (field.IsPrivate)
    {
        return "private";
    }

    return "unknown";
}

static bool IsTypeVisibleOutsideAssembly(Type type)
{
    return type.IsPublic || type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamORAssem;
}

static bool IsVisibilityOutsideAssembly(string visibility)
{
    return string.Equals(visibility, "public", StringComparison.Ordinal)
        || string.Equals(visibility, "protected", StringComparison.Ordinal)
        || string.Equals(visibility, "protected internal", StringComparison.Ordinal);
}

static bool ImplementsInterface(Type type, string fullName)
{
    return type.GetInterfaces().Any(i => string.Equals(i.FullName, fullName, StringComparison.Ordinal));
}

static string GetTypeDisplayName(Type type)
{
    if (type.IsByRef)
    {
        return $"{GetTypeDisplayName(type.GetElementType()!)}&";
    }

    if (type.IsPointer)
    {
        return $"{GetTypeDisplayName(type.GetElementType()!)}*";
    }

    if (type.IsArray)
    {
        return $"{GetTypeDisplayName(type.GetElementType()!)}[{new string(',', type.GetArrayRank() - 1)}]";
    }

    if (type.IsGenericParameter)
    {
        return type.Name;
    }

    string? fullName = type.FullName;
    if (!type.IsGenericType)
    {
        return fullName ?? type.Name;
    }

    string genericName = fullName ?? type.Name;
    int backtick = genericName.IndexOf('`');
    if (backtick >= 0)
    {
        genericName = genericName[..backtick];
    }

    string args = string.Join(", ", type.GetGenericArguments().Select(GetTypeDisplayName));
    return $"{genericName}<{args}>";
}

static void WriteSummary(string readmePath, string revitDir, IEnumerable<AssemblySummary> summaries, IEnumerable<string> failures)
{
    var lines = new List<string>
    {
        "# Revit API Export",
        string.Empty,
        $"Source directory: `{revitDir}`",
        string.Empty,
        "Generated files:",
        string.Empty
    };

    foreach (AssemblySummary summary in summaries.OrderBy(s => s.Assembly, StringComparer.Ordinal))
    {
        lines.Add($"- `{Path.GetFileName(summary.TypesPath)}`: {summary.TypeCount} types, {summary.PublicTypeCount} externally visible, {summary.NamespaceCount} namespaces");
        lines.Add($"- `{Path.GetFileName(summary.MembersPath)}`: {summary.MemberCount} members");
        lines.Add($"- `{Path.GetFileName(summary.PublicTypesPath)}`: filtered externally visible types only");
        lines.Add($"- `{Path.GetFileName(summary.PublicMembersPath)}`: {summary.PublicMemberCount} public/protected members on externally visible types");
    }

    string[] failureItems = failures.ToArray();
    if (failureItems.Length > 0)
    {
        lines.Add(string.Empty);
        lines.Add("Load failures:");
        lines.Add(string.Empty);
        foreach (string failure in failureItems)
        {
            lines.Add($"- {failure}");
        }
    }

    lines.Add(string.Empty);
    lines.Add("Record shape:");
    lines.Add(string.Empty);
    lines.Add("- Type records include namespace, base type, interfaces, generic arguments, attributes, visibility, and lifecycle flags.");
    lines.Add("- Member records include kind, declaring type, signature, return type, parameters, attributes, and basic dispatch flags.");
    lines.Add(string.Empty);
    lines.Add("Regenerate with:");
    lines.Add(string.Empty);
    lines.Add("```powershell");
    lines.Add("dotnet run --project tools/RevitApiExtractor/RevitApiExtractor.csproj -- \"C:\\Program Files\\Autodesk\\Revit 2026\" \"docs/review/revit-api\"");
    lines.Add("```");

    File.WriteAllLines(readmePath, lines, new UTF8Encoding(false));
}

static void WriteLlmArtifacts(string outputDir)
{
    List<TypeRecord> publicTypes = LoadJsonLines<TypeRecord>(Directory.EnumerateFiles(outputDir, "*.public.types.jsonl"));
    List<MemberRecord> publicMembers = LoadJsonLines<MemberRecord>(Directory.EnumerateFiles(outputDir, "*.public.members.jsonl"));

    WriteJsonLines(
        Path.Combine(outputDir, "revit.autodesk.revit.db.public.types.jsonl"),
        publicTypes.Where(t => t.Namespace?.StartsWith("Autodesk.Revit.DB", StringComparison.Ordinal) == true));

    WriteJsonLines(
        Path.Combine(outputDir, "revit.autodesk.revit.db.public.members.jsonl"),
        publicMembers.Where(m => m.Namespace?.StartsWith("Autodesk.Revit.DB", StringComparison.Ordinal) == true));

    WriteJsonLines(
        Path.Combine(outputDir, "revit.autodesk.revit.ui.public.types.jsonl"),
        publicTypes.Where(t => mNamespaceStartsWithUi(t.Namespace)));

    WriteJsonLines(
        Path.Combine(outputDir, "revit.autodesk.revit.ui.public.members.jsonl"),
        publicMembers.Where(m => mNamespaceStartsWithUi(m.Namespace)));

    WriteJsonLines(
        Path.Combine(outputDir, "revit.autodesk.revit.public.types.jsonl"),
        publicTypes.Where(t => t.Namespace?.StartsWith("Autodesk.Revit", StringComparison.Ordinal) == true));

    WriteJsonLines(
        Path.Combine(outputDir, "revit.autodesk.revit.public.members.jsonl"),
        publicMembers.Where(m => m.Namespace?.StartsWith("Autodesk.Revit", StringComparison.Ordinal) == true));

    var namespaceIndex = publicTypes
        .GroupBy(t => t.Namespace ?? string.Empty, StringComparer.Ordinal)
        .Select(g =>
        {
            int memberCount = publicMembers.Count(m => string.Equals(m.Namespace ?? string.Empty, g.Key, StringComparison.Ordinal));
            return new NamespaceIndexRecord(
                Namespace: g.Key,
                TypeCount: g.Count(),
                MemberCount: memberCount,
                Assemblies: g.Select(t => t.Assembly).Distinct(StringComparer.Ordinal).OrderBy(a => a, StringComparer.Ordinal).ToArray());
        })
        .OrderByDescending(r => r.TypeCount)
        .ThenBy(r => r.Namespace, StringComparer.Ordinal)
        .ToArray();

    File.WriteAllText(
        Path.Combine(outputDir, "revit.public.namespaces.json"),
        JsonSerializer.Serialize(namespaceIndex, new JsonSerializerOptions { WriteIndented = true }),
        new UTF8Encoding(false));

    var llmIndex = new LlmIndexRecord(
        PublicTypeFiles: Directory.EnumerateFiles(outputDir, "*.public.types.jsonl").Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray()!,
        PublicMemberFiles: Directory.EnumerateFiles(outputDir, "*.public.members.jsonl").Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray()!,
        NamespaceIndexFile: "revit.public.namespaces.json",
        AutodeskRevitTypeFile: "revit.autodesk.revit.public.types.jsonl",
        AutodeskRevitMemberFile: "revit.autodesk.revit.public.members.jsonl",
        DbTypeFile: "revit.autodesk.revit.db.public.types.jsonl",
        DbMemberFile: "revit.autodesk.revit.db.public.members.jsonl",
        UiTypeFile: "revit.autodesk.revit.ui.public.types.jsonl",
        UiMemberFile: "revit.autodesk.revit.ui.public.members.jsonl");

    File.WriteAllText(
        Path.Combine(outputDir, "revit.llm.index.json"),
        JsonSerializer.Serialize(llmIndex, new JsonSerializerOptions { WriteIndented = true }),
        new UTF8Encoding(false));

    static bool mNamespaceStartsWithUi(string? ns) =>
        ns?.StartsWith("Autodesk.Revit.UI", StringComparison.Ordinal) == true;
}

static List<T> LoadJsonLines<T>(IEnumerable<string> paths)
{
    var items = new List<T>();
    foreach (string path in paths)
    {
        foreach (string line in File.ReadLines(path))
        {
            T? item = JsonSerializer.Deserialize<T>(line);
            if (item is not null)
            {
                items.Add(item);
            }
        }
    }

    return items;
}

static void WriteJsonLines<T>(string path, IEnumerable<T> items)
{
    using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
    foreach (T item in items)
    {
        writer.WriteLine(JsonSerializer.Serialize(item));
    }
}

static string FindRepoRoot(string startDirectory)
{
    DirectoryInfo? directory = new DirectoryInfo(startDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "LECG.sln")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Could not locate repository root from tool output path.");
}

sealed class RevitAssemblyLoadContext : AssemblyLoadContext
{
    private readonly string _revitDir;
    private readonly Dictionary<string, string> _runtimeAssemblies;
    private readonly AssemblyDependencyResolver? _projectResolver;
    private readonly string _nugetPackagesRoot;

    public RevitAssemblyLoadContext(string revitDir, string addinAssemblyPath)
        : base(nameof(RevitAssemblyLoadContext), isCollectible: false)
    {
        _revitDir = revitDir;
        _runtimeAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            ?.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .GroupBy(path => Path.GetFileNameWithoutExtension(path)!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _projectResolver = File.Exists(addinAssemblyPath)
            ? new AssemblyDependencyResolver(addinAssemblyPath)
            : null;
        _nugetPackagesRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        string candidate = Path.Combine(_revitDir, $"{assemblyName.Name}.dll");
        if (File.Exists(candidate))
        {
            return LoadFromAssemblyPath(candidate);
        }

        if (assemblyName.Name is not null && _runtimeAssemblies.TryGetValue(assemblyName.Name, out string? runtimeAssembly) && runtimeAssembly is not null)
        {
            return LoadFromAssemblyPath(runtimeAssembly);
        }

        string? resolvedPath = _projectResolver?.ResolveAssemblyToPath(assemblyName);
        if (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath))
        {
            return LoadFromAssemblyPath(resolvedPath);
        }

        string? packageAssemblyPath = ResolveFromNuGetCache(assemblyName.Name);
        if (!string.IsNullOrWhiteSpace(packageAssemblyPath) && File.Exists(packageAssemblyPath))
        {
            return LoadFromAssemblyPath(packageAssemblyPath);
        }

        return null;
    }

    private string? ResolveFromNuGetCache(string? assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return null;
        }

        string packageRoot = Path.Combine(_nugetPackagesRoot, assemblyName.ToLowerInvariant());
        if (!Directory.Exists(packageRoot))
        {
            return null;
        }

        return Directory.EnumerateFiles(packageRoot, $"{assemblyName}.dll", SearchOption.AllDirectories)
            .OrderByDescending(GetFrameworkPreference)
            .ThenByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static int GetFrameworkPreference(string path)
    {
        if (path.Contains($"{Path.DirectorySeparatorChar}net8.0{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return 6;
        }

        if (path.Contains($"{Path.DirectorySeparatorChar}net7.0{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return 5;
        }

        if (path.Contains($"{Path.DirectorySeparatorChar}net6.0{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (path.Contains($"{Path.DirectorySeparatorChar}netstandard2.1{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (path.Contains($"{Path.DirectorySeparatorChar}netstandard2.0{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (path.Contains($"{Path.DirectorySeparatorChar}net462{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 0;
    }
}

sealed class HostAssemblyResolver
{
    private readonly string _revitDir;
    private readonly AssemblyDependencyResolver? _projectResolver;
    private readonly string _nugetPackagesRoot;

    public HostAssemblyResolver(string revitDir, string addinAssemblyPath)
    {
        _revitDir = revitDir;
        _projectResolver = File.Exists(addinAssemblyPath)
            ? new AssemblyDependencyResolver(addinAssemblyPath)
            : null;
        _nugetPackagesRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
    }

    public Assembly? Resolve(AssemblyName assemblyName)
    {
        string candidate = Path.Combine(_revitDir, $"{assemblyName.Name}.dll");
        if (File.Exists(candidate))
        {
            return Assembly.LoadFrom(candidate);
        }

        string? resolvedPath = _projectResolver?.ResolveAssemblyToPath(assemblyName);
        if (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath))
        {
            return Assembly.LoadFrom(resolvedPath);
        }

        string? packageAssemblyPath = ResolveFromNuGetCache(assemblyName.Name);
        if (!string.IsNullOrWhiteSpace(packageAssemblyPath) && File.Exists(packageAssemblyPath))
        {
            return Assembly.LoadFrom(packageAssemblyPath);
        }

        return null;
    }

    private string? ResolveFromNuGetCache(string? assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return null;
        }

        string packageRoot = Path.Combine(_nugetPackagesRoot, assemblyName.ToLowerInvariant());
        if (!Directory.Exists(packageRoot))
        {
            return null;
        }

        return Directory.EnumerateFiles(packageRoot, $"{assemblyName}.dll", SearchOption.AllDirectories)
            .OrderByDescending(GetFrameworkPreference)
            .ThenByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static int GetFrameworkPreference(string path)
    {
        if (path.Contains($"{Path.DirectorySeparatorChar}net8.0{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return 6;
        }

        if (path.Contains($"{Path.DirectorySeparatorChar}net7.0{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return 5;
        }

        if (path.Contains($"{Path.DirectorySeparatorChar}net6.0{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (path.Contains($"{Path.DirectorySeparatorChar}netstandard2.1{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (path.Contains($"{Path.DirectorySeparatorChar}netstandard2.0{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (path.Contains($"{Path.DirectorySeparatorChar}net462{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 0;
    }
}

internal sealed record TypeRecord(
    string Assembly,
    string? Namespace,
    string Name,
    string FullName,
    string Kind,
    string Visibility,
    string? BaseType,
    string[] Interfaces,
    string[] GenericArguments,
    string[] Attributes,
    bool IsAbstract,
    bool IsSealed,
    bool IsStatic,
    bool IsDisposable);

internal sealed record MemberRecord(
    string Assembly,
    string? Namespace,
    string DeclaringType,
    string MemberKind,
    string Name,
    string Signature,
    string Visibility,
    string? ReturnType,
    ParameterRecord[] Parameters,
    string[] Attributes,
    bool IsStatic,
    bool IsAbstract,
    bool IsVirtual,
    string? ConstantValue = null);

internal sealed record ParameterRecord(
    string Name,
    string Type,
    int Position,
    bool IsOut,
    bool IsOptional,
    bool HasDefaultValue,
    string? DefaultValue);

internal sealed record AssemblySummary(
    string Assembly,
    string TypesPath,
    string MembersPath,
    string PublicTypesPath,
    string PublicMembersPath,
    int TypeCount,
    int PublicTypeCount,
    int MemberCount,
    int PublicMemberCount,
    int NamespaceCount);

internal sealed record NamespaceIndexRecord(
    string Namespace,
    int TypeCount,
    int MemberCount,
    string[] Assemblies);

internal sealed record LlmIndexRecord(
    string[] PublicTypeFiles,
    string[] PublicMemberFiles,
    string NamespaceIndexFile,
    string AutodeskRevitTypeFile,
    string AutodeskRevitMemberFile,
    string DbTypeFile,
    string DbMemberFile,
    string UiTypeFile,
    string UiMemberFile);
