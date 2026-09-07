# Temporary local knowledge lab

## Purpose and boundaries

Generate 3,000 distinct, documentation-grounded research answers locally, then retain reviewed native C# functions and compact search references instead of requiring a permanent teacher model. This is offline knowledge harvesting, not self-training or proof of complete Revit expertise. General natural-language reasoning still needs a model or a constrained command interface.

No generated code is executed, loaded into Revit, or deployed automatically. Compilation proves API/type compatibility only. Model output is untrusted even when it compiles. Transactions can still be semantically wrong, use stale elements, or fail under actual document conditions.

## Hardware and model evidence

- Verified RTX 4070 SUPER, 12,282 MiB VRAM, approximately 64 GB RAM; Ollama 0.33.3. Driver 591.86 advertises CUDA 13.1 capability, not necessarily an installed CUDA toolkit.
- Imported `lecg-revit-teacher:q4ks-20260905`: community Q4_K_S conversion of [schauh11/revit-coder-14b](https://huggingface.co/schauh11/revit-coder-14b), downloaded from [mradermacher/revit-coder-14b-GGUF](https://huggingface.co/mradermacher/revit-coder-14b-GGUF).
- Pinned revision: `775e4377803162dbadb357c13a26daf4a0fb7c81`; file `revit-coder-14b.Q4_K_S.gguf`, 8,573,476,384 bytes; local SHA256 `6eaae188e22e2ac188516973077558a40414b56253a64e8797ae5cd5db6d66fc`.
- The teacher's first four research responses were malformed. Plain ChatML GPU and CPU probes also produced unreadable text. Exact underlying cause remains unproven; do not describe this as a GPU limitation or a universal failure of the original model.
- Fallback: user's existing `qwen3:14b`, no additional download. The same baseline produced 12 valid JSON answers. Its first code tasks exposed inaccessible methods in the XML documentation; the corrected bank filters real public metadata and includes exact signatures. A subsequent 12-answer test produced 9 lookup candidates and 3 compilable code candidates, one of which lacked transaction commit. The current validator rejects that pattern.
- Hugging Face skill guidance informed pinned download and smoke-before-scale evaluation. The provided evaluation script targets cloud providers; this Windows/GGUF task instead uses an isolated C# harness to compile against the installed Revit API. No cloud inference provider is used.
- The publisher's benchmark scores structural patterns, not compilation or runtime execution. Its accuracy claims are not adopted as validation evidence.

## Campaign

### Revision 5: stricter API context and retest-before-resume

The original campaign-v3 stopped at 125 answers because its resumed code segment exceeded the rejection threshold. Its results are retained unchanged. Revision 4 tested richer context and revealed that the model still shortened types and sometimes invented API stubs; its results also remain separate.

The current campaign is **campaign-v5**. It supplies fully qualified types, exact return and argument types, documented enum names, return/exception documentation, and constrained C# scaffolds with caller-supplied objects. Each scaffold is compile-checked before entering the question bank. Eight unsupported scaffold candidates (including operator syntax and unavailable drawing references) were recorded as exclusions and replaced by other documented methods; the bank still contains 3,000 questions.

The supplied transaction template checks document editability, Start/Commit results, commits before returning, and conditionally rolls back/rethrows on errors. Transaction selection is a conservative engineering policy based on documentation and method-name hints, **not proof of actual runtime requirements**. Native reference exports now contain these explicitly labeled engineered scaffolds as well as installed API facts; do not attribute the scaffolds or policy to Autodesk documentation.

Generated C# must resolve the expected symbol in the actual `RevitAPI` assembly. Generated API stubs cannot satisfy that check. Scaffold candidates may define only the public static `Candidate` class; extra Revit method calls are rejected except for the transaction wrapper. These are static filters, not a security sandbox or proof of behavior. No candidate code is executed.

Local generation uses a 4,096-token context and output caps of 240 tokens for lookups / 1,000 for code. A rejected response gets **at most one** diagnostic repair request. Both raw attempts, validations and their combined token usage are retained. No unlimited retry loop is used. The bank, model, settings and harness binary are fingerprinted for resume.

Qualification prioritizes all 16 previously failed methods, 16 methods spread across the rest of the API bank, and eight lookups. `Campaign.ps1 Start` is blocked until all 40 answers exist, all 16 regressions pass, at least 80% of code passes first try, at least 90% passes after bounded repair, and all lookup outputs satisfy their schema. These criteria do not imply semantic accuracy or runtime safety. Live qualification evidence is in `campaign-v5\qualification-result.json`.

**Qualification passed at 2026-09-05 07:46 UTC:** 40 answers completed in 197 seconds. All 16 prior failures passed on their first attempt; across the complete 32-method sample, 31 passed on their first attempt (96.875%). All eight lookup answers passed schema checks. One `PlanViewRange.GetLevelId` candidate remained rejected after its one repair attempt because it selected an invalid exception constructor. The repair did not improve this sample's pass count; both attempts are retained. The local campaign was then resumed through the qualification-gated controller. Runtime verification and automatic deployment counts remain zero. A later stop or failure is reflected in `progress.json`, not this historical snapshot.

### Historical initial qualification

Qualification result, September 5, 2026: campaign-v3 completed 64 answers in 183 seconds: 48 lookup candidates, 14 compilation-qualified candidates pending review, 2 rejected code candidates, zero malformed JSON. Local usage: 14,654 prompt tokens and 8,492 generated tokens. The rejection checks caught a missing commit and another unsuitable candidate. No generated function has been runtime-verified or deployed. The remaining campaign was authorized to continue locally; `progress.json` is the source of current status, not this snapshot. The initial pace suggests approximately 2–3 hours for the full run, subject to workload, quality stops and Revit pauses.

- 2,216 lookup tasks: two English and two Spanish search phrases for each existing exact getter/setter operation.
- 784 C# method tasks: public, non-obsolete, non-generic methods selected round-robin across documented declaring types.
- These are research tasks, **not 3,000 newly installed functions**. Lookup phrases still require semantic review; method wrappers still require engineering and runtime tests.
- One local request at a time; current limits and bounded repair policy are described above. Local generation consumes electricity and GPU time; the orchestration conversation itself still uses the hosted assistant's quota.
- Pauses before the next request whenever Revit is open. Any active request may finish first; the fallback model expires from memory after its two-minute keep-alive. No user application is killed.
- Every completed answer is appended immediately; matching model and bank fingerprints are required for resume. A partial/corrupt JSONL row stops resume rather than silently discarding evidence.
- Automatic stop if more than 25% of answers violate the JSON contract after ten answers, or more than 75% of code candidates are rejected after sixteen code tasks.
- Reports are refreshed each 100 answers and on normal/error exit. A `STOP` marker stops between requests. No cloud fallback, retry storm, or automatic new download.

## Files and operation

Harness: `C:\LECG\RevitAddins\LECG\RevitCopilot\Research\KnowledgeLab.csproj`, target `net10.0`. The production add-in explicitly excludes `Research\**\*.cs`.

From `C:\LECG\RevitAddins\LECG\RevitCopilot\Research`:

```powershell
dotnet build -p:SkipRevitDeploy=true
dotnet bin/x64/Debug/net10.0/KnowledgeLab.dll self-test
dotnet bin/x64/Debug/net10.0/KnowledgeLab.dll prepare --output artifacts/campaign-v5 --retest-from artifacts/campaign-v3
dotnet bin/x64/Debug/net10.0/KnowledgeLab.dll run --limit 40 --model qwen3:14b --output artifacts/campaign-v5
.\Campaign.ps1 Qualify
.\Campaign.ps1 Start
.\Campaign.ps1 Status
.\Campaign.ps1 Stop
```

Use `Start` again to resume. Access to this directory may require an elevated terminal because existing files are administrator-owned. Do not use plain add-in builds that deploy over the live installation.

Current evidence directory: `C:\LECG\RevitAddins\LECG\RevitCopilot\Research\artifacts\campaign-v5`. The older campaign folders are retained for comparison. Do not rerun `prepare` over an existing answered bank with different settings.

- `questions.json`, `run-manifest.json`, `answers.jsonl`: reproducible inputs, provenance and raw results.
- `progress.json`: latest state; distinguish running, paused, failed and review-pending completion.
- `summary.json`: counts, local tokens and measured inference time; runtime-verified new functions remains zero until a separate promotion process exists.
- `api-reference.json`: model-independent reference entries from installed documentation and metadata.
- `lookup-candidates.json`, `code-candidates.json`: quarantined review packs, explicitly not approved for production/execution.

Model weights, outputs and build artifacts are ignored by Git. Failed experimental campaigns remain separate from the corrected campaign.

## Promotion and eventual removal

1. Check every selected candidate against official Revit 2026 signatures, arguments, units and document conditions. Reject invented inputs, hidden side effects and unverifiable assumptions.
2. Convert useful candidates into explicit native C# operations with constrained schemas, fresh document/element checks and relevant read-only or preview/apply integration.
3. For writes, require `Transaction.Start()`, commit-status checks, rollback/error handling and local confirmation. Test rollback, cancellation, invalid inputs and stale IDs as well as success.
4. Run scratch-document Revit tests and regression tests. Promote only tested operations and reviewed lookup phrases; preserve source and test evidence with each.
5. Benchmark native execution and retrieval separately from model response time. Reuse deterministic recipes without repeatedly generating the same code.
6. Once harvest/review is complete, remove only the task-owned teacher model and its exact downloaded GGUF if still desired. Never remove the user's pre-existing `qwen3:14b`, shared Ollama blobs or unrelated caches. The native add-in must not require the research harness, Ollama or the teacher file.

Current limitation: this turn prepares/starts harvesting; it does not establish that all 784 method candidates can become safe supported tools, nor that any generated model-changing function has passed Revit runtime tests.
