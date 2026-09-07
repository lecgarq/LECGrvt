# Project conversations, approval repair, and panel redesign

Target: Autodesk Revit 2026; Copilot `net10.0-windows`. Main LECG remains `net8.0-windows` and is not redeployed by this repair.

## Behavior

- Saved projects retain a Codex thread ID and transcript, partitioned by signed-in account and canonical file/central/cloud identity.
- First message creates a thread named after the project file. Loading the model catalog or opening a file does not create an empty chat.
- Reopening/switching back resumes the thread; Reconnect retains it. Missing/unreadable history fails visibly, without silently replacing it.
- New chat requires confirmation, preserves the old Codex thread, and archives its local record. The panel shows the latest 60 messages, with an earlier-messages control.
- First Save migrates an unsaved conversation. Save As uses a separate project identity. Detached/unsaved projects are session-only until saved normally; cloud identity uses project/model GUIDs.
- A per-project file lease prevents simultaneous use of one conversation from two Revit windows.
- Old generic `LECG Revit Copilot ...` chats are not automatically assigned to a model: they predate reliable project mappings and remain in Codex. Persistence starts with this version's project conversations.
- Conversation context is not a live model cache. Resuming sends a short reminder to re-query facts and obtain fresh previews and confirmations.
- Direct API remains a separate, non-persistent conversation; its history is reset on project changes to prevent cross-project context.

## Approval and isolation

The previous client used `approvalPolicy=never` and declined server requests. It now requests user-reviewed approvals and displays structured choices with **no preselected answer** and no default accept button.

Supported user-input requests and bounded Revit MCP forms receive the user's explicit response. Unsupported requests, secrets/URL forms, file edits, shell execution, and filesystem/network permission grants remain denied. Closing/hiding the panel, stopping a turn, reconnecting, or changing projects cancels pending decisions. Foreign-thread and stale-turn requests cannot approve work.

`agent_apply` retains its destructive annotation, local Revit Yes/No dialog (default No), one-use preview IDs, document/revision/expiry validation, and atomic transaction commit/rollback. Nothing in this repair approves deletion of the user's six links.

Panel MCP servers use a process-specific pipe and immutable expected project/runtime identifiers. These are checked in the Revit ExternalEvent dispatcher before tools run. Revit `Document.Equals` is used for native document identity, not CLR wrapper identity. A stale session cannot silently run against another open model. The legacy external MCP endpoint remains compatible.

## Presentation

The actual WPF panel uses LECG's Paper/Stone/Water/Ink palette, a project identity card, compact model and quota sections, a clearer composer, differentiated messages, and separate decision cards. Usage still refreshes every 60 seconds while visible and reacts to server updates. Example screenshots use synthetic project text, not user-model inspection results.

## Verification

- Offline protocol suite: startup without empty threads, restart/resume, same-filename isolation, project switching, first Save, new-chat archival, account partitioning, file lease, explicit approval/decline, cancelled approval recovery, corrupt-history preservation, filename titles, and approval policy. No live AI calls.
- Revit scratch suite: **63 passed**, including rendered panel checks at 340/480 px, explicit UI decline, cancellation on hiding the panel, project/runtime guard rejection through the actual MCP process, native reads, existing preview/commit/rollback safeguards, and API batches. Final result: `RevitCopilot/Tests/bin/x64/Release/net10.0-windows/results/20260905_180011/smoke-results.json`.
- The first runtime run exposed a CLR-wrapper identity bug; corrected using the installed Revit API's documented Document.Equals semantics, then rerun successfully.
- Catalog checks: 39 native operations; 2,216 property bindings remain structurally valid. This is not a claim that every property was executed in every project.
- Main project and Copilot Release builds pass. Tests do not require a live AI inference request; real project/user approval remains the final interactive acceptance check.

## Build and install

Installation verified September 5, 2026 at 18:01 UTC: six changed file targets across the initial update and final two-file patch; zero SHA256 mismatches; the installed Copilot DLL equals the final scratch-tested DLL; main LECG DLL unchanged. Pre-feature recovery backup: `outputs/deployment-backups/combined-20260905-115747-7eca0be9`; final-patch backup: `outputs/deployment-backups/combined-20260905-120102-a6a51ca2`. Installed MCP startup handshake, 16 exposed tools, and actual capability discovery passed again after the final patch.

Rendered examples: [project chat](RevitCopilot-Project-Chat.png), [approval card](RevitCopilot-Approval-Card.png).

From `C:\LECG\RevitAddins\LECG\RevitCopilot\Tests`:

```powershell
dotnet build -c Release -p:SkipRevitDeploy=true
```

From `C:\LECG\RevitAddins\LECG\RevitCopilot\Tests\ConnectionTests`:

```powershell
dotnet build -p:SkipRevitDeploy=true
dotnet bin/x64/Debug/net10.0/ConnectionTests.dll --project-sessions
```

With Revit closed, from the repository root:

```powershell
./tools/InstallCombinedRevit2026.ps1 -CopilotOnly
```

The installer skips identical files, backs up replaced files, validates paths and SHA256 hashes, and rolls back its scoped changes on failure. Saved conversations live under `%LOCALAPPDATA%\RevitCopilot\project-conversations`; JSON writes are atomic with a `.previous` recovery copy. These files contain conversation content and should be treated as project-sensitive data.

## Acceptance in a real project

1. Open a saved RVT, open Copilot, send a read-only request. Its Codex chat should have the RVT filename.
2. Close/reopen Revit and the same file. The panel restores messages; the next send resumes the same chat.
3. Switch to another model. Its history should be separate.
4. Request a fresh preview of an intended change, inspect it, then ask to proceed. Choose an explicit approval in the panel and inspect Revit's final confirmation. Choosing No/Decline must leave the model unchanged.

Protocol reference: [official Codex App Server documentation](https://learn.chatgpt.com/docs/app-server). Wire shapes were also checked against the locally installed Codex-generated JSON schemas.
