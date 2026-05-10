# Phase 5: Verification & Polish — Context

**Gathered:** 2026-05-10
**Status:** Ready for planning
**Source:** /gsd:discuss-phase 5 — interactive session with user
**Requirements:** REQ-05 (Unit tests for all renaming services) + final bug fixes

<domain>
## Phase Boundary

Close v1.1 by (a) bringing every service under `src/Services/Renaming/` to a baseline of dedicated unit-test coverage and (b) landing three specific deferred polish fixes with RED-before-GREEN discipline.

**In scope (REQ-05 + polish):**
- Audit `src/Services/Renaming/` (6 services) against existing test fixtures; build a service→fixture coverage matrix; add fixtures where missing or deepen where thin.
- Extract pure-data helpers (Phase 04 pattern) from Revit-API-bound logic where extraction is < ~30 LOC and cleanly decouples; unit-test the helpers.
- Fix the three named polish items, each gated by a RED test demonstrating the bug:
  1. `count`-on-rollback at `BatchRenameExecutionService.cs:185`
  2. `Dimension.FamilyLabel` null-clear (Phase 4 C1 follow-up) in the dimension reassignment loop
  3. Standard-item progress capping below 100%
- Phase-end manual Revit verification checklist exercising the three fixes (Phase 4 pattern, trust-based sign-off acceptable).
- Flip REQ-05 → Complete in REQUIREMENTS.md; phase closure SUMMARY.

**Out of scope (deferred to v1.2 Phase 6 — Batch Rename Maturity):**
- Coverage of `LECG.Core/Rename/` (FormulaNameUpdater, RenameRuleEngine) — already covered; not re-audited here.
- Coverage of naming policies (FamilyNamePolicy, DetailFamilyNamePolicy, LinePatternNamingPolicy, SearchTermPolicy, MaterialBumpMapNormalizer) — already covered; not re-audited here.
- All other v1.2 Phase 6 backlog items (facade collapse, async CollectBaseElements, scope-UX pills, SwapStyle orphaning, ParamGroup dedup, etc.).
- New scopes (Schedules, Tags, Title Blocks).
- UI overhaul (multi-select, header funnel chrome, modernization).

</domain>

<decisions>
## Implementation Decisions

### Service Scope (REQ-05 boundary)
- **Only `src/Services/Renaming/`** counts for REQ-05 in this phase. Six services:
  `BaseElementCollectionService`, `BatchRenameExecutionService`, `FormulaUpdateService`,
  `RenameRulePipelineService`, `SearchReplaceService`, `SearchReplacePreviewService`.
- Existing naming-policy fixtures and `LECG.Core/Rename/` fixtures are NOT re-opened — they already exist and pass.

### Fixtures to Add or Deepen
- **`RenameRulePipelineServiceTests`** — new fixture. Cover pipeline composition / rule ordering / pass-through cases. Direct coverage today only exists via `RenameRuleEngineTests` (engine-level).
- **`BatchRenameExecutionServiceTests`** — new direct fixture. Broader surface than the Phase-4-focused `BatchRenameSafeRenameTests`: skip detection (built-in / reporting / name-conflict / standard-item), transaction loop ordering, progress accounting, composite log formatting.
- **`SearchReplaceServiceTests`** — new fixture. **Full coverage** treatment despite v1.2 deletion candidacy — if/when the facade is collapsed, the tests get deleted with it.
- **`BaseElementCollectionServiceTests`** — deepen from the current 4-test surface. Cover collector branches per scope (Types / Families / Materials / Parameters / GraphicsStyle / Sheets / etc.) at the `ElementLabelService.GetLabelsFromRaw` boundary plus null-fallback paths. Stay pure-data — don't introduce Revit-API harness here.
- `FormulaUpdateServiceTests` (3 tests) and `SearchReplacePreviewServiceTests` (8 tests) and `BatchRenameSafeRenameTests` (9 tests) **stay as-is** — confirmed adequate in the matrix.

### Coverage Depth
- **Pure-data helpers + key branches.** Extract pure helpers from Revit-bound code following the Phase 04 convention (`Collect*` / `Evaluate*` / `Build*` / `Format*`), unit-test the helpers. Cover obvious branches + error paths. Do NOT chase Revit-API-bound branches with a harness.
- **Extraction policy:** extract where the new helper is ≤ ~30 LOC and decoupling is clean. Where extraction would require invasive surgery, leave a **skip-gated RED test** naming the unreachable Revit dependency (Phase 03-00 / 04-00 pattern).
- No coverlet / numeric coverage gate — acceptance is the matrix (see below), not a percentage.

### Polish Bug Fixes (RED-test gated)
Each polish item ships with a failing test written FIRST, then the fix. Inclusion bar: only items with a reproducible repro + test land in this phase.

