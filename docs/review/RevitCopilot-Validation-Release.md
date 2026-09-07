# Shared MCP knowledge and runtime-validation release

September 5, 2026. This report supersedes the coverage figures in `RevitCopilot-Shared-Knowledge.md`; that report remains evidence for the earlier installation.

## Delivery scope

The Revit 2026 Copilot contains 52 native operations (35 reads and 17 changes), 29 curated reusable recipe templates, 2,216 API property bindings (1,411 getters and 805 setters), and 3,000 shared searchable research references. These are different inventories, not 3,000 independently executable commands.

Four new handwritten, read-only operations are available through the existing MCP `agent_read`/batch route:

| Operation | Result |
| --- | --- |
| `element_action_checks` | Current API feasibility for deletion, mirroring, creating parts, and phase modification; never authorization |
| `elements_joined` | Whether two current-document elements are geometrically joined |
| `type_compound_layers` | Ordered wall/floor/roof/ceiling type layers, widths in millimeters, functions and material identities |
| `instance_transform` | Instance-to-document origin and basis, reflection and conformality; does not open linked documents |

New recipes cover element feasibility, compound layers, geometry joins, and instance coordinates. The MCP still exposes 16 top-level tools; bounded discovery avoids transmitting every capability on each request.

The reference library and built-in recipes are bundled with Copilot and available across Revit 2026 project files. User-confirmed recipes are shared at `C:\Users\LECG Arquitectura\AppData\Local\RevitCopilot\recipes\workflows.json`, with atomic saves, cross-process locking, deduplication and recovery copies. Project conversations remain separate and resume by project/account identity. Procedures are reusable; element IDs, preview tokens and project literals are not.

The temporary research model is not required for runtime use. No generated candidate code is loaded or executed. The selected chat model still performs natural-language planning; native execution and local reference retrieval do not require another inference call.

## Validation contract

Tests operate only on detached, non-family, non-linked disposable copies of the 12 installed Autodesk project samples. Linked models remain unloaded. Original source hashes are checked on completion; disposable documents are closed without saving. Test-only confirmation callbacks are restricted to the sample-copy harness; production confirmation remains required.

Each changed-value setter success requires actual production preview and apply dispatch, verification that preview restored the original value, a committed result equal to the requested typed value through the direct Revit API, and an outer transaction-group rollback restoring the property and element count. Numeric perturbations use native API units on disposable fixtures, not a claim of physically appropriate design choices.

A same-value write is labeled `same_value_only`, never a validated edit. Rejected contexts and missing fixtures remain explicit. Getter success means real invocation and serialization, not complete engineering-semantic validation. A pass on one fixture does not certify every project, view, family, worksharing state or linked-model context.

Successful changed-value results can seed later campaigns. The final report records its baseline path/hash and tested binary hashes. Earlier successes are retained as evidence, not represented as newly repeated tests. API search and individual reference retrieval expose this evidence to the agent; it does not grant permission or replace fresh project checks and previews.

The 784 method research entries also have a metadata-only triage at `C:\LECG\RevitAddins\LECG\outputs\validation-expansion\method-audit.json`: 245 potential document/element adapters, 329 supporting API references, 129 collection helpers, 46 callback/event contracts, 23 rendering helpers and 12 external/session workflows. Triage is not runtime approval. Many entries are implementation support rather than meaningful standalone BIM commands.

## Reproduction

Working directory: `C:\LECG\RevitAddins\LECG\RevitCopilot`. Targets: `net10.0-windows` for the Revit 2026 add-in/tests and `net10.0` for MCP/offline utilities.

```powershell
dotnet build Tests/RevitCopilot.SmokeTests.csproj -c Release -p:Platform=x64 -p:SkipRevitDeploy=true --no-restore
dotnet build Tests/ConnectionTests/ConnectionTests.csproj -c Release -p:Platform=x64 -p:SkipRevitDeploy=true --no-restore
dotnet Tests/ConnectionTests/bin/x64/Release/net10.0/ConnectionTests.dll --agent-library
dotnet Tests/ConnectionTests/bin/x64/Release/net10.0/ConnectionTests.dll --api-library
dotnet Tests/ConnectionTests/bin/x64/Release/net10.0/ConnectionTests.dll --project-sessions
dotnet Tests/ConnectionTests/bin/x64/Release/net10.0/ConnectionTests.dll --usage-only
```

Do not rebuild test-owned assemblies while the test Revit process is running. Do not run a plain deployment build against an open Revit session. The sample harness is `C:\LECG\RevitAddins\LECG\RevitCopilot\Tests\SampleBenchmark.cs`; source-evidence export is `C:\LECG\RevitAddins\LECG\RevitCopilot\Research\RuntimeValidationExport.cs`.

