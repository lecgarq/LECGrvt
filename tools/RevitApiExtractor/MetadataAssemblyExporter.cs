using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;

static class MetadataAssemblyExporter
{
    public static AssemblySummary Export(string assemblyPath, string outputDir)
    {
        using FileStream stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        MetadataReader reader = peReader.GetMetadataReader();

        string assemblyName = reader.IsAssembly ? reader.GetString(reader.GetAssemblyDefinition().Name) : Path.GetFileNameWithoutExtension(assemblyPath);
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
        var provider = new MetadataNameProvider(reader);

        using var typeWriter = new StreamWriter(typesPath, false, new UTF8Encoding(false));
        using var memberWriter = new StreamWriter(membersPath, false, new UTF8Encoding(false));
        using var publicTypeWriter = new StreamWriter(publicTypesPath, false, new UTF8Encoding(false));
        using var publicMemberWriter = new StreamWriter(publicMembersPath, false, new UTF8Encoding(false));

        foreach (TypeDefinitionHandle handle in reader.TypeDefinitions)
        {
            TypeDefinition type = reader.GetTypeDefinition(handle);
            string name = reader.GetString(type.Name);
            string? ns = GetNamespace(reader, handle, type);
            string fullName = string.IsNullOrWhiteSpace(ns) ? name : $"{ns}.{name}";
            bool isPublicType = IsTypeVisibleOutsideAssembly(type.Attributes);

            if (!string.IsNullOrWhiteSpace(ns))
            {
                namespaces.Add(ns);
            }

            string[] interfaces = type.GetInterfaceImplementations()
                .Select(h => GetEntityName(reader, reader.GetInterfaceImplementation(h).Interface))
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray()!;

            string[] genericArguments = type.GetGenericParameters()
                .Select(h => reader.GetString(reader.GetGenericParameter(h).Name))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            string[] attributes = GetAttributeNames(reader, type.GetCustomAttributes());

            string? baseTypeName = type.BaseType.IsNil ? null : GetEntityName(reader, type.BaseType);
            string typeKind = GetTypeKind(type.Attributes, baseTypeName);

            var typeRecord = new TypeRecord(
                Assembly: assemblyName,
                Namespace: ns,
                Name: name,
                FullName: fullName,
                Kind: typeKind,
                Visibility: GetTypeVisibility(type.Attributes),
                BaseType: baseTypeName,
                Interfaces: interfaces,
                GenericArguments: genericArguments,
                Attributes: attributes,
                IsAbstract: type.Attributes.HasFlag(TypeAttributes.Abstract),
                IsSealed: type.Attributes.HasFlag(TypeAttributes.Sealed),
                IsStatic: type.Attributes.HasFlag(TypeAttributes.Abstract) && type.Attributes.HasFlag(TypeAttributes.Sealed),
                IsDisposable: interfaces.Contains("System.IDisposable", StringComparer.Ordinal));

            typeWriter.WriteLine(JsonSerializer.Serialize(typeRecord));
            typeCount++;

            if (isPublicType)
            {
                publicTypeWriter.WriteLine(JsonSerializer.Serialize(typeRecord));
                publicTypeCount++;
            }

            foreach (MemberRecord member in GetMemberRecords(reader, provider, assemblyName, fullName, ns, type, typeKind))
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

    private static IEnumerable<MemberRecord> GetMemberRecords(MetadataReader reader, MetadataNameProvider provider, string assemblyName, string declaringType, string? typeNamespace, TypeDefinition type, string typeKind)
    {
        foreach (MethodDefinitionHandle handle in type.GetMethods())
        {
            MethodDefinition method = reader.GetMethodDefinition(handle);
            string name = reader.GetString(method.Name);
            if (!ShouldExportMethod(name, method.Attributes))
            {
                continue;
            }

            MethodSignature<string> signature = method.DecodeSignature(provider, null);
            string visibility = GetMethodVisibility(method.Attributes);
            string[] attributes = GetAttributeNames(reader, method.GetCustomAttributes());
            ParameterRecord[] parameters = GetParameters(reader, method.GetParameters(), signature.ParameterTypes);

            string memberKind = name switch
            {
                ".ctor" => "constructor",
                ".cctor" => "constructor",
                _ => "method"
            };

            string signatureText = $"{declaringType}.{name}({string.Join(", ", parameters.Select(p => p.Type))})";
            yield return new MemberRecord(
                Assembly: assemblyName,
                Namespace: typeNamespace,
                DeclaringType: declaringType,
                MemberKind: memberKind,
                Name: name,
                Signature: signatureText,
                Visibility: visibility,
                ReturnType: memberKind == "constructor" ? null : signature.ReturnType,
                Parameters: parameters,
                Attributes: attributes,
                IsStatic: method.Attributes.HasFlag(MethodAttributes.Static),
                IsAbstract: method.Attributes.HasFlag(MethodAttributes.Abstract),
                IsVirtual: method.Attributes.HasFlag(MethodAttributes.Virtual));
        }

        foreach (FieldDefinitionHandle handle in type.GetFields())
        {
            FieldDefinition field = reader.GetFieldDefinition(handle);
            string name = reader.GetString(field.Name);
            bool isEnumValue = field.Attributes.HasFlag(FieldAttributes.Literal);
            if (typeKind == "enum" && !isEnumValue)
            {
                continue;
            }

            if (typeKind != "enum" && field.Attributes.HasFlag(FieldAttributes.SpecialName))
            {
                continue;
            }

            string fieldType = field.DecodeSignature(provider, null);

            yield return new MemberRecord(
                Assembly: assemblyName,
                Namespace: typeNamespace,
                DeclaringType: declaringType,
                MemberKind: isEnumValue ? "enumValue" : "field",
                Name: name,
                Signature: $"{declaringType}.{name}",
                Visibility: GetFieldVisibility(field.Attributes),
                ReturnType: fieldType,
                Parameters: Array.Empty<ParameterRecord>(),
                Attributes: GetAttributeNames(reader, field.GetCustomAttributes()),
                IsStatic: field.Attributes.HasFlag(FieldAttributes.Static),
                IsAbstract: false,
                IsVirtual: false);
        }

        foreach (PropertyDefinitionHandle handle in type.GetProperties())
        {
            PropertyDefinition property = reader.GetPropertyDefinition(handle);
            string name = reader.GetString(property.Name);
            MethodDefinition? accessor = TryGetAccessor(reader, property.GetAccessors().Getter, property.GetAccessors().Setter);
            MethodSignature<string> signature = property.DecodeSignature(provider, null);
            MethodAttributes? accessorAttributes = accessor is MethodDefinition accessorMethod ? accessorMethod.Attributes : null;

            yield return new MemberRecord(
                Assembly: assemblyName,
                Namespace: typeNamespace,
                DeclaringType: declaringType,
                MemberKind: "property",
                Name: name,
                Signature: $"{declaringType}.{name}",
                Visibility: accessorAttributes is null ? "unknown" : GetMethodVisibility(accessorAttributes.Value),
                ReturnType: signature.ReturnType,
                Parameters: signature.ParameterTypes.Select((typeName, index) => new ParameterRecord($"index{index}", typeName, index, false, false, false, null)).ToArray(),
                Attributes: GetAttributeNames(reader, property.GetCustomAttributes()),
                IsStatic: accessorAttributes?.HasFlag(MethodAttributes.Static) ?? false,
                IsAbstract: accessorAttributes?.HasFlag(MethodAttributes.Abstract) ?? false,
                IsVirtual: accessorAttributes?.HasFlag(MethodAttributes.Virtual) ?? false);
        }

        foreach (EventDefinitionHandle handle in type.GetEvents())
        {
            EventDefinition eventDefinition = reader.GetEventDefinition(handle);
            string name = reader.GetString(eventDefinition.Name);
            MethodDefinition? accessor = TryGetAccessor(reader, eventDefinition.GetAccessors().Adder, eventDefinition.GetAccessors().Remover);
            MethodAttributes? accessorAttributes = accessor is MethodDefinition accessorMethod ? accessorMethod.Attributes : null;

            yield return new MemberRecord(
                Assembly: assemblyName,
                Namespace: typeNamespace,
                DeclaringType: declaringType,
                MemberKind: "event",
                Name: name,
                Signature: $"{declaringType}.{name}",
                Visibility: accessorAttributes is null ? "unknown" : GetMethodVisibility(accessorAttributes.Value),
                ReturnType: GetEntityName(reader, eventDefinition.Type),
                Parameters: Array.Empty<ParameterRecord>(),
                Attributes: GetAttributeNames(reader, eventDefinition.GetCustomAttributes()),
                IsStatic: accessorAttributes?.HasFlag(MethodAttributes.Static) ?? false,
                IsAbstract: accessorAttributes?.HasFlag(MethodAttributes.Abstract) ?? false,
                IsVirtual: accessorAttributes?.HasFlag(MethodAttributes.Virtual) ?? false);
        }
    }

    private static ParameterRecord[] GetParameters(MetadataReader reader, ParameterHandleCollection handles, ImmutableArray<string> parameterTypes)
    {
        var parameters = new List<ParameterRecord>();
        int typeIndex = 0;

        foreach (ParameterHandle handle in handles)
        {
            Parameter parameter = reader.GetParameter(handle);
            if (parameter.SequenceNumber == 0)
            {
                continue;
            }

            string typeName = typeIndex < parameterTypes.Length ? parameterTypes[typeIndex] : "<unknown>";
            parameters.Add(new ParameterRecord(
                Name: reader.GetString(parameter.Name),
                Type: typeName,
                Position: parameter.SequenceNumber - 1,
                IsOut: parameter.Attributes.HasFlag(ParameterAttributes.Out),
                IsOptional: parameter.Attributes.HasFlag(ParameterAttributes.Optional),
                HasDefaultValue: false,
                DefaultValue: null));
            typeIndex++;
        }

        return parameters.ToArray();
    }

    private static string[] GetAttributeNames(MetadataReader reader, CustomAttributeHandleCollection attributes)
    {
        return attributes
            .Select(handle => GetCustomAttributeTypeName(reader, reader.GetCustomAttribute(handle)))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray()!;
    }

    private static string GetCustomAttributeTypeName(MetadataReader reader, CustomAttribute attribute)
    {
        EntityHandle ctor = attribute.Constructor;
        return ctor.Kind switch
        {
            HandleKind.MemberReference => GetMemberReferenceParentName(reader, reader.GetMemberReference((MemberReferenceHandle)ctor)),
            HandleKind.MethodDefinition => GetMethodDefinitionParentName(reader, reader.GetMethodDefinition((MethodDefinitionHandle)ctor)),
            _ => "<unknown>"
        };
    }

    private static string GetMemberReferenceParentName(MetadataReader reader, MemberReference memberReference)
    {
        return GetEntityName(reader, memberReference.Parent);
    }

    private static string GetMethodDefinitionParentName(MetadataReader reader, MethodDefinition methodDefinition)
    {
        return GetEntityName(reader, methodDefinition.GetDeclaringType());
    }

    private static MethodDefinition? TryGetAccessor(MetadataReader reader, MethodDefinitionHandle getter, MethodDefinitionHandle setter)
    {
        if (!getter.IsNil)
        {
            return reader.GetMethodDefinition(getter);
        }

        if (!setter.IsNil)
        {
            return reader.GetMethodDefinition(setter);
        }

        return null;
    }

    private static string? GetNamespace(MetadataReader reader, TypeDefinitionHandle handle, TypeDefinition type)
    {
        if (type.GetDeclaringType().IsNil)
        {
            string ns = reader.GetString(type.Namespace);
            return string.IsNullOrWhiteSpace(ns) ? null : ns;
        }

        TypeDefinition parent = reader.GetTypeDefinition(type.GetDeclaringType());
        string? parentNamespace = GetNamespace(reader, type.GetDeclaringType(), parent);
        string parentName = reader.GetString(parent.Name);
        return string.IsNullOrWhiteSpace(parentNamespace) ? parentName : $"{parentNamespace}.{parentName}";
    }

    private static string GetEntityName(MetadataReader reader, EntityHandle handle)
    {
        return handle.Kind switch
        {
            HandleKind.TypeReference => GetTypeReferenceName(reader, reader.GetTypeReference((TypeReferenceHandle)handle)),
            HandleKind.TypeDefinition => GetTypeDefinitionName(reader, (TypeDefinitionHandle)handle),
            HandleKind.TypeSpecification => "<type-spec>",
            _ => "<unknown>"
        };
    }

    internal static string GetTypeReferenceName(MetadataReader reader, TypeReference typeReference)
    {
        string ns = reader.GetString(typeReference.Namespace);
        string name = reader.GetString(typeReference.Name);
        return string.IsNullOrWhiteSpace(ns) ? name : $"{ns}.{name}";
    }

    internal static string GetTypeDefinitionName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        TypeDefinition type = reader.GetTypeDefinition(handle);
        string name = reader.GetString(type.Name);
        string? ns = GetNamespace(reader, handle, type);
        return string.IsNullOrWhiteSpace(ns) ? name : $"{ns}.{name}";
    }

    private static bool ShouldExportMethod(string name, MethodAttributes attributes)
    {
        if (name is ".ctor" or ".cctor")
        {
            return true;
        }

        if (!attributes.HasFlag(MethodAttributes.SpecialName))
        {
            return true;
        }

        return name.StartsWith("op_", StringComparison.Ordinal);
    }

    private static string GetTypeKind(TypeAttributes attributes, string? baseTypeName)
    {
        if ((attributes & TypeAttributes.ClassSemanticsMask) == TypeAttributes.Interface)
        {
            return "interface";
        }

        if (string.Equals(baseTypeName, "System.Enum", StringComparison.Ordinal))
        {
            return "enum";
        }

        if (string.Equals(baseTypeName, "System.MulticastDelegate", StringComparison.Ordinal))
        {
            return "delegate";
        }

        if (string.Equals(baseTypeName, "System.ValueType", StringComparison.Ordinal))
        {
            return "struct";
        }

        return "class";
    }

    private static string GetTypeVisibility(TypeAttributes attributes)
    {
        return (attributes & TypeAttributes.VisibilityMask) switch
        {
            TypeAttributes.Public => "public",
            TypeAttributes.NotPublic => "internal",
            TypeAttributes.NestedPublic => "public",
            TypeAttributes.NestedFamily => "protected",
            TypeAttributes.NestedFamORAssem => "protected internal",
            TypeAttributes.NestedAssembly => "internal",
            TypeAttributes.NestedFamANDAssem => "private protected",
            TypeAttributes.NestedPrivate => "private",
            _ => "unknown"
        };
    }

    private static bool IsTypeVisibleOutsideAssembly(TypeAttributes attributes)
    {
        return GetTypeVisibility(attributes) is "public" or "protected" or "protected internal";
    }

    private static bool IsVisibilityOutsideAssembly(string visibility)
    {
        return visibility is "public" or "protected" or "protected internal";
    }

    private static string GetMethodVisibility(MethodAttributes attributes)
    {
        return (attributes & MethodAttributes.MemberAccessMask) switch
        {
            MethodAttributes.Public => "public",
            MethodAttributes.Family => "protected",
            MethodAttributes.FamORAssem => "protected internal",
            MethodAttributes.Assembly => "internal",
            MethodAttributes.FamANDAssem => "private protected",
            MethodAttributes.Private => "private",
            _ => "unknown"
        };
    }

    private static string GetFieldVisibility(FieldAttributes attributes)
    {
        return (attributes & FieldAttributes.FieldAccessMask) switch
        {
            FieldAttributes.Public => "public",
            FieldAttributes.Family => "protected",
            FieldAttributes.FamORAssem => "protected internal",
            FieldAttributes.Assembly => "internal",
            FieldAttributes.FamANDAssem => "private protected",
            FieldAttributes.Private => "private",
            _ => "unknown"
        };
    }
}

sealed class MetadataNameProvider : ISignatureTypeProvider<string, object?>
{
    private readonly MetadataReader _reader;

