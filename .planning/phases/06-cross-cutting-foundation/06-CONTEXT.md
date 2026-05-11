---
phase: 6
slug: cross-cutting-foundation
milestone: v2.0
status: discussed
created: 2026-05-10
authored_by: gsd:discuss-phase
---

# Phase 6 — Context

**Goal (from ROADMAP):** Establish the unified logging, progress, and dialog-suppression substrate every later v2.0 phase consumes — and close the one v1.1 documentation gap (GAPS-01) that lives in the same context.

**Requirements:** CROSS-01, CROSS-02, CROSS-03, GAPS-01.

This file captures decisions extracted from the /gsd:discuss-phase 6 session so the researcher and planner can act without re-asking the user.

---

## 1. Logger unification (CROSS-01)

### 1.1 Migration shape — DECIDED
- **Pure DI.** `Logger.Instance` singleton is physically removed by the end of this phase.
- Every call site receives `ILogger` via constructor injection.
- No `[Obsolete]` facade transitional period — singleton goes away in this phase.

### 1.2 Relationship to `IProgressReporter` — DECIDED
- `IProgressReporter` **stays as an interface** but becomes a thin, severity-aware adapter over `ILogger`.
- Every `IProgressReporter` impl takes `ILogger` via constructor and forwards `LogWarning`/`LogError` while **preserving severity** (this is the core CROSS-02 fix).
- The interface remains because long-running services consume it for the `Report(message, percentage)` channel; logging methods on it are forwarding sugar, not a parallel sink.

### 1.3 Scope tag — DECIDED
- **Per-call scope parameter.** Each `ILogger` method takes an explicit `scope` argument (e.g. `logger.LogWarning(message, scope: "Purge")`).
- No constructor-bound scope (`ForScope`) and no `ILogger<T>` generic-category style. Verbose but no hidden state.
- The existing `Microsoft.Extensions.Logging` forwarding in `Logger.cs` continues to use the scope as the category name.

### 1.4 Migration breadth — DECIDED (after reconciliation)
- **Migrate every existing `Logger.Instance` call site in this phase.** 30+ files.
- Hot paths first (Purge, Convert Family, Compact Styles, Category Changer, Batch Rename) then sweep the rest.
- Phase exits with **zero remaining references** to `Logger.Instance` in `src/`.

### 1.5 Out of scope for this phase
- `Logger.cs` structured-sink wiring (`MsLoggerFactory`) is already in place — keep as-is.
- Dispatcher / threading model (currently in `Logger.cs`) is preserved verbatim under the new contract.
- Tag taxonomy cleanup (`[V6-EVENT]`/`[OK]` etc) — deferred per REQUIREMENTS.md "Future Requirements".

---

## 2. Severity preservation in `IProgressReporter` (CROSS-02)

### 2.1 Pipe fix — DECIDED
- Root cause confirmed in scout: `SimpleProgressReporter`, `LegacyProgressReporter`, `RevitCommandProgressReporter` all collapse `LogWarning`/`LogError` to a plain message (no `LogLevel` carried).
- Replacement: every impl forwards to `ILogger.LogWarning` / `ILogger.LogError` so a `LogEntry` with the correct `LogLevel` reaches `LogView`.

