---
phase: 04-advanced-renaming-logic
verified: pending
status: in-progress
score: 0/5 must-haves verified
re_verification: false
---

# Phase 04: Advanced Renaming Logic — Verification Report

**Phase Goal:** Extend Batch Rename with formula-safe rename (REQ-02), dimension-label-safe rename (REQ-03), and skip-reason surfacing with side-effect counts in the UI/log (REQ-04). All wired across plans 04-00..04-04.

**Requirements:** REQ-02, REQ-03, REQ-04
**Verified:** pending
**Status:** in-progress
**Re-verification:** No — initial verification

---

## Pre-Flight Build

| Step | Expected | Status | Notes |
|------|----------|--------|-------|
| `dotnet build LECG.sln` (full deploy, no SkipRevitDeploy) | 0 errors / 0 warnings; DLLs copied to `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG\` | ⬜ pending | Revit must be CLOSED before this step |
| Revit launch | Plugin loads without error dialog | ⬜ pending | |
| Unit tests `dotnet test LECG.Tests/...` | 131/131 PASS, 0 skip | ✅ PASS | Confirmed 2026-05-10 before session start |

---

## Section A — REQ-02: Formula Safe Rename

**Setup:** Wall-hosted family with ≥2 parameters where param `A` is referenced in formulas:
- `Width = A * 2`
- `Visibility = A > 100`

| ID | Step | Expected | Status | Notes |
|----|------|----------|--------|-------|
| A1 | Tick row for param `A` in Batch Rename, set NewValue `A_renamed`, hit Apply | Apply executes without error | ⬜ pending | |
| A2 | Open the family in Revit's family editor | Parameter exists as `A_renamed` (not `A`) | ⬜ pending | |
| A3 | Inspect `Width` formula | Formula now reads `A_renamed * 2` | ⬜ pending | |
| A4 | Inspect `Visibility` formula | Formula now reads `A_renamed > 100` | ⬜ pending | |
| A5 | Load family back into the project | Family loads without errors; no Revit error dialogs | ⬜ pending | |
| A6 | Check LogView | Line reads `Renamed 'A' to 'A_renamed' (updated 2 formulas)` | ⬜ pending | |

---

## Section B — REQ-03: Dimension-Label Safe Rename

**Setup:** Family with parameter `L` driving two Dimension labels.

| ID | Step | Expected | Status | Notes |
|----|------|----------|--------|-------|
| B1 | Tick row for param `L`, set NewValue `L_renamed`, hit Apply | Apply executes without error | ⬜ pending | |
| B2 | Open the family in the family editor | Parameter is `L_renamed` | ⬜ pending | |
| B3 | Inspect both Dimension properties (Label dropdown) | Both Dimensions show FamilyLabel = `L_renamed` | ⬜ pending | |
| B4 | Change a type's `L_renamed` value in the Family Types dialog | Geometry updates correctly in preview | ⬜ pending | |
| B5 | Load family back into project | Family reloads; existing instances still hold their dimension values | ⬜ pending | |
| B6 | Check LogView | Line includes `(updated 0 formulas, 2 dimension labels)` or dim-only variant | ⬜ pending | |

---

## Section C — REQ-03 Edge: Dimension.FamilyLabel Setter Behavior (Open Question 1)

| ID | Step | Expected | Status | Notes |
|----|------|----------|--------|-------|
| C1 | If FamilyLabel reassignment throws a Revit exception during B-series tests: record the exception type and the rolled-back state; check whether param was renamed but dimension unaffected; check if null-clear-first was needed | If exception: rolled-back state matches expectation; note constraint. If no exception: confirm setter works directly. | ⬜ pending | If null-clear-first required, open follow-up task in STATE.md "Next Steps" — NOT a Phase 4 blocker if B-series otherwise passes |

---

## Section D — REQ-04: Skip Reason UI + Logs

**Setup:** Family with built-in param (e.g., `Type Mark`, Id < 0), one reporting param, one renameable param.

| ID | Step | Expected | Status | Notes |
|----|------|----------|--------|-------|
| D1 | Open Batch Rename on the prepared family | Grid populates with rows for all parameter types | ⬜ pending | |
| D2 | Built-in row (e.g., `Type Mark`) | Row is muted opacity; checkbox disabled; Status = "built-in parameter" (or exact string from 04-01); tooltip on hover shows full Status string | ⬜ pending | |
| D3 | Reporting param row | Row is muted; checkbox disabled; Status = "reporting parameter" | ⬜ pending | |
| D4 | Set NewValue on a renameable row to a name already in use by another param | Status flips to `name '{X}' already in use`; IsChecked auto-clears; row mutes | ⬜ pending | |
| D5 | Cross-batch collision: set rows A→C and B→C in the same batch | First row A: Status empty; second row B: Status = `name 'C' already claimed by another row in this batch`; IsChecked clears on row B; rows muted accordingly | ⬜ pending | |
| D6 | Standard-item scope: pick a Wall type (system family), set new name | Row muted; checkbox disabled; Status = system-family reason | ⬜ pending | |
| D7 | LogView after Apply with skipped rows | One LogWarning per skipped row with format `Skipped '{name}' ({context}): {reason}` | ⬜ pending | |
| D8 | Preview-time side-effect count on a formula-referenced row | Status shows `+N formulas` (or composite) BEFORE clicking Apply | ⬜ pending | |

---

## Section E — Regression

| ID | Step | Expected | Status | Notes |
|----|------|----------|--------|-------|
| E1 | Phase 02 FormulaAutoGrouping: open a family with a formula param, run FormulaAutoGrouping, confirm move to "Other" group | Succeeds; formula preserved | ⬜ pending | |
| E2 | Phase 03 Batch Rename grid: confirm column order Sel \| Type \| Category \| Original \| New \| Status; default Category-asc sort; FilterCategory dropdown; no blank rows | All Phase 03 grid behaviors intact | ⬜ pending | |
| E3 | Phase 02.5 ConvertFamily: run on a wall-hosted family | Host preservation still works; no regression | ⬜ pending | |

---

## Verification Sign-Off

| Section | Items | PASS | FAIL | N-A |
|---------|-------|------|------|-----|
| A — REQ-02 Formula Safe Rename | 6 | 0 | 0 | 0 |
| B — REQ-03 Dimension-Label Safe Rename | 6 | 0 | 0 | 0 |
| C — REQ-03 Edge (Open Question 1) | 1 | 0 | 0 | 0 |
| D — REQ-04 Skip Reason UI/Logs | 8 | 0 | 0 | 0 |
| E — Regression | 3 | 0 | 0 | 0 |
| **Total** | **24** | **0** | **0** | **0** |

**Overall status:** PENDING

---

## Notes / Observations

_Record any unexpected behavior, Revit exceptions, or edge-case observations below during the session._

---

_Verified: (pending)_
_Verifier: (pending)_
