# Dedicated-collection batch: tooling adopted, qualification blocked

Historical report. The read-only check and approved scope amendment subsequently
resolved this block without quarantining the sample; see [writable snapshot results](writable-snapshot-results.md).

## Outcome

- Ledger committed as **f046465**, **624/805** changed-value-tested setters.
- Inventory refreshed after that commit; live export tests passed without a skip.
- The exact 14 dedicated-collection cases were preregistered and implemented.
- The first case executed; the restoration guard aborted the batch. **13 cases did
  not run**, despite the TRX reporting 14 failed NUnit cases.
- **Zero new validations** credited. No dedicated results or ledger proposal applied.
- Original **18–34/70 estimate unchanged**: the required 14-case evidence is incomplete.
- The 18 class-collector cases remain unimplemented/unrun as a separate next batch.
- All opened documents were disposable detached Autodesk sample copies, closed
  without saving. Original model hashes and unsaved-copy hashes verified. No
  production documents, family documents, deployment or MCP contract changes.

## Tool adoption

| Tool | Actual adoption / verification |
| --- | --- |
| ricaun.RevitTest.TestAdapter 1.11.1 | Existing test-only runner retained. Compatibility test passed inside installed Revit 2026.5, .NET 10.0.11, zero open documents. |
| Nice3point.Revit.Api.RevitAPI / RevitAPIUI 2026.4.10 | Replaced the harness's two hardcoded compile-time references. PrivateAssets=all, ExcludeAssets=runtime; no Autodesk API DLL copied into test output. The package-built harness passed the live compatibility gate. |
| ilspycmd 11.0.0.9375 | Pinned local tool manifest under tools/SetterSurfaceAudit/.config. No production package dependency; global 9.1 installation unchanged. Used offline against installed Autodesk metadata. |
| PublicApiGenerator 11.5.4 | Already present only in SetterSurfaceAudit. Existing checked-in public surface and 805-entry setter-surface.json retained; no denominator invented or expanded. |

Nice3point 2026.4.10 references are older than this installed 2026.5 build. The runtime
gate demonstrates this harness loads; it does not establish compatibility of every
member in those versions. Installed binary paths/hashes remain intentionally in
safety checks and metadata extraction: package references do not replace evidence
of the actual API executing in Revit.

