using System.Security.Cryptography;
using System.Text.Json;

namespace KnowledgeLab;

internal static class RuntimeReferenceExport
{
    internal static void Write(string campaign, string destination)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        string questionsPath = Path.Combine(campaign, "questions.json");
        string answersPath = Path.Combine(campaign, "answers.jsonl");
        var questions = JsonSerializer.Deserialize<Question[]>(File.ReadAllText(questionsPath), options)!;
        var answers = File.ReadLines(answersPath).Select(line => JsonSerializer.Deserialize<JsonElement>(line))
            .ToDictionary(row => row.GetProperty("id").GetString()!);
        if (questions.Length != 3000 || questions.DistinctBy(q => q.Id).Count() != 3000 || answers.Count != 3000)
            throw new InvalidDataException("Export requires the completed 3,000-entry campaign.");
        var entries = questions.OrderBy(q => q.Id, StringComparer.Ordinal).Select(q =>
        {
            var answer = answers[q.Id];
            if (answer.GetProperty("member").GetString() != q.Member)
                throw new InvalidDataException("Research/source identity mismatch: " + q.Id);
            // Retain installed API documentation only. Never ship generated C# or prompt text.
            string source = q.Source;
            if (q.Kind == "code")
            {
                int documentation = source.IndexOf("DOCUMENTATION:\n", StringComparison.Ordinal);
                source = source.Split('\n', 2)[0] + (documentation < 0 ? "" : "\n" + source[documentation..]);
            }
            if (source.Contains("CONSTRAINED SCAFFOLD", StringComparison.Ordinal))
                throw new InvalidDataException("Unstripped code scaffold: " + q.Id);
            return new { q.Id, q.Kind, q.Member, q.Operation, source,
                research_status = answer.GetProperty("validation").GetProperty("status").GetString() };
        }).ToArray();
        string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
        var export = new { schema_version = 1, revit_version = 2026,
            provenance = "Installed Revit 2026 API metadata/XML; generated answers and executable code excluded.",
            questions_sha256 = Hash(questionsPath), answers_sha256 = Hash(answersPath), entries };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        File.WriteAllText(destination, JsonSerializer.Serialize(export, options));
        Console.WriteLine($"Exported {entries.Length} source references to {Path.GetFullPath(destination)}; no inference requested.");
    }
}
