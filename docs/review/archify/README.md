# LECG plugin knowledge atlas

Start with the [interactive knowledge map](lecg-knowledge.html). Select a node's SRC badge to open its source references. The map organizes the whole plugin into 12 areas; it is a navigation aid, not an exhaustive runtime call graph.

For detailed lookup, use [knowledge-index.json](knowledge-index.json). It inventories command classes, service files by domain, native MCP operation names and argument contracts, curated recipes, documentation paths, and recorded API coverage. Prefer a relevant section or a text search over loading the entire index into a conversation.

## What this knowledge includes

| Area | Where the knowledge lives | What it covers and how it can be used |
| --- | --- | --- |
| Naming and batch rename | [Rename rules](../../../LECG.Core/Rename/RenameRuleEngine.cs), [rename services](../../../src/Services/Renaming) | Search/replace, prefixes, suffixes, case rules, preview and batch execution. MCP exposes a bounded `rename_elements` adapter, not the full ribbon dialog. |
| Cleanup and model health | [Purge and compaction](../../../src/Services/PurgeAndCompaction), [warnings](../../../src/Services/Health/WarningsService.cs), [schemas](../../../src/Services/Schemas/SchemaCleanerService.cs) | Native purge passes, line/fill/text style compaction, schema cleanup and warnings. MCP `delete_elements` is explicit reviewed deletion; it does not mean all native purge tools are exposed to the agent. |
| Materials and visualization | [Materials](../../../src/Services/Materials), [render appearance](../../../src/Services/RenderAppearance), [view graphics](../../../src/Services/Graphics) | Material assignment, PBR textures/assets, graphics synchronization and view presentation. Some graphics helpers are explicitly shared with Copilot; richer ribbon tools remain separate. |
| Topography and slabs | [Topography](../../../src/Services/Topography) | Toposolids, conversion, boundary splitting, point cleanup, elevation offsets and slab shape resets. Copilot explicitly reuses offset and reset functionality. |
| Alignment | [Alignment](../../../src/Services/Alignment), [alignment commands](../../../src/Commands/AlignCommands.cs) | Element alignment/distribution, edge geometry, raycasting and toposolid vertex processing. Source presence is not proof of an MCP adapter. |
| Families and parameters | [Family services](../../../src/Services/FamilyConversion) | Family editing helpers, geometry conversion, parameter setup, save/load and shared-to-family parameter conversion. Production MCP changes currently reject family documents; these native helpers are not a general batch family-authoring agent. |
| CAD conversion | [CAD services](../../../src/Services/CadConversion) | DWG import, geometry extraction, flattening/tessellation, hatches, family construction, save/load and placement. |
| Model organization | [Filter copying](../../../src/Services/Infrastructure/FilterCopyService.cs), [linked model export](../../../src/Services/Infrastructure/LinkedModelExportService.cs), [capability catalog](../../../RevitCopilot/Agent/CapabilityCatalog.cs) | View filters, linked models, levels, views, sheets, schedules, rooms, worksets and phases. Check the catalog for actual agent operations. |
| UI and LECG branding | [Ribbon](../../../src/Core/Ribbon/RibbonService.cs), [views](../../../src/Views), [themes](../../../src/Resources/Themes), [icon system](../LECG-icon-system.md) | Command access, WPF views/viewmodels, shared controls, selection UX, colors, fonts and deterministic icon assets. |
| Copilot conversations | [Panel](../../../RevitCopilot/UI/DockablePanel.xaml), [project conversation store](../../../RevitCopilot/Services/ProjectConversationStore.cs), [project sessions](../RevitCopilot-Project-Conversations.md) | In-Revit chat, model/effort selection, account usage, approval UI and account/project-specific conversation persistence. |
| MCP execution | [Agent tools](../../../RevitCopilot/McpServer/AgentTools.cs), [dispatcher](../../../RevitCopilot/Revit/ExternalEventDispatcher.cs), [executor](../../../RevitCopilot/Revit/ToolExecutor.cs) | Tool discovery and transport to Revit's execution context. Actual changes use the production preview/apply contract. |
| API reference knowledge | [Reference retrieval](../../../RevitCopilot/Agent/KnowledgeLibrary.cs), [property catalog](../../../RevitCopilot/Agent/RevitApiCatalog.cs), [API exports](../15-revit-api-export.md) | Shared source references, executable property bindings, adapter mappings and bounded lookup. Generated research candidates are not loaded as runtime code. |
| Reusable workflows | [Recipe library](../../../RevitCopilot/Agent/WorkflowLibrary.cs), [batch contract](../../../RevitCopilot/Agent/AgentBatch.cs) | Curated templates and user-reviewed receipt-backed recipes; 1–12 batch steps, fresh inputs and symbolic references to earlier results. |
| Core engineering | [Bootstrapper](../../../src/Core/Bootstrapper.cs), [pure policies](../../../LECG.Core), [infrastructure](../../../src/Services/Infrastructure) | Dependency registration, selection, Revit job scheduling, transaction helpers, logging, settings, caches and Revit-independent rules. |
| Tests and evidence | [Main tests](../../../LECG.Tests), [Copilot tests](../../../RevitCopilot/Tests), [validation release](../RevitCopilot-Validation-Release.md) | Unit checks, offline contracts, scratch fixtures, MCP transport and disposable Autodesk sample campaigns, with explicit coverage gaps. |
| Delivery and decisions | [Review index](../README.md), [combined deployment](../RevitCopilot-Combined-Deployment.md), [deployment docs](../../deployment/README.md), [planning](../../../.planning), [project summary](../master-context.jsonl) | Architecture decisions, plans, build/deployment procedures, release evidence and recovery. Historical reports remain dated snapshots. |

