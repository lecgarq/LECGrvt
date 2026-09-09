# Orientation hints — verify live

Domain: architectural/BIM automation, Revit 2026 only.
These are navigation hints, not a second index or current capability claims.

| Project | Target | Important boundary |
|---|---|---|
| LECG.csproj | net8.0-windows | Ribbon commands, services, WPF UI; DI in src/Core/Bootstrapper.cs |
| RevitCopilot/RevitCopilot.csproj | net10.0-windows | In-process Revit executor and chat add-in |
| RevitCopilot/McpServer/RevitCopilot.McpServer.csproj | net10.0 | MCP transport; links catalog files from the parent Agent folder |

Evaluate the containing project with:
`dotnet msbuild '<known project>' -getProperty:TargetFramework,DefineConstants -p:SkipRevitDeploy=true -nologo`.
This reads evaluated properties without building or deploying. Never transfer
semantic evidence between projects because they share a source path.

Known source anchors: src/Commands, src/Services, src/Core/Bootstrapper.cs,
RevitCopilot/Agent/CapabilityCatalog.cs, RevitCopilot/Agent/RevitApiCatalog.cs,
RevitCopilot/Revit/ToolExecutor.Changes.cs, RevitCopilot/McpServer/AgentTools.cs.
Get current domains and counts through query-index, not a copied directory list.

Some old CAD interfaces were removed at revision 774354b4. Do not assume the old
interface-per-service convention still applies. Resolve the requested symbol and
registration before selecting an implementation.

The inventory and rendered HTML have independent freshness. Neither a successful
build nor a refreshed inventory updates the rendered snapshot.

Scripts require Node 18+ (built-in fetch) and Git. Serena and ast-grep are optional
read-only query providers: if unavailable, disclose it and do not install anything.
The local Serena dashboard API exposes project/configuration, not Roslyn readiness.
The first script reports reachable/degraded/unavailable. Reachable confirms the
live matching workspace and configured C# LSP backend, not symbol readiness.
The first evidence lookup establishes actual resolution without a standalone
health call. Initial instructions and emitter-module loading are once-per-session setup.