## Final results

Completed campaign: `C:\LECG\RevitAddins\LECG\RevitCopilot\Tests\bin\x64\Release\net10.0-windows\benchmarks\20260905_203238\benchmark-results.json`. All 12 samples completed, all native fixture suites and MCP transports passed, and there were zero unhandled operation failures. Original sample hashes were unchanged. The campaign reused changed-value evidence from earlier completed campaigns; it did not rerun already-proven setters on every model.

| API coverage outcome | Count |
| --- | ---: |
| Getter invoked successfully in at least one applicable fixture | 1,217 |
| Getter unsupported or without an applicable fixture | 194 |
| Setter verified with an actual changed value | 618 |
| Setter accepted only an original-value write | 70 |
| Setter rejected in the available tested contexts | 35 |
| Setter without a matching fixture | 82 |

All 805 setters are accounted for, but **187 are not validated changed-value edits**. Examples include family-authoring geometry, fabrication parts without appropriate content/configuration, point-cloud assets, specialized reinforcement, and properties requiring additional model settings or compatible reference objects. These are real remaining coverage limits, not passing tests. Family-document changes remain outside the current production write contract.

All 3,000 research records are accounted for: 2,216 accessor references, nine method references mapped to tested handwritten adapters, 753 compiled candidates not promoted to runtime execution, and 22 rejected generated answers. The documentation remains searchable even when execution is unavailable. Full per-entry evidence is in `operation-coverage.csv` and `research-3000-accounting.csv` beside the campaign report; the installed API ledger is sourced from `C:\LECG\RevitAddins\LECG\RevitCopilot\Agent\Knowledge\revit2026-validation.json`.

The final packaged build passed 78 scratch checks at `C:\LECG\RevitAddins\LECG\RevitCopilot\Tests\bin\x64\Release\net10.0-windows\results\20260905_203931\smoke-results.json`, including both joined and unjoined geometry, all reported transform vector components, type-layer postconditions, invalid input rejection, and transaction checks. Offline tests passed for 52 operation registrations, 29 recipe templates, 3,000 shared references, all API bindings/evidence, quota notification handling, and per-project history/approval isolation. All tests made zero live AI inference calls; this statement does not imply this development conversation itself used no tokens.

The final transport benchmark measured 120 warm observations per mode across 12 models. Three separate reads had a 31.32 ms median / 136.06 ms p95; the equivalent read batch had a 20.50 ms median / 34.74 ms p95. These are local MCP round-trip measurements, excluding model reasoning. Repeated local reference search averaged 3.10 ms in the narrow offline check, and property search averaged 0.25 ms. These are measured workloads, not universal latency or intelligence claims.

## Installed bundle and recovery

Copilot-only installation completed at **2026-09-05 20:41:11 UTC**. Six files were replaced and checksummed. The installed server passed startup initialization, 16-tool listing, bundled path resolution, and actual API discovery. The main LECG UI DLL and manifest were preserved.

- Installed add-in: `C:\Users\LECG Arquitectura\AppData\Roaming\Autodesk\Revit\Addins\2026\RevitCopilot\RevitCopilot.dll`
- Installed/tested SHA-256: `6E0FDA8B8B867AB057B99820BBFE10D779586D32C013AF0A414561C66203D0AE`
- Recovery backup and exact file map: `C:\LECG\RevitAddins\LECG\outputs\deployment-backups\combined-20260905-144110-7940bb56`
- Unchanged main UI SHA-256: `CD9E11E32D4EB8E5262F26AE143379C379160359804BD56F300BB2323FD7815A`

The first installation attempt stopped during preflight because the old standalone MCP process held its DLL open; no installed files were replaced by that failed attempt. Only the verified Copilot server process was then stopped, with Revit closed. The subsequent installation succeeded. This already-open desktop task's old MCP transport is closed; the fresh installed-server check passed independently. Refresh that desktop connection before invoking its tools: **Settings > MCP servers > Restart**, as described in the [official MCP documentation](https://learn.chatgpt.com/docs/extend/mcp?surface=cli). No desktop restart was performed automatically because it could interrupt other active tasks.

Open Revit and choose **Add-Ins > LECG Copilot > AI Copilot**. The panel launches its bundled server independently. A first request can be: “Use your shared project-health recipe. Inspect this project's warnings, links, phases and worksets. Keep the result short and do not modify anything.” Reusable procedures load on demand; no import or retraining is needed for each project.
