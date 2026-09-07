# Shared Revit 2026 knowledge

## Delivered behavior

The Copilot build embeds all 3,000 installed-API source references from campaign-v5. They are available from any active Revit 2026 project through `agent_read` operations `knowledge_search` and `knowledge_get`. No teacher model, research directory, external service, or generated C# loader is needed at runtime. The pack is 1,928,047 bytes before assembly embedding.

The pack preserves source member IDs, research IDs, research qualification status, and SHA-256 hashes of the campaign inputs and answers. It excludes generated code, prompts, unreviewed generated aliases, and model answers. A compiled research candidate is not a runtime-verified function.

Search returns at most five short matches (three by default). Fetch returns a bounded source excerpt with character pagination. Results identify an existing executable adapter where one is explicitly mapped; all other methods remain documentation-only. Neither a search match nor source text grants execution permission.

## Available tools and recipes

There are 48 registered operations: 31 reads and 17 changes, plus the existing 2,216 API property bindings (1,411 getters and 805 setters). The MCP protocol retains its 16 top-level tools; operations are discovered on demand.

The nine additions are:

| Operation | Purpose |
| --- | --- |
| `knowledge_search` | Find relevant shared source references and available adapters |
| `knowledge_get` | Fetch one bounded reference, its provenance and adapter schema |
| `element_dependents` | Inspect logical children before cleanup; actual deletion remains a preview decision |
| `element_valid_types` | Find compatible types for the current instance |
| `phases_list` | Read project phases in chronological order |
| `design_options_list` | Inspect primary options and option-set identifiers |
| `worksets_list` | Read user worksets; explicitly identify a non-workshared document |
| `schedule_fields` | Inspect ordered schedule columns, headings and hidden flags |
| `view_filters` | Inspect applied view filters, enabled state and visibility |

The shared library now supplies 25 curated recipe templates. New templates cover project health, phases, worksets/design options, dependencies, compatible type changes, schedule columns, view filters, and source reference lookup. Templates are labeled `curated_template`, not as previously executed workflows.

User-reviewed successful workflows are stored at `C:\Users\LECG Arquitectura\AppData\Local\RevitCopilot\recipes\workflows.json`. Saved arguments become fresh-input placeholders, while valid symbolic batch references retain their structure. Identical saves reuse the existing recipe. A cross-process file lock prevents competing Revit sessions from overwriting each other; atomic replacement retains `workflows.json.previous` for recovery. A conflicting save reports an error so it can be retried after the other save finishes.

Project conversations remain separate by account and project identity, resume on reopening, and use project filenames as titles. Shared procedures must never reuse another project's element IDs or preview tokens. Reads reporting unsupported items do not produce successful-execution receipts for recipe saving.

## Validation and scope

Builds target `net10.0-windows` for the Revit add-in and `net10.0` for MCP/offline checks, with Revit 2026 API references. The local builds completed with zero errors and warnings.

Offline checks passed for all 2,216 binding registrations, 3,000 embedded references, read/change discovery boundaries, explicit adapter mapping, bounded retrieval, recipe parameterization, duplicate prevention, recovery copies, concurrent-save protection, and per-project conversation isolation/resume. The session tests used a fake local server and made zero AI calls. The observed warm reference search mean was 3.20 ms for a narrow repeated test query; this is not full chat latency or general retrieval accuracy.

Scratch Revit tests passed at `C:\LECG\RevitAddins\LECG\RevitCopilot\Tests\bin\x64\Release\net10.0-windows\results\20260905_193602\smoke-results.json`. They verified ribbon opening of the sidebar, native read postconditions against the Revit API, schedule/filter identities, phase order, pagination, invalid targets, unsupported-result receipt rejection, transactions/rollback, and MCP transport. Live LLM testing was disabled.