1. **`count`-on-rollback (`BatchRenameExecutionService.cs:185`)** — success counter increments inside the transaction lambda even when the SubTransaction rolls back. RED test asserts counter stays at 0 when the lambda throws / rolls back; fix moves the increment outside the rollback boundary.
2. **`Dimension.FamilyLabel` null-clear (Phase 4 C1)** — in the dimension reassignment loop, null-clear `Dimension.FamilyLabel` before assigning `newParam` in case Revit refuses overwrite. RED test asserts the null-clear sequencing in the extracted pure-data helper (`ExecuteDimensionReassignments` already accepts `List<Action>` — add a null-clear action upstream).
3. **Standard-item progress capping below 100%** — progress bar fails to reach 100% on standard-item batches. RED test asserts final progress value = total after a simulated batch; fix corrects the counting loop.

Where a polish item is Revit-API-bound and cannot be pure-tested directly, leave a **skip-gated RED test** naming the bug and anchor the fix decision with a brief in-code comment.

### REQ-05 Acceptance Criteria
- **Service→fixture coverage matrix** in `05-VERIFICATION.md`: one row per `src/Services/Renaming/` service → dedicated fixture name → list of scenarios covered → ✅/⚠/❌ status.
- Matrix passes when every row is ✅ and the full suite is 100% GREEN.
- No numeric coverage threshold; matrix is the audit trail.

### Manual Revit Verification at Phase End
- Short checklist (Phase 4 pattern) in `05-VERIFICATION.md` exercising the three polish fixes in live Revit:
  - `count`-on-rollback: trigger a forced-rollback path, confirm reported success count = 0.
  - Dimension null-clear: rename a dimension-label-driving param, confirm the dimension label reassigns cleanly with the null-clear step.
  - Progress capping: run a standard-item batch (Sheets or Materials), confirm progress reaches 100%.
- Trust-based sign-off acceptable (Phase 4 precedent).

### Phase Closure Mechanics
- Flip REQ-05 Pending → Complete in `.planning/REQUIREMENTS.md` only after matrix + manual checklist pass.
- Write 05-SUMMARY artifacts per plan; phase-end SUMMARY recaps matrix + polish-fix scorecard.
- v1.1 milestone audit gap closure note: this phase closes REQ-05, completing the v1.1 must-haves checklist.

### Claude's Discretion
- Exact scenario list per matrix row — pick the highest-value behaviors per service.
- Wave breakdown (suggested: Wave 0 matrix + RED scaffolds → Wave 1 deepen `BaseElementCollectionServiceTests` + add `RenameRulePipelineServiceTests` → Wave 2 add `BatchRenameExecutionServiceTests` direct + `SearchReplaceServiceTests` → Wave 3 polish RED→GREEN cycle for the three bugs → Wave 4 phase-end manual Revit verify + closure). Planner finalizes.
- Whether helper extraction lands in the same plan as the tests, or splits into a small "extract" plan + a separate "test the extract" plan.
- Where skip-gated RED is used: pick the reason string naming the unreachable Revit dependency, consistent with Phase 03-00 / 04-00 convention.
- Test fixture file naming: follow the Phase-04 convention (`{ServiceName}Tests.cs` under `LECG.Tests/Services/`).

</decisions>

<specifics>
## Specific Ideas

- "RED-before-GREEN for every polish item" matches the user's discipline pattern from Phases 3 & 4 (skip-gated RED tests naming the implementing plan ID).
- Coverage matrix > coverage percentage: the audit trail in `05-VERIFICATION.md` is the artifact a future auditor (or `gsd-audit-milestone`) reads — a checklist tells a clearer story than a coverlet report.
- Pure-data helper extraction is the established Phase 04 idiom — it's a known-good pattern that decouples Revit and yields testable surface without a harness.
- `SearchReplaceService` gets full coverage even though it's a v1.2 deletion candidate — if it survives, we have coverage; if it dies, the tests die with it. Symmetric.
- No new scope coverage (Schedules, Tags, Title Blocks) — that's a v1.2 Phase 6 addition.

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- **Phase 04 pure-data helper convention** — `CollectFormulaUpdates`, `ExecuteDimensionReassignments`, `EvaluateFamilyParamSkipReason`, `EvaluateStandardItemSkipReason`, `ApplyPreFlightSkipReasons`, `FormatSafeRenameLog`, `BuildDimensionsByLabelName`. New Phase 5 extractions should follow the same naming (`Collect*` / `Evaluate*` / `Build*` / `Format*`).
- **Skip-gated RED pattern** (Phase 03-00 / 04-00) — `[Fact(Skip = "Implemented by plan 05-XX")]` with reason strings naming the implementing plan; one non-skipped anchor per fixture for discovery.
- **`dotnet build LECG.Tests/LECG.Tests.csproj -p:SkipRevitDeploy=true`** — canonical local build command while Revit is open (Phase 03-00 build convention).
- **`ExecuteDimensionReassignments`** already accepts `List<Action>` not `List<Dimension>` — Phase 4 designed exactly this seam for testability. The null-clear polish reuses this seam.
- **`ApplyPreFlightSkipReasons`** delegate pattern from Plan 04-01 — `Func<ElementRowViewModel, string?>` — already a unit-testable pre-flight loop running OUTSIDE `_transactionService.Run`. Likely reusable for new pre-flight test scenarios in `BatchRenameExecutionServiceTests`.

