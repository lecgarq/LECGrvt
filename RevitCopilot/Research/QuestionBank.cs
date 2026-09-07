using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LECG.RevitCopilot.Agent;

namespace KnowledgeLab;

internal sealed record Question(string Id, string Kind, string Member, string? Operation, string Source, string Prompt);

internal static class QuestionBank
{
    internal const string ApiDirectory = @"C:\Program Files\Autodesk\Revit 2026";
    internal static readonly List<object> Exclusions = [];
    internal static Question[] Prepare()
    {
        var bindings = RevitApiCatalog.All.Values.OrderBy(b => b.Operation, StringComparer.Ordinal).Select(b =>
        {
            string member = "P:" + b.Property.DeclaringType!.FullName + "." + b.Property.Name;
            string source = b.Capability.Description + "\nArguments: " + b.Capability.Arguments;
            return Make("lookup", member, b.Operation, source,
                "Return JSON {\"member\":\"exact member ID\",\"english_queries\":[\"two distinct short search questions\"],\"spanish_queries\":[\"two distinct short search questions\"]}. " +
                "Each question must describe this precise read or change operation, mention the relevant element type, and avoid undocumented capabilities. No code, explanations, or extra keys.");
        }).ToArray();
        // Round-robin declaring types so selection is not dominated by the alphabetically first subsystem.
        var methodGroups = XDocument.Load(Path.Combine(ApiDirectory, "RevitAPI.xml")).Descendants("member")
            .Where(m => ((string?)m.Attribute("name"))?.StartsWith("M:Autodesk.Revit.DB.", StringComparison.Ordinal) == true)
            .Where(m => !((string)m.Attribute("name")!).Contains(".#ctor", StringComparison.Ordinal) && !((string)m.Attribute("name")!).Contains(".Dispose", StringComparison.Ordinal))
            .Where(m => !string.IsNullOrWhiteSpace(m.Element("summary")?.Value))
            .Where(m => CandidateValidator.PublicSignature((string)m.Attribute("name")!) is not null)
            .GroupBy(m => ((string)m.Attribute("name")!).Split('(')[0][..((string)m.Attribute("name")!).Split('(')[0].LastIndexOf('.')])
            .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new Queue<XElement>(g.OrderBy(m => (string)m.Attribute("name")!, StringComparer.Ordinal))).ToArray();
        List<Question> methods = [];
        int required = Math.Max(0, 3000 - bindings.Length);
        while (methods.Count < required && methodGroups.Any(g => g.Count > 0))
        foreach (var group in methodGroups)
        {
            if (methods.Count >= required || !group.TryDequeue(out XElement? member) || member is null) continue;
            string id = (string)member.Attribute("name")!;
            string documentation = Normalize(member.Element("summary")?.Value ?? "") + "\n" + string.Join("\n", member.Elements("param").Select(p => (string?)p.Attribute("name") + ": " + Normalize(p.Value))) + "\nReturns: " + Normalize(member.Element("returns")?.Value ?? "") + "\nExceptions: " + string.Join("; ", member.Elements("exception").Select(e => (string?)e.Attribute("cref") + " " + Normalize(e.Value)));
            string source = ApiContract.Describe(id, documentation) + "\nDOCUMENTATION:\n" + (documentation.Length > 1600 ? documentation[..1600] : documentation);
            var candidate = Make("code", id, null, source,
                "Return JSON {\"member\":\"exact member ID\",\"csharp\":\"complete compilable C# source\"}. " +
                "Use the CONSTRAINED SCAFFOLD as your implementation; add only simple argument validation grounded in the documentation. " +
                "Preserve all global:: qualified types, typed caller inputs, exact API invocation, return type and transaction control flow. " +
                "Never instantiate input objects, invent enum values, use sample IDs, locate a document, add other Revit calls, or replace types with similarly named ones. " +
                "If a transaction is supplied, preserve Start/Commit status checks, commit BEFORE return, and rollback/rethrow in catch. " +
                "No UI, files, process, network, reflection, unsafe code, markdown, placeholders or TODOs. Return the complete class, not a snippet.");
            string scaffold = source.Split("CONSTRAINED SCAFFOLD:\n", 2)[1].Split("\nDOCUMENTATION:", 2)[0];
            var validation = CandidateValidator.Validate(candidate, JsonSerializer.Serialize(new { member = id, csharp = scaffold }));
            if (validation.Status == "compiled_candidate_NOT_runtime_verified") methods.Add(candidate);
            else Exclusions.Add(new { member = id, validation });
        }
        List<Question> all = [];
        int nextBinding = 0, nextMethod = 0;
        while (nextBinding < bindings.Length || nextMethod < methods.Count)
        {
            if (nextMethod < methods.Count) all.Add(methods[nextMethod++]);
            for (int i = 0; i < 3 && nextBinding < bindings.Length; i++) all.Add(bindings[nextBinding++]);
        }
        return all.ToArray();
    }

    private static Question Make(string kind, string member, string? operation, string source, string instruction)
    {
        string prompt = instruction + "\nRevit: 2026 only. MEMBER: " + member + (operation is null ? "" : "\nOPERATION: " + operation) + "\nINSTALLED API REFERENCE:\n" + source;
        string id = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(prompt)))[..24];
        return new(id, kind, member, operation, source, prompt);
    }
    private static string Normalize(string text) => Regex.Replace(text, @"\s+", " ").Trim();
}
