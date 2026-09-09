# LECG query-mode live acceptance — 2026-09-08

Follow-up: the [query/report corrections](lecg-query-followup.md) supersede the
pagination suggestion, case 4 structural misroute and active discovery route below.
This original sweep remains a historical record, not the current routing contract.

Revision: `774354b4d1d745b42ea2b73a73ad17cf68aebf58`. Each case began with
its own live freshness invocation. All ten reported CURRENT inventory: revision,
recorded inputs, live hashes and input set matched. Rendered documentation remains
ASSUMED_STALE, one commit behind; it was not used as evidence.

## Budget changes verified, not rewritten

The requested health probe already runs inside `scripts/freshness.mjs` (lines 22,
78). It establishes matching workspace/C# LSP reachability, not compiler readiness.
The first evidence-bearing symbol lookup establishes resolution where successful.
No per-query `get_current_config` or `initial_instructions` call was made.

`scripts/budget.mjs:37` already exposes the pure in-process `emitPacket` checker.
It was loaded once into the orchestration runtime; packet assembly/validation
used JavaScript computation, without a validator subprocess or MCP invocation.
Validation consumed zero evidence-tool calls in every ledger. Session setup and
post-run artifact/unit/build checks are separate, not hidden inside query costs.
Paths in this section are relative to `.codex/skills/lecg-map-codebase/`.

## Results — not a blanket pass

| Case | Live outcome | Calls / 8 | Evidence bytes / 6,144 |
|---|---|---:|---:|
| 2 | Bounded command-to-assignment-service path confirmed; not full transitive execution | 5 | 5,996 |
| 3 | Partial: 187 unvalidated setters counted, only 18 identities returned | 2 | 5,520 |
| 4 | Partial: two direct base-list matches; indirect implementations not established | 2 | 1,506 |
| 5 | Answered: explicit element deletion is not an unused-content purge contract | 4 | 2,537 |
| 6 | Unresolved: removed ICadFamilyBuildService did not resolve | 2 | 1,440 |
| 7 | Answered: service-file presence does not establish registration/exposure | 6 | 3,791 |
| 8 | All 19 Materials-domain entries returned; not all cross-domain material code | 2 | 3,472 |
| 9 | Partial: rename_elements guard and callers confirmed; design rationale not established | 7 | 4,786 |
| 10 | Unresolved: ExecuteAsync interface/signature unspecified; no caller test passed | 2 | 1,440 |
| 11, additional | Discovery unavailable: CLI exists, but no local graph or exposed query provider | 2 | 1,556 |

Cases 2–10: four scoped answers, three partial answers, two unresolved targets.
Case 11 is an additional unavailable-route result. No call ceiling was reached.
Case 2 has only **148 bytes** spare: within the hard limit, but not comfortable
headroom. Its first service-body request returned a shortened result; that output
and the successful retry are both counted. These are warm manual runs, not a
blind performance benchmark or measured token-saving claim.

## Remaining limitations exposed by the run

- Case 3 cannot enumerate the remaining 169 member identities through the current
  allowlist. Its truncation advice says to refine, but this group has no pagination
  or member filter. Repeating the same query cannot settle the complete listing.
  A bounded pagination/filter contract would require a separately approved change.
- Case 4's structural route cannot satisfy literal semantic completeness. Do not
  relabel two direct declarations as every implementing class.
- Case 6 deliberately preserves the original absent-symbol fixture as an
  abstention check, not implementation qualification. Case 10 similarly preserves
  the unspecified target. The real-target implementation/caller comparisons
  remain cases 12 and 13 in the existing CRG qualification; they were not rerun.
- Case 8's earlier stale-inventory baseline is superseded by this live current
  result. The result is still limited to the named inventory domain.
- Case 9 explicitly binds its placeholder X to rename_elements. The bounded
  planning search found availability notes, not an explicit design decision.
- Case 11 cannot qualify discovery-to-Serena promotion without an existing query
  provider and graph. No installation, build or substitute discovery route ran.

## Verification and artifacts

All ten script tests passed. Independent post-run checks recomputed every saved
ledger's UTF-8 byte sum, call count, packet size, heading structure and limits;
all ten saved packets passed those mechanical checks. This is not a claim that
all ten questions were fully answered.

`dotnet build -p:SkipRevitDeploy=true --no-restore` passed with zero errors;
two NU1900 warnings reflect unavailable NuGet vulnerability metadata offline.
No live add-in deployment or Revit model change occurred. No production source,
query route or CRG promotion was changed during this run.

- [Raw evidence, individual packets and ledgers](lecg-query-live-acceptance.json)
- [CRG qualification, including milestone risk-panel limitation](crg-qualification.md)

CRG remains milestone-only. Its graph diff is record bookkeeping; its risk and
test-gap panel is an incomplete review hint, not authoritative blast-radius proof.
