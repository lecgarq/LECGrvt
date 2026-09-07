# Combined UI and MCP deployment — September 5, 2026

## Installed

The completed main LECG UI refactor and the first three reviewed native MCP operations are installed. The UI changes were preserved; this integration did not rewrite XAML, themes or view code.

- Main LECG (`net8.0-windows`): `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG\LECG.dll`. Its existing machine-wide manifest was preserved. No duplicate per-user LECG registration was created.
- Copilot (`net10.0-windows`): `C:\Users\LECG Arquitectura\AppData\Roaming\Autodesk\Revit\Addins\2026\RevitCopilot\RevitCopilot.dll`.
- Bundled MCP (`net10.0`): adjacent `mcp` folder. The existing compatibility copy at `C:\Users\LECG Arquitectura\AppData\Local\RevitCopilot\mcp` was updated as well.
- Target: Revit 2026 only. Added MCP operations: `host_inserts`, `element_phase_status`, `host_bottom_faces`; 39 handwritten operations total, plus the existing 2,216 property bindings. Remaining harvested candidates were not automatically promoted.

## Verification

- Release builds succeeded. The main solution emitted existing obsolete-API/nullability warnings; the Copilot/test build had zero warnings and errors. No unrelated warnings were fixed in this integration.
- Main LECG tests: 270 passed, 5 skipped, zero failed. Evidence: `docs\review\test-results\combined-ui-backend.trx`.
- Fresh Revit scratch test: 59 passed, zero failed, live AI inference disabled. Evidence: `RevitCopilot\Tests\bin\x64\Release\net10.0-windows\results\20260905_165747\smoke-results.json`.
- After installation, the installed MCP bundle passed initialization, 16-tool listing and actual API discovery.
- All 105 deployed files were checked against their source SHA256 hashes; zero mismatches.
- Full visual inspection of every redesigned window and live-model user acceptance are not claimed. The scratch tests validate the MCP operations and integration, not every UI interaction or arbitrary project condition.

## Recovery and connector restart

Successful-install backup: `C:\LECG\RevitAddins\LECG\outputs\deployment-backups\combined-20260905-110436-763e3b9f`.

`file-map.json` maps each deployed file to its previous contents; `receipt.json` records installation success. Preserve this directory for rollback. Restore only mapped files with Revit and affected connectors closed; do not replace whole unrelated add-in directories.

One earlier attempt encountered a locked MCP dependency. Its deployed changes were restored and verified to match the earlier backup before retrying. Seven verified background processes running the compatibility MCP server were stopped to release locks; no Revit instance, user project or unrelated application was stopped. An AI client holding an old connector may need reconnecting or a fresh chat.

Installer: `C:\LECG\RevitAddins\LECG\tools\InstallCombinedRevit2026.ps1`. It uses existing tested Release outputs, checks registered paths, rejects reparse-point destinations, preflights locks, backs up overwritten files, verifies installed hashes and attempts file-level rollback on failure. It does not build or delete unrelated old dependencies.

## Use

Reopen Revit. The main LECG tools use the updated UI; Copilot's existing MCP interface exposes the new read operations. A safe first request is: “Find the native MCP tool for openings in a wall, then list the openings in my selected wall. Do not modify anything.” The AI still needs current-document selection/UniqueIds. Writes continue to require preview and local confirmation.