Primary package sources: [Nice3point](https://www.nuget.org/packages/Nice3point.Revit.Api.RevitAPI/),
[ILSpy tool pin](https://www.nuget.org/packages/ilspycmd/11.0.0.9375).

## First case and diagnostic

Operation: `api.set:Autodesk.Revit.DB.Analysis.MassLevelData.ConceptualConstructionId`.
Model: `BIM_Projekt_Golden_Nugget-Architektur_und_Ingenieurbau.rvt`.
Source SHA256: `A1FDD9A27A0D3B62977E99DF2FB872125920150D37A89DBECE4845B827AFE222`.

The property changed from ElementId -1 to 638139, survived committed fresh read-back,
and restored to -1 after outer rollback. However, element **1462965** did not restore
its exact observable snapshot. Element counts stayed 29,881; warning sets restored.

A single-case diagnostic identified the difference:

- Category: `BuiltInCategory.OST_SpotElevations` (-2000263).
- Parameter: `BuiltInParameter.SPOT_ELEV_SINGLE_OR_UPPER_VALUE` (-1006490).
- Exact double representation: **0 before, 8E-323 after**.

A fresh-copy **no-setter-write control** reproduced that same parameter difference
after an empty inner transaction, regeneration, commit and outer rollback. Its
receipt explicitly records `setter_attempted:false` and `no_write_control:true`.
This rules out attributing this observation specifically to the tested setter.
It does not distinguish regeneration, cached/native read behavior, or an underlying
model issue. No epsilon, parameter omission or normalization has been introduced.

Earlier raw receipts retain `status:validated` for the successful property comparison
but also `rollback_verified:false` and `infrastructure_failure:true`; they are **not
validations**. The merge guard rejects them. New harness receipts also reset status
to rejected-with-reason on infrastructure failure, avoiding that misleading display.

The frozen protocol requires a batch abort after this failure. Resuming by excluding
the offending parameter, changing the fixture, or quarantining this case requires an
explicit protocol amendment; none has been silently applied.

## ViewSheetSet.IsAutomatic classification probe

Installed `ViewSheetSetProxy.IsAutomatic` metadata shows its setter updates
`m_isAutomatic`; it does not itself persist the document change. In the live probe:

| Read/action | Result |
| --- | --- |
| Before | false |
| Same wrapper after setter | true |
| Fresh wrapper before Save | false |
| Make set current; ViewSheetSetting.Save() | InvalidOperationException: Save of the setting was unsuccessful. |
| Rollback / close unsaved / source hash verification | Passed |

The candidate persistence precondition was **not established**. This is evidence of
wrapper-local state plus a context-rejected compound Save attempt, not demonstrated
conditional read-only behavior. Do not relabel the historical standalone-setter
results for all 12 models from one failed compound probe. Its ledger entry remains
same_value_only; the context rejection stays explicit in this diagnostic receipt.
Classification-only receipts cannot receive standalone-setter credit.

## Evidence map

All paths below are relative to docs/review/setter-validation-gate. Each run contains
provenance.json with revision **f046465**, actual binary SHA256 identities, model
hashes and source hashes. They are runs of locally edited harness code based on that
revision, not claims that the edits were already committed at execution time.

- `nice3point-runtime-gate.trx`: live package-reference compatibility pass.
- `automatic-20260909T054601-b1e3e476ee8f482f8077ef594a7b51d5.trx`;
  `automatic-runs/20260909T054636-6e1b6e3d071145f2ac83d6b8f55b2924/`.
- `dedicated-20260909T054710-ea06326d0a0e4756bb231701d1df7b7b.trx`;
  `dedicated-runs/20260909T054746-f3bf2dc25d9d46c2aa7cac6638bb7f1a/`.
- `restoration-20260909T055117-2a2c9a06b0994c7c8fec7cb6c4777de2.trx`;
  `restoration-runs/20260909T055153-5a1b7fa488ed447d889039d7687ca20b/`.
- `control-20260909T055444-9cc1911ea0e9453faeed10fd95277488.trx`;
  `restoration-runs/20260909T055520-2dcf13d36546455a9c14fe344ed9a147/`.

## Checks and execution

Root deployment-disabled build passed (offline vulnerability-feed warnings only).
Harness build for **net10.0-windows, x64** passed. Offline regression suite: **18/18**,
including rejecting incomplete dedicated case sequences and classification-only
credit. The dedicated reconciliation CLI is prepared and unit-tested but has **not**
merged a completed live 14-case batch.

From `C:\LECG\RevitAddins\LECG`:

```powershell
dotnet build -p:SkipRevitDeploy=true --no-restore
node --test tools/SetterValidationProbe/*.test.mjs .codex/skills/lecg-map-codebase/scripts/*.test.mjs
```

From `C:\LECG\RevitAddins\LECG\tools\SetterValidationProbe`, with Revit closed:

```powershell
# Diagnostic reproduction only; it is expected to fail the restoration assertion.
./Run-Pilot.ps1 -Batch Control
```

Do not rerun the full Dedicated batch until the restoration limitation has an
approved resolution. It currently stops at the same first case. A narrow next step
is to quarantine this sample for the batch and run the remaining eligible cases
with unchanged strict per-case checks. That also defers FabricArea.TagViewId and
StructuralConnectionHandler.ApprovalTypeId, whose only historical reachable fixture
is this sample. This changes the frozen protocol and is not yet approved; no deferred
case may be reported as tested. The forecast remains frozen until that decision.