### 2.2 Sink wiring — DECIDED
- `IProgressReporter` impls take `ILogger` via constructor. Single sink. No fallback to `Logger.Instance` (it won't exist).

### 2.3 `Report(message, percentage)` channel — DECIDED
- **Stays separate from `LogView`.** Progress remains a UI affordance (progress bar / status text) and routes through `ILogger.UpdateProgress` / `OnProgressUpdate` (already wired). Not logged as an `Info` entry.

### 2.4 LogView treatment — DECIDED
- **Pipe fix + visual polish on existing colors.**
- LogView already renders severity (`LogView.xaml` lines 55-64: success `#22543D`, warning `#744210`, error `#822727` driven by `LogEntry.Level`). No new controls in this phase.
- Visual polish allowed: tighten the existing palette / weak contrast and/or add a small severity indicator if today's color cue is hard to scan. **Bounded:** no chips/badges that change row chrome substantially; UI-02 (severity filter, search, copy, collapsible groups) remains a Phase 13 task.

### 2.5 Audit scope — DECIDED
- **Grep-and-fix sweep** of every `IProgressReporter.LogWarning` / `LogError` call site, plus every `Logger.Instance.LogWarning` / `LogError` site, to confirm each produces a `Warning`/`Error` `LogEntry` end-to-end.
- A test (or tests) MUST cover: an `IProgressReporter.LogWarning` call results in a `LogEntry` with `LogLevel.Warning` reaching `Logger.Entries`. Same for `LogError`.

---

## 3. Dialog whitelist (CROSS-03)

### 3.1 Whitelist location — DECIDED
- **Hardcoded static list in code** (no JSON config, not per-command).
- Concretely: a single `DialogWhitelist` class (location TBD by planner; suggested: `src/Core/` near `SafeFailureHandler.cs`) holding an `IReadOnlyDictionary<string, int>` keyed by `DialogId`, value = `OverrideResult` to apply.
- Reviewed/extended via code change. No runtime configuration.

### 3.2 Match key — DECIDED
- **`DialogId` only, exact string match.**
- No message-substring fallback. If a `TaskDialog` shares a `DialogId` across distinct user-facing variants, the planner must split it into two `DialogId`s upstream or leave it off the whitelist (so it reaches the user).

### 3.3 Per-entry decision — RESOLVED IN CONTEXT
- The user picked "DialogId only" for the match key (not "DialogId + decision per entry"), but a decision is still required per entry because Purge currently cancels (`OverrideResult(2)`) and Convert Family sometimes accepts (`OverrideResult(1)`).
- **Resolution:** the whitelist entry stores `(DialogId, OverrideResult)`. Matching uses `DialogId` only; the override result is part of the entry payload, not the match key. Researcher/planner should treat this as decided.

### 3.4 Default for unknown / unmatched dialogs — DECIDED
- **Reach the user.** No `OverrideResult` call. Strictest reading of CROSS-03 success criterion 3.
- This explicitly **changes** the current Purge behavior (which cancels every dialog). Migration risk noted below.

### 3.5 Logging policy — DECIDED
- **Every whitelist hit → `Info` log** (`"Auto-handled whitelisted dialog '<DialogId>' with result <n>"` or similar).
- **Every reach-user dialog → `Warning` log** before the dialog appears (best-effort; the event is fired synchronously so the log is queued before the user sees the modal).
- The "one-time whitelist loaded at command start" Info entry is **not** part of this decision (user picked option without it).

### 3.6 Regression risk to flag
- Current Purge cancels **all** dialogs (`PurgeCommand.cs:38-41`). After CROSS-03 lands, any dialog whose `DialogId` is not yet whitelisted will reach the user — potentially including dialogs that were silently auto-cancelled in v1.1.
- **Planner action:** before exit, exercise Purge against a representative dirty model and enumerate every `DialogId` Revit emits. Each must be a deliberate whitelist add OR a deliberate "let user handle" decision. Document the enumeration in the phase summary.

---

## 4. GAPS-01 — Retroactive `02-VERIFICATION.md`

### 4.1 Content — DECIDED
- **Compile existing evidence only.** No new xUnit runs, no new manual Revit checks.
- Source material to pull from:
  - `.planning/milestones/v1.1-phases/02-formula-auto-grouping-bug-fix/02-SUMMARY.md`
  - `.planning/milestones/v1.1-phases/02-formula-auto-grouping-bug-fix/02-VALIDATION.md`
  - `.planning/milestones/v1.1-phases/02-formula-auto-grouping-bug-fix/02-UAT.md`
  - Existing `dotnet test LECG.Tests --filter "FormulaAutoGroup"` results (status: green per v1.1 sign-off).

### 4.2 Template — DECIDED
- **Mirror v1.1-era sibling shape.** Use `.planning/milestones/v1.1-phases/03-grid-collection-fixes/03-VERIFICATION.md` as the structural reference. Match section order/headings so the v1.1 archive is internally consistent.

### 4.3 REQ-07 evidence scope — DECIDED
- **xUnit-only evidence is sufficient.** REQ-07 (FormulaAutoGrouping) is fully testable at the unit level; live Revit re-observation is **out of scope** for GAPS-01.
- Live Revit re-check belongs to GAPS-02 (deferred manual checks) and is **not** bundled here.

### 4.4 Location — DECIDED
- `.planning/milestones/v1.1-phases/02-formula-auto-grouping-bug-fix/02-VERIFICATION.md`. Sits next to its sibling artifacts in the v1.1 archive. Discoverable to future readers without indirection.

---

## 5. Code context (scout findings)

| Asset | Path | Relevance |
|------|------|-----------|
| `ILogger` + `Logger` singleton | `src/Services/Infrastructure/Logging/Logger.cs` | Extend `ILogger` contract (severity + scope), delete singleton. Existing MS.Extensions forwarding (`ConfigureStructuredLogger`) and dispatcher logic stay. |
| `IProgressReporter` interface | `src/Services/Infrastructure/IProgressReporter.cs` | Interface stays. `SimpleProgressReporter` collapses severity (lines 45-53) — fix here. |
| `LegacyProgressReporter` | `src/Services/Infrastructure/LegacyProgressReporter.cs` | Same severity-collapse bug (lines 27-35). |
| `RevitCommandProgressReporter` | `src/Services/Infrastructure/RevitCommandProgressReporter.cs` | Same severity-collapse bug (lines 27-35). |
| `LogView` (already severity-aware) | `src/Views/LogView.xaml` lines 55-64 + `LogView.xaml.cs` | View renders by `LogEntry.Level` already. Pipe fix is sufficient for CROSS-02. |
| Purge dialog handler | `src/Commands/PurgeCommand.cs:36-42, 63, 91` | Currently cancel-all. Migrate to whitelist + reach-user default. |
| Convert Family dialog handler | `src/Commands/ConvertFamilyCommand.cs:25-66` | Substring-match heuristic. Migrate to whitelist by `DialogId`. |
| Logger.Instance call sites | 30+ files (grep `Logger\.Instance`) | All migrate to constructor-injected `ILogger`. |
| v1.1 phase 02 sibling artifacts | `.planning/milestones/v1.1-phases/02-formula-auto-grouping-bug-fix/` | Source material for retroactive 02-VERIFICATION.md. |
| Sibling VERIFICATION template | `.planning/milestones/v1.1-phases/03-grid-collection-fixes/03-VERIFICATION.md` | Structural reference for GAPS-01. |

---

## 6. Decisions explicitly NOT made here (planner: do not block)

- File locations for new types (e.g. where `DialogWhitelist` lives) — planner's call.
- Test-class names and per-test breakdown — planner's call.
- Whether to split CROSS-01 into multiple plans (e.g. contract first, migration sweep second) — planner's call.
- Specific `DialogId` values that go on the whitelist — researcher must enumerate from current Purge + Convert Family runs and propose. CONTEXT only fixes the **policy**, not the contents.

---

## 7. Scope guardrail

The following came up adjacent to the phase and are **deferred** to other phases / future work:

- **UI-02** (severity filter, text search, copy-to-clipboard, collapsible scope groups in LogView) — Phase 13, not here. Visual polish in 2.4 is bounded.
- **Tag taxonomy unification** (`[V6-EVENT]`, `[OK]`, `[SUCCESS]` etc) — Future Requirements, revisit after CROSS-01 lands.
- **GAPS-02** (manual Revit checks for Spanish locale, English regression, mixed-batch refuse-all, wall-hosted Convert Family) — separate requirement, separate work item.
- **Per-command whitelist defaults** — rejected in favor of a single global whitelist with one default policy.
