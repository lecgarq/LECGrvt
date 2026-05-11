---
phase: 06-cross-cutting-foundation
captured: 2026-05-10
status: blocked
revit_version: 2026
reason: no-runtime-access
---

# Phase 6 — DialogId Discovery (BLOCKED — fallback to research guesses)

## Blocker

DialogId discovery requires a live Revit 2026 session loaded with the LECG addin, plus a representative dirty model (>50 elements, with unused styles and unloaded families). Neither was available during phase execution.

**Consequence for Wave 3 (plan 06-03 — DialogWhitelist):** Wave 3 MUST fall back to the LOW-confidence DialogId guesses recorded in `.planning/phases/06-cross-cutting-foundation/06-RESEARCH.md` §"Proposed whitelist entries". Every whitelist entry derived from those guesses MUST be explicitly annotated in code and tests as:

```csharp
// confidence: low — unverified; replace after a real discovery pass populates 06-DIALOG-DISCOVERY.md
```

Until a real discovery pass rewrites this file with runtime-confirmed DialogId strings, all whitelist entries are provisional and may fail silently in production (wrong DialogId → whitelist miss → dialog reaches user unexpectedly, or wrong override result applied to an unintended dialog).

**This blocker does NOT block Waves 1 or 2.** The ILogger scope contract (Wave 1) and the Logger.Instance migration sweep (Wave 2) are independent of dialog discovery.

---

## How to Unblock

Run the following 8-step procedure once a Revit 2026 session and a representative model are available. The temporary diagnostic patch must NOT be committed.

**Step 1 — Patch `PurgeCommand.OnDialogShowing` (do NOT commit):**

In `src/Commands/PurgeCommand.cs`, replace the body of `OnDialogShowing` with:

```csharp
string dialogId = e.DialogId ?? "(null)";
string message = e is TaskDialogShowingEventArgs td ? td.Message : "(non-task)";
Logger.Instance.LogWarning($"[DISCOVERY-PURGE] DialogId='{dialogId}' Message='{message.Replace("\n", " | ")}'");
// intentionally NOT calling e.OverrideResult — dialog reaches user
```

**Step 2 — Patch `ConvertFamilyCommand.OnDialogShowing` (do NOT commit):**

In `src/Commands/ConvertFamilyCommand.cs`, replace the entire body of `OnDialogShowing` with:

```csharp
string dialogId = e.DialogId ?? "(null)";
string message = e is TaskDialogShowingEventArgs td ? td.Message : "(non-task)";
Logger.Instance.LogWarning($"[DISCOVERY-CONVERT] DialogId='{dialogId}' Message='{message.Replace("\n", " | ")}'");
// intentionally NOT calling e.OverrideResult — dialog reaches user
```

**Step 3 — Build and load:**

```
dotnet build -c Debug
```

Start Revit 2026, load the addin from the build output directory.

**Step 4 — Run Purge (Deep mode):**

Open a representative dirty model. Run Purge Unused in Deep mode. For each dialog that appears, read the body text, then dismiss it manually. Repeat until the purge completes or you have captured at least 5 distinct DialogIds.

**Step 5 — Run Convert Family:**

Run Convert Family against a family that already exists in the project (to trigger the "already exists / overwrite" path). Capture all dialogs.

**Step 6 — Copy log output into this file:**

Copy every `[DISCOVERY-PURGE]` and `[DISCOVERY-CONVERT]` line from LogView (or the LECG log file) and fill in the placeholder tables below, replacing the "(no captures)" rows.

**Step 7 — Revert the diagnostic patch:**

```
git checkout -- src/Commands/PurgeCommand.cs src/Commands/ConvertFamilyCommand.cs
```

Verify with `git status` that only this markdown file is modified (or staged).

**Step 8 — Commit the updated discovery file:**

```
git add .planning/phases/06-cross-cutting-foundation/06-DIALOG-DISCOVERY.md
git commit -m "docs(phase-06): capture DialogId enumeration from runtime discovery"
```

Then update the frontmatter: change `status: blocked` to `status: captured`, add `model: <model name>`, and remove the `reason:` field.

---

## Purge (Deep mode)

| DialogId | Message (truncated) | Proposed OverrideResult | Rationale |
|----------|---------------------|-------------------------|-----------|
| (no captures — see Blocker) | | | |

---

## Convert Family

| DialogId | Message (truncated) | Proposed OverrideResult | Rationale |
|----------|---------------------|-------------------------|-----------|
| (no captures — see Blocker) | | | |

---

## LOW-Confidence Research Fallback

The following entries are sourced from `06-RESEARCH.md §"Proposed whitelist entries"` and are used by Wave 3 as a provisional whitelist until this file is replaced with runtime-confirmed data. All are marked LOW confidence.

| DialogId | Proposed OverrideResult | Rationale | Confidence |
|----------|------------------------|-----------|------------|
| `TaskDialog_ExtrusionTooThin` | 2 (cancel) | Cited in PurgeCommand comment as the specific dialog motivating cancel-all | LOW — inferred from comment, not observed |
| `TaskDialog_BaseSketchInvalid` | 2 (cancel) | Also cited in PurgeCommand comment | LOW |
| `TaskDialog_LoadFamily` | 1 (accept) | LoadFamily during deep purge triggers a confirm dialog when family already loaded | LOW |
| `TaskDialog_Overwrite` | 1 (accept) | ConvertFamily "already exists / will be replaced" case | LOW |
| `TaskDialog_DuplicateFamily` | 1 (accept) | ConvertFamily duplicate check | LOW |

**Critical caveat (from RESEARCH.md):** Revit's `DialogId` values are not documented by Autodesk. These strings are plausible conventions but are NOT confirmed. The actual values emitted at runtime may differ (e.g., `TaskDialog_ExtrusionTooThin` may actually be `Revit_TaskDialog_Extrusion_Is_Too_Thin` or a GUID-like ID). Wave 3 MUST mark each whitelist entry with the confidence annotation shown in the Blocker section above.