### Established Patterns
- xUnit + plain `[Fact]` / `[Theory]`; pure-data static helpers; reflection over NSubstitute when proxying ITransactionService is blocked by RevitAPI.dll (Phase 04-03 decision).
- Locale-safe Revit API access (Phase 02.5 decision held) — applies to any new helper that touches BuiltInParameter / LabelUtils.
- LogView-only logging surface (Phase 02.5 decision held).
- Per-parameter SubTransaction with skip-and-continue (Phase 02 decision held) — the `count`-on-rollback polish lives at this boundary.
- One anchor test per fixture so `dotnet test --filter` discovers skip-gated fixtures.

### Integration Points
- **`BatchRenameExecutionService.cs:185`** — `count`-on-rollback bug location. RED test asserts the counter contract; fix relocates the increment to a post-commit branch.
- **`BatchRenameExecutionService` dimension reassignment loop** (Plan 04-04 surface) — `Dimension.FamilyLabel` null-clear lands here; reuses the `List<Action>` seam already in `ExecuteDimensionReassignments`.
- **Standard-item progress accounting in `BatchRenameExecutionService`** — progress-capping fix lands in the loop that publishes progress via `IProgressReporter`; RED test asserts final value = total.
- **`LECG.Tests/Services/`** — all new fixtures land here.
- **`.planning/REQUIREMENTS.md`** — REQ-05 row flips to Complete only after matrix + manual checklist pass.
- **`05-VERIFICATION.md`** (new) — matrix + manual checklist artifact. Phase-end deliverable.

### Notable Risks / Pitfalls
- **`SearchReplaceService` is a facade** — testing the dispatch contract risks coupling tests to internals that will be deleted in v1.2. Mitigation: test the observable behavior (what the facade returns / how it dispatches), not the call structure.
- **`BatchRenameExecutionService` is large and Revit-bound** — direct testing requires careful helper extraction. Stay disciplined on the ≤ ~30 LOC extraction rule; otherwise skip-gate.
- **`BaseElementCollectionService` deepening can balloon** — there are many collector branches per scope. Time-box the deepening or pick the top-N highest-risk scopes (Types / Families / Materials / Parameters are the load-bearing ones).
- **`count`-on-rollback fix MUST NOT change observable user-visible behavior** beyond the bug fix — e.g., don't accidentally fix it for already-rolled-back error cases in a way that changes the log/summary string format.
- **Progress-capping fix** may interact with multi-batch reporting — verify the test covers both single-batch and multi-batch progress sequences.

</code_context>

<deferred>
## Deferred Ideas

### Continues to belong to v1.2 Phase 6 (Batch Rename Maturity)
- Collapse `SearchReplaceService` pass-through facade leaking ViewModel types.
- Async `CollectBaseElements` (UI-freeze on large Parameters-scope batches).
- Scope UX (single-select pills replacing the multi-select-rendered exclusive scope).
- Dedup `ParamGroup` swallowed try/catch (`BaseElementCollectionService.cs:213` + `:275`).
- Per-column header funnel chrome (visual UI deferred from Plan 03-05).
- `SwapStyle` orphaning non-`CurveElement` consumers of GraphicsStyle.
- Add more scopes to Batch Rename (Schedules, Tags, Title Blocks).
- Coverage of `LECG.Core/Rename/` helpers and naming policies — already covered, not re-audited in Phase 5.
- Coverlet / numeric coverage gating — explicitly NOT introduced in Phase 5; if v1.2 wants it, add then.

### Out of any milestone (no firm home)
- A property-based testing harness (e.g., FsCheck) for RenameRuleEngine / FormulaNameUpdater.
- Snapshot testing for log message formats.
- CI integration to enforce the service→fixture matrix mechanically (today it's hand-maintained in `05-VERIFICATION.md`).

</deferred>

---

*Phase: 05-verification-and-polish*
*Context gathered: 2026-05-10 via /gsd:discuss-phase*