## Knowledge is stored in different forms

- **Source implementation:** what the native plugin can potentially do. Confirm the command is wired into the ribbon or MCP and check its document requirements.
- **Registered agent operation:** a concrete callable contract in `CapabilityCatalog` or `RevitApiCatalog`. A service file alone does not establish an agent operation.
- **API reference:** source documentation that helps explain or implement an operation. Searchable material is not executable permission.
- **Curated recipe:** a reusable template, not evidence of a prior successful execution.
- **User-reviewed recipe:** successful execution receipts reviewed and saved with fresh-input placeholders.
- **Test evidence:** a dated result for particular binaries, samples and contexts. Compilation and successful serialization do not certify all engineering semantics.
- **Project conversation:** account/project-specific history. It must not supply another project's element identifiers or preview tokens.

The September 5 validation release reports 52 native operations, 29 curated templates, 3,000 references and 2,216 property bindings. The refreshable index independently inventories the current source declarations and records the embedded validation states. The report's 618 changed-value setter successes leave 187 setters without a validated changed-value edit; family-document writes remain outside the production MCP contract. These figures describe recorded evidence, not a new live test campaign.

## How to use this with an agent

Start here, choose the relevant domain, then read only the corresponding source or report. For a model operation, discover the actual current MCP contract before planning execution. For existing workflows, search recipes first and fetch only the relevant steps. For missing adapters, consult a bounded API reference and treat implementation plus testing as new work.

Archify presents the repository knowledge. It does not automatically ingest all historical chats, update a model's training, or add this documentation index to the Revit MCP runtime. The existing shared reference and recipe libraries continue to provide runtime retrieval.

## Keep the atlas current

Run from `C:\LECG\RevitAddins\LECG`:

```powershell
node docs/review/archify/refresh-inventory.mjs
dotnet build -p:SkipRevitDeploy=true
```

The refresh uses Node's built-in libraries, reads this checkout, and writes only `knowledge-index.json` through a temporary file. It makes no network or AI calls. It records input hashes and whether the inventoried source bytes match HEAD. Its extraction follows the current C# declaration syntax and fails for empty or duplicate inventories; review the extraction if declaration style changes. Counts describe source inventories, not a live ribbon enumeration or runtime test.

The HTML diagram is a separately reviewed snapshot pinned to commit `d3a105199a04aa7295b634dba829397b805cd18e`. Refreshing the inventory does not update the HTML. After a relevant architecture change, inspect the new source, update the diagram specification and revision, then validate, deliver, collect browser evidence and inspect the screenshots again.

The installed Archify entrypoint is `C:\Users\LECG Arquitectura\.codex\skills\archify\bin\archify.mjs`. With that skill available:

```powershell
node 'C:\Users\LECG Arquitectura\.codex\skills\archify\bin\archify.mjs' validate architecture docs/review/archify/lecg-knowledge.architecture.json --quality showcase --repo-root . --json
node 'C:\Users\LECG Arquitectura\.codex\skills\archify\bin\archify.mjs' deliver architecture docs/review/archify/lecg-knowledge.architecture.json docs/review/archify/lecg-knowledge.html --quality showcase --repo-root . --json
node 'C:\Users\LECG Arquitectura\.codex\skills\archify\bin\archify.mjs' visual-check docs/review/archify/lecg-knowledge.html --json
```

Main LECG targets `net8.0-windows`; Copilot targets `net10.0-windows` and its MCP server targets `net10.0`. This atlas is documentation and adds no assembly, deployment step or model inference to Revit.

## Verification artifacts

- [Diagram specification](lecg-knowledge.architecture.json)
- [Delivery receipt](lecg-knowledge.delivery.json): exact specification and HTML hashes, nine artifact checks and revision-bound source verification
- [Browser receipt](lecg-knowledge.visual-check.json): viewport measurements and screenshot paths
- [Visual evidence](lecg-knowledge.visual-check.html): light/dark contact sheet
- [Review record](verification.json): perceptual review scope and build result

The self-contained HTML was generated using the installed MIT-licensed Archify skill. Its embedded notices are retained and its [MIT license](LICENSE-ARCHIFY.txt) accompanies the artifact; Archify's full repository is not vendored into LECG.
