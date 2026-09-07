# LECG Revit Copilot

AI tool layer for Autodesk Revit 2026.5. The in-Revit WPF panel defaults to the local Codex App Server and uses the ChatGPT subscription already signed into Codex. MCP now exposes 16 entrypoints: the original six tools plus discovery, 36 hand-written operations, 2,216 bound API property accessor functions, batching, and recipe storage/retrieval. Direct API-provider mode remains an optional six-tool fallback. The existing LECG add-in remains independently deployable. See [API expansion evidence and limitations](RevitCopilot-API-Expansion.md).

## Requirements

- Autodesk Revit 2026.5
- Microsoft .NET 10 SDK 10.0.400 or later
- Windows x64
- The local Codex client installed and signed in with ChatGPT
- An API key only if the optional `Direct API` fallback is selected

The project references these installed Revit 2026.5 assemblies with `Private=False`:

- `C:\Program Files\Autodesk\Revit 2026\RevitAPI.dll`
- `C:\Program Files\Autodesk\Revit 2026\RevitAPIUI.dll`

## Safe build

From `C:\LECG\RevitAddins\LECG\RevitCopilot`:

```powershell
dotnet build -c Debug -p:SkipRevitDeploy=true
```

Output:

```text
C:\LECG\RevitAddins\LECG\RevitCopilot\bin\x64\Debug\net10.0-windows\RevitCopilot.dll
```

The nested `RevitCopilot\global.json` selects the .NET 10 SDK. The repository root remains pinned to .NET 8 for the existing LECG solution.

The add-in project now builds and bundles its `net10.0` MCP server and dependencies under the output's `mcp` directory. A Release deployment (`dotnet build -c Release`, with Revit closed) copies both the `net10.0-windows` add-in and the bundled server into `%APPDATA%\Autodesk\Revit\Addins\2026\RevitCopilot`. A separate MCP-server build is no longer required for the Revit panel installation.

Server discovery prefers the `mcp` directory beside the loaded add-in DLL. For older installations it also checks the Copilot settings root and `%LOCALAPPDATA%\RevitCopilot\mcp`. Incomplete installations are skipped when another complete candidate exists. If none can be used, the error includes each checked directory and its missing or inaccessible files. This repairs the previous installation's dependence on a single per-user server location.

To verify the package without opening Revit or sending an AI prompt, build `RevitCopilot\Tests\ConnectionTests\ConnectionTests.csproj`, then run its executable with `--installation-only` followed by the full path of the packaged `RevitCopilot.dll`. It tests missing and partial installations, fallback paths, bundled-server priority, actual production path resolution, the bundled server's initialize handshake, its 16-tool inventory, and real API function discovery.

## Chat directly inside Revit — no API key

1. Start Revit 2026.5 and open the target project.
2. Select **Add-Ins → LECG Copilot → AI Copilot**.
3. Keep **Chat with: Codex (ChatGPT subscription)** selected.
4. Select a **Model** and **Reasoning** effort. These lists come from your signed-in Codex server, including paginated catalog entries; no model names or effort levels are hardcoded in the panel. Hidden catalog entries are labeled and are not a guarantee of inference access.
5. Type the request in the panel and press **Send** or `Ctrl+Enter`.

**Refresh models** reloads the catalog and keeps the current selection when available. Switching models preserves the conversation and sends the chosen model and effort on the next turn. **Reconnect** restarts the Codex connection and begins a new conversation; the visible earlier messages remain for reference. Repeat any relevant details in the new conversation. Direct API mode disables these Codex-specific controls and uses its existing provider configuration.

The current account catalog reports `gpt-6-astra` and `gpt-5.6-sol`, both with `low`, `medium`, `high`, `xhigh`, `max`, and `ultra`. Catalog contents can change; the panel displays what the server returns rather than assuming that a model named GPT-6 Sol exists.

While Codex is working, **Send** changes to **Cancel** so a delayed turn can be interrupted without restarting Revit.

### Quota and token activity

The **Account usage** section shows every allowance bucket returned by Codex, percentage remaining, and reset time (Windows local time with UTC offset). This is shared account usage, not an allowance reserved for Revit. It reads `account/rateLimits/read`, accepts `account/rateLimits/updated` notifications even between AI turns, and refreshes every 60 seconds while the panel is visible in Codex mode. **Refresh usage** performs a manual check. Notification updates arrive as the server reports them; this is not a per-second estimate. Refresh errors retain the last reading and label its age. Missing data is shown as unavailable, not zero consumption.

