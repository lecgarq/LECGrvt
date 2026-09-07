# Revit 2026 API expansion — 2026-09-05

## Delivered scope

The installed Revit 2026 API assembly supplies **2,216 executable property accessor bindings: 1,411 getters and 805 setters**. Each name identifies a real declared API accessor on an Element subclass. These are property-read/property-write functions, not 2,216 independent modeling commands and not the entire Revit API.

The count excludes obsolete types/members, indexers, static access, constructors, session/document operations and unsupported value types. No arbitrary code or unrestricted method invocation is exposed. Existing 36 hand-written operations remain available alongside this adapter. The complete MCP interface has 16 entrypoints, so thousands of function definitions do not enter every prompt.

This expansion is available in Codex subscription/MCP mode. The optional direct-provider API fallback still uses its original six-tool schema.

## How to use it from the panel

1. Ask a concrete task in ordinary language. The panel is instructed to use `elements_find` for fresh IDs and runtime types, then `api_search` for relevant properties.
2. Related inspections can use `agent_read_batch`: up to 12 operations in one Revit request, with explicit stop-on-error results.
3. Related edits can use `agent_preview_batch`: up to 12 steps in one rolled-back transaction. `agent_apply` asks for one local confirmation and either commits the whole batch or rolls it all back.
4. Save successful workflow receipts only after reviewing the result. Batched recipes retain their operations and symbolic dependencies while replacing old literal inputs with fresh-input placeholders.

Example panel request:

> Inspect up to 10 walls. Use the API library to read width and relevant wall properties, and batch related reads. Do not change the model. Keep the answer short.

Change example:

> Find the selected views, inspect their current scales, and preview setting them to 1:100 as one batch. Do not apply until I confirm.

Raw operation contract example:

```json
{
  "operation": "api.get:Autodesk.Revit.DB.Wall.Width",
  "arguments_json": "{\"unique_ids\":[\"CURRENT-WALL-UNIQUE-ID\"]}"
}
```

The identifier above is an explanatory placeholder, not an executable target. Actual requests must use an ID returned from the current model.

## Safeguards and limitations

- API property changes reuse the existing preview, one-use 10-minute token, document-revision check, local confirmation and transaction rollback pipeline.
- Each property operation targets at most 50 explicit elements. An atomic batch allows at most 200 target entries across steps; deletion is capped at 200 affected elements including dependencies across the batch.
- Element references use current-document UniqueIds or explicit negative built-in sentinel IDs. Wrong element classes and unsupported signatures fail before mutation.
- String inputs are bounded; enum inputs must use exact named values. Numeric writes to double/float/XYZ properties require explicit `units: "revit_internal"`. Native Revit lengths are feet; not every numeric property is a length. Prefer existing millimeter-aware operations where available.
- `$step:stepId.field` references resolve separately during preview and commit. Rolled-back object identifiers are not reused in committed execution. Forward, missing and nested-batch references are rejected.
- API applicability remains project-specific. For example, not every view has a discipline, and curtain-panel properties do not apply to ordinary families. Those errors are returned rather than hidden.
- Family-document editing, general object construction, arbitrary API methods, unrestricted automation and complete API coverage are not supplied by this property adapter. Broader modeling/creation commands remain a separate expansion area.

## Verification evidence

Scratch-model result: `C:\LECG\RevitAddins\LECG\RevitCopilot\Tests\bin\x64\Release\net10.0-windows\results\20260905_062006\smoke-results.json`.

- **54 checks passed**, zero failed; live model inference disabled.
- The getter sweep executed **561 distinct property reads successfully**. Eleven additional reads returned documented applicability errors on the fixture; the report preserves their names and errors. The remaining getters were not exercised because matching fixtures were unavailable.
- Setter tests cover names, integers, colors, booleans, native-unit doubles, named enums and current-document ElementId references. They also check rollback, denied confirmation, wrong target classes, invalid enum inputs and token reuse.
- Batch tests cover fresh result dependencies, preview rollback, successful atomic commit, failed-step full rollback and read stop-on-error. A real named-pipe/ExternalEvent integration check executes three reads in one request and verifies queue/execution timing fields.
- Offline tests structurally validate all 2,216 bindings against actual public API accessors, bounded typed discovery, symbolic dependency resolution, saved-batch structure and operation-scope rejection. Structural checks are not presented as successful execution of every accessor.
- Package tests verify the initialize handshake, all 16 MCP entrypoints and a real `api_search` call against the bundled server.

## Performance evidence, not promises

One offline run measured catalog initialization at about **449 ms** and subsequent focused searches at **0.23 ms average**. The final Revit integration run executed three batched reads in **1.26 ms**, with **11.39 ms queue wait**. These are local fixture measurements, not end-to-end chat latency or production benchmarks.

The panel receives only relevant function summaries. Batches reduce the required tool round trips, while `elements_find` avoids sorting/materializing the entire result set for a small page. Returned `execution_ms` and `queue_wait_ms` separate native operation time from waiting for Revit. AI reasoning/network latency is still outside those measurements; no live before/after chat benchmark was performed to avoid additional inference usage.

The OpenAI Docs skill informed the compact discovery and batching instructions, consistent with [official function-calling guidance](https://developers.openai.com/api/docs/guides/function-calling) on clear contracts and combining sequential functions. This is local tool batching, not the paid-provider Batch API.

## Source and build

Source root: `C:\LECG\RevitAddins\LECG\RevitCopilot`. Add-in target: `net10.0-windows`; MCP and offline tests: `net10.0`.

- `Agent\RevitApiCatalog.cs`: supported accessor discovery, binding and keyword search.
- `Revit\ToolExecutor.Api.cs`: typed current-document invocation and conversion guards.
- `Agent\AgentBatch.cs` and `Revit\ToolExecutor.Batch.cs`: dependency resolution, bounded inspection and atomic batches.
- `Tests\ApiSmokeChecks.cs` and `Tests\ConnectionTests\ApiLibraryChecks.cs`: runtime and offline evidence.

Build without deployment: from the source root, `dotnet build -c Release -p:SkipRevitDeploy=true`. Build runtime tests from its `Tests` directory with the same command. The connection-test executable supports `--api-library`, `--agent-library`, and `--installation-only <absolute RevitCopilot.dll path>`. Runtime journal testing requires Revit closed and the test-only flags `REVIT_COPILOT_AGENT_TEST=1`, `REVIT_COPILOT_API_TEST=1`, `REVIT_COPILOT_LIVE_CODEX_TEST=0`, `REVIT_COPILOT_USAGE_TEST=0`.

For intentional installation, close Revit and run `dotnet build -c Release` from the source root; also build its `McpServer` subdirectory normally to update the separate desktop registration. Do not use deployment builds while Revit is open.
