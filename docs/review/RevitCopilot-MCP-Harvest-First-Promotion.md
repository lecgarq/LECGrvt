# MCP harvest: first native integration

## Parallel ownership with the Claude UI task

Codex owns `RevitCopilot\Agent\`, `RevitCopilot\Revit\ToolExecutor*.cs`, MCP backend integration and the backend test files changed in this task. Claude may refactor `RevitCopilot\UI\` and associated UI-only resources. Coordinate before changing `Llm\`, `Configuration\`, shared models, the main project file, or application startup. Do not deploy while the two tasks are active. Do not revert another agent's edits or perform broad cleanup of this untracked/dirty workspace.

This task changes no XAML or UI code. It adds the shared discovery file to the MCP and research project links and adds a test-only MCP bundle target. The actual installation is unchanged until a coordinated deployment is approved/performed after testing.

## What MCP gains

Three reviewed read-only native operations are registered in `agent_capabilities` and dispatched by `agent_read` / `agent_read_batch`. The MCP protocol still exposes 16 top-level tools; the handwritten operation catalog grows from 36 to 39. The existing 2,216 property-accessor bindings remain available. No general method invoker, dynamic C# loader, model-output loader or additional LLM is introduced.

| Native operation | Harvest source, campaign-v5 | Reviewed behavior |
|---|---|---|
| `host_inserts` | `742369128674aae8a9b74352`, `HostObject.FindInserts` | Current-document host UniqueId; explicit opening/shadow/embedded flags; bounded pages with current insert IDs. Defaults include rectangular openings but exclude shadows/embedded variants. |
| `element_phase_status` | `da314384a8346ca50371fffc`, `Element.ArePhasesModifiable` | Up to 50 current element UniqueIds; phase-property editability and existing phase identifiers. Does not guarantee that a proposed phase assignment is valid. |
| `host_bottom_faces` | `2c0d92d0a287c6d4189043f2`, `HostObjectUtils.GetBottomFaces` | Supported floor/roof/ceiling host; bounded references and square-meter face areas. Unsupported hosts fail explicitly. References must be re-queried after geometry edits. |

Source method semantics were checked against the installed Revit 2026 XML documentation. Production adapters were written and reviewed separately from the model responses. They use constrained arguments, current-document resolution and existing error envelopes. All three are read-only and open no Revit transaction. Test fixtures explicitly start/commit transactions and roll back on errors.

The remaining 759 compilation-qualified candidates are **not** connected by this change. The count is based on the completed research batch's 762 qualified candidates minus these three selected source methods; it is not a count of guaranteed future tools.

## Search reuse and measured scope

A small, reviewed in-process vocabulary supplies English/Spanish terms for the three operations and four existing property getters: created phase, pinned status, view scale and wall-type width. Property vocabulary was reviewed from harvest IDs `69e36c8f29f85a3eee59dba7`, `7c3b4363346128d22bf2163e`, `0e1ae2344563c98c944815a4`, `e790a894e759013620c63d92`; native-method phrases were curated separately. Accent normalization supports terms such as `creación` and `demolición`. No research JSON is loaded at runtime.

The vocabulary ranks existing operations; it cannot register new executable code. Read aliases do not silently add setter aliases. The caller must still choose read/change mode, inspect the returned schema and resolve current project inputs. Search is a relevance heuristic, not an authorization or intent classifier.

Local regression: four curated Spanish property queries improved from 0/4 correct top-ranked results under the previous ranking to 4/4 with the reviewed vocabulary. Three native Spanish queries also selected their expected operations. Warm mean for the four-query mix was 0.98 ms in this run. This is a small, deliberately targeted test—not evidence of general language understanding, an overall accuracy percentage, or faster complete chat responses.

## Build and verification

Production add-in: `C:\LECG\RevitAddins\LECG\RevitCopilot\RevitCopilot.csproj`, `net10.0-windows`; MCP server and connection tests: `net10.0`. Target API: Revit 2026 only.

From the corresponding project directory:

```powershell
dotnet build -c Release -p:SkipRevitDeploy=true
```

Connection checks from `C:\LECG\RevitAddins\LECG\RevitCopilot\Tests\ConnectionTests` after its Debug build:

```powershell
dotnet bin/x64/Debug/net10.0/ConnectionTests.dll --agent-library
dotnet bin/x64/Debug/net10.0/ConnectionTests.dll --api-library
dotnet bin/x64/Debug/net10.0/ConnectionTests.dll --installation-only C:\LECG\RevitAddins\LECG\RevitCopilot\bin\x64\Release\net10.0-windows\RevitCopilot.dll
```

These checks passed: 39 operation registrations, bilingual discovery, read/change boundary, recipe parameterization, all 2,216 accessor bindings, bundled MCP startup, 16-tool listing and actual API discovery. No model inference was requested.

Scratch runtime tests add rectangular openings to a disposable wall, check filter/pagination behavior, compare phase status to the API, validate a 100-square-foot floor's returned square-meter area and reference resolution, exercise six invalid-input/type cases, and check no element-count or phase mutation from reads. A separate test launches the built MCP server and sends actual `initialize` and `tools/call` messages through its Revit pipe and ExternalEvent route for all three operations in one batch.

Runtime result: **passed**, with live model inference disabled. Evidence: `C:\LECG\RevitAddins\LECG\RevitCopilot\Tests\bin\x64\Release\net10.0-windows\results\20260905_163104\smoke-results.json`. All three native operations passed positive and negative scratch-document checks, and actual MCP server discovery plus the three-operation `agent_read_batch` completed successfully. The test-owned Revit instance exited normally.

Measured in this small fixture: warm `host_inserts` mean 0.06821 ms through the test executor; the three-read MCP batch reported 0.4765 ms native execution and 1.6728 ms queue wait. These figures exclude AI reasoning, complete protocol round-trip/startup cost, and large-project workload. They establish working native execution, not a before/after speedup for arbitrary chat requests. No general real-project, workshared, linked-model or exhaustive geometry coverage is claimed.

## Handoff

The first backend pack was **deployed with the completed main LECG UI refactor on September 5, 2026 at 17:04 UTC**. See `RevitCopilot-Combined-Deployment.md` for paths, checks and backup details. Keep future UI and backend changes separate in review. Preserve the research corpus and provenance; retain only reviewed vocabulary and native functions as runtime dependencies. The temporary teacher model is not required for these operations.