`thread/tokenUsage/updated` supplies last-turn and thread token totals, including a separate cached-input count. These are activity counters, not tokens left in the subscription. The display never consumes reset credits, purchases credits, or changes quota limits. Direct API mode pauses this subscription display and directs the user to their provider. Quota checks send no model prompts. The initial reasoning effort is **low** when supported; subsequent explicit effort choices remain under user control. Agent instructions request small queries, concise answers, and avoidance of duplicate reads.

Verified with the installed-account usage endpoint and the Revit panel on September 4, 2026 (UTC result directory `Tests\bin\x64\Release\net10.0-windows\results\20260905_044150`). All 13 scratch-model checks passed, including actual quota text rendering and the low-effort default; `live_llm_tested` is **false** for this run. `ConnectionTests --usage-only` covers multi-bucket precedence and partial notifications, missing/null data, percentage clamping, token totals, reset invalidation, and updates without an active turn. No inference calls were needed for these checks. Per-turn token notifications were tested with protocol fixtures, not an additional paid/subscription model request.

### Implemented: reusable agent functions

The hand-written catalog exposes 19 read operations and 17 change operations (including atomic batching). A separate API search exposes 1,411 property reads and 805 property changes. These are actual public accessor bindings, not a claim that every binding has been exercised in Revit. Session logs remain an audit trail, not model training. See [capabilities and verification](RevitCopilot-Agent-Capabilities.md).

1. Existing LECG rename, solid-fill material graphics, slab offset and shape-reset services are reused directly. New change operations require a rollback preview, fresh one-use token, and local Revit confirmation before committing.
2. Eight starter recipes are labeled curated templates. Successful operations produce receipts; saving 1–8 receipt-backed steps requires local user review. Every argument value becomes a fresh-input placeholder, including element identifiers.
3. Keyword search returns a few summaries; the agent fetches only relevant full recipes. The local library needs no embeddings or inference. The model still uses subscription quota when interpreting requests and coordinating tools.

Recipes are data, not executable code or unattended agents. Atomic change batches can run up to 12 steps with fresh inter-step result references, and retain their structure when saved as a recipe. Resolve current project inputs and use the normal preview/confirmation path for every new change. This is not the entire Revit API; cleanup currently means explicitly reviewed deletion, not automatic unused-content purging.

When the panel loads, the add-in starts `codex app-server --stdio`, verifies that its active authentication type is `chatgpt`, creates a persistent Codex thread, and loads the model catalog. Each message streams its result back to the panel. Codex uses the `lecg-revit` MCP server to reach the active Revit document. No OpenAI API key is read in this mode.

The embedded thread explicitly configures the Revit server with the absolute system .NET host, deployed server DLL, and server working directory. This avoids reliance on a Revit-inherited PATH or working directory, and does not rewrite global Codex settings. Before inference, the client verifies that the thread's MCP catalog exposes `project_info`. Failed initialization surfaces connection diagnostics and supports a fresh retry with **Reconnect**. The MCP server exposes tools, not resources. Control requests have a 45-second timeout; startup/model refresh has a 60-second UI timeout.

The embedded Codex thread is read-only for the filesystem and automatically declines shell/file-change approval requests. Its Revit-specific instructions require an explicit confirmation message before parameter changes or deletion. The production tool layer still validates document identity and worksharing ownership and uses explicit Revit transactions with rollback.

## Chat from Codex through MCP — no API key

The `lecg-revit` MCP server is registered globally in Codex and starts automatically when Codex needs it. Restart Codex after the first registration, then:

1. Start Revit 2026.5 and open the project you want to inspect or change. The Copilot pane does not need to be open.
2. Open this repository in Codex.
3. Ask, for example: `Use lecg-revit to show project information and list up to 10 walls.`
4. Review and approve requests that modify parameters or delete elements.

If Revit is closed, the tool returns: `Revit 2026 is not connected. Start Revit and confirm the LECG Revit Copilot add-in loaded.`