    public MetadataNameProvider(MetadataReader reader)
    {
        _reader = reader;
    }

    public string GetArrayType(string elementType, ArrayShape shape) => $"{elementType}[{new string(',', shape.Rank - 1)}]";
    public string GetByReferenceType(string elementType) => $"{elementType}&";
    public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";
    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => $"{genericType}<{string.Join(", ", typeArguments)}>";
    public string GetGenericMethodParameter(object? genericContext, int index) => $"!!{index}";
    public string GetGenericTypeParameter(object? genericContext, int index) => $"!{index}";
    public string GetModifiedType(string modifierType, string unmodifiedType, bool isRequired) => unmodifiedType;
    public string GetPinnedType(string elementType) => elementType;
    public string GetPointerType(string elementType) => $"{elementType}*";
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
    {
        PrimitiveTypeCode.Boolean => "System.Boolean",
        PrimitiveTypeCode.Byte => "System.Byte",
        PrimitiveTypeCode.Char => "System.Char",
        PrimitiveTypeCode.Double => "System.Double",
        PrimitiveTypeCode.Int16 => "System.Int16",
        PrimitiveTypeCode.Int32 => "System.Int32",
        PrimitiveTypeCode.Int64 => "System.Int64",
        PrimitiveTypeCode.IntPtr => "System.IntPtr",
        PrimitiveTypeCode.Object => "System.Object",
        PrimitiveTypeCode.SByte => "System.SByte",
        PrimitiveTypeCode.Single => "System.Single",
        PrimitiveTypeCode.String => "System.String",
        PrimitiveTypeCode.TypedReference => "System.TypedReference",
        PrimitiveTypeCode.UInt16 => "System.UInt16",
        PrimitiveTypeCode.UInt32 => "System.UInt32",
        PrimitiveTypeCode.UInt64 => "System.UInt64",
        PrimitiveTypeCode.UIntPtr => "System.UIntPtr",
        PrimitiveTypeCode.Void => "System.Void",
        _ => typeCode.ToString()
    };
    public string GetSZArrayType(string elementType) => $"{elementType}[]";
    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => MetadataAssemblyExporter.GetTypeDefinitionName(reader, handle);
    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => MetadataAssemblyExporter.GetTypeReferenceName(reader, reader.GetTypeReference(handle));
    public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) => reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
}
