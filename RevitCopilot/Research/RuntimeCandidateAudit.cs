using System.Text.Json;
using Microsoft.CodeAnalysis;

namespace KnowledgeLab;

internal static class RuntimeCandidateAudit
{
    internal static void Write(string campaign, string destination)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var bank = JsonSerializer.Deserialize<Question[]>(File.ReadAllText(Path.Combine(campaign, "questions.json")), options)!;
        var rows = bank.Where(q => q.Kind == "code").Select(q =>
        {
            IMethodSymbol method = CandidateValidator.PublicMethod(q.Member) ?? throw new InvalidDataException(q.Member);
            string owner = method.ContainingType.ToDisplayString();
            bool IsElement(INamedTypeSymbol? type) => type is not null && (type.ToDisplayString() == "Autodesk.Revit.DB.Element" || IsElement(type.BaseType));
            string classification;
            string reason;
            if (method.ContainingType.TypeKind == TypeKind.Interface || owner.Contains(".Events.", StringComparison.Ordinal))
                (classification, reason) = ("callback_or_event_contract", "Requires a Revit callback/event implementation; not a standalone active-model command.");
            else if (owner.Contains(".DirectContext3D.", StringComparison.Ordinal) || owner.Contains(".Visualization.", StringComparison.Ordinal))
                (classification, reason) = ("rendering_infrastructure", "Rendering subsystem support, not a standalone BIM operation.");
            else if (owner.Contains(".ExternalService.", StringComparison.Ordinal) || owner.Contains(".ExternalResource", StringComparison.Ordinal)
                || owner.Contains(".TransmissionData", StringComparison.Ordinal) || owner.Contains(".WorksharingUtils", StringComparison.Ordinal))
                (classification, reason) = ("external_or_session_workflow", "Requires a separately scoped external/session workflow and input contract.");
            else if (owner.EndsWith("Iterator", StringComparison.Ordinal) || owner.EndsWith("Array", StringComparison.Ordinal)
                || owner.EndsWith("Set", StringComparison.Ordinal) && !IsElement(method.ContainingType))
                (classification, reason) = ("collection_support", "Collection plumbing; useful inside implementations, not an independent agent capability.");
            else if (IsElement(method.ContainingType))
                (classification, reason) = ("element_adapter_candidate", "Can be considered for a typed current-element adapter after semantic review and fixtures.");
            else if (method.IsStatic && method.Parameters.Any(p => p.Type.ToDisplayString() == "Autodesk.Revit.DB.Document"))
                (classification, reason) = ("document_adapter_candidate", "Has an explicit document input; still requires typed inputs, transaction review and fixtures.");
            else
                (classification, reason) = ("supporting_api_reference", "Requires an additional object-lifetime/geometry/settings contract; no automatic execution.");
            return new { q.Id, q.Member, owner, classification, reason, is_static = method.IsStatic,
                parameters = method.Parameters.Select(p => new { p.Name, type = p.Type.ToDisplayString() }), return_type = method.ReturnType.ToDisplayString() };
        }).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        File.WriteAllText(destination, JsonSerializer.Serialize(new { total = rows.Length,
            note = "Metadata-based triage only. No candidate code executed and no runtime approval granted.", entries = rows }, options));
        Console.WriteLine(JsonSerializer.Serialize(rows.GroupBy(r => r.classification).Select(g => new { classification = g.Key, count = g.Count() })));
    }
}
