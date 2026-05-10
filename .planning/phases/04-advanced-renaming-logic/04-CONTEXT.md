# Phase 4: Advanced Renaming Logic — Context

**Gathered:** 2026-05-10
**Status:** Ready for planning
**Source:** /gsd:discuss-phase 4 — interactive session with user
**Requirements:** REQ-02 (Safe Rename for formula-referenced), REQ-03 (Safe Rename for dimension-label), REQ-04 (Reason for Skip in UI/Logs)

<domain>
## Phase Boundary

Replace the current "skip-with-reason" behavior for stubborn FamilyParameters (formula-referenced, dimension-label, element-associated) with **atomic Safe Rename** that mutates the parameter name AND updates every dependent reference (formulas via `FormulaNameUpdater.UpdateFormula`, dimension labels via `Dimension.FamilyLabel` reassignment) in the same family edit. AND surface skip reasons in the Batch Rename grid (today: log-only for FamilyParameter; nothing for standard items).

**In scope (REQ-02/03/04):**
- Wire up the existing-but-orphaned `IFormulaUpdateService` for REQ-02
- Add a dimension-label update path for REQ-03 (no service yet)
- Convert standard-item rename failures from "throw at execution" to "pre-flight skip-with-reason" for the four detected conditions
- Surface skip reasons in the existing `Status` column on `ElementRowViewModel`
- Disable + mute checkbox on skipped rows
- Mirror skip reasons into LogView

**Out of scope (deferred to v1.2 Phase 6 — Batch Rename Maturity):**
- UI overhaul (shift-click multi-select, Excel-style per-column filters, modern visual styling)
- Adding new scopes (Schedules, Tags, Title Blocks, etc.)
- Async `CollectBaseElements`
- Single-select pill UX for the exclusive scope toggle
- Collapsing the `SearchReplaceService` pass-through facade
- Fixing `count`-on-rollback bug at `BatchRenameExecutionService.cs:185`

</domain>

<decisions>
## Implementation Decisions

### Safe Rename Trigger
- **Always-on, no toggle.** Renaming a formula-referenced or dimension-label parameter performs rename + reference update by default. The current "skip with reason" path goes away for these three categories.
- No "Safe Rename" checkbox in the dialog — keeps the UX surface unchanged this phase.

### Safe Rename Scope (which categories convert from skip → rename+update)
- ✅ **Formula-referenced (REQ-02)** — rename param, update every formula in the family that mentions the old name via `FormulaNameUpdater.UpdateFormula`.
- ✅ **Dimension-label (REQ-03)** — rename param, then for every `Dimension` whose `FamilyLabel.Definition.Name` matched the old name, reassign `Dimension.FamilyLabel = newParam`.
- ✅ **Element-associated (geometry/material/visibility/nesting)** — flip from skip to allowed. Revit re-resolves these by ParameterId, so rename is safe in practice; no explicit reference update needed.

### Categories that REMAIN skipped (with reason surfaced)
- ❌ **Built-in** (`fp.Id.Value < 0`) — physically un-renameable.
- ❌ **Reporting parameters** (`fp.IsReporting`) — driven, not driving; renaming risks breaking the dimension→param wiring with no public API to repair it.
- ❌ **Name conflict** — when `newName` collides with an existing parameter in the same family. Skip with `"new name '{newName}' conflicts with existing parameter"`. No auto-suffixing — predictable behavior beats surprise names.

### Standard-Item Skip Detection (REQ-04 — net-new)
Today, standard items either succeed or throw at execution. Phase 4 adds a **pre-flight check** that produces structured skip reasons for these four conditions:
1. **Read-only types / built-in** — element types whose `Name` is read-only (e.g., system Wall types).
2. **Name conflict** — same-named element exists in the same scope (Types/Families/Materials/etc.).
3. **Sheet number locked / restricted format** — Sheets scope only; numbering schemes that reject the new value. Detected via the dry-run pass.
4. **System families** — `Family.IsSystemFamily == true` core types (Wall/Floor/Roof). Subset of read-only with stricter rules.

Detection happens during the dry-run pass (see Failure Policy → Two-Pass Execution).

