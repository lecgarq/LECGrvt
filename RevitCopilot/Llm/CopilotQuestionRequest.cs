using System.Text.Json;

namespace LECG.RevitCopilot.Llm;

internal sealed record CopilotQuestion(string Id, string Text, string[] Options, bool Boolean = false, bool Required = true);

internal sealed class CopilotQuestionRequest(string id, string method, IReadOnlyList<CopilotQuestion> questions)
{
    internal string Id { get; } = id;
    internal string Method { get; } = method;
    internal string Title { get; } = method == "mcpServer/elicitation/request" ? "Revit tool · confirmation" : "Copilot · decision needed";
    internal IReadOnlyList<CopilotQuestion> Questions { get; } = questions;
    internal TaskCompletionSource<object> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal object CancelResponse => Method == "mcpServer/elicitation/request"
        ? new { action = "decline", content = (object?)null } : new { answers = new Dictionary<string, object>() };

    internal bool Submit(IReadOnlyDictionary<string, string> answers)
    {
        if (Questions.Any(q => (q.Required && (!answers.TryGetValue(q.Id, out var a) || string.IsNullOrWhiteSpace(a))) ||
            (answers.TryGetValue(q.Id, out var value) && q.Options.Length > 0 && !q.Options.Contains(value)))) return false;
        object response;
        if (Method == "mcpServer/elicitation/request")
        {
            var content = Questions.Where(q => answers.ContainsKey(q.Id)).ToDictionary(q => q.Id,
                q => q.Boolean ? (object)(answers[q.Id] == "Yes") : answers[q.Id]);
            response = new { action = "accept", content };
        }
        else response = new { answers = Questions.ToDictionary(q => q.Id, q => new { answers = answers.TryGetValue(q.Id, out var a) ? new[] { a } : Array.Empty<string>() }) };
        return Completion.TrySetResult(response);
    }

    internal static CopilotQuestionRequest? Parse(string id, string method, JsonElement p)
    {
        List<CopilotQuestion> questions = [];
        if (method is "item/tool/requestUserInput" or "tool/requestUserInput")
        {
            if (!p.TryGetProperty("questions", out var list) || list.ValueKind != JsonValueKind.Array || list.GetArrayLength() is < 1 or > 10) return null;
            foreach (var q in list.EnumerateArray())
            {
                if (q.TryGetProperty("isSecret", out var secret) && secret.ValueKind == JsonValueKind.True) return null;
                string[] options = q.TryGetProperty("options", out var o) && o.ValueKind == JsonValueKind.Array
                    ? o.EnumerateArray().Select(x => x.GetProperty("label").GetString()!).ToArray() : [];
                questions.Add(new(q.GetProperty("id").GetString()!, q.GetProperty("question").GetString()!, options));
            }
        }
        else if (method == "mcpServer/elicitation/request")
        {
            if (p.GetProperty("serverName").GetString() != "lecg-revit" ||
                p.GetProperty("mode").GetString() is not ("form" or "openai/form")) return null;
            var schema = p.GetProperty("requestedSchema");
            if (schema.GetProperty("type").GetString() != "object") return null;
            var required = schema.TryGetProperty("required", out var r) ? r.EnumerateArray().Select(x => x.GetString()).ToHashSet() : [];
            string message = p.GetProperty("message").GetString() ?? "Review this request";
            foreach (var property in schema.GetProperty("properties").EnumerateObject())
            {
                string? type = property.Value.GetProperty("type").GetString();
                if (type is not ("string" or "boolean")) return null;
                if (property.Value.TryGetProperty("format", out _)) return null;
                string[] options = type == "boolean" ? ["Yes", "No"] :
                    property.Value.TryGetProperty("enum", out var e) ? e.EnumerateArray().Select(x => x.GetString()!).ToArray() : [];
                string title = property.Value.TryGetProperty("title", out var t) ? t.GetString()! : property.Name;
                questions.Add(new(property.Name, message + "\n\n" + title, options, type == "boolean", required.Contains(property.Name)));
            }
            if (questions.Count is < 1 or > 10) return null;
        }
        else return null;
        if (questions.Any(q => string.IsNullOrWhiteSpace(q.Id)) || questions.Select(q => q.Id).Distinct().Count() != questions.Count) return null;
        return new(id, method, questions);
    }
}