The connection stays on the computer: Codex launches the MCP process over STDIO, and that process connects to Revit through a named pipe restricted to the current Windows user. The bridge then queues work through Revit's `ExternalEvent` mechanism. No API key is read by MCP mode.

MCP server deployment:

```powershell
cd C:\LECG\RevitAddins\LECG\RevitCopilot\McpServer
dotnet build -c Release -p:SkipRevitDeploy=false
codex mcp add lecg-revit -- dotnet "$env:LOCALAPPDATA\RevitCopilot\mcp\RevitCopilot.McpServer.dll"
codex mcp get lecg-revit
```

## Configure the optional Direct API fallback

On first Revit startup the add-in creates:

```text
%LOCALAPPDATA%\RevitCopilot\config.json
%LOCALAPPDATA%\RevitCopilot\system_prompt.txt
%LOCALAPPDATA%\RevitCopilot\sessions\
```

Select **Direct API (provider credits)** in the panel to call a Responses-compatible provider directly. This mode requires separate provider API credits. Recommended API-key setup:

```powershell
setx REVIT_COPILOT_API_KEY "your-api-key"
```

Restart Revit after setting the environment variable. `OPENAI_API_KEY` is also accepted. As a fallback, `api_key` can be populated in `config.json`.

Default configuration:

```json
{
  "endpoint": "https://api.openai.com/v1/responses",
  "model": "gpt-5.6",
  "api_key": "",
  "request_timeout_seconds": 120,
  "max_agent_iterations": 12
}
```

The endpoint and model can be changed without recompiling. The add-in reads `system_prompt.txt` at the start of every user request, so prompt edits take effect immediately.

## Intentional deployment

Close Revit before deploying. From `C:\LECG\RevitAddins\LECG\RevitCopilot`:

```powershell
dotnet build -c Release -p:SkipRevitDeploy=false
```

This installs:

```text
%APPDATA%\Autodesk\Revit\Addins\2026\RevitCopilot.addin
%APPDATA%\Autodesk\Revit\Addins\2026\RevitCopilot\RevitCopilot.dll
```

Start Revit 2026.5, open a project, and select **Add-Ins → LECG Copilot → AI Copilot**. The pane docks on the right.

## Agent behavior

Codex mode uses the App Server JSON-RPC protocol for ChatGPT authentication, persistent threads, streamed agent events, and MCP tool calls. Direct API mode uses native JSON function tools through the Responses API and preserves multi-turn state with `previous_response_id`. Both modes execute Revit operations through the same queued `ExternalEvent` dispatcher.

Available tools:

- `project_info`
- `elements_query`
- `element_get`
- `element_set_parameter`
- `elements_delete`
- `view_isolate_or_select`

All Revit API access—including the quick **Add Current Selection** action—is marshaled through `IExternalEventHandler`. Queries do not open transactions. Parameter changes and deletions use explicit Revit transactions and therefore appear in Undo history.

Completed chat state and tool activity are written to timestamped JSON files in `%LOCALAPPDATA%\RevitCopilot\sessions`.

## Safety behavior

Before any model mutation, the tool layer verifies that the element belongs to the active document and is not owned by another worksharing user. Mutation failures roll back the active transaction and return an actionable JSON error to the model.

Numeric values sent to double parameters are interpreted as raw Revit internal feet. Formatted strings such as `2500 mm` are parsed using the project units. Double parameter results include the raw internal value, formatted display value, Forge specification ID, and an explicit `unit_type_unknown` flag.

## Main files

