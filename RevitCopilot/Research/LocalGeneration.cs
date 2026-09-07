using System.Net.Http.Json;
using System.Text.Json;

namespace KnowledgeLab;

internal sealed record GenerationAttempt(string Output, Validation Validation, int PromptTokens, int GeneratedTokens, double EvalSeconds, string? DoneReason);

internal static class LocalGeneration
{
    internal static async Task<GenerationAttempt[]> Run(HttpClient client, Question question, string model, CancellationToken cancellation)
    {
        List<GenerationAttempt> attempts = [];
        for (int attempt = 0; attempt < 2; attempt++)
        {
            string prompt = question.Prompt;
            if (attempt > 0)
                prompt += "\nREPAIR: The previous candidate failed validation. Diagnostics: " + string.Join("; ", attempts[^1].Validation.Errors) +
                    "\nUse the supplied scaffold's exact types and invocation. Only define public static class Candidate. Never define Autodesk/System types or substitute short type names. Return complete valid JSON.";
            var request = new
            {
                model, stream = false, think = false, format = "json", keep_alive = "2m",
                messages = new[] { new { role = "system", content = "Revit 2026 research. Output only requested JSON. For code: preserve the supplied scaffold and global:: type names; define only public static class Candidate, never API stubs. No markdown." }, new { role = "user", content = prompt } },
                options = new { num_ctx = 4096, num_predict = question.Kind == "lookup" ? 240 : 1000, temperature = 0, seed = 20260905 }
            };
            using var response = await client.PostAsJsonAsync("api/chat", request, cancellation);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Local model returned {response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellation)}");
            using var answer = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellation);
            var data = answer!.RootElement;
            string output = data.GetProperty("message").GetProperty("content").GetString() ?? "";
            var validation = CandidateValidator.Validate(question, output);
            attempts.Add(new(output, validation, data.GetProperty("prompt_eval_count").GetInt32(), data.GetProperty("eval_count").GetInt32(),
                data.GetProperty("eval_duration").GetDouble() / 1e9, data.GetProperty("done_reason").GetString()));
            if (validation.Status is "compiled_candidate_NOT_runtime_verified" or "candidate_aliases_need_semantic_review") break;
        }
        return attempts.ToArray();
    }
}
