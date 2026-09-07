using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using KnowledgeLab;

string command = args.FirstOrDefault() ?? "help";
string Option(string name, string fallback) => Array.IndexOf(args, name) is int index && index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
string root = Path.GetFullPath(Option("--output", "artifacts/campaign-v5"));
Directory.CreateDirectory(root);
string bankPath = Path.Combine(root, "questions.json");
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = false };
if (command == "export-validation")
{
    RuntimeValidationExport.Write(Option("--benchmark", ""), Option("--destination", "../Agent/Knowledge/revit2026-validation.json"));
    return;
}
if (command == "audit-candidates")
{
    RuntimeCandidateAudit.Write(root, Option("--destination", "../../outputs/validation-expansion/method-audit.json"));
    return;
}
if (command == "export-runtime")
{
    RuntimeReferenceExport.Write(root, Option("--destination", "../Agent/Knowledge/revit2026-reference.json"));
    return;
}
if (command == "prepare")
{
    Question[] questions = QuestionBank.Prepare();
    string priorPath = Option("--retest-from", "");
    if (priorPath.Length > 0)
    {
        var prior = File.ReadLines(Path.Combine(priorPath, "answers.jsonl")).Select(line => JsonSerializer.Deserialize<JsonElement>(line)).ToArray();
        var rejectedMembers = prior.Where(r => r.GetProperty("kind").GetString() == "code" && r.GetProperty("validation").GetProperty("status").GetString() == "rejected_candidate")
            .Select(r => r.GetProperty("member").GetString()).ToHashSet();
        var failures = questions.Where(q => q.Kind == "code" && rejectedMembers.Contains(q.Member)).ToArray();
        var remainingCode = questions.Where(q => q.Kind == "code" && !rejectedMembers.Contains(q.Member)).ToArray();
        var spread = Enumerable.Range(0, 16).Select(i => remainingCode[i * (remainingCode.Length - 1) / 15]).ToArray();
        var qualification = failures.Concat(spread).Concat(questions.Where(q => q.Kind == "lookup").Take(8)).DistinctBy(q => q.Id).ToArray();
        var priorityIds = qualification.Select(q => q.Id).ToHashSet();
        questions = qualification.Concat(questions.Where(q => !priorityIds.Contains(q.Id))).ToArray();
        await File.WriteAllTextAsync(Path.Combine(root, "qualification-plan.json"), JsonSerializer.Serialize(new { prior_rejections = rejectedMembers.Count, retest_failures = failures.Length, spread_code = spread.Length, lookups = 8, total = qualification.Length, ids = priorityIds }, jsonOptions));
    }
    if (questions.Length != 3000 || questions.DistinctBy(q => q.Id).Count() != 3000) throw new InvalidOperationException("Expected exactly 3,000 distinct questions.");
    string prepared = JsonSerializer.Serialize(questions, jsonOptions);
    if (File.Exists(bankPath) && File.Exists(Path.Combine(root, "answers.jsonl")) && await File.ReadAllTextAsync(bankPath) != prepared)
        throw new InvalidOperationException("Do not overwrite the question bank of an existing run. Use a new output directory.");
    await File.WriteAllTextAsync(bankPath, prepared);
    await File.WriteAllTextAsync(Path.Combine(root, "bank-exclusions.json"), JsonSerializer.Serialize(QuestionBank.Exclusions, jsonOptions));
    Console.WriteLine(JsonSerializer.Serialize(new { prepared = questions.Length, lookups = questions.Count(q => q.Kind == "lookup"), code_tasks = questions.Count(q => q.Kind == "code"), file = bankPath }));
    return;
}
if (command == "self-test")
{
    var question = new Question("test", "code", "M:Autodesk.Revit.DB.Element.GetTypeId", null, "", "");
    string good = JsonSerializer.Serialize(new { member = question.Member, csharp = "using Autodesk.Revit.DB; public static class Candidate { public static ElementId Run(Element e) => e.GetTypeId(); }" });
    Validation valid = CandidateValidator.Validate(question, good);
    if (!valid.Compiles || !valid.ReferencesExpectedMember || !valid.RestrictedCode) throw new Exception(JsonSerializer.Serialize(valid));
    string bad = JsonSerializer.Serialize(new { member = question.Member, csharp = "using Autodesk.Revit.DB; public static class Candidate { public static ElementId Run(Element e) { System.IO.File.Delete(\"x\"); return e.GetTypeId(); } }" });
    if (CandidateValidator.Validate(question, bad).RestrictedCode) throw new Exception("Forbidden external operation was not flagged.");
    string missingCommit = JsonSerializer.Serialize(new { member = question.Member, csharp = "using Autodesk.Revit.DB; public static class Candidate { public static ElementId Run(Document d, Element e) { using var t = new Transaction(d, \"Test\"); t.Start(); return e.GetTypeId(); } }" });
    if (CandidateValidator.Validate(question, missingCommit).Status != "rejected_candidate") throw new Exception("Incomplete transaction candidate was not rejected.");
    if (CandidateValidator.PublicSignature("M:Autodesk.Revit.DB.ADPInformation.ProductId") is not null) throw new Exception("Internal API leaked into the public query bank.");
    string spoof = JsonSerializer.Serialize(new { member = question.Member, csharp = "namespace Autodesk.Revit.DB { public class Element { public int GetTypeId() => 0; } } public static class Candidate { public static int Run(Autodesk.Revit.DB.Element e) => e.GetTypeId(); }" });
    var spoofResult = CandidateValidator.Validate(question, spoof);
    if (!spoofResult.Compiles || spoofResult.ReferencesExpectedMember) throw new Exception("API identity spoof test failed.");
    foreach (string id in new[] { "M:Autodesk.Revit.DB.Analysis.FieldDomainPointsByUV.SetGridCoordinates(System.Collections.Generic.ICollection{System.Double},System.Collections.Generic.ICollection{System.Double})", "M:Autodesk.Revit.DB.Analysis.GenericZone.AddSpaces(System.Collections.Generic.ISet{Autodesk.Revit.DB.ElementId})", "M:Autodesk.Revit.DB.Analysis.EnergyDataSettings.CheckAnalysisType(Autodesk.Revit.DB.Analysis.AnalysisMode)", "M:Autodesk.Revit.DB.AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(Autodesk.Revit.DB.Document,Autodesk.Revit.DB.FamilySymbol)" })
    {
        string source = ApiContract.Describe(id, "");
        string scaffold = source.Split("CONSTRAINED SCAFFOLD:\n", 2)[1];
        var fixture = new Question("fixture", "code", id, null, source, "");
        var result = CandidateValidator.Validate(fixture, JsonSerializer.Serialize(new { member = id, csharp = scaffold }));
        if (result.Status != "compiled_candidate_NOT_runtime_verified") throw new Exception(id + ": " + JsonSerializer.Serialize(result));
        if (source.Contains("TRANSACTION POLICY: own"))
        {
            var omitted = CandidateValidator.Validate(fixture, JsonSerializer.Serialize(new { member = id, csharp = "public static class Candidate { public static void Run() {} }" }));
            if (omitted.Status != "rejected_candidate") throw new Exception("Missing required invocation/transaction accepted.");
        }
    }
    Console.WriteLine("PASS: valid API candidate compiles and resolves its exact member; external file operation is rejected. No generated code executed.");
    return;
}
if (command == "report") { CampaignReport.Write(root); Console.WriteLine(File.ReadAllText(Path.Combine(root, "summary.json"))); return; }
if (command != "run") { Console.WriteLine("Commands: prepare | self-test | run --limit 12|3000 [--output path] [--model name]. STOP file requests a graceful stop. Existing results are resumed, never overwritten."); return; }
using FileStream runLock = new(Path.Combine(root, "run.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
Question[] bank = JsonSerializer.Deserialize<Question[]>(await File.ReadAllTextAsync(bankPath), jsonOptions)!;
string bankHash = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(bankPath)));
string modelName = Option("--model", "lecg-revit-teacher:q4ks-20260905");
if (modelName != "lecg-revit-teacher:q4ks-20260905" && modelName != "qwen3:14b") throw new ArgumentException("Only the approved locally installed teacher or baseline model is allowed.");
int limit = Math.Clamp(int.Parse(Option("--limit", "12")), 1, bank.Length);
using HttpClient client = new() { BaseAddress = new Uri("http://127.0.0.1:11434/"), Timeout = TimeSpan.FromMinutes(3) };
using var tags = await client.GetFromJsonAsync<JsonDocument>("api/tags");
var tag = tags!.RootElement.GetProperty("models").EnumerateArray().FirstOrDefault(m => m.GetProperty("name").GetString() == modelName);
if (tag.ValueKind == JsonValueKind.Undefined) throw new InvalidOperationException("Approved model is not installed locally; the runner never downloads automatically.");
string digest = tag.GetProperty("digest").GetString()!;
string manifestPath = Path.Combine(root, "run-manifest.json");
string manifest = JsonSerializer.Serialize(new { validation_policy = 4, max_attempts = 2, harness_sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(QuestionBank).Assembly.Location))), bank_sha256 = bankHash, model = modelName, model_digest = digest, num_ctx = 4096, code_max_tokens = 1000, seed = 20260905, temperature = 0, parallel = 1 }, jsonOptions);
if (File.Exists(manifestPath) && await File.ReadAllTextAsync(manifestPath) != manifest) throw new InvalidOperationException("Resume manifest mismatch: use a separate output directory for a different bank/model.");
await File.WriteAllTextAsync(manifestPath, manifest);
string answersPath = Path.Combine(root, "answers.jsonl");
HashSet<string> completed = [];
if (File.Exists(answersPath)) foreach (string line in File.ReadLines(answersPath))
{
    using var row = JsonDocument.Parse(line);
    completed.Add(row.RootElement.GetProperty("id").GetString()!);
}
using CancellationTokenSource cancellation = new();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
int count = 0, invalid = 0, codeCount = 0, rejectedCode = 0;
var runTimer = Stopwatch.StartNew();
try
{
    foreach (Question question in bank.Where(q => !completed.Contains(q.Id)).Take(limit))
    {
        if (File.Exists(Path.Combine(root, "STOP"))) break;
        while (Process.GetProcessesByName("Revit").Length > 0)
        {
            await File.WriteAllTextAsync(Path.Combine(root, "progress.json"), JsonSerializer.Serialize(new { status = "paused_revit_open", completed = completed.Count, total = bank.Length }), cancellation.Token);
            Console.WriteLine("PAUSED: Revit is open; preserving GPU headroom. Close Revit to resume, or create STOP in the campaign folder.");
            if (File.Exists(Path.Combine(root, "STOP"))) return;
            await Task.Delay(TimeSpan.FromSeconds(30), cancellation.Token);
        }
        var timer = Stopwatch.StartNew();
        var attempts = await LocalGeneration.Run(client, question, modelName, cancellation.Token);
        string output = attempts[^1].Output;
        Validation validation = attempts[^1].Validation;
        if (!validation.SchemaValid) invalid++;
        if (question.Kind == "code") { codeCount++; if (validation.Status != "compiled_candidate_NOT_runtime_verified") rejectedCode++; }
        double seconds = attempts.Sum(a => a.EvalSeconds);
        int tokens = attempts.Sum(a => a.GeneratedTokens);
        var record = new { id = question.Id, kind = question.Kind, member = question.Member, operation = question.Operation, model = modelName, model_digest = digest,
            timestamp_utc = DateTimeOffset.UtcNow, output, validation, elapsed_ms = timer.Elapsed.TotalMilliseconds,
            prompt_tokens = attempts.Sum(a => a.PromptTokens), generated_tokens = tokens, attempts,
            tokens_per_second = seconds > 0 ? tokens / seconds : 0, done_reason = attempts[^1].DoneReason };
        await File.AppendAllTextAsync(answersPath, JsonSerializer.Serialize(record, jsonOptions) + Environment.NewLine, cancellation.Token);
        completed.Add(question.Id); count++;
        string progress = JsonSerializer.Serialize(new { status = "running", completed = completed.Count, total = bank.Length, this_run = count, invalid_schema = invalid, last_id = question.Id, last_validation = validation.Status, elapsed_seconds = runTimer.Elapsed.TotalSeconds }, jsonOptions);
        await File.WriteAllTextAsync(Path.Combine(root, "progress.json"), progress, cancellation.Token);
        Console.WriteLine(progress);
        if (count % 100 == 0) CampaignReport.Write(root);
        if (count >= 10 && invalid > count / 4) throw new InvalidOperationException("Quality gate stopped the campaign: more than 25% invalid JSON/contracts. Inspect and repair before continuing.");
        if (codeCount >= 16 && rejectedCode > codeCount * 3 / 4) throw new InvalidOperationException("Quality gate stopped the campaign: more than 75% rejected code candidates. Inspect before continuing.");
    }
    await File.WriteAllTextAsync(Path.Combine(root, "progress.json"), JsonSerializer.Serialize(new { status = completed.Count == bank.Length ? "queries_complete_review_pending" : "limit_reached_review_pending", completed = completed.Count, total = bank.Length, elapsed_seconds = runTimer.Elapsed.TotalSeconds }, jsonOptions));
}
catch (Exception ex)
{
    await File.WriteAllTextAsync(Path.Combine(root, "progress.json"), JsonSerializer.Serialize(new { status = cancellation.IsCancellationRequested ? "cancelled" : "failed", completed = completed.Count, total = bank.Length, error = ex.Message }, jsonOptions));
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}
finally
{
    try { CampaignReport.Write(root); } catch (Exception reportError) { Console.Error.WriteLine("Report failed: " + reportError.Message); }
    // Only unload the task-owned teacher, never the user's unrelated baseline model.
    if (modelName.StartsWith("lecg-revit-teacher:", StringComparison.Ordinal))
        try { using var unloaded = await client.PostAsJsonAsync("api/generate", new { model = modelName, keep_alive = 0, stream = false }); } catch { }
}