### Failure Policy (when reference update fails mid-family)
- **Per-parameter SubTransaction.** Each "rename param + update its formulas + reassign its dimension labels" runs inside a SubTransaction inside the family's `EditFamily` cycle. If any sub-step throws, that ONE param's SubTransaction rolls back; other params in the same family still commit.
- **Two-pass execution:**
  1. **Pass 1 — Dry-run (validate):** For each checked row, simulate the rename: check name conflicts (including conflicts created by other rows being renamed in the same batch), test formula-update producibility (`FormulaNameUpdater.UpdateFormula` returns a string — verify Revit will accept it via a probe SubTransaction or formula-syntax check), test that each `Dimension.FamilyLabel` candidate reassignment is reachable. Anything that fails validation is flipped to skip-with-reason BEFORE commit pass.
  2. **Pass 2 — Commit:** Execute only rows that survived dry-run. Per-param SubTransaction is the runtime safety net for anything dry-run missed.
- **Non-sticky skip.** A row that was skipped this Apply round is NOT marked sticky-skipped. If the user fixes the cause (e.g., renames the conflicting param first) and hits Apply again, the row is re-evaluated fresh.
- **Per-family transaction grain unchanged.** All params in one family still go through one `EditFamily`/`LoadFamily` cycle — matches existing architecture; per-param SubTransactions live INSIDE that cycle.

### Skip-Reason Surfacing in UI (REQ-04)
- **Status column reuse.** `ElementRowViewModel` already carries a `Status` field (Phase 03). Skip reason renders in `Status` for skipped rows; for safe-rename rows, `Status` shows the side-effect count (see below). No new columns; preserves the shared `ElementGridControl` shape (`Sel | Type | Category | Name | Status` — Batch Rename grid keeps its `Sel | Type | Category | Original | New | Status` shape).
- **Tooltip.** Hovering a skipped row's Status shows the full reason text (in case it's truncated).
- **Disabled checkbox + muted row.** Skipped rows: checkbox `IsEnabled = false`, row text rendered with reduced opacity / muted gray. The user cannot tick a skipped row, eliminating "Apply does nothing" confusion.
- **LogView mirror.** One log line per skipped row: `Skipped '{originalName}' ({type}/{family}): {reason}`. Severity = `LogWarning`. Consistent across FamilyParameter and standard items (today only FamilyParameter logs skips).

### Side-Effect Preview (count of references that will be touched)
- **Inline in Status column.** For safe-rename rows, `Status` reads (e.g.) `"+2 formulas, +1 dimension"`. Computed during the dry-run pass — already running, no extra work.
- **No confirmation modal.** Apply triggers immediately; trust the dry-run + per-param rollback. Adding a confirm dialog crosses into UI-overhaul territory (deferred to v1.2 Phase 6).
- **Recompute on every preview.** Dry-run is part of the preview pipeline behind the existing 150ms debounce. Always fresh. No caching, no invalidation bugs.

### Logging Conventions
- Skip reasons: `LogWarning` severity, format `Skipped '{name}' ({context}): {reason}`.
- Safe Rename success: extend the existing `Renamed '{old}' to '{new}'` line with side-effect count when applicable, e.g. `Renamed '{old}' to '{new}' (updated 2 formulas, 1 dimension label)`.
- All logs continue to flow through `LogView` only — no new log surface (Phase 02.5 decision held).
- Locale-safe Revit API access throughout (Phase 02.5 decision held — prefer BuiltInParameter / LabelUtils over English-string comparisons).

