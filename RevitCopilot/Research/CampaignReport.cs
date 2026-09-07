using System.Text.Json;

namespace KnowledgeLab;

internal static class CampaignReport
{
    internal static void Write(string root)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var bank = JsonSerializer.Deserialize<Question[]>(File.ReadAllText(Path.Combine(root, "questions.json")), options)!;
        string answers = Path.Combine(root, "answers.jsonl");
        var rows = File.Exists(answers) ? File.ReadLines(answers).Select(line => JsonSerializer.Deserialize<JsonElement>(line)).ToArray() : [];
        var lookupRows = rows.Where(r => Status(r) == "candidate_aliases_need_semantic_review").ToArray();
        var codeRows = rows.Where(r => Status(r) == "compiled_candidate_NOT_runtime_verified").ToArray();
        File.WriteAllText(Path.Combine(root, "lookup-candidates.json"), JsonSerializer.Serialize(new
        {
            approved_for_production = false, review_required = "Check meaning, language and exact read/change intent against each source; schema checks are not semantic verification.",
            candidates = lookupRows.Select(r => new { result = r, source = bank.Single(q => q.Id == r.GetProperty("id").GetString()).Source })
        }, options));
        File.WriteAllText(Path.Combine(root, "code-candidates.json"), JsonSerializer.Serialize(new
        {
            approved_for_execution = false, review_required = "Manual C# review, transaction and failure checks, scratch Revit runtime tests and regression tests required. Never load or execute this file as code.",
            candidates = codeRows.Select(r => new { result = r, source = bank.Single(q => q.Id == r.GetProperty("id").GetString()).Source })
        }, options));
        File.WriteAllText(Path.Combine(root, "api-reference.json"), JsonSerializer.Serialize(new
        {
            provenance = "Installed Revit 2026 API metadata and XML documentation; generated model answers excluded.",
            entries = bank.Select(q => new { q.Member, q.Operation, q.Source })
        }, options));
        var summary = new
        {
            target = bank.Length, completed = rows.Length, lookup_candidates = lookupRows.Length,
            compiled_candidates_pending_review = codeRows.Length, rejected = rows.Length - lookupRows.Length - codeRows.Length,
            runtime_verified_new_functions = 0, automatically_deployed_functions = 0,
            local_prompt_tokens = rows.Sum(r => r.GetProperty("prompt_tokens").GetInt64()),
            local_generated_tokens = rows.Sum(r => r.GetProperty("generated_tokens").GetInt64()),
            inference_elapsed_seconds = rows.Sum(r => r.GetProperty("elapsed_ms").GetDouble()) / 1000,
            updated_utc = DateTimeOffset.UtcNow
        };
        File.WriteAllText(Path.Combine(root, "summary.json"), JsonSerializer.Serialize(summary, options));
    }

    private static string? Status(JsonElement row) => row.GetProperty("validation").GetProperty("status").GetString();
}