- `C:\LECG\RevitAddins\LECG\RevitCopilot\RevitCopilot.csproj`
- `C:\LECG\RevitAddins\LECG\RevitCopilot\RevitCopilotApp.cs`
- `C:\LECG\RevitAddins\LECG\RevitCopilot\RevitCopilotCommand.cs`
- `C:\LECG\RevitAddins\LECG\RevitCopilot\UI\DockablePanel.xaml`
- `C:\LECG\RevitAddins\LECG\RevitCopilot\UI\DockablePanel.xaml.cs`
- `C:\LECG\RevitAddins\LECG\RevitCopilot\Revit\ExternalEventDispatcher.cs`
- `C:\LECG\RevitAddins\LECG\RevitCopilot\Revit\ToolExecutor.cs`
- `C:\LECG\RevitAddins\LECG\RevitCopilot\Mcp\McpBridgeService.cs`
- `C:\LECG\RevitAddins\LECG\RevitCopilot\McpServer\RevitCopilot.McpServer.csproj`
- `C:\LECG\RevitAddins\LECG\RevitCopilot\McpServer\RevitTools.cs`
- `C:\LECG\RevitAddins\LECG\RevitCopilot\Llm\CodexAppServerClient.cs`
- `C:\LECG\RevitAddins\LECG\RevitCopilot\Llm\IAgentClient.cs`
- `C:\LECG\RevitAddins\LECG\RevitCopilot\Llm\LlmClient.cs`
- `C:\LECG\RevitAddins\LECG\RevitCopilot\RevitCopilot.addin`

Codex App Server protocol: <https://learn.chatgpt.com/docs/app-server>

Codex authentication: <https://learn.chatgpt.com/docs/auth>

OpenAI tool-calling contract: <https://developers.openai.com/api/docs/guides/function-calling>

## Verified behavior — September 4, 2026

The Release build completed with zero warnings/errors. A journal-driven session in installed Revit 2026.5 opened a new, unsaved project, clicked the actual ribbon command, verified the pane with `IsShown()`, executed all six production tools through `ExternalEvent`, and sent a live prompt from the Revit backend through the ChatGPT-authenticated Codex App Server.

Thirteen assertions passed: pane visibility; project information through the MCP named-pipe bridge; live responses using the actual model and effort selectors with GPT-6 Astra / low and GPT-5.6 Sol / low, both identifying the project as `Project1`; direct project information; wall query; parameter inspection; committed comment update; `2500 mm` conversion to internal length; invalid-value rejection with the previous value preserved and transaction closed; active selection; committed deletion; and availability of Revit Undo after mutations. Revit saved only the generated scratch model and exited cleanly.

This caught and fixed a document-identity bug: Revit can return distinct managed wrappers for the same document. Mutation validation now resolves the element in the active document and checks link status and document paths instead of relying on CLR reference equality.

Evidence:

- Journal: `C:\LECG\RevitAddins\LECG\RevitCopilot\Tests\journal.0010.txt` (`LECG_COPILOT_SMOKE PASS`, line 1811).
- Results: `C:\LECG\RevitAddins\LECG\RevitCopilot\Tests\bin\x64\Release\net10.0-windows\results\20260905_041534\smoke-results.json` (`live_llm_tested: true`).

The separate `Tests\ConnectionTests\ConnectionTests.csproj` executable links the production connection client and verifies dynamic catalog parsing, every advertised model/effort pair, rejection of an invalid effort, MCP initialization, and reconnect without inference. Run with `--no-dotnet-path` to test startup without a discoverable `dotnet` on PATH. `--live` additionally sends read-only prompts through Astra and Sol and requires an open Revit model. Only Astra / low and Sol / low were tested with inference; the remaining catalog entries and effort combinations were validated as selectable server-provided options, not individually submitted to the model service.

The assertions do not test worksharing against another user's ownership, linked models, actually invoking Undo, or the optional Direct API provider. The live Codex test used ChatGPT authentication; the API key was absent from both supported environment variables and configuration.

### Repeat the isolated Revit test

Close Revit. From `C:\LECG\RevitAddins\LECG\RevitCopilot\Tests`:

```powershell
dotnet build -c Release -p:SkipRevitDeploy=true
& 'C:\Program Files\Autodesk\Revit 2026\Revit.exe' 'C:\LECG\RevitAddins\LECG\RevitCopilot\Tests\journal.RevitCopilotSmokeTest.txt'
```

Keep the journal's Windows CRLF line endings. Its adjacent test manifest loads the production add-in and a test-only application. Never deploy that test manifest to the normal Revit add-ins folder. The test runs on its own new scratch project, writes a timestamped `smoke-results.json` under the test output's `results` directory, and exits. Check `passed: true`; successful playback alone is not proof that the assertions ran.

For a manual acceptance test, use Codex mode in the Revit panel with a disposable model: query walls, inspect one wall, request a comment change, confirm it in a second message, inspect the result, and use Revit Undo. A separate live-provider check is only needed for the optional Direct API fallback.