### Claude's Discretion
- Exact name of the new dimension-label update service (e.g., `IDimensionLabelUpdateService` vs. inline helper inside `BatchRenameExecutionService`).
- Whether to extract a `RenameSkipDetector` strategy or keep the detection inline alongside `GetRenameSkipReason` (which currently lives in `BatchRenameExecutionService`).
- Wave breakdown of plans (suggested: Wave 0 test scaffolds → Wave 1 REQ-04 UI/log surfacing + standard-item skip detection → Wave 2 REQ-02 formula safe rename → Wave 3 REQ-03 dimension safe rename → Wave 4 phase-end manual Revit verify). Planner to finalize.
- How the dry-run pass surfaces "this row will fail at commit" without actually mutating Revit state — likely a probe SubTransaction inside the family edit, but the planner can reconsider.
- Whether the dry-run pass for standard items happens INSIDE the main rename transaction or in a pre-flight read-only walk. Recommendation: pre-flight read-only — keeps the main transaction small and predictable.

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`IFormulaUpdateService` / `FormulaUpdateService`** (`src/Services/Renaming/FormulaUpdateService.cs`) — already implemented, registered in `Bootstrapper.cs:131`, with ZERO consumers. Phase 4 wires it into `BatchRenameExecutionService` for REQ-02. The implementation just delegates to `FormulaNameUpdater.UpdateFormula(formula, oldName, newName)` from `LECG.Core.Rename.FormulaNameUpdater`.
- **`FormulaNameUpdater.UpdateFormula`** (`LECG.Core/Rename/FormulaNameUpdater.cs`) — pure-string formula rewriting; already covered by `LECG.Tests/Services/FormulaNameUpdaterTests.cs`. Reuse as-is.
- **`FormulaNameUpdater.ContainsReference`** — already used in `BatchRenameExecutionService.BuildFormulaReferencedNames` (line 426) and `FormulaAutoGroupingCommand.cs:687`. Phase 4 leverages this same helper to enumerate which formulas need updating per parameter.
- **`GetRenameSkipReason`** (`src/Services/Renaming/BatchRenameExecutionService.cs:~334-375`) — existing skip-reason returner for FamilyParameter. Phase 4 narrows it (formula-referenced, dimension-label, element-associated cease being skip reasons) and a parallel `GetStandardItemSkipReason` is added for the four standard-item conditions.
- **`BuildDimensionLabelNames`** (`BatchRenameExecutionService.cs:~378-405`) — already builds the dimension-label name set for skip detection. Phase 4 extends usage: it now also drives the dimension reassignment loop (collect dimensions whose label matches each rename, then reassign after rename).
- **`ElementRowViewModel.Status`** (`src/ViewModels/Components/ElementRowViewModel.cs:25`) — exists, plain settable string. Carrier for skip reasons + side-effect counts.
- **`ElementGridControl`** (Phase 03 deliverable) — Batch Rename grid keeps its inline `LecgDataGrid` (Phase 03-05 decision); the Status column is what's exposed for surfacing.
- **SubTransaction pattern** — proven in `FormulaAutoGroupingCommand.MoveParamsToGroup` (Phase 02). Phase 4 reuses the same per-parameter SubTransaction pattern inside `EditFamily`.

### Established Patterns
- CommunityToolkit MVVM `ObservableObject` / `ObservableProperty` (used by ElementRowViewModel).
- Per-parameter SubTransaction with skip-and-continue (Phase 02 decision: "one failed move logs and continues").
- LogView-only logging surface (Phase 02.5 decision).
- Locale-safe Revit API access (Phase 02.5 decision).
- Skip-gated RED tests with reason strings naming the implementing plan (Phase 03-00 test scaffolding pattern).
- `dotnet build LECG.Tests/LECG.Tests.csproj -p:SkipRevitDeploy=true` while Revit is open (Phase 03-00 build convention).

### Integration Points
- **`BatchRenameExecutionService.RunFamilyRenameLoop`** (~line 200-280) — the per-family `EditFamily`/`LoadFamily` orchestrator. Phase 4 inserts the per-param SubTransaction wrapper here.
- **`BatchRenameExecutionService.GetRenameSkipReason`** — narrowed to keep only the three remaining skip categories (built-in, reporting, name-conflict).
- **`SearchReplacePreviewService.ProcessPreview`** (`src/Services/Renaming/SearchReplacePreviewService.cs:110-118`) — populates `ElementRowViewModel.Status` today. Phase 4 extends to populate skip reasons + side-effect counts during the dry-run pass.
- **`SearchReplaceView.xaml`** — Status column already bound (Phase 03-05). Add `IsEnabled="{Binding IsRenameable}"` (or equivalent) to the checkbox column for the disabled-on-skip behavior. Add a row Style trigger for muted color.
- **`LECG.Tests/Services/`** — REQ-05 test coverage of preview/pipeline services lives here. Phase 4 adds `FormulaUpdateServiceTests`, `BatchRenameSafeRenameTests` (or similar), `RenameSkipDetectorTests`.