The follow-up campaign on all 12 installed Autodesk sample copies completed in `C:\LECG\RevitAddins\LECG\RevitCopilot\Tests\bin\x64\Release\net10.0-windows\benchmarks\20260905_193652`. All 48 registered operations passed the coverage checks; all 12 reviewed fixture suites and transports passed, including the repaired Japanese floor fixture. Actual MCP reference search, reference fetch and phase reads passed in each sample. All original sample SHA-256 hashes matched after testing. Links remained unloaded in the copies, and write fixtures were rolled back.

The API coverage contains 1,189 getters invoked successfully on at least one suitable fixture, 222 getters unsupported or missing a suitable fixture, seven reviewed setters passed, and 798 setters awaiting reviewed runtime tests. There are still 141 failing getter/model combinations where an operation passed elsewhere. These remain visible in `operation-coverage.csv` and `benchmark-results.json`; neither unsupported contexts nor a successful tool envelope count as a successful getter invocation. This campaign does not validate arbitrary setters or execute generated candidate code.

The campaign exercised 564 MCP tool calls: 528 latency-workload calls plus 36 shared-knowledge checks. Across 120 warm observations per mode, three individual reads had a 30.62 ms median and 119.68 ms p95; the equivalent three-read batch had a 19.47 ms median and 39.77 ms p95. These local measurements exclude AI reasoning and are not a guarantee of complete chat speed. The benchmark made zero AI requests.

This delivery does not certify every possible Revit element type, family, linked model or worksharing state. Coverage gaps must remain explicit. General planning and natural-language reasoning still use the selected chat model; native tools and local retrieval do not require additional inference calls.

## Build and use

From `C:\LECG\RevitAddins\LECG\RevitCopilot`:

```powershell
dotnet build Tests/RevitCopilot.SmokeTests.csproj -c Release -p:Platform=x64 -p:SkipRevitDeploy=true --no-restore
dotnet build Tests/ConnectionTests/ConnectionTests.csproj -c Release -p:Platform=x64 -p:SkipRevitDeploy=true --no-restore
dotnet Tests/ConnectionTests/bin/x64/Release/net10.0/ConnectionTests.dll --agent-library
dotnet Tests/ConnectionTests/bin/x64/Release/net10.0/ConnectionTests.dll --api-library
dotnet Tests/ConnectionTests/bin/x64/Release/net10.0/ConnectionTests.dll --project-sessions
```

The deterministic exporter is `C:\LECG\RevitAddins\LECG\RevitCopilot\Research\RuntimeReferenceExport.cs`. To regenerate the source artifact after an intentional reviewed research-bank update:

```powershell
dotnet Research/bin/x64/Release/net10.0/KnowledgeLab.dll export-runtime --output Research/artifacts/campaign-v5 --destination Agent/Knowledge/revit2026-reference.json
```

Open a project and choose **Add-Ins > LECG Copilot > AI Copilot**. A practical first request is: "Use your shared project-health recipe. Inspect this project's warnings, links, phases and worksets. Give a compact summary and do not modify anything."

After verifying a useful workflow, ask: "Save this successful workflow as a reusable recipe with fresh inputs for other projects." Revit requests local confirmation before saving it. Changes to a model continue through preview, user review, local confirmation and transaction commit/rollback.

Installation completed on September 5, 2026 at 19:42:12 UTC. The Copilot-only installer replaced six files and verified their hashes. Recovery files and the exact file map are at `C:\LECG\RevitAddins\LECG\outputs\deployment-backups\combined-20260905-134211-3ed8ca37`. The main LECG UI DLL was unchanged.

Installed add-in: `C:\Users\LECG Arquitectura\AppData\Roaming\Autodesk\Revit\Addins\2026\RevitCopilot\RevitCopilot.dll`. Its SHA-256 is `F35D30FB295F6A11F12E1F674496A0A8DA230ADBC5886E33AD508AFF426DC48F`, identical to the tested build. The installed MCP passed initialization, 16-tool listing, path resolution and actual API discovery checks after deployment.