### Notable Risks / Pitfalls (carried forward from research)
- **`count` increments inside conditional transaction lambda even on rollback** (`BatchRenameExecutionService.cs:185`) — pre-existing bug; Phase 4 should not worsen it but is explicitly NOT fixing it (deferred to v1.2 Phase 6). Document in summary.
- **`ElementData` → `ReplaceItem` history**: Phase 03-04 deleted `ReplaceItem`; current row model is `ElementRowViewModel`. Any plan referencing "add Category to ReplaceItem" needs to read CURRENT `ElementRowViewModel` (no `ReplaceItem` in this codebase anymore).
- **Per-family transaction containing EditFamily/LoadFamily on UI thread** — already the existing model; Phase 4 doesn't change that.
- **Two-pass dry-run + commit can be slow** on large batches — accepted. Behind the 150ms debounce, recompute-every-preview is responsive enough.

</code_context>

<specifics>
## Specific Ideas

- "Always-on Safe Rename" matches the user's mental model: when they ask the tool to rename a parameter, it should rename the parameter — not silently skip because of a formula reference. The tool's job is to fix the references.
- Status column already does double duty in Phase 03 (e.g., DivideToposolid: "Ready to divide (N layers)"). Reusing it for skip reasons + side-effect counts is consistent.
- Element-associated parameters were previously skipped out of caution; user agrees that since Revit re-resolves by ParameterId, rename is safe. Phase 4 flips this from skip to allowed.
- No specific UX reference — user trusts Claude's judgment on Status text format, muted-row exact color, and tooltip layout.

</specifics>

<deferred>
## Deferred Ideas

All items below belong to **v1.2 Phase 6: Batch Rename Maturity** (already queued in ROADMAP.md). Captured here so they're explicit, not lost.

### From this discussion (user's UI vision)
- **Shift-click multi-select on preview rows** + then click the header checkbox to bulk check/uncheck the selected range.
- **Excel-style per-column filter** on Type / Category / Original / New / Id columns — header funnel popup with searchable checklist of distinct values. (Functional `SetColumnFilter(name, predicate)` plumbing already shipped in Phase 03-05; the visual chrome is what's missing.)
- **Modernize the preview UI look** — current grid feels dated. v1.2 Phase 6 should refresh the visual style.
- **Add more scopes to Batch Rename** — user mentioned Schedules, Tags, Title Blocks etc. would be nice. Each is a one-branch addition in `BaseElementCollectionService` after the v1.2 collector decomposition.

### Pre-existing v1.2 Phase 6 backlog (from research/SYNTHESIS)
- Collapse `SearchReplaceService` pass-through facade leaking ViewModel types
- Fix `count` increment on transaction rollback (`BatchRenameExecutionService.cs:185`)
- Fix standard-item progress capping below 100%
- Fix `SwapStyle` orphaning non-`CurveElement` consumers of GraphicsStyle
- Async `CollectBaseElements` (currently freezes UI on Parameters scope with large models)
- Scope UX (single-select pills replace the misleading multi-select-rendered exclusive scope)
- Dedup `ParamGroup` swallowed try/catch (`BaseElementCollectionService.cs:213` + `:275`)
- Retire dead `IFormulaUpdateService` registration — **NOTE: Phase 4 RESCUES this; the v1.2 backlog item is now obsolete and should be removed when v1.2 Phase 6 starts**
- Per-column header funnel chrome (visual UI deferred from Plan 03-05) — same item as "Excel-style per-column filter" above

### Out of any milestone (no firm home)
- "Show only changed" toggle in preview
- Conflict preview when two rule outputs collapse to the same NewValue
- Keyboard shortcut hints / Enter-to-Apply in the dialog
- Persistent column widths across sessions

</deferred>

---

*Phase: 04-advanced-renaming-logic*
*Context gathered: 2026-05-10 via /gsd:discuss-phase*
